﻿/**
 * Crunch - Arbitrary-precision integer arithmetic library
 * Copyright (C) 2014 Nenad Vukicevic crunch.secureroom.net/license
 */

/**
 * @module Crunch
 * Radix: 28 bits
 * Endianness: Big
 *
 * @param {boolean} rawIn   - expect 28-bit arrays
 * @param {boolean} rawOut  - return 28-bit arrays
 */
function Crunch(rawIn, rawOut) {

    "use strict";

    /**
     * BEGIN CONSTANTS
     * zeroes, primes and ptests for Miller-Rabin primality
     */

    // sieve of Eratosthenes for first 1900 primes
    var primes = (function (n) {
        var arr = new Array(Math.ceil((n - 2) / 32)),
            maxi = (n - 3) / 2,
            p = [2];

        for (var q = 3, i, index, bit; q < n; q += 2) {
            i = (q - 3) / 2;
            index = i >> 5;
            bit = i & 31;

            if ((arr[index] & (1 << bit)) == 0) {
                // q is prime
                p.push(q);
                i += q;

                for (var d = q; i < maxi; i += d) {
                    index = i >> 5;
                    bit = i & 31;

                    arr[index] |= (1 << bit);
                }
            }
        }

        return p;

    })(16382);

    var zeroes = (function (n) {
        for (var z = []; z.push(0) < n;) { }
        return z;
    })(50000);

    var ptests = primes.slice(0, 10).map(function (v) {
        return [v];
    });
    /* END CONSTANTS */

    function cut(x) {
        while (x[0] === 0 && x.length > 1) {
            x.shift();
        }

        return x;
    }

    function cmp(x, y) {
        var xl = x.length,
            yl = y.length, i; //zero front pad problem

        if (x.negative && !y.negative || xl < yl) {
            return -1;
        } else if (!x.negative && y.negative || xl > yl) {
            return 1;
        }

        for (i = 0; i < xl; i++) {
            if (x[i] < y[i]) return -1;
            if (x[i] > y[i]) return 1;
        }

        return 0;
    }

    /**
     * Most significant bit, base 28, position from left
     */
    function msb(x) {
        if (x !== 0) {
            for (var i = 134217728, z = 0; i > x; z++) {
                i /= 2;
            }

            return z;
        }
    }

    /**
     * Least significant bit, base 28, position from right
     */
    function lsb(x) {
        if (x !== 0) {
            for (var z = 0; !(x & 1) ; z++) {
                x /= 2;
            }

            return z;
        }
    }

    function add(x, y) {
        var n = x.length,
            t = y.length,
            i = Math.max(n, t),
            c = 0,
            z = zeroes.slice(0, i);

        if (n < t) {
            x = zeroes.slice(0, t - n).concat(x);
        } else if (n > t) {
            y = zeroes.slice(0, n - t).concat(y);
        }

        for (i -= 1; i >= 0; i--) {
            z[i] = x[i] + y[i] + c;

            if (z[i] > 268435455) {
                c = 1;
                z[i] -= 268435456;
            } else {
                c = 0;
            }
        }

        if (c === 1) {
            z.unshift(c);
        }

        return z;
    }

    function sub(x, y, internal) {
        var n = x.length,
            t = y.length,
            i = Math.max(n, t),
            c = 0,
            z = zeroes.slice(0, i);

        if (n < t) {
            x = zeroes.slice(0, t - n).concat(x);
        } else if (n > t) {
            y = zeroes.slice(0, n - t).concat(y);
        }

        for (i -= 1; i >= 0; i--) {
            z[i] = x[i] - y[i] - c;

            if (z[i] < 0) {
                c = 1;
                z[i] += 268435456;
            } else {
                c = 0;
            }
        }

        if (c === 1 && !internal) {
            z = sub(zeroes.slice(0, z.length), z, true);
            z.negative = true;
        }

        return z;
    }

    /**
     * Signed Addition
     */
    function sad(x, y) {
        var z;

        if (x.negative) {
            if (y.negative) {
                z = add(x, y);
                z.negative = true;
            } else {
                z = cut(sub(y, x, false));
            }
        } else {
            z = y.negative ? cut(sub(x, y, false)) : add(x, y);
        }

        return z;
    }

    /**
     * Signed Subtraction
     */
    function ssb(x, y) {
        var z;

        if (x.negative) {
            if (y.negative) {
                z = cut(sub(y, x, false));
            } else {
                z = add(x, y);
                z.negative = true;
            }
        } else {
            z = y.negative ? add(x, y) : cut(sub(x, y, false));
        }

        return z;
    }

    /**
     * Multiplication - HAC 14.12
     */
    function mul(x, y) {
        var yl, yh, c,
            n = x.length,
            i = y.length,
            z = zeroes.slice(0, n + i);

        while (i--) {
            c = 0;

            yl = y[i] & 16383;
            yh = y[i] >> 14;

            for (var j = n - 1, xl, xh, t1, t2; j >= 0; j--) {
                xl = x[j] & 16383;
                xh = x[j] >> 14;

                t1 = yh * xl + xh * yl;
                t2 = yl * xl + ((t1 & 16383) << 14) + z[j + i + 1] + c;

                z[j + i + 1] = t2 & 268435455;
                c = yh * xh + (t1 >> 14) + (t2 >> 28);
            }

            z[i] = c;
        }

        if (z[0] === 0) {
            z.shift();
        }

        z.negative = (x.negative ^ y.negative) ? true : false;

        return z;
    }

    /**
     *  Karatsuba Multiplication, works faster when numbers gets bigger
     */
    function mulk(x, y) {
        var z, lx, ly, negx, negy, b;

        if (x.length > y.length) {
            z = x; x = y; y = z;
        }
        lx = x.length;
        ly = y.length;
        negx = x.negative,
        negy = y.negative;
        x.negative = false;
        y.negative = false;

        if (lx <= 100) {
            z = mul(x, y);
        } else if (ly / lx >= 2) {
            b = (ly + 1) >> 1;
            z = sad(
              lsh(mulk(x, y.slice(0, ly - b)), b * 28),
              mulk(x, y.slice(ly - b, ly))
            );
        } else {
            b = (ly + 1) >> 1;
            var
                x0 = x.slice(lx - b, lx),
                x1 = x.slice(0, lx - b),
                y0 = y.slice(ly - b, ly),
                y1 = y.slice(0, ly - b),
                z0 = mulk(x0, y0),
                z2 = mulk(x1, y1),
                z1 = ssb(sad(z0, z2), mulk(ssb(x1, x0), ssb(y1, y0)));
            z2 = lsh(z2, b * 2 * 28);
            z1 = lsh(z1, b * 28);

            z = sad(sad(z2, z1), z0);
        }

        z.negative = (negx ^ negy) ? true : false;
        x.negative = negx;
        y.negative = negy;

        return z;
    }

    /**
     * Squaring - HAC 14.16
     */
    function sqr(x) {
        var l1, h1, t1, t2, c,
            i = x.length,
            z = zeroes.slice(0, 2 * i);

        while (i--) {
            l1 = x[i] & 16383;
            h1 = x[i] >> 14;

            t1 = 2 * h1 * l1;
            t2 = l1 * l1 + ((t1 & 16383) << 14) + z[2 * i + 1];

            z[2 * i + 1] = t2 & 268435455;
            c = h1 * h1 + (t1 >> 14) + (t2 >> 28);

            for (var j = i - 1, l2, h2; j >= 0; j--) {
                l2 = (2 * x[j]) & 16383;
                h2 = x[j] >> 13;

                t1 = h2 * l1 + h1 * l2;
                t2 = l2 * l1 + ((t1 & 16383) << 14) + z[j + i + 1] + c;
                z[j + i + 1] = t2 & 268435455;
                c = h2 * h1 + (t1 >> 14) + (t2 >> 28);
            }

            z[i] = c;
        }

        if (z[0] === 0) {
            z.shift();
        }

        return z;
    }

    function rsh(x, s) {
        var ss = s % 28,
            ls = Math.floor(s / 28),
            l = x.length - ls,
            z = x.slice(0, l);

        if (ss) {
            while (--l) {
                z[l] = ((z[l] >> ss) | (z[l - 1] << (28 - ss))) & 268435455;
            }

            z[l] = z[l] >> ss;

            if (z[0] === 0) {
                z.shift();
            }
        }

        z.negative = x.negative;

        return z;
    }

    function lsh(x, s) {
        var ss = s % 28,
            ls = Math.floor(s / 28),
            l = x.length,
            z = [],
            t = 0;

        if (ss) {
            while (l--) {
                z[l] = ((x[l] << ss) + t) & 268435455;
                t = x[l] >>> (28 - ss);
            }

            if (t !== 0) {
                z.unshift(t);
            }

            z.negative = x.negative;

        } else {
            z = x;
        }

        return (ls) ? z.concat(zeroes.slice(0, ls)) : z;
    }

    /**
     * Division - HAC 14.20
     */
    function div(x, y, internal) {
        var u, v, xt, yt, d, q, k, i, z,
            s = msb(y[0]) - 1;

        if (s > 0) {
            u = lsh(x, s);
            v = lsh(y, s);
        } else {
            u = x.slice();
            v = y.slice();
        }

        d = u.length - v.length;
        q = [0];
        k = v.concat(zeroes.slice(0, d));
        yt = v[0] * 268435456 + v[1];

        // only cmp as last resort
        while (u[0] > k[0] || (u[0] === k[0] && cmp(u, k) > -1)) {
            q[0]++;
            u = sub(u, k, false);
        }

        for (i = 1; i <= d; i++) {
            q[i] = u[i - 1] === v[0] ? 268435455 : ~~((u[i - 1] * 268435456 + u[i]) / v[0]);

            xt = u[i - 1] * 72057594037927936 + u[i] * 268435456 + u[i + 1];

            while (q[i] * yt > xt) { //condition check can fail due to precision problem at 28-bit
                q[i]--;
            }

            k = mul(v, [q[i]]).concat(zeroes.slice(0, d - i)); //concat after multiply, save cycles
            u = sub(u, k, false);

            if (u.negative) {
                u = sub(v.concat(zeroes.slice(0, d - i)), u, false);
                q[i]--;
            }
        }

        if (internal) {
            z = (s > 0) ? rsh(cut(u), s) : cut(u);
        } else {
            z = cut(q);
            z.negative = (x.negative ^ y.negative) ? true : false;
        }

        return z;
    }

    function mod(x, y) {
        //For negative x, cmp doesn't work and result of div is negative
        //so take result away from the modulus to get the correct result
        if (x.negative) {
            return sub(y, div(x, y, true));
        }

        switch (cmp(x, y)) {
            case -1:
                return x;
            case 0:
                return [0];
            default:
                return div(x, y, true);
        }
    }

    /**
     * Greatest Common Divisor - HAC 14.61 - Binary Extended GCD, used to calc inverse, x <= modulo, y <= exponent
     */
    function gcd(x, y) {
        var g = Math.min(lsb(x[x.length - 1]), lsb(y[y.length - 1])),
            u = rsh(x, g),
            v = rsh(y, g),
            a = [1], b = [0], c = [0], d = [1], s;

        while ((u.length !== 1 || u[0] !== 0)&&u.length!==0) {
            s = lsb(u[u.length - 1]);
            u = rsh(u, s);
            while (s--) {
                if ((a[a.length - 1] & 1) === 0 && (b[b.length - 1] & 1) === 0) {
                    a = rsh(a, 1);
                    b = rsh(b, 1);
                } else {
                    a = rsh(sad(a, y), 1);
                    b = rsh(ssb(b, x), 1);
                }
            }

            s = lsb(v[v.length - 1]);
            v = rsh(v, s);
            while (s--) {
                if ((c[c.length - 1] & 1) === 0 && (d[d.length - 1] & 1) === 0) {
                    c = rsh(c, 1);
                    d = rsh(d, 1);
                } else {
                    c = rsh(sad(c, y), 1);
                    d = rsh(ssb(d, x), 1);
                }
            }

            if (cmp(u, v) >= 0) {
                u = sub(u, v, false);
                a = ssb(a, c);
                b = ssb(b, d);
            } else {
                v = sub(v, u, false);
                c = ssb(c, a);
                d = ssb(d, b);
            }
        }

        if (v.length === 1 && v[0] === 1) {
            return d;
        }
    }

    /**
     * Inverse 1/x mod y
     */
    function inv(x, y) {
        var z = gcd(y, x);
        return (typeof z !== "undefined" && z.negative) ? sub(y, z, false) : z;
    }

    /**
     * Barret Modular Reduction - HAC 14.42
     */
    function bmr(x, m, mu) {
        var q1, q2, q3, r1, r2, z, s, k = m.length;

        if (cmp(x, m) < 0) {
            return x;
        }

        if (typeof mu === "undefined") {
            mu = div([1].concat(zeroes.slice(0, 2 * k)), m, false);
        }

        q1 = x.slice(0, x.length - (k - 1));
        q2 = mul(q1, mu);
        q3 = q2.slice(0, q2.length - (k + 1));

        s = x.length - (k + 1);
        r1 = (s > 0) ? x.slice(s) : x.slice();

        r2 = mul(q3, m);
        s = r2.length - (k + 1);

        if (s > 0) {
            r2 = r2.slice(s);
        }

        z = cut(sub(r1, r2, false));

        if (z.negative) {
            z = cut(sub([1].concat(zeroes.slice(0, k + 1)), z, false));
        }

        while (cmp(z, m) >= 0) {
            z = cut(sub(z, m, false));
        }

        return z;
    }

    /**
     * Modular Exponentiation - HAC 14.76 Right-to-left binary exp
     */
    function exp(x, e, n) {
        var c = 268435456,
            r = [1],
            u = div(r.concat(zeroes.slice(0, 2 * n.length)), n, false);

        for (var i = e.length - 1; i >= 0; i--) {
            if (i === 0) {
                c = 1 << (27 - msb(e[0]));
            }

            for (var j = 1; j < c; j *= 2) {
                if (e[i] & j) {
                    r = bmr(mul(r, x), n, u);
                }
                x = bmr(sqr(x), n, u);
            }
        }

        return bmr(mul(r, x), n, u);
    }

    /**
     * Garner's algorithm, modular exponentiation - HAC 14.71
     */
    function gar(x, p, q, d, u, dp1, dq1) {
        var vp, vq, t;

        if (typeof dp1 === "undefined") {
            dp1 = mod(d, dec(p));
            dq1 = mod(d, dec(q));
        }

        vp = exp(mod(x, p), dp1, p);
        vq = exp(mod(x, q), dq1, q);

        if (cmp(vq, vp) < 0) {
            t = cut(sub(vp, vq, false));
            t = cut(bmr(mul(t, u), q, undefined));
            t = cut(sub(q, t, false));
        } else {
            t = cut(sub(vq, vp, false));
            t = cut(bmr(mul(t, u), q, undefined)); //bmr instead of mod, div can fail because of precision
        }

        return cut(add(vp, mul(t, p)));
    }

    /**
     * Simple Mod - When n < 2^14
     */
    function mds(x, n) {
        for (var i = 0, z = 0, l = x.length; i < l; i++) {
            z = ((x[i] >> 14) + (z << 14)) % n;
            z = ((x[i] & 16383) + (z << 14)) % n;
        }

        return z;
    }

    function dec(x) {
        var z;

        if (x[x.length - 1] > 0) {
            z = x.slice();
            z[z.length - 1] -= 1;
            z.negative = x.negative;
        } else {
            z = sub(x, [1], false);
        }

        return z;
    }

    /**
     * Miller-Rabin Primality Test
     */
    function mrb(x, iterations) {
        var m = dec(x),
            s = lsb(m[x.length - 1]),
            r = rsh(x, s);

        for (var i = 0, j, t, y; i < iterations; i++) {
            y = exp(ptests[i], r, x);

            if ((y.length > 1 || y[0] !== 1) && cmp(y, m) !== 0) {
                j = 1;
                t = true;

                while (t && s > j++) {
                    y = mod(sqr(y), x);

                    if (y.length === 1 && y[0] === 1) {
                        return false;
                    }

                    t = cmp(y, m) !== 0;
                }

                if (t) {
                    return false;
                }
            }
        }

        return true;
    }

    function tpr(x) {
        if (x.length === 1 && x[0] < 16384 && primes.indexOf(x[0]) >= 0) {
            return true;
        }

        for (var i = 1, l = primes.length; i < l; i++) {
            if (mds(x, primes[i]) === 0) {
                return false;
            }
        }

        return mrb(x, 3);
    }

    /**
     * Quick add integer n to arbitrary precision integer x avoiding overflow
     */
    function qad(x, n) {
        var l = x.length - 1;

        if (x[l] + n < 268435456) {
            x[l] += n;
        } else {
            x = add(x, [n]);
        }

        return x;
    }

    function npr(x) {
        x = qad(x, 1 + x[x.length - 1] % 2);

        while (!tpr(x)) {
            x = qad(x, 2);
        }

        return x;
    }

    function fct(n) {
        var z = [1],
            a = [1];

        while (a[0]++ < n) {
            z = mul(z, a);
        }

        return z;
    }

    /**
     * Convert byte array to 28 bit array
     */
    function ci(a) {
        var x = [0, 0, 0, 0, 0, 0].slice((a.length - 1) % 7),
            z = [];

        if (a[0] < 0) {
            a[0] *= -1;
            z.negative = true;
        } else {
            z.negative = false;
        }

        x = x.concat(a);

        for (var i = 0; i < x.length; i += 7) {
            z.push((x[i] * 1048576 + x[i + 1] * 4096 + x[i + 2] * 16 + (x[i + 3] >> 4)), ((x[i + 3] & 15) * 16777216 + x[i + 4] * 65536 + x[i + 5] * 256 + x[i + 6]));
        }

        return cut(z);
    }

    /**
     * Convert 28 bit array to byte array
     */
    function co(a) {
        if (typeof a !== "undefined") {
            var x = [0].slice((a.length - 1) % 2).concat(a),
                z = [];

            for (var u, v, i = 0; i < x.length;) {
                u = x[i++];
                v = x[i++];

                z.push((u >> 20), (u >> 12 & 255), (u >> 4 & 255), ((u << 4 | v >> 24) & 255), (v >> 16 & 255), (v >> 8 & 255), (v & 255));
            }

            z = cut(z);

            if (a.negative) {
                z[0] *= -1;
            }

            return z;
        }
    }

    function stringify(x) {
        var a = [],
            b = [10],
            z = [0],
            i = 0, q;

        do {
            q = x;
            x = div(q, b);
            a[i++] = sub(q, mul(b, x)).pop();
        } while (cmp(x, z));

        return a.reverse().join("");
    }

    function parse(s) {
        var x = s.split(""),
            p = [1],
            a = [0],
            b = [10],
            n = false;

        if (x[0] === "-") {
            n = true;
            x.shift();
        }

        while (x.length) {
            a = add(a, mul(p, [x.pop()]));
            p = mul(p, b);
        }

        a.negative = n;

        return a;
    }

    function transformIn(a) {
        return rawIn ? a : Array.prototype.slice.call(a).map(function (v) { return ci(v.slice()) });
    }

    function transformOut(x) {
        return rawOut ? x : co(x);
    }

    return {
        /**
         * Return zero array length n
         *
         * @method zero
         * @param {Number} n
         * @return {Array} 0 length n
         */
        zero: function (n) {
            return zeroes.slice(0, n);
        },

        /**
         * Signed Addition - Safe for signed MPI
         *
         * @method add
         * @param {Array} x
         * @param {Array} y
         * @return {Array} x + y
         */
        add: function (x, y) {
            return transformOut(
              sad.apply(null, transformIn(arguments))
            );
        },

        /**
         * Signed Subtraction - Safe for signed MPI
         *
         * @method sub
         * @param {Array} x
         * @param {Array} y
         * @return {Array} x - y
         */
        sub: function (x, y) {
            return transformOut(
              ssb.apply(null, transformIn(arguments))
            );
        },

        /**
         * Multiplication
         *
         * @method mul
         * @param {Array} x
         * @param {Array} y
         * @return {Array} x * y
         */
        mul: function (x, y) {
            return transformOut(
              mul.apply(null, transformIn(arguments))
            );
        },

        /**
         * Multiplication, with karatsuba method
         *
         * @method mulk
         * @param {Array} x
         * @param {Array} y
         * @return {Array} x * y
         */
        mulk: function (x, y) {
            return transformOut(
              mulk.apply(null, transformIn(arguments))
            );
        },

        /**
         * Squaring
         *
         * @method sqr
         * @param {Array} x
         * @return {Array} x * x
         */
        sqr: function (x) {
            return transformOut(
              sqr.apply(null, transformIn(arguments))
            );
        },

        /**
         * Modular Exponentiation
         *
         * @method exp
         * @param {Array} x
         * @param {Array} e
         * @param {Array} n
         * @return {Array} x^e % n
         */
        exp: function (x, e, n) {
            return transformOut(
              exp.apply(null, transformIn(arguments))
            );
        },

        /**
         * Division
         *
         * @method div
         * @param {Array} x
         * @param {Array} y
         * @return {Array} x / y || undefined
         */
        div: function (x, y) {
            if (y.length !== 1 || y[0] !== 0) {
                return transformOut(
                  div.apply(null, transformIn(arguments))
                );
            }
        },

        /**
         * Modulus
         *
         * @method mod
         * @param {Array} x
         * @param {Array} y
         * @return {Array} x % y
         */
        mod: function (x, y) {
            return transformOut(
              mod.apply(null, transformIn(arguments))
            );
        },

        /**
         * Barret Modular Reduction
         *
         * @method bmr
         * @param {Array} x
         * @param {Array} y
         * @param {Array} [mu]
         * @return {Array} x % y
         */
        bmr: function (x, y, mu) {
            return transformOut(
              bmr.apply(null, transformIn(arguments))
            );
        },

        /**
         * Garner's Algorithm
         *
         * @method gar
         * @param {Array} x
         * @param {Array} p
         * @param {Array} q
         * @param {Array} d
         * @param {Array} u
         * @param {Array} [dp1]
         * @param {Array} [dq1]
         * @return {Array} x^d % pq
         */
        gar: function (x, p, q, d, u, dp1, dq1) {
            return transformOut(
              gar.apply(null, transformIn(arguments))
            );
        },

        /**
         * Mod Inverse
         *
         * @method inv
         * @param {Array} x
         * @param {Array} y
         * @return {Array} 1/x % y || undefined
         */
        inv: function (x, y) {
            return transformOut(
              inv.apply(null, transformIn(arguments))
            );
        },

        /**
         * Remove leading zeroes
         *
         * @method cut
         * @param {Array} x
         * @return {Array} x without leading zeroes
         */
        cut: function (x) {
            return transformOut(
              cut.apply(null, transformIn(arguments))
            );
        },


        /**
         * Factorial - for n < 268435456
         *
         * @method factorial
         * @param {Number} n
         * @return {Array} n!
         */
        factorial: function (n) {
            return transformOut(
              fct.apply(null, [n % 268435456])
            );
        },

        /**
         * Bitwise AND, OR, XOR
         * Undefined if x and y different lengths
         *
         * @method OP
         * @param {Array} x
         * @param {Array} y
         * @return {Array} x OP y
         */
        and: function (x, y) {
            if (x.length === y.length) {
                for (var i = 0, z = []; i < x.length; i++) { z[i] = x[i] & y[i] }
                return z;
            }
        },

        or: function (x, y) {
            if (x.length === y.length) {
                for (var i = 0, z = []; i < x.length; i++) { z[i] = x[i] | y[i] }
                return z;
            }
        },

        xor: function (x, y) {
            if (x.length === y.length) {
                for (var i = 0, z = []; i < x.length; i++) { z[i] = x[i] ^ y[i] }
                return z;
            }
        },

        /**
         * Bitwise NOT
         *
         * @method not
         * @param {Array} x
         * @return {Array} NOT x
         */
        not: function (x) {
            for (var i = 0, z = [], m = rawIn ? 268435455 : 255; i < x.length; i++) { z[i] = ~x[i] & m }
            return z;
        },

        /**
         * Left Shift
         *
         * @method leftShift
         * @param {Array} x
         * @param {Integer} s
         * @return {Array} x << s
         */
        leftShift: function (x, s) {
            return transformOut(lsh(transformIn([x]).pop(), s));
        },

        /**
         * Zero-fill Right Shift
         *
         * @method rightShift
         * @param {Array} x
         * @param {Integer} s
         * @return {Array} x >>> s
         */
        rightShift: function (x, s) {
            return transformOut(rsh(transformIn([x]).pop(), s));
        },

        /**
         * Decrement
         *mod
         * @method decrement
         * @param {Array} x
         * @return {Array} x - 1
         */
        decrement: function (x) {
            return transformOut(
              dec.apply(null, transformIn(arguments))
            );
        },

        /**
         * Compare values of two MPIs - Not safe for signed or leading zero MPI
         *
         * @method compare
         * @param {Array} x
         * @param {Array} y
         * @return {Number} 1: x > y
         *                  0: x = y
         *                 -1: x < y
         */
        compare: function (x, y) {
            return cmp(x, y);
        },

        /**
         * Find Next Prime
         *
         * @method nextPrime
         * @param {Array} x
         * @return {Array} 1st prime > x
         */
        nextPrime: function (x) {
            return transformOut(
              npr.apply(null, transformIn(arguments))
            );
        },

        /**
         * Primality Test
         * Sieve then Miller-Rabin
         *
         * @method testPrime
         * @param {Array} x
         * @return {boolean} is prime
         */
        testPrime: function (x) {
            return (x[x.length - 1] % 2 === 0) ? false : tpr.apply(null, transformIn(arguments));
        },

        /**
         * Array base conversion
         *
         * @method transform
         * @param {Array} x
         * @param {boolean} toRaw
         * @return {Array}  toRaw: 8 => 28-bit array
         *                 !toRaw: 28 => 8-bit array
         */
        transform: function (x, toRaw) {
            return toRaw ? ci(x) : co(x);
        },

        /**
         * Integer to String conversion
         *
         * @method stringify
         * @param {Array} x
         * @return {String} base 10 number as string
         */
        stringify: function (x) {
            return stringify(ci(x));
        },

        /**
         * String to Integer conversion
         *
         * @method parse
         * @param {String} s
         * @return {Array} x
         */
        parse: function (s) {
            return co(parse(s));
        },

        gcd: function (x, y) {
            return transformOut(
             gcd.apply(null, transformIn(arguments))
           );
        }
    }
}

/*
 * @version   : 1.14.0 - Bridge.NET
 * @author    : Object.NET, Inc. http://bridge.net/
 * @date      : 2016-06-08
 * @copyright : Copyright (c) 2008-2016, Object.NET, Inc. (http://object.net/). All rights reserved.
 * @license   : See license.txt and https://github.com/bridgedotnet/Bridge.NET/blob/master/LICENSE.
 */
(function(n){"use strict";var bt={global:n,emptyFn:function(){},identity:function(n){return n},isPlainObject:function(n){if(typeof n=="object"&&n!==null){if(typeof Object.getPrototypeOf=="function"){var t=Object.getPrototypeOf(n);return t===Object.prototype||t===null}return Object.prototype.toString.call(n)==="[object Object]"}return!1},toPlain:function(n){var i,t,r,u,f;if(!n||Bridge.isPlainObject(n)||typeof n!="object")return n;if(typeof n.toJSON=="function")return n.toJSON();if(Bridge.isArray(n)){for(i=[],t=0;t<n.length;t++)i.push(Bridge.toPlain(n[t]));return i}r={};for(f in n)u=n[f],Bridge.isFunction(u)||(r[f]=u);return r},ref:function(n,t){Bridge.isArray(t)&&(t=System.Array.toIndex(n,t));var i={};return Object.defineProperty(i,"v",{get:function(){return n[t]},set:function(i){n[t]=i}}),i},property:function(n,t,i){n[t]=i;var u=t.charAt(0)==="$",r=u?t.slice(1):t;n["get"+r]=function(n){return function(){return this[n]}}(t);n["set"+r]=function(n){return function(t){this[n]=t}}(t)},event:function(n,t,i){n[t]=i;var u=t.charAt(0)==="$",r=u?t.slice(1):t;n["add"+r]=function(n){return function(t){this[n]=Bridge.fn.combine(this[n],t)}}(t);n["remove"+r]=function(n){return function(t){this[n]=Bridge.fn.remove(this[n],t)}}(t)},createInstance:function(n){return n===System.Decimal?System.Decimal.Zero:n===System.Int64?System.Int64.Zero:n===System.UInt64?System.UInt64.Zero:n===System.Double||n===System.Single||n===System.Byte||n===System.SByte||n===System.Int16||n===System.UInt16||n===System.Int32||n===System.UInt32||n===Bridge.Int?0:typeof n.getDefaultValue=="function"?n.getDefaultValue():n===Boolean?!1:n===Date?new Date(0):n===Number?0:n===String?"":new n},clone:function(n){return Bridge.isArray(n)?System.Array.clone(n):Bridge.is(n,System.ICloneable)?n.clone():null},copy:function(n,t,i,r){typeof i=="string"&&(i=i.split(/[,;\s]+/));for(var u,f=0,e=i?i.length:0;f<e;f++)u=i[f],(r!==!0||n[u]==undefined)&&(n[u]=Bridge.is(t[u],System.ICloneable)?Bridge.clone(t[u]):t[u]);return n},get:function(n){return n&&n.$staticInit!==null&&n.$staticInit(),n},ns:function(n,t){var r=n.split("."),i=0;for(t||(t=Bridge.global),i=0;i<r.length;i++)typeof t[r[i]]=="undefined"&&(t[r[i]]={}),t=t[r[i]];return t},ready:function(n,t){var i=function(){t?n.apply(t):n()};if(typeof Bridge.global.jQuery!="undefined")Bridge.global.jQuery(i);else if(typeof Bridge.global.document=="undefined"||Bridge.global.document.readyState==="complete"||Bridge.global.document.readyState==="loaded")i();else Bridge.on("DOMContentLoaded",Bridge.global.document,i)},on:function(n,t,i,r){var u=function(n){var t=i.apply(r||this,arguments);return t===!1&&(n.stopPropagation(),n.preventDefault()),t},f=function(){var n=i.call(r||t,Bridge.global.event);return n===!1&&(Bridge.global.event.returnValue=!1,Bridge.global.event.cancelBubble=!0),n};t.addEventListener?t.addEventListener(n,u,!1):t.attachEvent("on"+n,f)},getHashCode:function(n,t){var u,i,r;if(Bridge.isEmpty(n,!0)){if(t)return 0;throw new System.InvalidOperationException("HashCode cannot be calculated for empty value");}if(n.getHashCode&&Bridge.isFunction(n.getHashCode)&&!n.__insideHashCode&&n.getHashCode.length===0)return n.__insideHashCode=!0,u=n.getHashCode(),delete n.__insideHashCode,u;if(Bridge.isBoolean(n))return n?1:0;if(Bridge.isDate(n))return n.valueOf()&4294967295;if(Bridge.isNumber(n))return n=n.toExponential(),parseInt(n.substr(0,n.indexOf("e")).replace(".",""),10)&4294967295;if(Bridge.isString(n)){for(i=0,r=0;r<n.length;r++)i=(i<<5)-i+n.charCodeAt(r)&4294967295;return i}return n.$$hashCode?n.$$hashCode:(n.$$hashCode=Math.random()*4294967296|0,n.$$hashCode)},getDefaultValue:function(n){return n.getDefaultValue&&n.getDefaultValue.length===0?n.getDefaultValue():n===Boolean?!1:n===Date?new Date(-864e13):n===Number?0:null},getTypeName:function(n){var i,t;return n.$$name?n.$$name:(i=n.constructor===Function?n.toString():n.constructor.toString(),t=/function (.{1,})\(/.exec(i),t&&t.length>1?t[1]:"Object")},getBaseType:function(n){if(n==null)throw new System.NullReferenceException;return n.$interface?null:n.$$inherits?n.$$inherits[0].$interface?Object:n.$$inherits[0]:n.$$name?Object:null},isAssignableFrom:function(n,t){if(n===null)throw new System.NullReferenceException;if(t===null)return!1;if(n==t||n==Object)return!0;var r=t.$$inherits,i,u;if(r)for(i=0;i<r.length;i++)if(u=Bridge.isAssignableFrom(n,r[i]),u)return!0;return!1},is:function(n,t,i,r){if(typeof t=="string"&&(t=Bridge.unroll(t)),n==null)return!!r;if(i!==!0){if(Bridge.isFunction(t.$is))return t.$is(n);if(Bridge.isFunction(t.instanceOf))return t.instanceOf(n)}if(n.constructor===t||n instanceof t)return!0;if(Bridge.isArray(n)||n instanceof Bridge.ArrayEnumerator)return System.Array.is(n,t);if(Bridge.isString(n))return System.String.is(n,t);if(Bridge.isBoolean(n))return System.Boolean.is(n,t);if(!t.$$inheritors)return!1;for(var f=t.$$inheritors,u=0;u<f.length;u++)if(Bridge.is(n,f[u]))return!0;return!1},as:function(n,t,i){return Bridge.is(n,t,!1,i)?n:null},cast:function(n,t,i){if(n===null)return null;var r=Bridge.as(n,t,i);if(r===null)throw new System.InvalidCastException("Unable to cast type "+(n?Bridge.getTypeName(n):"'null'")+" to type "+Bridge.getTypeName(t));return r},apply:function(n,t){for(var u=Bridge.getPropertyNames(t,!0),i,r=0;r<u.length;r++)i=u[r],typeof n[i]=="function"&&typeof t[i]!="function"?n[i](t[i]):n[i]=t[i];return n},merge:function(n,t,i){var r,e,u,s,h,f,o;if(n instanceof System.Decimal&&Bridge.isNumber(t))return new System.Decimal(t);if(n instanceof System.Int64&&Bridge.isNumber(t))return new System.Int64(t);if(n instanceof System.UInt64&&Bridge.isNumber(t))return new System.UInt64(t);if(n instanceof Boolean||n instanceof Number||n instanceof String||n instanceof Function||n instanceof Date||n instanceof System.Double||n instanceof System.Single||n instanceof System.Byte||n instanceof System.SByte||n instanceof System.Int16||n instanceof System.UInt16||n instanceof System.Int32||n instanceof System.UInt32||n instanceof Bridge.Int||n instanceof System.Decimal)return t;if(Bridge.isArray(t)&&Bridge.isFunction(n.add||n.push))for(h=Bridge.isArray(n)?n.push:n.add,e=0;e<t.length;e++)f=t[e],Bridge.isArray(f)||(f=[typeof i=="undefined"?f:Bridge.merge(i(),f)]),h.apply(n,f);else for(r in t)u=t[r],typeof n[r]=="function"?r.match(/^\s*get[A-Z]/)?Bridge.merge(n[r](),u):n[r](u):(o="set"+r.charAt(0).toUpperCase()+r.slice(1),typeof n[o]=="function"&&typeof u!="function"?n[o](u):u&&u.constructor===Object&&n[r]?(s=n[r],Bridge.merge(s,u)):n[r]=u);return n},getEnumerator:function(n,t){if(typeof n=="string"&&(n=System.String.toCharArray(n)),t&&n&&n["getEnumerator"+t])return n["getEnumerator"+t].call(n);if(n&&n.getEnumerator)return n.getEnumerator();if(Object.prototype.toString.call(n)==="[object Array]"||n&&Bridge.isDefined(n.length))return new Bridge.ArrayEnumerator(n);throw new System.InvalidOperationException("Cannot create enumerator");},getPropertyNames:function(n,t){var i=[];for(var r in n)(t||typeof n[r]!="function")&&i.push(r);return i},isDefined:function(n,t){return typeof n!="undefined"&&(t?n!==null:!0)},isEmpty:function(n,t){return typeof n=="undefined"||n===null||(t?!1:n==="")||(!t&&Bridge.isArray(n)?n.length===0:!1)},toArray:function(n){var t,r,u,i=[];if(Bridge.isArray(n))for(t=0,u=n.length;t<u;++t)i.push(n[t]);else for(t=Bridge.getEnumerator(n);t.moveNext();)r=t.getCurrent(),i.push(r);return i},isArray:function(n){return Object.prototype.toString.call(n)in{"[object Array]":1,"[object Uint8Array]":1,"[object Int8Array]":1,"[object Int16Array]":1,"[object Uint16Array]":1,"[object Int32Array]":1,"[object Uint32Array]":1,"[object Float32Array]":1,"[object Float64Array]":1}},isFunction:function(n){return typeof n=="function"},isDate:function(n){return Object.prototype.toString.call(n)==="[object Date]"},isNull:function(n){return n===null||n===undefined},isBoolean:function(n){return typeof n=="boolean"},isNumber:function(n){return typeof n=="number"&&isFinite(n)},isString:function(n){return typeof n=="string"},unroll:function(n){var r=n.split("."),t=Bridge.global[r[0]],i=1;for(i;i<r.length;i++){if(!t)return null;t=t[r[i]]}return t},referenceEquals:function(n,t){return Bridge.hasValue(n)?n===t:!Bridge.hasValue(t)},staticEquals:function(n,t){return Bridge.hasValue(n)?Bridge.hasValue(t)?Bridge.equals(n,t):!1:!Bridge.hasValue(t)},equals:function(n,t){if(n==null&&t==null)return!0;if(n&&Bridge.isFunction(n.equals)&&n.equals.length===1)return n.equals(t);if(t&&Bridge.isFunction(t.equals)&&t.equals.length===1)return t.equals(n);if(Bridge.isDate(n)&&Bridge.isDate(t))return n.valueOf()===t.valueOf();if(Bridge.isNull(n)&&Bridge.isNull(t))return!0;if(Bridge.isNull(n)!==Bridge.isNull(t))return!1;var i=n===t;return!i&&typeof n=="object"&&typeof t=="object"&&n!==null&&t!==null&&n.$struct&&t.$struct&&n.$$name===t.$$name?Bridge.getHashCode(n)===Bridge.getHashCode(t)&&Bridge.objectEquals(n,t):i},objectEquals:function(n,t){Bridge.$$leftChain=[];Bridge.$$rightChain=[];var i=Bridge.deepEquals(n,t);return delete Bridge.$$leftChain,delete Bridge.$$rightChain,i},deepEquals:function(n,t){if(typeof n=="object"&&typeof t=="object"){if(n===t)return!0;if(Bridge.$$leftChain.indexOf(n)>-1||Bridge.$$rightChain.indexOf(t)>-1)return!1;for(var i in t)if(t.hasOwnProperty(i)!==n.hasOwnProperty(i)||typeof t[i]!=typeof n[i])return!1;for(i in n){if(t.hasOwnProperty(i)!==n.hasOwnProperty(i)||typeof n[i]!=typeof t[i])return!1;if(n[i]===t[i])continue;else if(typeof n[i]=="object"){if(Bridge.$$leftChain.push(n),Bridge.$$rightChain.push(t),!Bridge.deepEquals(n[i],t[i]))return!1;Bridge.$$leftChain.pop();Bridge.$$rightChain.pop()}else if(!Bridge.equals(n[i],t[i]))return!1}return!0}return Bridge.equals(n,t)},compare:function(n,t,i){if(Bridge.isDefined(n,!0)){if(Bridge.isNumber(n)||Bridge.isString(n)||Bridge.isBoolean(n))return Bridge.isString(n)&&!Bridge.hasValue(t)?1:n<t?-1:n>t?1:0;if(Bridge.isDate(n))return Bridge.compare(n.valueOf(),t.valueOf())}else{if(i)return 0;throw new System.NullReferenceException;}if(Bridge.isFunction(n.compareTo))return n.compareTo(t);if(Bridge.isFunction(t.compareTo))return-t.compareTo(n);if(i)return 0;throw new System.Exception("Cannot compare items");},equalsT:function(n,t){if(Bridge.isDefined(n,!0)){if(Bridge.isNumber(n)||Bridge.isString(n)||Bridge.isBoolean(n))return n===t;if(Bridge.isDate(n))return n.valueOf()===t.valueOf()}else throw new System.NullReferenceException;return n.equalsT?n.equalsT(t):t.equalsT(n)},format:function(n,t){return Bridge.isNumber(n)?Bridge.Int.format(n,t):Bridge.isDate(n)?Bridge.Date.format(n,t):n.format(t)},getType:function(n){if(!Bridge.isDefined(n,!0))throw new System.NullReferenceException("instance is null");if(typeof n=="number")return Math.floor(n,0)===n?System.Int32:System.Double;try{return n.constructor}catch(t){return Object}},isLower:function(n){var t=String.fromCharCode(n);return t===t.toLowerCase()&&t!==t.toUpperCase()},isUpper:function(n){var t=String.fromCharCode(n);return t!==t.toLowerCase()&&t===t.toUpperCase()},coalesce:function(n,t){return Bridge.hasValue(n)?n:t},fn:{equals:function(n){return this===n?!0:n==null||this.constructor!==n.constructor?!1:this.equals===n.equals&&this.$method===n.$method&&this.$scope===n.$scope},call:function(n,t){var i=Array.prototype.slice.call(arguments,2);return n=n||Bridge.global,n[t].apply(n,i)},makeFn:function(n,t){switch(t){case 0:return function(){return n.apply(this,arguments)};case 1:return function(){return n.apply(this,arguments)};case 2:return function(){return n.apply(this,arguments)};case 3:return function(){return n.apply(this,arguments)};case 4:return function(){return n.apply(this,arguments)};case 5:return function(){return n.apply(this,arguments)};case 6:return function(){return n.apply(this,arguments)};case 7:return function(){return n.apply(this,arguments)};case 8:return function(){return n.apply(this,arguments)};case 9:return function(){return n.apply(this,arguments)};case 10:return function(){return n.apply(this,arguments)};case 11:return function(){return n.apply(this,arguments)};case 12:return function(){return n.apply(this,arguments)};case 13:return function(){return n.apply(this,arguments)};case 14:return function(){return n.apply(this,arguments)};case 15:return function(){return n.apply(this,arguments)};case 16:return function(){return n.apply(this,arguments)};case 17:return function(){return n.apply(this,arguments)};case 18:return function(){return n.apply(this,arguments)};case 19:return function(){return n.apply(this,arguments)};default:return function(){return n.apply(this,arguments)}}},bind:function(n,t,i,r){if(t&&t.$method===t&&t.$scope===n)return t;var u;return u=arguments.length===2?Bridge.fn.makeFn(function(){Bridge.caller.unshift(this);var i=t.apply(n,arguments);return Bridge.caller.shift(this),i},t.length):Bridge.fn.makeFn(function(){var u=i||arguments,f;return r===!0?(u=Array.prototype.slice.call(arguments,0),u=u.concat(i)):typeof r=="number"&&(u=Array.prototype.slice.call(arguments,0),r===0?u.unshift.apply(u,i):r<u.length?u.splice.apply(u,[r,0].concat(i)):u.push.apply(u,i)),Bridge.caller.unshift(this),f=t.apply(n,u),Bridge.caller.shift(this),f},t.length),u.$method=t,u.$scope=n,u.equals=Bridge.fn.equals,u},bindScope:function(n,t){var i=Bridge.fn.makeFn(function(){var i=Array.prototype.slice.call(arguments,0),r;return i.unshift.apply(i,[n]),Bridge.caller.unshift(this),r=t.apply(n,i),Bridge.caller.shift(this),r},t.length);return i.$method=t,i.$scope=n,i.equals=Bridge.fn.equals,i},$build:function(n){var t=function(){for(var i=t.$invocationList,r=null,u,n=0;n<i.length;n++)u=i[n],r=u.apply(null,arguments);return r};return(t.$invocationList=n?Array.prototype.slice.call(n,0):[],t.$invocationList.length===0)?null:t},combine:function(n,t){if(!n||!t)return n||t;var i=n.$invocationList?n.$invocationList:[n],r=t.$invocationList?t.$invocationList:[t];return Bridge.fn.$build(i.concat(r))},getInvocationList:function(){},remove:function(n,t){if(!n||!t)return n||null;for(var r=n.$invocationList?n.$invocationList:[n],f=t.$invocationList?t.$invocationList:[t],e=[],o,u,i=r.length-1;i>=0;i--){for(o=!1,u=0;u<f.length;u++)if(r[i]===f[u]||r[i].$method&&r[i].$method===f[u].$method&&r[i].$scope&&r[i].$scope===f[u].$scope){o=!0;break}o||e.push(r[i])}return e.reverse(),Bridge.fn.$build(e)}},sleep:function(n,t){if(Bridge.hasValue(t)&&(n=t.getTotalMilliseconds()),isNaN(n)||n<-1||n>2147483647)throw new System.ArgumentOutOfRangeException("timeout","Number must be either non-negative and less than or equal to Int32.MaxValue or -1");n==-1&&(n=2147483647);for(var i=(new Date).getTime();(new Date).getTime()-i<n;)if((new Date).getTime()-i>2147483647)break}},nt,tt,o,b,vt,yt,pt,k,wt,t;n.Bridge=bt;n.Bridge.caller=[];n.System={};n.System.Diagnostics={};n.System.Diagnostics.Contracts={};n.System.Threading={};nt={hasValue:function(n){return n!==null&&n!==undefined},getValue:function(n){if(!System.Nullable.hasValue(n))throw new System.InvalidOperationException("Nullable instance doesn't have a value.");return n},getValueOrDefault:function(n,t){return System.Nullable.hasValue(n)?n:t},add:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n+t:null},band:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n&t:null},bor:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n|t:null},and:function(n,t){return n===!0&&t===!0?!0:n===!1||t===!1?!1:null},or:function(n,t){return n===!0||t===!0?!0:n===!1&&t===!1?!1:null},div:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n/t:null},eq:function(n,t){return Bridge.hasValue(n)?n===t:!Bridge.hasValue(t)},equals:function(n,t,i){return Bridge.hasValue(n)?i?i(n,t):Bridge.equals(n,t):!Bridge.hasValue(t)},toString:function(n,t){return Bridge.hasValue(n)?t?t(n):n.toString():""},getHashCode:function(n,t){return Bridge.hasValue(n)?t?t(n):Bridge.getHashCode(n):0},xor:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n^t:null},gt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)&&n>t},gte:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)&&n>=t},neq:function(n,t){return Bridge.hasValue(n)?n!==t:Bridge.hasValue(t)},lt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)&&n<t},lte:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)&&n<=t},mod:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n%t:null},mul:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n*t:null},sl:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n<<t:null},sr:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n>>t:null},srr:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n>>>t:null},sub:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n-t:null},bnot:function(n){return Bridge.hasValue(n)?~n:null},neg:function(n){return Bridge.hasValue(n)?-n:null},not:function(n){return Bridge.hasValue(n)?!n:null},pos:function(n){return Bridge.hasValue(n)?+n:null},lift:function(){for(var n=1;n<arguments.length;n++)if(!Bridge.hasValue(arguments[n]))return null;return arguments[0]==null?null:arguments[0].apply==undefined?arguments[0]:arguments[0].apply(null,Array.prototype.slice.call(arguments,1))},lift1:function(n,t){return Bridge.hasValue(t)?typeof n=="function"?n.apply(null,Array.prototype.slice.call(arguments,1)):t[n].apply(t,Array.prototype.slice.call(arguments,2)):null},lift2:function(n,t,i){return Bridge.hasValue(t)&&Bridge.hasValue(i)?typeof n=="function"?n.apply(null,Array.prototype.slice.call(arguments,1)):t[n].apply(t,Array.prototype.slice.call(arguments,2)):null},liftcmp:function(n,t,i){return Bridge.hasValue(t)&&Bridge.hasValue(i)?typeof n=="function"?n.apply(null,Array.prototype.slice.call(arguments,1)):t[n].apply(t,Array.prototype.slice.call(arguments,2)):!1},lifteq:function(n,t,i){var r=Bridge.hasValue(t),u=Bridge.hasValue(i);return!r&&!u||r&&u&&(typeof n=="function"?n.apply(null,Array.prototype.slice.call(arguments,1)):t[n].apply(t,Array.prototype.slice.call(arguments,2)))},liftne:function(n,t,i){var r=Bridge.hasValue(t),u=Bridge.hasValue(i);return r!==u||r&&(typeof n=="function"?n.apply(null,Array.prototype.slice.call(arguments,1)):t[n].apply(t,Array.prototype.slice.call(arguments,2)))}};System.Nullable=nt;Bridge.hasValue=System.Nullable.hasValue;tt={is:function(n,t){return Bridge.isString(n)?n.constructor===t||n instanceof t?!0:t===System.Collections.IEnumerable||t.$$name&&System.String.startsWith(t.$$name,"System.Collections.Generic.IEnumerable$1")||t.$$name&&System.String.startsWith(t.$$name,"System.IComparable$1")||t.$$name&&System.String.startsWith(t.$$name,"System.IEquatable$1")?!0:!1:!1},lastIndexOf:function(n,t,i,r){var u=n.lastIndexOf(t,i);return u<i-r+1?-1:u},lastIndexOfAny:function(n,t,i,r){var e=n.length,f,u;if(!e)return-1;for(t=String.fromCharCode.apply(null,t),i=i||e-1,r=r||e,f=i-r+1,f<0&&(f=0),u=i;u>=f;u--)if(t.indexOf(n.charAt(u))>=0)return u;return-1},isNullOrWhiteSpace:function(n){return n==null||n.match(/^ *$/)!==null},isNullOrEmpty:function(n){return Bridge.isEmpty(n,!1)},fromCharCount:function(n,t){if(t>=0)return String(Array(t+1).join(String.fromCharCode(n)));throw new System.ArgumentOutOfRangeException("count","cannot be less than zero");},format:function(n){var i=this,r=Array.prototype.slice.call(arguments,1),t=this.decodeBraceSequence;return n.replace(/(\{+)((\d+|[a-zA-Z_$]\w+(?:\.[a-zA-Z_$]\w+|\[\d+\])*)(?:\,(-?\d*))?(?:\:([^\}]*))?)(\}+)|(\{+)|(\}+)/g,function(n,u,f,e,o,s,h,c,l){return c?t(c):l?t(l):u.length%2==0||h.length%2==0?t(u)+f+t(h):t(u,!0)+i.handleElement(e,o,s,r)+t(h,!0)})},handleElement:function(n,t,i,r){var u;if(n=parseInt(n,10),n>r.length-1)throw new System.FormatException("Input string was not in a correct format.");return u=r[n],u==null&&(u=""),u=i&&Bridge.is(u,System.IFormattable)?Bridge.format(u,i):""+u,t&&(t=parseInt(t,10),Bridge.isNumber(t)||(t=null)),System.String.alignString(u.toString(),t)},decodeBraceSequence:function(n,t){return n.substr(0,(n.length+(t?0:1))/2)},alignString:function(n,t,i,r){if(!t)return n;if(i||(i=" "),Bridge.isNumber(i)&&(i=String.fromCharCode(i)),r||(r=t<0?1:2),t=Math.abs(t),t+1>=n.length)switch(r){case 2:n=Array(t+1-n.length).join(i)+n;break;case 3:var u=t-n.length,f=Math.ceil(u/2),e=u-f;n=Array(e+1).join(i)+n+Array(f+1).join(i);break;case 1:default:n=n+Array(t+1-n.length).join(i)}return n},startsWith:function(n,t){return t.length?t.length>n.length?!1:(t=System.String.escape(t),n.match("^"+t)!==null):!0},endsWith:function(n,t){return t.length?t.length>n.length?!1:(t=System.String.escape(t),n.match(t+"$")!==null):!0},contains:function(n,t){if(t==null)throw new System.ArgumentNullException;return n==null?!1:n.indexOf(t)>-1},indexOfAny:function(n,t){var i,r,e,u,o,f;if(t==null)throw new System.ArgumentNullException;if(n==null||n==="")return-1;if(i=arguments.length>2?arguments[2]:0,i<0)throw new System.ArgumentOutOfRangeException("startIndex","startIndex cannot be less than zero");if(r=arguments.length>3?arguments[3]:n.length-i,r<0)throw new System.ArgumentOutOfRangeException("length","must be non-negative");if(r>n.length-i)throw new System.ArgumentOutOfRangeException("Index and length must refer to a location within the string");for(e=n.substr(i,r),u=0;u<t.length;u++)if(o=String.fromCharCode(t[u]),f=e.indexOf(o),f>-1)return f+i;return-1},indexOf:function(n,t){var i,u,f,r;if(t==null)throw new System.ArgumentNullException;if(n==null||n==="")return-1;if(i=arguments.length>2?arguments[2]:0,i<0||i>n.length)throw new System.ArgumentOutOfRangeException("startIndex","startIndex cannot be less than zero and must refer to a location within the string");if(t==="")return arguments.length>2?i:0;if(u=arguments.length>3?arguments[3]:n.length-i,u<0)throw new System.ArgumentOutOfRangeException("length","must be non-negative");if(u>n.length-i)throw new System.ArgumentOutOfRangeException("Index and length must refer to a location within the string");return(f=n.substr(i,u),r=arguments.length===5&&arguments[4]%2!=0?f.toLocaleUpperCase().indexOf(t.toLocaleUpperCase()):f.indexOf(t),r>-1)?arguments.length===5?System.String.compare(t,f.substr(r,t.length),arguments[4])===0?r+i:-1:r+i:-1},equals:function(){return System.String.compare.apply(this,arguments)===0},compare:function(n,t){if(n==null)return t==null?0:-1;if(t==null)return 1;if(arguments.length>=3)if(Bridge.isBoolean(arguments[2])){if(arguments[2]&&(n=n.toLocaleUpperCase(),t=t.toLocaleUpperCase()),arguments.length===4)return n.localeCompare(t,arguments[3].name)}else switch(arguments[2]){case 1:return n.localeCompare(t,System.Globalization.CultureInfo.getCurrentCulture().name,{sensitivity:"accent"});case 2:return n.localeCompare(t,System.Globalization.CultureInfo.invariantCulture.name);case 3:return n.localeCompare(t,System.Globalization.CultureInfo.invariantCulture.name,{sensitivity:"accent"});case 4:return n===t?0:n>t?1:-1;case 5:return n.toUpperCase()===t.toUpperCase()?0:n.toUpperCase()>t.toUpperCase()?1:-1}return n.localeCompare(t)},toCharArray:function(n,t,i){var u,r;if(t<0||t>n.length||t>n.length-i)throw new System.ArgumentOutOfRangeException("startIndex","startIndex cannot be less than zero and must refer to a location within the string");if(i<0)throw new System.ArgumentOutOfRangeException("length","must be non-negative");for(Bridge.hasValue(t)||(t=0),Bridge.hasValue(i)||(i=n.length),u=[],r=t;r<t+i;r++)u.push(n.charCodeAt(r));return u},escape:function(n){return n.replace(/[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g,"\\$&")},replaceAll:function(n,t,i){var r=new RegExp(System.String.escape(t),"g");return n.replace(r,i)},insert:function(n,t,i){return n>0?t.substring(0,n)+i+t.substring(n,t.length):i+t},remove:function(n,t,i){if(n==null)throw new System.NullReferenceException;if(t<0)throw new System.ArgumentOutOfRangeException("startIndex","StartIndex cannot be less than zero");if(i!=null){if(i<0)throw new System.ArgumentOutOfRangeException("count","Count cannot be less than zero");if(i>n.length-t)throw new System.ArgumentOutOfRangeException("count","Index and count must refer to a location within the string");}else if(t>=n.length)throw new System.ArgumentOutOfRangeException("startIndex","startIndex must be less than length of string");return i==null||t+i>n.length?n.substr(0,t):n.substr(0,t)+n.substr(t+i)},split:function(n,t,i,r){for(var o=!Bridge.hasValue(t)||t.length===0?new RegExp("\\s","g"):new RegExp(t.map(System.String.escape).join("|"),"g"),f=[],e,u=0;;u=o.lastIndex)if(e=o.exec(n)){if(r!==1||e.index>u){if(f.length===i-1)return f.push(n.substr(u)),f;f.push(n.substring(u,e.index))}}else return(r!==1||u!==n.length)&&f.push(n.substr(u)),f},trimEnd:function(n,t){return n.replace(t?new RegExp("["+System.String.escape(String.fromCharCode.apply(null,t))+"]+$"):/\s*$/,"")},trimStart:function(n,t){return n.replace(t?new RegExp("^["+System.String.escape(String.fromCharCode.apply(null,t))+"]+"):/^\s*/,"")},trim:function(n,t){return System.String.trimStart(System.String.trimEnd(n,t),t)}};System.String=tt;o={nameEquals:function(n,t,i){return i?n.toLowerCase()===t.toLowerCase():n.charAt(0).toLowerCase()+n.slice(1)===t.charAt(0).toLowerCase()+t.slice(1)},checkEnumType:function(n){if(!n)throw new System.ArgumentNullException("enumType");if(n.prototype&&!n.prototype.$enum)throw new System.ArgumentException("","enumType");},toName:function(n){return n.charAt(0).toUpperCase()+n.slice(1)},parse:function(n,t,i,r){var s,f,e,v,h,u;if(System.Enum.checkEnumType(n),s={},System.Int32.tryParse(t,s))return s.v;if(f=n,n.prototype&&n.prototype.$flags){var c=t.split(","),l=0,a=!0;for(e=c.length-1;e>=0;e--){v=c[e].trim();h=!1;for(u in f)if(o.nameEquals(u,v,i)){l|=f[u];h=!0;break}if(!h){a=!1;break}}if(a)return l}else for(u in f)if(o.nameEquals(u,t,i))return f[u];if(r!==!0)throw new System.ArgumentException("Invalid Enumeration Value");return null},toString:function(n,t,i){var u,f,r;if(System.Enum.checkEnumType(n),u=n,(n.prototype&&n.prototype.$flags||i===!0)&&t!==0){f=[];for(r in u)u[r]&t&&f.push(o.toName(r));return f.length?f.join(", "):t.toString()}for(r in u)if(u[r]===t)return o.toName(r);return t.toString()},getValues:function(n){var r,t,i;System.Enum.checkEnumType(n);r=[];t=n;for(i in t)t.hasOwnProperty(i)&&i.indexOf("$")<0&&r.push(t[i]);return r},format:function(n,t,i){System.Enum.checkEnumType(n);var r;if(!Bridge.hasValue(t)&&(r="value")||!Bridge.hasValue(i)&&(r="format"))throw new System.ArgumentNullException(r);switch(i){case"G":case"g":return System.Enum.toString(n,t);case"x":case"X":return t.toString(16);case"d":case"D":return t.toString();case"f":case"F":return System.Enum.toString(n,t,!0);default:throw new System.FormatException;}},getNames:function(n){var i,r,t;System.Enum.checkEnumType(n);i=[];r=n;for(t in r)r.hasOwnProperty(t)&&t.indexOf("$")<0&&i.push(o.toName(t));return i},getName:function(n,t){var r,i;System.Enum.checkEnumType(n);r=n;for(i in r)if(r[i]===t)return i.charAt(0).toUpperCase()+i.slice(1);return null},hasFlag:function(n,t){return!!(n&t)},isDefined:function(n,t){var i,u,r;System.Enum.checkEnumType(n);i=n;u=Bridge.isString(t);for(r in i)if(u?o.nameEquals(r,t,!1):i[r]===t)return!0;return!1},tryParse:function(n,t,i,r){return(i.v=0,i.v=o.parse(n,t,r,!0),i.v==null)?!1:!0}};System.Enum=o;var i=function(n){return n.test(navigator.userAgent.toLowerCase())},it=Bridge.global.document&&Bridge.global.document.compatMode==="CSS1Compat",a=function(n,t){var i;return n&&(i=t.exec(navigator.userAgent.toLowerCase()))?parseFloat(i[1]):0},r=Bridge.global.document?Bridge.global.document.documentMode:null,y=i(/opera/),kt=y&&i(/version\/10\.5/),rt=i(/\bchrome\b/),d=i(/webkit/),h=!rt&&i(/safari/),dt=h&&i(/applewebkit\/4/),gt=h&&i(/version\/3/),ni=h&&i(/version\/4/),ti=h&&i(/version\/5\.0/),ii=h&&i(/version\/5/),u=!y&&(i(/msie/)||i(/trident/)),e=u&&(i(/msie 7/)&&r!==8&&r!==9&&r!==10||r===7),s=u&&(i(/msie 8/)&&r!==7&&r!==9&&r!==10||r===8),c=u&&(i(/msie 9/)&&r!==7&&r!==8&&r!==10||r===9),p=u&&(i(/msie 10/)&&r!==7&&r!==8&&r!==9||r===10),ut=u&&(i(/trident\/7\.0/)&&r!==7&&r!==8&&r!==9&&r!==10||r===11),f=u&&i(/msie 6/),v=!d&&!u&&i(/gecko/),w=v&&i(/rv:1\.9/),ri=v&&i(/rv:2\.0/),ui=v&&i(/rv:5\./),fi=v&&i(/rv:10\./),ei=w&&i(/rv:1\.9\.0/),oi=w&&i(/rv:1\.9\.1/),si=w&&i(/rv:1\.9\.2/),ft=i(/windows|win32/),et=i(/macintosh|mac os x/),ot=i(/linux/),hi=a(!0,/\bchrome\/(\d+\.\d+)/),l=a(!0,/\bfirefox\/(\d+\.\d+)/),ci=a(u,/msie (\d+\.\d+)/),li=a(y,/version\/(\d+\.\d+)/),ai=a(h,/version\/(\d+\.\d+)/),vi=a(d,/webkit\/(\d+\.\d+)/),yi=Bridge.global.location?/^https/i.test(Bridge.global.location.protocol):!1,st=/iPhone/i.test(navigator.platform),ht=/iPod/i.test(navigator.platform),g=/iPad/i.test(navigator.userAgent),pi=/Blackberry/i.test(navigator.userAgent),ct=/Android/i.test(navigator.userAgent),lt=et||ft||ot&&!ct,at=g,wi=!lt&&!at,bi={isStrict:it,isIEQuirks:u&&!it&&(f||e||s||c),isOpera:y,isOpera10_5:kt,isWebKit:d,isChrome:rt,isSafari:h,isSafari3:gt,isSafari4:ni,isSafari5:ii,isSafari5_0:ti,isSafari2:dt,isIE:u,isIE6:f,isIE7:e,isIE7m:f||e,isIE7p:u&&!f,isIE8:s,isIE8m:f||e||s,isIE8p:u&&!(f||e),isIE9:c,isIE9m:f||e||s||c,isIE9p:u&&!(f||e||s),isIE10:p,isIE10m:f||e||s||c||p,isIE10p:u&&!(f||e||s||c),isIE11:ut,isIE11m:f||e||s||c||p||ut,isIE11p:u&&!(f||e||s||c||p),isGecko:v,isGecko3:w,isGecko4:ri,isGecko5:ui,isGecko10:fi,isFF3_0:ei,isFF3_5:oi,isFF3_6:si,isFF4:4<=l&&l<5,isFF5:5<=l&&l<6,isFF10:10<=l&&l<11,isLinux:ot,isWindows:ft,isMac:et,chromeVersion:hi,firefoxVersion:l,ieVersion:ci,operaVersion:li,safariVersion:ai,webKitVersion:vi,isSecure:yi,isiPhone:st,isiPod:ht,isiPad:g,isBlackberry:pi,isAndroid:ct,isDesktop:lt,isTablet:at,isPhone:wi,iOS:st||g||ht,standalone:Bridge.global.navigator?!!Bridge.global.navigator.standalone:!1};Bridge.Browser=bi;b=!1;vt={cache:{},initCtor:function(){var n=arguments[0];if(this.$multipleCtors&&arguments.length>0&&typeof n=="string"&&(n=n==="constructor"?"$constructor":n,(n==="$constructor"||System.String.startsWith(n,"constructor$"))&&Bridge.isFunction(this[n]))){this[n].apply(this,Array.prototype.slice.call(arguments,1));return}this.$constructor&&this.$constructor.apply(this,arguments)},initConfig:function(n,t,i,r,u){var f,e=Bridge.isFunction(i),o=function(){var t,n;if(n=Bridge.isFunction(i)?i():i,n.fields)for(t in n.fields)this[t]=n.fields[t];if(n.properties)for(t in n.properties)Bridge.property(this,t,n.properties[t]);if(n.events)for(t in n.events)Bridge.event(this,t,n.events[t]);if(n.alias)for(t in n.alias)this[t]&&(this[t]=this[n.alias[t]]);n.init&&(f=n.init)};e||o.apply(u);u.$initMembers=function(){n&&!r&&t.$initMembers&&t.$initMembers.apply(this,arguments);e&&o.apply(this);f&&f.apply(this,arguments)}},define:function(n,t,i){function r(){if(!(this instanceof r)){var i=Array.prototype.slice.call(arguments,0),n=Object.create(r.prototype),t=r.apply(n,i);return typeof t=="object"?t:n}b||(this.$staticInit&&this.$staticInit(),this.$initMembers&&this.$initMembers.apply(this,arguments),this.$$initCtor.apply(this,arguments))}var d=!1,v,p,w,k;if(i===!0?(d=!0,i=t,t=Bridge.global):i||(i=t,t=Bridge.global),Bridge.isFunction(i))return v=function(){var u=Array.prototype.slice.call(arguments),r,f,t;return(u.unshift(n),r=Bridge.Class.genericName.apply(null,u),t=Bridge.Class.cache[r],t)?t:(f=i.apply(null,u.slice(1)),f.$cacheName=r,t=Bridge.define(r,f,!0),Bridge.get(t))},Bridge.Class.generic(n,t,v);d||(Bridge.Class.staticInitAllow=!1);i=i||{};var u=i.$inherits||i.inherits,f=i.$statics||i.statics,g=i.$entryPoint,h,nt=i.$cacheName,s,c=i.$scope||t||Bridge.global,o,tt,y,l,a,e;i.$inherits?delete i.$inherits:delete i.inherits;g&&delete i.$entryPoint;Bridge.isFunction(f)?f=null:i.$statics?delete i.$statics:delete i.statics;i.$cacheName&&delete i.$cacheName;c=Bridge.Class.set(c,n,r);nt&&(Bridge.Class.cache[nt]=r);r.$$name=n;u&&Bridge.isFunction(u)&&(u=u());h=u?u[0].prototype:this.prototype;b=!0;s=u?new u[0]:{};b=!1;f&&(p=f.$config||f.config,p&&!Bridge.isFunction(p)&&(Bridge.Class.initConfig(u,h,p,!0,r),f.$config?delete f.$config:delete f.config));w=i.$config||i.config;w&&!Bridge.isFunction(w)?(Bridge.Class.initConfig(u,h,w,!1,i),i.$config?delete i.$config:delete i.config):i.$initMembers=u&&h.$initMembers?function(){h.$initMembers.apply(this,arguments)}:function(){};i.$$initCtor=Bridge.Class.initCtor;y=0;k=[];for(e in i)k.push(e);for(o=0;o<k.length;o++)e=k[o],tt=i[e],l=e==="constructor",a=l?"$constructor":e,Bridge.isFunction(tt)&&(l||System.String.startsWith(e,"constructor$"))&&(y++,l=!0),s[a]=i[e],l&&(function(n){r[n]=function(){var t=Array.prototype.slice.call(arguments);this.$initMembers&&this.$initMembers.apply(this,t);t.unshift(n);this.$$initCtor.apply(this,t)}}(a),r[a].prototype=s,r[a].prototype.constructor=r);if(y===0&&(s.$constructor=u?function(){h.$constructor.apply(this,arguments)}:function(){}),y>1&&(s.$multipleCtors=!0),s.$$name=n,r.prototype=s,r.prototype.constructor=r,i.$interface&&(r.$interface=i.$interface,delete i.$interface),f)for(e in f)r[e]=f[e];for(u||(u=[Object]),r.$$inherits=u,o=0;o<u.length;o++)c=u[o],c.$$inheritors||(c.$$inheritors=[]),c.$$inheritors.push(r);return v=function(){Bridge.Class.staticInitAllow&&(r.$staticInit=null,r.$initMembers&&r.$initMembers.call(r),r.constructor&&r.constructor.call(r))},g&&Bridge.Class.$queue.push(r),r.$staticInit=v,r},addExtend:function(n,t){var i,r;for(Array.prototype.push.apply(n.$$inherits,t),i=0;i<t.length;i++)r=t[i],r.$$inheritors||(r.$$inheritors=[]),r.$$inheritors.push(n)},set:function(n,t,i,r){for(var u=t.split("."),e,h,o,s,f=0;f<u.length-1;f++)typeof n[u[f]]=="undefined"&&(n[u[f]]={}),n=n[u[f]];if(e=u[u.length-1],o=n[e],o)for(h in o)s=o[h],typeof s=="function"&&s.$$name&&function(n,t,i){Object.defineProperty(n,t,{get:function(){return Bridge.Class.staticInitAllow&&(i.$staticInit&&i.$staticInit(),Bridge.Class.defineProperty(n,t,i)),i},set:function(n){i=n},enumerable:!0,configurable:!0})}(i,h,s);return r!==!0?function(n,t,i){Object.defineProperty(n,t,{get:function(){return Bridge.Class.staticInitAllow&&(i.$staticInit&&i.$staticInit(),Bridge.Class.defineProperty(n,t,i)),i},set:function(n){i=n},enumerable:!0,configurable:!0})}(n,e,i):n[e]=i,n},defineProperty:function(n,t,i){Object.defineProperty(n,t,{value:i,enumerable:!0,configurable:!0})},genericName:function(){for(var t=arguments[0],n=1;n<arguments.length;n++)t+="$"+Bridge.getTypeName(arguments[n]);return t},generic:function(n,t,i){return i||(i=t,t=Bridge.global),i.$$name=n,Bridge.Class.set(t,n,i,!0),i},init:function(n){var t,i;for(Bridge.Class.staticInitAllow=!0,t=0;t<Bridge.Class.$queue.length;t++)i=Bridge.Class.$queue[t],i.$staticInit&&i.$staticInit();Bridge.Class.$queue.length=0;n&&n()}};Bridge.Class=vt;Bridge.Class.$queue=[];Bridge.define=Bridge.Class.define;Bridge.init=Bridge.Class.init;Bridge.define("System.IFormattable",{statics:{$is:function(n){return Bridge.isNumber(n)?!0:Bridge.isDate(n)?!0:Bridge.is(n,System.IFormattable,!0)}}});Bridge.define("System.IComparable");Bridge.define("System.IFormatProvider");Bridge.define("System.ICloneable");Bridge.define("System.IComparable$1",function(){return{}});Bridge.define("System.IEquatable$1",function(){return{}});Bridge.define("Bridge.IPromise");Bridge.define("System.IDisposable");Bridge.define("System.Char",{inherits:[System.IComparable,System.IFormattable],statics:{min:0,max:65535,instanceOf:function(n){return typeof n=="number"&&Math.round(n,0)==n&&n>=System.Char.min&&n<=System.Char.max},getDefaultValue:function(){return 0},parse:function(n){if(!Bridge.hasValue(n))throw new System.ArgumentNullException("s");if(n.length!==1)throw new System.FormatException;return n.charCodeAt(0)},tryParse:function(n,t){var i=n&&n.length===1;return t.v=i?n.charCodeAt(0):0,i},format:function(n,t,i){return Bridge.Int.format(n,t,i)},charCodeAt:function(n,t){if(n==null)throw new System.ArgumentNullException;if(n.length!=1)throw new System.FormatException("String must be exactly one character long");return n.charCodeAt(t)},isWhiteSpace:function(n){return/\s/.test(n)},isDigit:function(n){return n<256?n>=48&&n<=57:new RegExp("[0-90-9٠-٩۰-۹߀-߉०-९০-৯੦-੯૦-૯୦-୯௦-௯౦-౯೦-೯൦-൯๐-๙໐-໙༠-༩၀-၉႐-႙០-៩᠐-᠙᥆-᥏᧐-᧙᪀-᪉᪐-᪙᭐-᭙᮰-᮹᱀-᱉᱐-᱙꘠-꘩꣐-꣙꤀-꤉꧐-꧙꩐-꩙꯰-꯹０-９]").test(String.fromCharCode(n))},isLetter:function(n){return n<256?n>=65&&n<=90||n>=97&&n<=122:new RegExp("[A-Za-za-zµß-öø-ÿāăąćĉċčďđēĕėęěĝğġģĥħĩīĭįıĳĵķĸĺļľŀłńņňŉŋōŏőœŕŗřśŝşšţťŧũūŭůűųŵŷźżž-ƀƃƅƈƌƍƒƕƙ-ƛƞơƣƥƨƪƫƭưƴƶƹƺƽ-ƿǆǉǌǎǐǒǔǖǘǚǜǝǟǡǣǥǧǩǫǭǯǰǳǵǹǻǽǿȁȃȅȇȉȋȍȏȑȓȕȗșțȝȟȡȣȥȧȩȫȭȯȱȳ-ȹȼȿɀɂɇɉɋɍɏ-ʓʕ-ʯͱͳͷͻ-ͽΐά-ώϐϑϕ-ϗϙϛϝϟϡϣϥϧϩϫϭϯ-ϳϵϸϻϼа-џѡѣѥѧѩѫѭѯѱѳѵѷѹѻѽѿҁҋҍҏґғҕҗҙқҝҟҡңҥҧҩҫҭүұҳҵҷҹһҽҿӂӄӆӈӊӌӎӏӑӓӕӗәӛӝӟӡӣӥӧөӫӭӯӱӳӵӷӹӻӽӿԁԃԅԇԉԋԍԏԑԓԕԗԙԛԝԟԡԣԥԧա-ևᴀ-ᴫᵫ-ᵷᵹ-ᶚḁḃḅḇḉḋḍḏḑḓḕḗḙḛḝḟḡḣḥḧḩḫḭḯḱḳḵḷḹḻḽḿṁṃṅṇṉṋṍṏṑṓṕṗṙṛṝṟṡṣṥṧṩṫṭṯṱṳṵṷṹṻṽṿẁẃẅẇẉẋẍẏẑẓẕ-ẝẟạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹỻỽỿ-ἇἐ-ἕἠ-ἧἰ-ἷὀ-ὅὐ-ὗὠ-ὧὰ-ώᾀ-ᾇᾐ-ᾗᾠ-ᾧᾰ-ᾴᾶᾷιῂ-ῄῆῇῐ-ΐῖῗῠ-ῧῲ-ῴῶῷℊℎℏℓℯℴℹℼℽⅆ-ⅉⅎↄⰰ-ⱞⱡⱥⱦⱨⱪⱬⱱⱳⱴⱶ-ⱻⲁⲃⲅⲇⲉⲋⲍⲏⲑⲓⲕⲗⲙⲛⲝⲟⲡⲣⲥⲧⲩⲫⲭⲯⲱⲳⲵⲷⲹⲻⲽⲿⳁⳃⳅⳇⳉⳋⳍⳏⳑⳓⳕⳗⳙⳛⳝⳟⳡⳣⳤⳬⳮⳳⴀ-ⴥⴧⴭꙁꙃꙅꙇꙉꙋꙍꙏꙑꙓꙕꙗꙙꙛꙝꙟꙡꙣꙥꙧꙩꙫꙭꚁꚃꚅꚇꚉꚋꚍꚏꚑꚓꚕꚗꜣꜥꜧꜩꜫꜭꜯ-ꜱꜳꜵꜷꜹꜻꜽꜿꝁꝃꝅꝇꝉꝋꝍꝏꝑꝓꝕꝗꝙꝛꝝꝟꝡꝣꝥꝧꝩꝫꝭꝯꝱ-ꝸꝺꝼꝿꞁꞃꞅꞇꞌꞎꞑꞓꞡꞣꞥꞧꞩꟺﬀ-ﬆﬓ-ﬗａ-ｚA-ZÀ-ÖØ-ÞĀĂĄĆĈĊČĎĐĒĔĖĘĚĜĞĠĢĤĦĨĪĬĮİĲĴĶĹĻĽĿŁŃŅŇŊŌŎŐŒŔŖŘŚŜŞŠŢŤŦŨŪŬŮŰŲŴŶŸŹŻŽƁƂƄƆƇƉ-ƋƎ-ƑƓƔƖ-ƘƜƝƟƠƢƤƦƧƩƬƮƯƱ-ƳƵƷƸƼǄǇǊǍǏǑǓǕǗǙǛǞǠǢǤǦǨǪǬǮǱǴǶ-ǸǺǼǾȀȂȄȆȈȊȌȎȐȒȔȖȘȚȜȞȠȢȤȦȨȪȬȮȰȲȺȻȽȾɁɃ-ɆɈɊɌɎͰͲͶΆΈ-ΊΌΎΏΑ-ΡΣ-ΫϏϒ-ϔϘϚϜϞϠϢϤϦϨϪϬϮϴϷϹϺϽ-ЯѠѢѤѦѨѪѬѮѰѲѴѶѸѺѼѾҀҊҌҎҐҒҔҖҘҚҜҞҠҢҤҦҨҪҬҮҰҲҴҶҸҺҼҾӀӁӃӅӇӉӋӍӐӒӔӖӘӚӜӞӠӢӤӦӨӪӬӮӰӲӴӶӸӺӼӾԀԂԄԆԈԊԌԎԐԒԔԖԘԚԜԞԠԢԤԦԱ-ՖႠ-ჅჇჍḀḂḄḆḈḊḌḎḐḒḔḖḘḚḜḞḠḢḤḦḨḪḬḮḰḲḴḶḸḺḼḾṀṂṄṆṈṊṌṎṐṒṔṖṘṚṜṞṠṢṤṦṨṪṬṮṰṲṴṶṸṺṼṾẀẂẄẆẈẊẌẎẐẒẔẞẠẢẤẦẨẪẬẮẰẲẴẶẸẺẼẾỀỂỄỆỈỊỌỎỐỒỔỖỘỚỜỞỠỢỤỦỨỪỬỮỰỲỴỶỸỺỼỾἈ-ἏἘ-ἝἨ-ἯἸ-ἿὈ-ὍὙὛὝὟὨ-ὯᾸ-ΆῈ-ΉῘ-ΊῨ-ῬῸ-Ώℂℇℋ-ℍℐ-ℒℕℙ-ℝℤΩℨK-ℭℰ-ℳℾℿⅅↃⰀ-ⰮⱠⱢ-ⱤⱧⱩⱫⱭ-ⱰⱲⱵⱾ-ⲀⲂⲄⲆⲈⲊⲌⲎⲐⲒⲔⲖⲘⲚⲜⲞⲠⲢⲤⲦⲨⲪⲬⲮⲰⲲⲴⲶⲸⲺⲼⲾⳀⳂⳄⳆⳈⳊⳌⳎⳐⳒⳔⳖⳘⳚⳜⳞⳠⳢⳫⳭⳲꙀꙂꙄꙆꙈꙊꙌꙎꙐꙒꙔꙖꙘꙚꙜꙞꙠꙢꙤꙦꙨꙪꙬꚀꚂꚄꚆꚈꚊꚌꚎꚐꚒꚔꚖꜢꜤꜦꜨꜪꜬꜮꜲꜴꜶꜸꜺꜼꜾꝀꝂꝄꝆꝈꝊꝌꝎꝐꝒꝔꝖꝘꝚꝜꝞꝠꝢꝤꝦꝨꝪꝬꝮꝹꝻꝽꝾꞀꞂꞄꞆꞋꞍꞐꞒꞠꞢꞤꞦꞨꞪＡ-Ｚǅǈǋǲᾈ-ᾏᾘ-ᾟᾨ-ᾯᾼῌῼʰ-ˁˆ-ˑˠ-ˤˬˮʹͺՙـۥۦߴߵߺࠚࠤࠨॱๆໆჼៗᡃᪧᱸ-ᱽᴬ-ᵪᵸᶛ-ᶿⁱⁿₐ-ₜⱼⱽⵯⸯ々〱-〵〻ゝゞー-ヾꀕꓸ-ꓽꘌꙿꜗ-ꜟꝰꞈꟸꟹꧏꩰꫝꫳꫴｰﾞﾟªºƻǀ-ǃʔא-תװ-ײؠ-ؿف-يٮٯٱ-ۓەۮۯۺ-ۼۿܐܒ-ܯݍ-ޥޱߊ-ߪࠀ-ࠕࡀ-ࡘࢠࢢ-ࢬऄ-हऽॐक़-ॡॲ-ॷॹ-ॿঅ-ঌএঐও-নপ-রলশ-হঽৎড়ঢ়য়-ৡৰৱਅ-ਊਏਐਓ-ਨਪ-ਰਲਲ਼ਵਸ਼ਸਹਖ਼-ੜਫ਼ੲ-ੴઅ-ઍએ-ઑઓ-નપ-રલળવ-હઽૐૠૡଅ-ଌଏଐଓ-ନପ-ରଲଳଵ-ହଽଡ଼ଢ଼ୟ-ୡୱஃஅ-ஊஎ-ஐஒ-கஙசஜஞடணதந-பம-ஹௐఅ-ఌఎ-ఐఒ-నప-ళవ-హఽౘౙౠౡಅ-ಌಎ-ಐಒ-ನಪ-ಳವ-ಹಽೞೠೡೱೲഅ-ഌഎ-ഐഒ-ഺഽൎൠൡൺ-ൿඅ-ඖක-නඳ-රලව-ෆก-ะาำเ-ๅກຂຄງຈຊຍດ-ທນ-ຟມ-ຣລວສຫອ-ະາຳຽເ-ໄໜ-ໟༀཀ-ཇཉ-ཬྈ-ྌက-ဪဿၐ-ၕၚ-ၝၡၥၦၮ-ၰၵ-ႁႎა-ჺჽ-ቈቊ-ቍቐ-ቖቘቚ-ቝበ-ኈኊ-ኍነ-ኰኲ-ኵኸ-ኾዀዂ-ዅወ-ዖዘ-ጐጒ-ጕጘ-ፚᎀ-ᎏᎠ-Ᏼᐁ-ᙬᙯ-ᙿᚁ-ᚚᚠ-ᛪᜀ-ᜌᜎ-ᜑᜠ-ᜱᝀ-ᝑᝠ-ᝬᝮ-ᝰក-ឳៜᠠ-ᡂᡄ-ᡷᢀ-ᢨᢪᢰ-ᣵᤀ-ᤜᥐ-ᥭᥰ-ᥴᦀ-ᦫᧁ-ᧇᨀ-ᨖᨠ-ᩔᬅ-ᬳᭅ-ᭋᮃ-ᮠᮮᮯᮺ-ᯥᰀ-ᰣᱍ-ᱏᱚ-ᱷᳩ-ᳬᳮ-ᳱᳵᳶℵ-ℸⴰ-ⵧⶀ-ⶖⶠ-ⶦⶨ-ⶮⶰ-ⶶⶸ-ⶾⷀ-ⷆⷈ-ⷎⷐ-ⷖⷘ-ⷞ〆〼ぁ-ゖゟァ-ヺヿㄅ-ㄭㄱ-ㆎㆠ-ㆺㇰ-ㇿ㐀-䶵一-鿌ꀀ-ꀔꀖ-ꒌꓐ-ꓷꔀ-ꘋꘐ-ꘟꘪꘫꙮꚠ-ꛥꟻ-ꠁꠃ-ꠅꠇ-ꠊꠌ-ꠢꡀ-ꡳꢂ-ꢳꣲ-ꣷꣻꤊ-ꤥꤰ-ꥆꥠ-ꥼꦄ-ꦲꨀ-ꨨꩀ-ꩂꩄ-ꩋꩠ-ꩯꩱ-ꩶꩺꪀ-ꪯꪱꪵꪶꪹ-ꪽꫀꫂꫛꫜꫠ-ꫪꫲꬁ-ꬆꬉ-ꬎꬑ-ꬖꬠ-ꬦꬨ-ꬮꯀ-ꯢ가-힣ힰ-ퟆퟋ-ퟻ豈-舘並-龎יִײַ-ﬨשׁ-זּטּ-לּמּנּסּףּפּצּ-ﮱﯓ-ﴽﵐ-ﶏﶒ-ﷇﷰ-ﷻﹰ-ﹴﹶ-ﻼｦ-ｯｱ-ﾝﾠ-ﾾￂ-ￇￊ-ￏￒ-ￗￚ-ￜ]").test(String.fromCharCode(n))},isHighSurrogate:function(n){return new RegExp("[\uD800-\uDBFF]").test(String.fromCharCode(n))},isLowSurrogate:function(n){return new RegExp("[\uDC00-\uDFFF]").test(String.fromCharCode(n))},isSurrogate:function(n){return new RegExp("[\uD800-\uDFFF]").test(String.fromCharCode(n))},isNull:function(n){return new RegExp("\x00").test(String.fromCharCode(n))},isSymbol:function(n){return n<256?[36,43,60,61,62,94,96,124,126,162,163,164,165,166,167,168,169,172,174,175,176,177,180,182,184,215,247].indexOf(n)!=-1:new RegExp("[₠-⃏⃐-⃿℀-⅏⅐-↏←-⇿∀-⋿⌀-⏿■-◿☀-⛿✀-➿⟀-⟯⟰-⟿⠀-⣿⤀-⥿⦀-⧿⨀-⫿⬀-⯿]").test(String.fromCharCode(n))},isSeparator:function(n){return n<256?n==32||n==160:new RegExp("[\u2028\u2029   ᠎ -   　]").test(String.fromCharCode(n))},isPunctuation:function(n){return n<256?[33,34,35,37,38,39,40,41,42,44,45,46,47,58,59,63,64,91,92,93,95,123,125,161,171,173,183,187,191].indexOf(n)!=-1:new RegExp("[!-#%-*,-/:;?@[-]_{}¡§«¶·»¿;·՚-՟։֊־׀׃׆׳״؉؊،؍؛؞؟٪-٭۔܀-܍߷-߹࠰-࠾࡞।॥॰૰෴๏๚๛༄-༒༔༺-༽྅࿐-࿔࿙࿚၊-၏჻፠-፨᐀᙭᙮᚛᚜᛫-᛭᜵᜶។-៖៘-៚᠀-᠊᥄᥅᨞᨟᪠-᪦᪨-᪭᭚-᭠᯼-᯿᰻-᰿᱾᱿᳀-᳇᳓‐-‧‰-⁃⁅-⁑⁓-⁞⁽⁾₍₎〈〉❨-❵⟅⟆⟦-⟯⦃-⦘⧘-⧛⧼⧽⳹-⳼⳾⳿⵰⸀-⸮⸰-⸻、-〃〈-】〔-〟〰〽゠・꓾꓿꘍-꘏꙳꙾꛲-꛷꡴-꡷꣎꣏꣸-꣺꤮꤯꥟꧁-꧍꧞꧟꩜-꩟꫞꫟꫰꫱꯫﴾﴿︐-︙︰-﹒﹔-﹡﹣﹨﹪﹫！-＃％-＊，-／：；？＠［-］＿｛｝｟-･-֊־᐀᠆‐-―⸗⸚⸺⸻〜〰゠︱︲﹘﹣－([{༺༼᚛‚„⁅⁽₍〈❨❪❬❮❰❲❴⟅⟦⟨⟪⟬⟮⦃⦅⦇⦉⦋⦍⦏⦑⦓⦕⦗⧘⧚⧼⸢⸤⸦⸨〈《「『【〔〖〘〚〝﴾︗︵︷︹︻︽︿﹁﹃﹇﹙﹛﹝（［｛｟｢)]}༻༽᚜⁆⁾₎〉❩❫❭❯❱❳❵⟆⟧⟩⟫⟭⟯⦄⦆⦈⦊⦌⦎⦐⦒⦔⦖⦘⧙⧛⧽⸣⸥⸧⸩〉》」』】〕〗〙〛〞〟﴿︘︶︸︺︼︾﹀﹂﹄﹈﹚﹜﹞）］｝｠｣«‘‛“‟‹⸂⸄⸉⸌⸜⸠»’”›⸃⸅⸊⸍⸝⸡_‿⁀⁔︳︴﹍-﹏＿!-#%-'*,./:;?@\\¡§¶·¿;·՚-՟։׀׃׆׳״؉؊،؍؛؞؟٪-٭۔܀-܍߷-߹࠰-࠾࡞।॥॰૰෴๏๚๛༄-༒༔྅࿐-࿔࿙࿚၊-၏჻፠-፨᙭᙮᛫-᛭᜵᜶។-៖៘-៚᠀-᠅᠇-᠊᥄᥅᨞᨟᪠-᪦᪨-᪭᭚-᭠᯼-᯿᰻-᰿᱾᱿᳀-᳇᳓‖‗†-‧‰-‸※-‾⁁-⁃⁇-⁑⁓⁕-⁞⳹-⳼⳾⳿⵰⸀⸁⸆-⸈⸋⸎-⸖⸘⸙⸛⸞⸟⸪-⸮⸰-⸹、-〃〽・꓾꓿꘍-꘏꙳꙾꛲-꛷꡴-꡷꣎꣏꣸-꣺꤮꤯꥟꧁-꧍꧞꧟꩜-꩟꫞꫟꫰꫱꯫︐-︖︙︰﹅﹆﹉-﹌﹐-﹒﹔-﹗﹟-﹡﹨﹪﹫！-＃％-＇＊，．／：；？＠＼｡､･]").test(String.fromCharCode(n))},isNumber:function(n){return n<256?[48,49,50,51,52,53,54,55,56,57,178,179,185,188,189,190].indexOf(n)!=-1:new RegExp("[0-9²³¹¼-¾٠-٩۰-۹߀-߉०-९০-৯৴-৹੦-੯૦-૯୦-୯୲-୷௦-௲౦-౯౸-౾೦-೯൦-൵๐-๙໐-໙༠-༳၀-၉႐-႙፩-፼ᛮ-ᛰ០-៩៰-៹᠐-᠙᥆-᥏᧐-᧚᪀-᪉᪐-᪙᭐-᭙᮰-᮹᱀-᱉᱐-᱙⁰⁴-⁹₀-₉⅐-ↂↅ-↉①-⒛⓪-⓿❶-➓⳽〇〡-〩〸-〺㆒-㆕㈠-㈩㉈-㉏㉑-㉟㊀-㊉㊱-㊿꘠-꘩ꛦ-ꛯ꠰-꠵꣐-꣙꤀-꤉꧐-꧙꩐-꩙꯰-꯹０-９0-9٠-٩۰-۹߀-߉०-९০-৯੦-੯૦-૯୦-୯௦-௯౦-౯೦-೯൦-൯๐-๙໐-໙༠-༩၀-၉႐-႙០-៩᠐-᠙᥆-᥏᧐-᧙᪀-᪉᪐-᪙᭐-᭙᮰-᮹᱀-᱉᱐-᱙꘠-꘩꣐-꣙꤀-꤉꧐-꧙꩐-꩙꯰-꯹０-９ᛮ-ᛰⅠ-ↂↅ-ↈ〇〡-〩〸-〺ꛦ-ꛯ²³¹¼-¾৴-৹୲-୷௰-௲౸-౾൰-൵༪-༳፩-፼៰-៹᧚⁰⁴-⁹₀-₉⅐-⅟↉①-⒛⓪-⓿❶-➓⳽㆒-㆕㈠-㈩㉈-㉏㉑-㉟㊀-㊉㊱-㊿꠰-꠵]").test(String.fromCharCode(n))},isControl:function(n){return n<256?n>=0&&n<=31||n>=127&&n<=159:new RegExp("[\x00-\x1f-]").test(String.fromCharCode(n))}}});Bridge.Class.addExtend(System.Char,[System.IComparable$1(System.Char),System.IEquatable$1(System.Char)]);Bridge.define("System.Exception",{constructor:function(n,t){this.message=n?n:null;this.innerException=t?t:null;this.errorStack=new Error;this.data=new System.Collections.Generic.Dictionary$2(Object,Object)()},getMessage:function(){return this.message},getInnerException:function(){return this.innerException},getStackTrace:function(){return this.errorStack.stack},getData:function(){return this.data},toString:function(){return this.getMessage()},statics:{create:function(n){return Bridge.is(n,System.Exception)?n:n instanceof TypeError?new System.NullReferenceException(n.message,new Bridge.ErrorException(n)):n instanceof RangeError?new System.ArgumentOutOfRangeException(null,n.message,new Bridge.ErrorException(n)):n instanceof Error?new Bridge.ErrorException(n):new System.Exception(n?n.toString():null)}}});Bridge.define("System.SystemException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"System error.",t)}});Bridge.define("System.OutOfMemoryException",{inherits:[System.SystemException],constructor:function(n,t){n||(n="Insufficient memory to continue the execution of the program.");System.SystemException.prototype.$constructor.call(this,n,t)}});Bridge.define("System.IndexOutOfRangeException",{inherits:[System.SystemException],constructor:function(n,t){n||(n="Index was outside the bounds of the array.");System.SystemException.prototype.$constructor.call(this,n,t)}});Bridge.define("System.TimeoutException",{inherits:[System.SystemException],constructor:function(n,t){n||(n="The operation has timed out.");System.SystemException.prototype.$constructor.call(this,n,t)}});Bridge.define("System.RegexMatchTimeoutException",{inherits:[System.TimeoutException],_regexInput:"",_regexPattern:"",_matchTimeout:null,config:{init:function(){this._matchTimeout=System.TimeSpan.fromTicks(-1)}},constructor:function(){System.TimeoutException.prototype.$constructor.call(this)},constructor$1:function(n){System.TimeoutException.prototype.$constructor.call(this,n)},constructor$2:function(n,t){System.TimeoutException.prototype.$constructor.call(this,n,t)},constructor$3:function(n,t,i){this._regexInput=n;this._regexPattern=t;this._matchTimeout=i;this.constructor$1("The RegEx engine has timed out while trying to match a pattern to an input string. This can occur for many reasons, including very large inputs or excessive backtracking caused by nested quantifiers, back-references and other factors.")},getPattern:function(){return this._regexPattern},getInput:function(){return this._regexInput},getMatchTimeout:function(){return this._matchTimeout}});Bridge.define("Bridge.ErrorException",{inherits:[System.Exception],constructor:function(n){System.Exception.prototype.$constructor.call(this,n.message);this.errorStack=n;this.error=n},getError:function(){return this.error}});Bridge.define("System.ArgumentException",{inherits:[System.Exception],constructor:function(n,t,i){System.Exception.prototype.$constructor.call(this,n||"Value does not fall within the expected range.",i);this.paramName=t?t:null},getParamName:function(){return this.paramName}});Bridge.define("System.ArgumentNullException",{inherits:[System.ArgumentException],constructor:function(n,t,i){t||(t="Value cannot be null.",n&&(t+="\nParameter name: "+n));System.ArgumentException.prototype.$constructor.call(this,t,n,i)}});Bridge.define("System.ArgumentOutOfRangeException",{inherits:[System.ArgumentException],constructor:function(n,t,i,r){t||(t="Value is out of range.",n&&(t+="\nParameter name: "+n));System.ArgumentException.prototype.$constructor.call(this,t,n,i);this.actualValue=r?r:null},getActualValue:function(){return this.actualValue}});Bridge.define("System.Globalization.CultureNotFoundException",{inherits:[System.ArgumentException],constructor:function(n,t,i,r,u){i||(i="Culture is not supported.",n&&(i+="\nParameter name: "+n),t&&(i+="\n"+t+" is an invalid culture identifier."));System.ArgumentException.prototype.$constructor.call(this,i,n,r);this.invalidCultureName=t?t:null;this.invalidCultureId=u?u:null},getInvalidCultureName:function(){return this.invalidCultureName},getInvalidCultureId:function(){return this.invalidCultureId}});Bridge.define("System.Collections.Generic.KeyNotFoundException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"Key not found.",t)}});Bridge.define("System.ArithmeticException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"Overflow or underflow in the arithmetic operation.",t)}});Bridge.define("System.DivideByZeroException",{inherits:[System.ArithmeticException],constructor:function(n,t){System.ArithmeticException.prototype.$constructor.call(this,n||"Division by 0.",t)}});Bridge.define("System.OverflowException",{inherits:[System.ArithmeticException],constructor:function(n,t){System.ArithmeticException.prototype.$constructor.call(this,n||"Arithmetic operation resulted in an overflow.",t)}});Bridge.define("System.FormatException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"Invalid format.",t)}});Bridge.define("System.InvalidCastException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"The cast is not valid.",t)}});Bridge.define("System.InvalidOperationException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"Operation is not valid due to the current state of the object.",t)}});Bridge.define("System.NotImplementedException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"The method or operation is not implemented.",t)}});Bridge.define("System.NotSupportedException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"Specified method is not supported.",t)}});Bridge.define("System.NullReferenceException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"Object is null.",t)}});Bridge.define("System.RankException",{inherits:[System.Exception],constructor:function(n,t){System.Exception.prototype.$constructor.call(this,n||"Attempted to operate on an array with the incorrect number of dimensions.",t)}});Bridge.define("Bridge.PromiseException",{inherits:[System.Exception],constructor:function(n,t,i){System.Exception.prototype.$constructor.call(this,t||(n.length&&n[0]?n[0].toString():"An error occurred"),i);this.arguments=System.Array.clone(n)},getArguments:function(){return this.arguments}});Bridge.define("System.OperationCanceledException",{inherits:[System.Exception],constructor:function(n,t,i){System.Exception.prototype.$constructor.call(this,n||"Operation was canceled.",i);this.cancellationToken=t||System.Threading.CancellationToken.none}});Bridge.define("System.Threading.Tasks.TaskCanceledException",{inherits:[System.OperationCanceledException],constructor:function(n,t,i){System.OperationCanceledException.prototype.$constructor.call(this,n||"A task was canceled.",null,i);this.task=t||null}});Bridge.define("System.AggregateException",{inherits:[System.Exception],constructor:function(n,t){this.innerExceptions=new System.Collections.ObjectModel.ReadOnlyCollection$1(System.Exception)(Bridge.hasValue(t)?Bridge.toArray(t):[]);System.Exception.prototype.$constructor.call(this,n||"One or more errors occurred.",this.innerExceptions.items.length?this.innerExceptions.items[0]:null)},handle:function(n){var r,i,t;if(!Bridge.hasValue(n))throw new System.ArgumentNullException("predicate");for(r=this.innerExceptions.getCount(),i=[],t=0;t<r;t++)n(this.innerExceptions.get(t))||i.push(this.innerExceptions.get(t));if(i.length>0)throw new System.AggregateException(this.getMessage(),i);},flatten:function(){var e=new System.Collections.Generic.List$1(System.Exception)(),n=new System.Collections.Generic.List$1(System.AggregateException)(),r,u,t,i,f;for(n.add(this),r=0;n.getCount()>r;)for(u=n.getItem(r++).innerExceptions,t=0;t<u.getCount();t++)(i=u.get(t),Bridge.hasValue(i))&&(f=Bridge.as(i,System.AggregateException),Bridge.hasValue(f)?n.add(f):e.add(i));return new System.AggregateException(this.getMessage(),e)}});Bridge.define("System.IndexOutOfRangeException",{inherits:[System.SystemException],constructor:function(n,t){n||(n="Index was outside the bounds of the array.");System.SystemException.prototype.$constructor.call(this,n,t)}});Bridge.define("System.Globalization.DateTimeFormatInfo",{inherits:[System.IFormatProvider,System.ICloneable],statics:{$allStandardFormats:{d:"shortDatePattern",D:"longDatePattern",f:"longDatePattern shortTimePattern",F:"longDatePattern longTimePattern",g:"shortDatePattern shortTimePattern",G:"shortDatePattern longTimePattern",m:"monthDayPattern",M:"monthDayPattern",o:"roundtripFormat",O:"roundtripFormat",r:"rfc1123",R:"rfc1123",s:"sortableDateTimePattern",S:"sortableDateTimePattern1",t:"shortTimePattern",T:"longTimePattern",u:"universalSortableDateTimePattern",U:"longDatePattern longTimePattern",y:"yearMonthPattern",Y:"yearMonthPattern"},constructor:function(){this.invariantInfo=Bridge.merge(new System.Globalization.DateTimeFormatInfo,{abbreviatedDayNames:["Sun","Mon","Tue","Wed","Thu","Fri","Sat"],abbreviatedMonthGenitiveNames:["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec",""],abbreviatedMonthNames:["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec",""],amDesignator:"AM",dateSeparator:"/",dayNames:["Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"],firstDayOfWeek:0,fullDateTimePattern:"dddd, dd MMMM yyyy HH:mm:ss",longDatePattern:"dddd, dd MMMM yyyy",longTimePattern:"HH:mm:ss",monthDayPattern:"MMMM dd",monthGenitiveNames:["January","February","March","April","May","June","July","August","September","October","November","December",""],monthNames:["January","February","March","April","May","June","July","August","September","October","November","December",""],pmDesignator:"PM",rfc1123:"ddd, dd MMM yyyy HH':'mm':'ss 'GMT'",shortDatePattern:"MM/dd/yyyy",shortestDayNames:["Su","Mo","Tu","We","Th","Fr","Sa"],shortTimePattern:"HH:mm",sortableDateTimePattern:"yyyy'-'MM'-'dd'T'HH':'mm':'ss",sortableDateTimePattern1:"yyyy'-'MM'-'dd",timeSeparator:":",universalSortableDateTimePattern:"yyyy'-'MM'-'dd HH':'mm':'ss'Z'",yearMonthPattern:"yyyy MMMM",roundtripFormat:"yyyy'-'MM'-'dd'T'HH':'mm':'ss.uzzz"})}},getFormat:function(n){switch(n){case System.Globalization.DateTimeFormatInfo:return this;default:return null}},getAbbreviatedDayName:function(n){if(n<0||n>6)throw new System.ArgumentOutOfRangeException("dayofweek");return this.abbreviatedDayNames[n]},getAbbreviatedMonthName:function(n){if(n<1||n>13)throw new System.ArgumentOutOfRangeException("month");return this.abbreviatedMonthNames[n-1]},getAllDateTimePatterns:function(n,t){var i=System.Globalization.DateTimeFormatInfo.$allStandardFormats,r,e,f,u,o=[];if(n){if(!i[n]){if(t)return null;throw new System.ArgumentException(null,"format");}r={};r[n]=i[n]}else r=i;for(i in r){for(e=r[i].split(" "),f="",u=0;u<e.length;u++)f=(u===0?"":f+" ")+this[e[u]];o.push(f)}return o},getDayName:function(n){if(n<0||n>6)throw new System.ArgumentOutOfRangeException("dayofweek");return this.dayNames[n]},getMonthName:function(n){if(n<1||n>13)throw new System.ArgumentOutOfRangeException("month");return this.monthNames[n-1]},getShortestDayName:function(n){if(n<0||n>6)throw new System.ArgumentOutOfRangeException("dayOfWeek");return this.shortestDayNames[n]},clone:function(){return Bridge.copy(new System.Globalization.DateTimeFormatInfo,this,["abbreviatedDayNames","abbreviatedMonthGenitiveNames","abbreviatedMonthNames","amDesignator","dateSeparator","dayNames","firstDayOfWeek","fullDateTimePattern","longDatePattern","longTimePattern","monthDayPattern","monthGenitiveNames","monthNames","pmDesignator","rfc1123","shortDatePattern","shortestDayNames","shortTimePattern","sortableDateTimePattern","timeSeparator","universalSortableDateTimePattern","yearMonthPattern","roundtripFormat"])}});Bridge.define("System.Globalization.NumberFormatInfo",{inherits:[System.IFormatProvider,System.ICloneable],statics:{constructor:function(){this.numberNegativePatterns=["(n)","-n","- n","n-","n -"];this.currencyNegativePatterns=["($n)","-$n","$-n","$n-","(n$)","-n$","n-$","n$-","-n $","-$ n","n $-","$ n-","$ -n","n- $","($ n)","(n $)"];this.currencyPositivePatterns=["$n","n$","$ n","n $"];this.percentNegativePatterns=["-n %","-n%","-%n","%-n","%n-","n-%","n%-","-% n","n %-","% n-","% -n","n- %"];this.percentPositivePatterns=["n %","n%","%n","% n"];this.invariantInfo=Bridge.merge(new System.Globalization.NumberFormatInfo,{nanSymbol:"NaN",negativeSign:"-",positiveSign:"+",negativeInfinitySymbol:"-Infinity",positiveInfinitySymbol:"Infinity",percentSymbol:"%",percentGroupSizes:[3],percentDecimalDigits:2,percentDecimalSeparator:".",percentGroupSeparator:",",percentPositivePattern:0,percentNegativePattern:0,currencySymbol:"¤",currencyGroupSizes:[3],currencyDecimalDigits:2,currencyDecimalSeparator:".",currencyGroupSeparator:",",currencyNegativePattern:0,currencyPositivePattern:0,numberGroupSizes:[3],numberDecimalDigits:2,numberDecimalSeparator:".",numberGroupSeparator:",",numberNegativePattern:1})}},getFormat:function(n){switch(n){case System.Globalization.NumberFormatInfo:return this;default:return null}},clone:function(){return Bridge.copy(new System.Globalization.NumberFormatInfo,this,["nanSymbol","negativeSign","positiveSign","negativeInfinitySymbol","positiveInfinitySymbol","percentSymbol","percentGroupSizes","percentDecimalDigits","percentDecimalSeparator","percentGroupSeparator","percentPositivePattern","percentNegativePattern","currencySymbol","currencyGroupSizes","currencyDecimalDigits","currencyDecimalSeparator","currencyGroupSeparator","currencyNegativePattern","currencyPositivePattern","numberGroupSizes","numberDecimalDigits","numberDecimalSeparator","numberGroupSeparator","numberNegativePattern"])}});Bridge.define("System.Globalization.CultureInfo",{inherits:[System.IFormatProvider,System.ICloneable],statics:{constructor:function(){this.cultures=this.cultures||{};this.invariantCulture=Bridge.merge(new System.Globalization.CultureInfo("iv",!0),{englishName:"Invariant Language (Invariant Country)",nativeName:"Invariant Language (Invariant Country)",numberFormat:System.Globalization.NumberFormatInfo.invariantInfo,dateTimeFormat:System.Globalization.DateTimeFormatInfo.invariantInfo});this.setCurrentCulture(System.Globalization.CultureInfo.invariantCulture)},getCurrentCulture:function(){return this.currentCulture},setCurrentCulture:function(n){this.currentCulture=n;System.Globalization.DateTimeFormatInfo.currentInfo=n.dateTimeFormat;System.Globalization.NumberFormatInfo.currentInfo=n.numberFormat},getCultureInfo:function(n){if(!n)throw new System.ArgumentNullException("name");return this.cultures[n]},getCultures:function(){for(var t=Bridge.getPropertyNames(this.cultures),i=[],n=0;n<t.length;n++)i.push(this.cultures[t[n]]);return i}},constructor:function(n,t){if(this.name=n,System.Globalization.CultureInfo.cultures||(System.Globalization.CultureInfo.cultures={}),System.Globalization.CultureInfo.cultures[n])Bridge.copy(this,System.Globalization.CultureInfo.cultures[n],["englishName","nativeName","numberFormat","dateTimeFormat"]);else{if(!t)throw new System.Globalization.CultureNotFoundException("name",n);System.Globalization.CultureInfo.cultures[n]=this}},getFormat:function(n){switch(n){case System.Globalization.NumberFormatInfo:return this.numberFormat;case System.Globalization.DateTimeFormatInfo:return this.dateTimeFormat;default:return null}},clone:function(){return new System.Globalization.CultureInfo(this.name)}});Bridge.Math={divRem:function(n,t,i){var r=n%t;return i.v=r,(n-r)/t},round:function(n,t,i){var u=Math.pow(10,t||0),r,f;return(n*=u,r=n>0|-(n<0),n%1==.5*r)?(f=Math.floor(n),(f+(i===4?r>0:f%2*r))/u):Math.round(n)/u},sinh:Math.sinh||function(n){return(Math.exp(n)-Math.exp(-n))/2},cosh:Math.cosh||function(n){return(Math.exp(n)+Math.exp(-n))/2},tanh:Math.tanh||function(n){if(n===Infinity)return 1;if(n===-Infinity)return-1;var t=Math.exp(2*n);return(t-1)/(t+1)}};yt={trueString:"True",falseString:"False",is:function(n,t){return t===System.IComparable||t.$$name==="System.IEquatable$1$Boolean"||t.$$name==="System.IComparable$1$Boolean"?!0:!1},instanceOf:function(n){return typeof n=="boolean"},getDefaultValue:function(){return!1},toString:function(n){return n?System.Boolean.trueString:System.Boolean.falseString},parse:function(n){if(!Bridge.hasValue(n))throw new System.ArgumentNullException("value");var t={v:!1};if(!System.Boolean.tryParse(n,t))throw new System.FormatException("Bad format for Boolean value");return t.v},tryParse:function(n,t){if(t.v=!1,!Bridge.hasValue(n))return!1;if(System.String.equals(System.Boolean.trueString,n,5))return t.v=!0,!0;if(System.String.equals(System.Boolean.falseString,n,5))return t.v=!1,!0;for(var i=0,r=n.length-1;i<n.length;){if(!System.Char.isWhiteSpace(n[i])&&!System.Char.isNull(n.charCodeAt(i)))break;i++}while(r>=i){if(!System.Char.isWhiteSpace(n[r])&&!System.Char.isNull(n.charCodeAt(r)))break;r--}return(n=n.substr(i,r-i+1),System.String.equals(System.Boolean.trueString,n,5))?(t.v=!0,!0):System.String.equals(System.Boolean.falseString,n,5)?(t.v=!1,!0):!1}};System.Boolean=yt,function(){var n=function(n,t,i){var r=Bridge.define(n,{inherits:[System.IComparable,System.IFormattable],statics:{min:t,max:i,instanceOf:function(n){return typeof n=="number"&&Math.floor(n,0)==n&&n>=t&&n<=i},getDefaultValue:function(){return 0},parse:function(n,r){return Bridge.Int.parseInt(n,t,i,r)},tryParse:function(n,r,u){return Bridge.Int.tryParseInt(n,r,t,i,u)},format:function(n,t,i){return Bridge.Int.format(n,t,i)}}});Bridge.Class.addExtend(r,[System.IComparable$1(r),System.IEquatable$1(r)])};n("System.Byte",0,255);n("System.SByte",-128,127);n("System.Int16",-32768,32767);n("System.UInt16",0,65535);n("System.Int32",-2147483648,2147483647);n("System.UInt32",0,4294967295)}();Bridge.define("Bridge.Int",{inherits:[System.IComparable,System.IFormattable],statics:{instanceOf:function(n){return typeof n=="number"&&isFinite(n)&&Math.floor(n,0)===n},getDefaultValue:function(){return 0},format:function(n,t,i){var u=(i||System.Globalization.CultureInfo.getCurrentCulture()).getFormat(System.Globalization.NumberFormatInfo),d=u.numberDecimalSeparator,tt=u.numberGroupSeparator,f=n instanceof System.Decimal,e=n instanceof System.Int64||n instanceof System.UInt64,g=f||e?n.isNegative():n<0,c,r,a,w,h,b,k,l;if(!e&&(f?!n.isFinite():!isFinite(n)))return Number.NEGATIVE_INFINITY===n||f&&g?u.negativeInfinitySymbol:u.positiveInfinitySymbol;if(!t)return this.defaultFormat(n,0,0,15,u,!0);if(c=t.match(/^([a-zA-Z])(\d*)$/),c){w=c[1].toUpperCase();r=parseInt(c[2],10);r=r>15?15:r;switch(w){case"D":return this.defaultFormat(n,isNaN(r)?1:r,0,0,u,!0);case"F":case"N":return isNaN(r)&&(r=u.numberDecimalDigits),this.defaultFormat(n,1,r,r,u,w==="F");case"G":case"E":for(var s=0,o=f||e?n.abs():Math.abs(n),v=c[1],nt=3,y,p;f||e?o.gte(10):o>=10;)f||e?o=o.div(10):o/=10,s++;while(f||e?o.ne(0)&&o.lt(1):o!==0&&o<1)f||e?o=o.mul(10):o*=10,s--;if(w==="G"){if(s>-5&&s<(r||15))return y=r?r-(s>0?s+1:1):0,p=r?r-(s>0?s+1:1):15,this.defaultFormat(n,1,y,p,u,!0);v=v==="G"?"E":"e";nt=2;y=(r||1)-1;p=(r||15)-1}else y=p=isNaN(r)?6:r;return s>=0?v+=u.positiveSign:(v+=u.negativeSign,s=-s),g&&(f||e?o=o.mul(-1):o*=-1),this.defaultFormat(o,1,y,p,u)+v+this.defaultFormat(s,nt,0,0,u,!0);case"P":return isNaN(r)&&(r=u.percentDecimalDigits),this.defaultFormat(n*100,1,r,r,u,!1,"percent");case"X":for(h=f?n.round().value.toHex().substr(2):e?n.toString(16):Math.round(n).toString(16),c[1]==="X"&&(h=h.toUpperCase()),r-=h.length;r-->0;)h="0"+h;return h;case"C":return isNaN(r)&&(r=u.currencyDecimalDigits),this.defaultFormat(n,1,r,r,u,!1,"currency");case"R":return b=f||e?n.toString():""+n,d!=="."&&(b=b.replace(".",d)),b.replace("e","E")}}if(t.indexOf(",.")!==-1||System.String.endsWith(t,",")){for(k=0,l=t.indexOf(",."),l===-1&&(l=t.length-1);l>-1&&t.charAt(l)===",";)k++,l--;f||e?n=n.div(Math.pow(1e3,k)):n/=Math.pow(1e3,k)}return t.indexOf("%")!==-1&&(f||e?n=n.mul(100):n*=100),t.indexOf("‰")!==-1&&(f||e?n=n.mul(1e3):n*=1e3),a=t.split(";"),(f||e?n.lt(0):n<0)&&a.length>1?(f||e?n=n.mul(-1):n*=-1,t=a[1]):t=a[(f||e?n.ne(0):!n)&&a.length>2?2:0],this.customFormat(n,t,u,!t.match(/^[^\.]*[0#],[0#]/))},defaultFormat:function(n,t,i,r,u,f,e){e=e||"number";var h=(u||System.Globalization.CultureInfo.getCurrentCulture()).getFormat(System.Globalization.NumberFormatInfo),s,p,w,rt,v,y,b=h[e+"GroupSizes"],o,k,d,l,a,g,nt,c="",tt=n instanceof System.Decimal,it=n instanceof System.Int64||n instanceof System.UInt64,ut=tt||it?n.isNegative():n<0;if(rt=Math.pow(10,r),s=tt?n.abs().toDecimalPlaces(r).toString():it?n.eq(System.Int64.MinValue)?n.value.toUnsigned().toString():n.abs().toString():""+ +Math.abs(n).toFixed(r),p=s.indexOf("."),p>0&&(o=h[e+"DecimalSeparator"]+s.substr(p+1),s=s.substr(0,p)),s.length<t&&(s=Array(t-s.length+1).join("0")+s),o?(o.length-1<i&&(o+=Array(i-o.length+2).join("0")),r===0?o=null:o.length-1>r&&(o=o.substr(0,r+1))):i>0&&(o=h[e+"DecimalSeparator"]+Array(i+1).join("0")),v=0,y=b[v],s.length<y)c=s,o&&(c+=o);else{for(k=s.length,d=!1,nt=f?"":h[e+"GroupSeparator"];!d;){if(a=y,l=k-a,l<0&&(y+=l,a+=l,l=0,d=!0),!a)break;g=s.substr(l,a);c=c.length?g+nt+c:g;k-=a;v<b.length-1&&(v++,y=b[v])}o&&(c+=o)}return ut?(w=System.Globalization.NumberFormatInfo[e+"NegativePatterns"][h[e+"NegativePattern"]],w.replace("-",h.negativeSign).replace("%",h.percentSymbol).replace("$",h.currencySymbol).replace("n",c)):System.Globalization.NumberFormatInfo[e+"PositivePatterns"]?(w=System.Globalization.NumberFormatInfo[e+"PositivePatterns"][h[e+"PositivePattern"]],w.replace("%",h.percentSymbol).replace("$",h.currencySymbol).replace("n",c)):c},customFormat:function(n,t,i,r){var p=0,s=-1,h=-1,w=0,b=-1,a=0,nt=1,u,f,o,v,c,k,tt=!1,y,l,e="",d=!1,it=!1,g=!1,rt=n instanceof System.Decimal,ut=n instanceof System.Int64||n instanceof System.UInt64,ft=rt||ut?n.isNegative():n<0;for(y="number",t.indexOf("%")!==-1?y="percent":t.indexOf("$")!==-1&&(y="currency"),f=0;f<t.length;f++)if(u=t.charAt(f),u==="'"||u==='"'){if(f=t.indexOf(u,f+1),f<0)break}else u==="\\"?f++:((u==="0"||u==="#")&&(w+=a,u==="0"&&(a?b=w:s<0&&(s=p)),p+=!a),a=a||u===".");for(s=s<0?1:p-s,ft&&(tt=!0),c=Math.pow(10,w),rt&&(n=n.abs().mul(c).round().div(c).toString()),n=ut?n.abs().mul(c).div(c).toString():""+Math.round(Math.abs(n)*c)/c,k=n.indexOf("."),h=k<0?n.length:k,f=h-p,l={groupIndex:Math.max(h,s),sep:r?"":i[y+"GroupSeparator"]},h===1&&n.charAt(0)==="0"&&(d=!0),o=0;o<t.length;o++)if(u=t.charAt(o),u==="'"||u==='"'){if(v=t.indexOf(u,o+1),e+=t.substring(o+1,v<0?t.length:v),v<0)break;o=v}else u==="\\"?(e+=t.charAt(o+1),o++):u==="#"||u==="0"?(g=!0,!it&&d&&u==="#"?f++:(l.buffer=e,f<h?(f>=0?(nt&&this.addGroup(n.substr(0,f),l),this.addGroup(n.charAt(f),l)):f>=h-s&&this.addGroup("0",l),nt=0):(b-->0||f<n.length)&&this.addGroup(f>=n.length?"0":n.charAt(f),l),e=l.buffer,f++)):u==="."?(g||d||(e+=n.substr(0,h),g=!0),(n.length>++f||b>0)&&(it=!0,e+=i[y+"DecimalSeparator"])):u!==","&&(e+=u);return tt&&(e="-"+e),e},addGroup:function(n,t){for(var i=t.buffer,f=t.sep,r=t.groupIndex,u=0,e=n.length;u<e;u++)i+=n.charAt(u),f&&r>1&&r--%3==1&&(i+=f);t.buffer=i;t.groupIndex=r},parseFloat:function(n,t){if(n==null)throw new System.ArgumentNullException("str");var i=(t||System.Globalization.CultureInfo.getCurrentCulture()).getFormat(System.Globalization.NumberFormatInfo),r=parseFloat(n.replace(i.numberDecimalSeparator,"."));if(isNaN(r)&&n!==i.nanSymbol){if(n===i.negativeInfinitySymbol)return Number.NEGATIVE_INFINITY;if(n===i.positiveInfinitySymbol)return Number.POSITIVE_INFINITY;throw new System.FormatException("Input string was not in a correct format.");}return r},tryParseFloat:function(n,t,i){if(i.v=0,n==null)return!1;var r=(t||System.Globalization.CultureInfo.getCurrentCulture()).getFormat(System.Globalization.NumberFormatInfo);return(i.v=parseFloat(n.replace(r.numberDecimalSeparator,".")),isNaN(i.v)&&n!==r.nanSymbol)?n===r.negativeInfinitySymbol?(i.v=Number.NEGATIVE_INFINITY,!0):n===r.positiveInfinitySymbol?(i.v=Number.POSITIVE_INFINITY,!0):!1:!0},parseInt:function(n,t,i,r){if(n==null)throw new System.ArgumentNullException("str");if(!/^[+-]?[0-9]+$/.test(n))throw new System.FormatException("Input string was not in a correct format.");var u=parseInt(n,r||10);if(isNaN(u))throw new System.FormatException("Input string was not in a correct format.");if(u<t||u>i)throw new System.OverflowException;return u},tryParseInt:function(n,t,i,r,u){return(t.v=0,!/^[+-]?[0-9]+$/.test(n))?!1:(t.v=parseInt(n,u||10),t.v<i||t.v>r)?!1:!0},isInfinite:function(n){return n===Number.POSITIVE_INFINITY||n===Number.NEGATIVE_INFINITY},trunc:function(n){return Bridge.isNumber(n)?n>0?Math.floor(n):Math.ceil(n):Bridge.Int.isInfinite(n)?n:null},div:function(n,t){if(!Bridge.isNumber(n)||!Bridge.isNumber(t))return null;if(t===0)throw new System.DivideByZeroException;return this.trunc(n/t)},mod:function(n,t){if(!Bridge.isNumber(n)||!Bridge.isNumber(t))return null;if(t===0)throw new System.DivideByZeroException;return n%t},check:function(n,t){if(System.Int64.is64Bit(n))return System.Int64.check(n,t);if(n instanceof System.Decimal)return System.Decimal.toInt(n,t);if(Bridge.isNumber(n)&&!t.instanceOf(n))throw new System.OverflowException;return Bridge.Int.isInfinite(n)?t===System.Int64||t===System.UInt64?t.MinValue:t.min:n},sxb:function(n){return Bridge.isNumber(n)?n|(n&128?4294967040:0):Bridge.Int.isInfinite(n)?System.SByte.min:null},sxs:function(n){return Bridge.isNumber(n)?n|(n&32768?4294901760:0):Bridge.Int.isInfinite(n)?System.Int16.min:null},clip8:function(n){return Bridge.isNumber(n)?Bridge.Int.sxb(n&255):Bridge.Int.isInfinite(n)?System.SByte.min:null},clipu8:function(n){return Bridge.isNumber(n)?n&255:Bridge.Int.isInfinite(n)?System.Byte.min:null},clip16:function(n){return Bridge.isNumber(n)?Bridge.Int.sxs(n&65535):Bridge.Int.isInfinite(n)?System.Int16.min:null},clipu16:function(n){return Bridge.isNumber(n)?n&65535:Bridge.Int.isInfinite(n)?System.UInt16.min:null},clip32:function(n){return Bridge.isNumber(n)?n|0:Bridge.Int.isInfinite(n)?System.Int32.min:null},clipu32:function(n){return Bridge.isNumber(n)?n>>>0:Bridge.Int.isInfinite(n)?System.UInt32.min:null},clip64:function(n){return Bridge.isNumber(n)?System.Int64(Bridge.Int.trunc(n)):Bridge.Int.isInfinite(n)?System.Int64.MinValue:null},clipu64:function(n){return Bridge.isNumber(n)?System.UInt64(Bridge.Int.trunc(n)):Bridge.Int.isInfinite(n)?System.UInt64.MinValue:null},sign:function(n){return Bridge.isNumber(n)?n===0?0:n<0?-1:1:null}}});Bridge.Class.addExtend(Bridge.Int,[System.IComparable$1(Bridge.Int),System.IEquatable$1(Bridge.Int)]);Bridge.define("System.Double",{inherits:[System.IComparable,System.IFormattable],statics:{min:-Number.MAX_VALUE,max:Number.MAX_VALUE,instanceOf:function(n){return typeof n=="number"},getDefaultValue:function(){return 0},parse:function(n,t){return Bridge.Int.parseFloat(n,t)},tryParse:function(n,t,i){return Bridge.Int.tryParseFloat(n,t,i)},format:function(n,t,i){return Bridge.Int.format(n,t,i)}}});Bridge.Class.addExtend(System.Double,[System.IComparable$1(System.Double),System.IEquatable$1(System.Double)]);Bridge.define("System.Single",{inherits:[System.IComparable,System.IFormattable],statics:{min:-34028234663852886e22,max:34028234663852886e22,instanceOf:System.Double.instanceOf,getDefaultValue:System.Double.getDefaultValue,parse:System.Double.parse,tryParse:System.Double.tryParse,format:System.Double.format}});Bridge.Class.addExtend(System.Single,[System.IComparable$1(System.Single),System.IEquatable$1(System.Single)]),function(n){function i(n,t,i){this.low=n|0;this.high=t|0;this.unsigned=!!i}function u(n){return!0===(n&&n.__isLong__)}function h(n,i){var r,u;if(i){if(n>>>=0,(u=0<=n&&256>n)&&(r=p[n]))return r;r=t(n,0>(n|0)?-1:0,!0);u&&(p[n]=r)}else{if(n|=0,(u=-128<=n&&128>n)&&(r=y[n]))return r;r=t(n,0>n?-1:0,!1);u&&(y[n]=r)}return r}function f(n,i){if(isNaN(n)||!isFinite(n))return i?s:e;if(i){if(0>n)return s;if(n>=d)return k}else{if(n<=-g)return r;if(n+1>=g)return b}return 0>n?f(-n,i).neg():t(n%4294967296|0,n/4294967296|0,i)}function t(n,t,r){return new i(n,t,r)}function v(n,t,i){var s,r,u,o,h;if(0===n.length)throw Error("empty string");if("NaN"===n||"Infinity"===n||"+Infinity"===n||"-Infinity"===n)return e;if("number"==typeof t?(i=t,t=!1):t=!!t,i=i||10,2>i||36<i)throw RangeError("radix");if(0<(s=n.indexOf("-")))throw Error("interior hyphen");if(0===s)return v(n.substring(1),t,i).neg();for(s=f(l(i,8)),r=e,u=0;u<n.length;u+=8)o=Math.min(8,n.length-u),h=parseInt(n.substring(u,u+o),i),8>o?(o=f(l(i,o)),r=r.mul(o).add(f(h))):(r=r.mul(s),r=r.add(f(h)));return r.unsigned=t,r}function o(n){return n instanceof i?n:"number"==typeof n?f(n):"string"==typeof n?v(n):t(n.low,n.high,n.unsigned)}var y,p,l,s,c,w,a,b,k,r;n.Bridge.$Long=i;i.__isLong__;Object.defineProperty(i.prototype,"__isLong__",{value:!0,enumerable:!1,configurable:!1});i.isLong=u;y={};p={};i.fromInt=h;i.fromNumber=f;i.fromBits=t;l=Math.pow;i.fromString=v;i.fromValue=o;var d=4294967296*4294967296,g=d/2,nt=h(16777216),e=h(0);i.ZERO=e;s=h(0,!0);i.UZERO=s;c=h(1);i.ONE=c;w=h(1,!0);i.UONE=w;a=h(-1);i.NEG_ONE=a;b=t(-1,2147483647,!1);i.MAX_VALUE=b;k=t(-1,-1,!0);i.MAX_UNSIGNED_VALUE=k;r=t(0,-2147483648,!1);i.MIN_VALUE=r;n=i.prototype;n.toInt=function(){return this.unsigned?this.low>>>0:this.low};n.toNumber=function(){return this.unsigned?4294967296*(this.high>>>0)+(this.low>>>0):4294967296*this.high+(this.low>>>0)};n.toString=function(n){if(n=n||10,2>n||36<n)throw RangeError("radix");if(this.isZero())return"0";if(this.isNegative()){if(this.eq(r)){var t=f(n),u=this.div(t),t=u.mul(t).sub(this);return u.toString(n)+t.toInt().toString(n)}return("undefined"==typeof n||10===n?"-":"")+this.neg().toString(n)}for(var u=f(l(n,6),this.unsigned),t=this,e="";;){var o=t.div(u),i=(t.sub(o.mul(u)).toInt()>>>0).toString(n),t=o;if(t.isZero())return i+e;for(;6>i.length;)i="0"+i;e=""+i+e}};n.getHighBits=function(){return this.high};n.getHighBitsUnsigned=function(){return this.high>>>0};n.getLowBits=function(){return this.low};n.getLowBitsUnsigned=function(){return this.low>>>0};n.getNumBitsAbs=function(){if(this.isNegative())return this.eq(r)?64:this.neg().getNumBitsAbs();for(var t=0!=this.high?this.high:this.low,n=31;0<n&&0==(t&1<<n);n--);return 0!=this.high?n+33:n+1};n.isZero=function(){return 0===this.high&&0===this.low};n.isNegative=function(){return!this.unsigned&&0>this.high};n.isPositive=function(){return this.unsigned||0<=this.high};n.isOdd=function(){return 1==(this.low&1)};n.isEven=function(){return 0==(this.low&1)};n.equals=function(n){return u(n)||(n=o(n)),this.unsigned!==n.unsigned&&1==this.high>>>31&&1==n.high>>>31?!1:this.high===n.high&&this.low===n.low};n.eq=n.equals;n.notEquals=function(n){return!this.eq(n)};n.neq=n.notEquals;n.lessThan=function(n){return 0>this.comp(n)};n.lt=n.lessThan;n.lessThanOrEqual=function(n){return 0>=this.comp(n)};n.lte=n.lessThanOrEqual;n.greaterThan=function(n){return 0<this.comp(n)};n.gt=n.greaterThan;n.greaterThanOrEqual=function(n){return 0<=this.comp(n)};n.gte=n.greaterThanOrEqual;n.compare=function(n){if(u(n)||(n=o(n)),this.eq(n))return 0;var t=this.isNegative(),i=n.isNegative();return t&&!i?-1:!t&&i?1:this.unsigned?n.high>>>0>this.high>>>0||n.high===this.high&&n.low>>>0>this.low>>>0?-1:1:this.sub(n).isNegative()?-1:1};n.comp=n.compare;n.negate=function(){return!this.unsigned&&this.eq(r)?r:this.not().add(c)};n.neg=n.negate;n.add=function(n){u(n)||(n=o(n));var e=this.high>>>16,i=this.high&65535,r=this.low>>>16,s=n.high>>>16,h=n.high&65535,c=n.low>>>16,f;return f=0+((this.low&65535)+(n.low&65535)),n=0+(f>>>16),n+=r+c,r=0+(n>>>16),r+=i+h,i=0+(r>>>16),i=i+(e+s)&65535,t((n&65535)<<16|f&65535,i<<16|r&65535,this.unsigned)};n.subtract=function(n){return u(n)||(n=o(n)),this.add(n.neg())};n.sub=n.subtract;n.multiply=function(n){var h,i,s,v;if(this.isZero()||(u(n)||(n=o(n)),n.isZero()))return e;if(this.eq(r))return n.isOdd()?r:e;if(n.eq(r))return this.isOdd()?r:e;if(this.isNegative())return n.isNegative()?this.neg().mul(n.neg()):this.neg().mul(n).neg();if(n.isNegative())return this.mul(n.neg()).neg();if(this.lt(nt)&&n.lt(nt))return f(this.toNumber()*n.toNumber(),this.unsigned);var w=this.high>>>16,y=this.high&65535,l=this.low>>>16,c=this.low&65535,b=n.high>>>16,p=n.high&65535,a=n.low>>>16;return n=n.low&65535,v=0+c*n,s=0+(v>>>16),s+=l*n,i=0+(s>>>16),s=(s&65535)+c*a,i+=s>>>16,s&=65535,i+=y*n,h=0+(i>>>16),i=(i&65535)+l*a,h+=i>>>16,i&=65535,i+=c*p,h+=i>>>16,i&=65535,h=h+(w*n+y*a+l*p+c*b)&65535,t(s<<16|v&65535,h<<16|i,this.unsigned)};n.mul=n.multiply;n.divide=function(n){var t,i,v;if(u(n)||(n=o(n)),n.isZero())throw Error("division by zero");if(this.isZero())return this.unsigned?s:e;if(this.unsigned)n.unsigned||(n=n.toUnsigned());else{if(this.eq(r))return n.eq(c)||n.eq(a)?r:n.eq(r)?c:(t=this.shr(1).div(n).shl(1),t.eq(e))?n.isNegative()?c:a:(i=this.sub(n.mul(t)),t.add(i.div(n)));if(n.eq(r))return this.unsigned?s:e;if(this.isNegative())return n.isNegative()?this.neg().div(n.neg()):this.neg().div(n).neg();if(n.isNegative())return this.div(n.neg()).neg()}if(this.unsigned){if(n.gt(this))return s;if(n.gt(this.shru(1)))return w;v=s}else v=e;for(i=this;i.gte(n);){t=Math.max(1,Math.floor(i.toNumber()/n.toNumber()));for(var p=Math.ceil(Math.log(t)/Math.LN2),p=48>=p?1:l(2,p-48),h=f(t),y=h.mul(n);y.isNegative()||y.gt(i);)t-=p,h=f(t,this.unsigned),y=h.mul(n);h.isZero()&&(h=c);v=v.add(h);i=i.sub(y)}return v};n.div=n.divide;n.modulo=function(n){return u(n)||(n=o(n)),this.sub(this.div(n).mul(n))};n.mod=n.modulo;n.not=function(){return t(~this.low,~this.high,this.unsigned)};n.and=function(n){return u(n)||(n=o(n)),t(this.low&n.low,this.high&n.high,this.unsigned)};n.or=function(n){return u(n)||(n=o(n)),t(this.low|n.low,this.high|n.high,this.unsigned)};n.xor=function(n){return u(n)||(n=o(n)),t(this.low^n.low,this.high^n.high,this.unsigned)};n.shiftLeft=function(n){return u(n)&&(n=n.toInt()),0==(n&=63)?this:32>n?t(this.low<<n,this.high<<n|this.low>>>32-n,this.unsigned):t(0,this.low<<n-32,this.unsigned)};n.shl=n.shiftLeft;n.shiftRight=function(n){return u(n)&&(n=n.toInt()),0==(n&=63)?this:32>n?t(this.low>>>n|this.high<<32-n,this.high>>n,this.unsigned):t(this.high>>n-32,0<=this.high?0:-1,this.unsigned)};n.shr=n.shiftRight;n.shiftRightUnsigned=function(n){if(u(n)&&(n=n.toInt()),n&=63,0===n)return this;var i=this.high;return 32>n?t(this.low>>>n|i<<32-n,i>>>n,this.unsigned):32===n?t(i,0,this.unsigned):t(i>>>n-32,0,this.unsigned)};n.shru=n.shiftRightUnsigned;n.toSigned=function(){return this.unsigned?t(this.low,this.high,!1):this};n.toUnsigned=function(){return this.unsigned?this:t(this.low,this.high,!0)}}(Bridge.global);System.Int64=function(n){if(this.constructor!==System.Int64)return new System.Int64(n);Bridge.hasValue(n)||(n=0);this.T=System.Int64;this.unsigned=!1;this.value=System.Int64.getValue(n)};System.Int64.$$name="System.Int64";System.Int64.prototype.$$name="System.Int64";System.Int64.$$inherits=[];Bridge.Class.addExtend(System.Int64,[System.IComparable,System.IFormattable,System.IComparable$1(System.Int64),System.IEquatable$1(System.Int64)]);System.Int64.instanceOf=function(n){return n instanceof System.Int64};System.Int64.is64Bit=function(n){return n instanceof System.Int64||n instanceof System.UInt64};System.Int64.getDefaultValue=function(){return System.Int64.Zero};System.Int64.getValue=function(n){return Bridge.hasValue(n)?n instanceof Bridge.$Long?n:n instanceof System.Int64?n.value:n instanceof System.UInt64?n.value.toSigned():Bridge.isArray(n)?new Bridge.$Long(n[0],n[1]):Bridge.isString(n)?Bridge.$Long.fromString(n):Bridge.isNumber(n)?Bridge.$Long.fromNumber(n):n instanceof System.Decimal?Bridge.$Long.fromString(n.toString()):Bridge.$Long.fromValue(n):null};System.Int64.create=function(n){return Bridge.hasValue(n)?n instanceof System.Int64?n:new System.Int64(n):null};System.Int64.lift=function(n){return Bridge.hasValue(n)?System.Int64.create(n):null};System.Int64.toNumber=function(n){return n?n.toNumber():null};System.Int64.prototype.toNumberDivided=function(n){var t=this.div(n),i=this.mod(n),r=i.toNumber()/n;return t.toNumber()+r};System.Int64.prototype.toJSON=function(){return this.toNumber()};System.Int64.prototype.toString=function(n,t){return!n&&!t?this.value.toString():Bridge.isNumber(n)&&!t?this.value.toString(n):Bridge.Int.format(this,n,t)};System.Int64.prototype.format=function(n,t){return Bridge.Int.format(this,n,t)};System.Int64.prototype.isNegative=function(){return this.value.isNegative()};System.Int64.prototype.abs=function(){if(this.T===System.Int64&&this.eq(System.Int64.MinValue))throw new System.OverflowException;return new this.T(this.value.isNegative()?this.value.neg():this.value)};System.Int64.prototype.compareTo=function(n){return this.value.compare(this.T.getValue(n))};System.Int64.prototype.add=function(n,t){var i=this.T.getValue(n),r=new this.T(this.value.add(i));if(t){var u=this.value.isNegative(),f=i.isNegative(),e=r.value.isNegative();if(u&&f&&!e||!u&&!f&&e||this.T===System.UInt64&&r.lt(System.UInt64.max(this,i)))throw new System.OverflowException;}return r};System.Int64.prototype.sub=function(n,t){var i=this.T.getValue(n),r=new this.T(this.value.sub(i));if(t){var u=this.value.isNegative(),f=i.isNegative(),e=r.value.isNegative();if(u&&!f&&!e||!u&&f&&e||this.T===System.UInt64&&this.value.lt(i))throw new System.OverflowException;}return r};System.Int64.prototype.isZero=function(){return this.value.isZero()};System.Int64.prototype.mul=function(n,t){var i=this.T.getValue(n),r=new this.T(this.value.mul(i)),u;if(t){var f=this.sign(),e=i.isZero()?0:i.isNegative()?-1:1,o=r.sign();if(this.T===System.Int64){if(this.eq(System.Int64.MinValue)||this.eq(System.Int64.MaxValue)){if(i.neq(1)&&i.neq(0))throw new System.OverflowException;return r}if(i.eq(Bridge.$Long.MIN_VALUE)||i.eq(Bridge.$Long.MAX_VALUE)){if(this.neq(1)&&this.neq(0))throw new System.OverflowException;return r}if(f===-1&&e===-1&&o!==1||f===1&&e===1&&o!==1||f===-1&&e===1&&o!==-1||f===1&&e===-1&&o!==-1)throw new System.OverflowException;if(u=r.abs(),u.lt(this.abs())||u.lt(System.Int64(i).abs()))throw new System.OverflowException;}else{if(this.eq(System.UInt64.MaxValue)){if(i.neq(1)&&i.neq(0))throw new System.OverflowException;return r}if(i.eq(Bridge.$Long.MAX_UNSIGNED_VALUE)){if(this.neq(1)&&this.neq(0))throw new System.OverflowException;return r}if(u=r.abs(),u.lt(this.abs())||u.lt(System.Int64(i).abs()))throw new System.OverflowException;}}return r};System.Int64.prototype.div=function(n){return new this.T(this.value.div(this.T.getValue(n)))};System.Int64.prototype.mod=function(n){return new this.T(this.value.mod(this.T.getValue(n)))};System.Int64.prototype.neg=function(n){if(n&&this.T===System.Int64&&this.eq(System.Int64.MinValue))throw new System.OverflowException;return new this.T(this.value.neg())};System.Int64.prototype.inc=function(n){return this.add(1,n)};System.Int64.prototype.dec=function(n){return this.sub(1,n)};System.Int64.prototype.sign=function(){return this.value.isZero()?0:this.value.isNegative()?-1:1};System.Int64.prototype.clone=function(){return new this.T(this)};System.Int64.prototype.ne=function(n){return this.value.neq(this.T.getValue(n))};System.Int64.prototype.neq=function(n){return this.value.neq(this.T.getValue(n))};System.Int64.prototype.eq=function(n){return this.value.eq(this.T.getValue(n))};System.Int64.prototype.lt=function(n){return this.value.lt(this.T.getValue(n))};System.Int64.prototype.lte=function(n){return this.value.lte(this.T.getValue(n))};System.Int64.prototype.gt=function(n){return this.value.gt(this.T.getValue(n))};System.Int64.prototype.gte=function(n){return this.value.gte(this.T.getValue(n))};System.Int64.prototype.equals=function(n){return this.value.eq(this.T.getValue(n))};System.Int64.prototype.equalsT=function(n){return this.equals(n)};System.Int64.prototype.getHashCode=function(){var n=this.sign()*397+this.value.high|0;return n*397+this.value.low|0};System.Int64.prototype.toNumber=function(){return this.value.toNumber()};System.Int64.parse=function(n){if(n==null)throw new System.ArgumentNullException("str");if(!/^[+-]?[0-9]+$/.test(n))throw new System.FormatException("Input string was not in a correct format.");var t=new System.Int64(n);if(n!==t.toString())throw new System.OverflowException;return t};System.Int64.tryParse=function(n,t){try{return n==null||!/^[+-]?[0-9]+$/.test(n)?(t.v=System.Int64(Bridge.$Long.ZERO),!1):(t.v=new System.Int64(n),n!==t.v.toString())?(t.v=System.Int64(Bridge.$Long.ZERO),!1):!0}catch(i){return t.v=System.Int64(Bridge.$Long.ZERO),!1}};System.Int64.divRem=function(n,t,i){n=System.Int64(n);t=System.Int64(t);var r=n.mod(t);return i.v=r,n.sub(r).div(t)};System.Int64.min=function(){for(var t=[],i,n=0,r=arguments.length;n<r;n++)t.push(System.Int64.getValue(arguments[n]));for(n=0,i=t[0];++n<t.length;)t[n].lt(i)&&(i=t[n]);return new System.Int64(i)};System.Int64.max=function(){for(var t=[],i,n=0,r=arguments.length;n<r;n++)t.push(System.Int64.getValue(arguments[n]));for(n=0,i=t[0];++n<t.length;)t[n].gt(i)&&(i=t[n]);return new System.Int64(i)};System.Int64.prototype.and=function(n){return new this.T(this.value.and(this.T.getValue(n)))};System.Int64.prototype.not=function(){return new this.T(this.value.not())};System.Int64.prototype.or=function(n){return new this.T(this.value.or(this.T.getValue(n)))};System.Int64.prototype.shl=function(n){return new this.T(this.value.shl(n))};System.Int64.prototype.shr=function(n){return new this.T(this.value.shr(n))};System.Int64.prototype.shru=function(n){return new this.T(this.value.shru(n))};System.Int64.prototype.xor=function(n){return new this.T(this.value.xor(this.T.getValue(n)))};System.Int64.check=function(n,t){if(Bridge.Int.isInfinite(n))return t===System.Int64||t===System.UInt64?t.MinValue:t.min;if(!n)return null;var i,r;if(t===System.Int64){if(n instanceof System.Int64)return n;if(i=n.value.toString(),r=new System.Int64(i),i!==r.value.toString())throw new System.OverflowException;return r}if(t===System.UInt64){if(n instanceof System.UInt64)return n;if(n.value.isNegative())throw new System.OverflowException;if(i=n.value.toString(),r=new System.UInt64(i),i!==r.value.toString())throw new System.OverflowException;return r}return Bridge.Int.check(n.toNumber(),t)};System.Int64.clip8=function(n){return n?Bridge.Int.sxb(n.value.low&255):Bridge.Int.isInfinite(n)?System.SByte.min:null};System.Int64.clipu8=function(n){return n?n.value.low&255:Bridge.Int.isInfinite(n)?System.Byte.min:null};System.Int64.clip16=function(n){return n?Bridge.Int.sxs(n.value.low&65535):Bridge.Int.isInfinite(n)?System.Int16.min:null};System.Int64.clipu16=function(n){return n?n.value.low&65535:Bridge.Int.isInfinite(n)?System.UInt16.min:null};System.Int64.clip32=function(n){return n?n.value.low|0:Bridge.Int.isInfinite(n)?System.Int32.min:null};System.Int64.clipu32=function(n){return n?n.value.low>>>0:Bridge.Int.isInfinite(n)?System.UInt32.min:null};System.Int64.clip64=function(n){return n?new System.Int64(n.value.toSigned()):Bridge.Int.isInfinite(n)?System.Int64.MinValue:null};System.Int64.clipu64=function(n){return n?new System.UInt64(n.value.toUnsigned()):Bridge.Int.isInfinite(n)?System.UInt64.MinValue:null};System.Int64.Zero=System.Int64(Bridge.$Long.ZERO);System.Int64.MinValue=System.Int64(Bridge.$Long.MIN_VALUE);System.Int64.MaxValue=System.Int64(Bridge.$Long.MAX_VALUE);System.UInt64=function(n){if(this.constructor!==System.UInt64)return new System.UInt64(n);Bridge.hasValue(n)||(n=0);this.T=System.UInt64;this.unsigned=!0;this.value=System.UInt64.getValue(n,!0)};System.UInt64.$$name="System.UInt64";System.UInt64.prototype.$$name="System.UInt64";System.UInt64.$$inherits=[];Bridge.Class.addExtend(System.UInt64,[System.IComparable,System.IFormattable,System.IComparable$1(System.UInt64),System.IEquatable$1(System.UInt64)]);System.UInt64.instanceOf=function(n){return n instanceof System.UInt64};System.UInt64.getDefaultValue=function(){return System.UInt64.Zero};System.UInt64.getValue=function(n){return Bridge.hasValue(n)?n instanceof Bridge.$Long?n:n instanceof System.UInt64?n.value:n instanceof System.Int64?n.value.toUnsigned():Bridge.isArray(n)?new Bridge.$Long(n[0],n[1],!0):Bridge.isString(n)?Bridge.$Long.fromString(n,!0):Bridge.isNumber(n)?Bridge.$Long.fromNumber(n,!0):n instanceof System.Decimal?Bridge.$Long.fromString(n.toString(),!0):Bridge.$Long.fromValue(n):null};System.UInt64.create=function(n){return Bridge.hasValue(n)?n instanceof System.UInt64?n:new System.UInt64(n):null};System.UInt64.lift=function(n){return Bridge.hasValue(n)?System.UInt64.create(n):null};System.UInt64.prototype.toJSON=System.Int64.prototype.toJSON;System.UInt64.prototype.toString=System.Int64.prototype.toString;System.UInt64.prototype.format=System.Int64.prototype.format;System.UInt64.prototype.isNegative=System.Int64.prototype.isNegative;System.UInt64.prototype.abs=System.Int64.prototype.abs;System.UInt64.prototype.compareTo=System.Int64.prototype.compareTo;System.UInt64.prototype.add=System.Int64.prototype.add;System.UInt64.prototype.sub=System.Int64.prototype.sub;System.UInt64.prototype.isZero=System.Int64.prototype.isZero;System.UInt64.prototype.mul=System.Int64.prototype.mul;System.UInt64.prototype.div=System.Int64.prototype.div;System.UInt64.prototype.toNumberDivided=System.Int64.prototype.toNumberDivided;System.UInt64.prototype.mod=System.Int64.prototype.mod;System.UInt64.prototype.neg=System.Int64.prototype.neg;System.UInt64.prototype.inc=System.Int64.prototype.inc;System.UInt64.prototype.dec=System.Int64.prototype.dec;System.UInt64.prototype.sign=System.Int64.prototype.sign;System.UInt64.prototype.clone=System.Int64.prototype.clone;System.UInt64.prototype.ne=System.Int64.prototype.ne;System.UInt64.prototype.neq=System.Int64.prototype.neq;System.UInt64.prototype.eq=System.Int64.prototype.eq;System.UInt64.prototype.lt=System.Int64.prototype.lt;System.UInt64.prototype.lte=System.Int64.prototype.lte;System.UInt64.prototype.gt=System.Int64.prototype.gt;System.UInt64.prototype.gte=System.Int64.prototype.gte;System.UInt64.prototype.equals=System.Int64.prototype.equals;System.UInt64.prototype.equalsT=System.Int64.prototype.equalsT;System.UInt64.prototype.getHashCode=System.Int64.prototype.getHashCode;System.UInt64.prototype.toNumber=System.Int64.prototype.toNumber;System.UInt64.parse=function(n){if(n==null)throw new System.ArgumentNullException("str");if(!/^[+-]?[0-9]+$/.test(n))throw new System.FormatException("Input string was not in a correct format.");var t=new System.UInt64(n);if(t.value.isNegative())throw new System.OverflowException;if(n!==t.toString())throw new System.OverflowException;return t};System.UInt64.tryParse=function(n,t){try{return n==null||!/^[+-]?[0-9]+$/.test(n)?(t.v=System.UInt64(Bridge.$Long.UZERO),!1):(t.v=new System.UInt64(n),t.v.isNegative())?(t.v=System.UInt64(Bridge.$Long.UZERO),!1):n!==t.v.toString()?(t.v=System.UInt64(Bridge.$Long.UZERO),!1):!0}catch(i){return t.v=System.UInt64(Bridge.$Long.UZERO),!1}};System.UInt64.min=function(){for(var t=[],i,n=0,r=arguments.length;n<r;n++)t.push(System.UInt64.getValue(arguments[n]));for(n=0,i=t[0];++n<t.length;)t[n].lt(i)&&(i=t[n]);return new System.UInt64(i)};System.UInt64.max=function(){for(var t=[],i,n=0,r=arguments.length;n<r;n++)t.push(System.UInt64.getValue(arguments[n]));for(n=0,i=t[0];++n<t.length;)t[n].gt(i)&&(i=t[n]);return new System.UInt64(i)};System.UInt64.divRem=function(n,t,i){n=System.UInt64(n);t=System.UInt64(t);var r=n.mod(t);return i.v=r,n.sub(r).div(t)};System.UInt64.prototype.and=System.Int64.prototype.and;System.UInt64.prototype.not=System.Int64.prototype.not;System.UInt64.prototype.or=System.Int64.prototype.or;System.UInt64.prototype.shl=System.Int64.prototype.shl;System.UInt64.prototype.shr=System.Int64.prototype.shr;System.UInt64.prototype.shru=System.Int64.prototype.shru;System.UInt64.prototype.xor=System.Int64.prototype.xor;System.UInt64.Zero=System.UInt64(Bridge.$Long.UZERO);System.UInt64.MinValue=System.UInt64.Zero;System.UInt64.MaxValue=System.UInt64(Bridge.$Long.MAX_UNSIGNED_VALUE);!function(n){function e(n){var u,i,f,o=n.length-1,e="",t=n[0];if(o>0){for(e+=t,u=1;o>u;u++)f=n[u]+"",i=r-f.length,i&&(e+=k(i)),e+=f;t=n[u];f=t+"";i=r-f.length;i&&(e+=k(i))}else if(0===t)return"0";for(;t%10==0;)t/=10;return e+t}function c(n,t,i){if(n!==~~n||t>n||n>i)throw Error(nt+n);}function rt(n,t,i,u){for(var o,s,f,e=n[0];e>=10;e/=10)--t;return--t<0?(t+=r,o=0):(o=Math.ceil((t+1)/r),t%=r),e=h(10,r-t),f=n[o]%e|0,null==u?3>t?(0==t?f=f/100|0:1==t&&(f=f/10|0),s=4>i&&99999==f||i>3&&49999==f||5e4==f||0==f):s=(4>i&&f+1==e||i>3&&f+1==e/2)&&(n[o+1]/e/100|0)==h(10,t-2)-1||(f==e/2||0==f)&&0==(n[o+1]/e/100|0):4>t?(0==t?f=f/1e3|0:1==t?f=f/100|0:2==t&&(f=f/10|0),s=(u||4>i)&&9999==f||!u&&i>3&&4999==f):s=((u||4>i)&&f+1==e||!u&&i>3&&f+1==e/2)&&(n[o+1]/e/1e3|0)==h(10,t-3)-1,s}function w(n,t,i){for(var u,f,r=[0],e=0,s=n.length;s>e;){for(f=r.length;f--;)r[f]*=t;for(r[0]+=o.indexOf(n.charAt(e++)),u=0;u<r.length;u++)r[u]>i-1&&(void 0===r[u+1]&&(r[u+1]=0),r[u+1]+=r[u]/i|0,r[u]%=i)}return r.reverse()}function ri(n,t){var i,u,f=t.d.length,e,r;for(32>f?(i=Math.ceil(f/3),u=Math.pow(4,-i).toString()):(i=16,u="2.3283064365386962890625e-10"),n.precision+=i,t=tt(n,1,t.times(u),new n(1)),e=i;e--;)r=t.times(t),t=r.times(r).minus(r).times(8).plus(1);return n.precision-=i,t}function i(n,t,i,f){var a,c,o,s,p,w,v,e,l,b=n.constructor;n:if(null!=t){if(e=n.d,!e)return n;for(a=1,s=e[0];s>=10;s/=10)a++;if(c=t-a,0>c)c+=r,o=t,v=e[l=0],p=v/h(10,a-o-1)%10|0;else if(l=Math.ceil((c+1)/r),s=e.length,l>=s){if(!f)break n;for(;s++<=l;)e.push(0);v=p=0;a=1;c%=r;o=c-r+1}else{for(v=s=e[l],a=1;s>=10;s/=10)a++;c%=r;o=c-r+a;p=0>o?0:v/h(10,a-o-1)%10|0}if(f=f||0>t||void 0!==e[l+1]||(0>o?v:v%h(10,a-o-1)),w=4>i?(p||f)&&(0==i||i==(n.s<0?3:2)):p>5||5==p&&(4==i||f||6==i&&(c>0?o>0?v/h(10,a-o):0:e[l-1])%10&1||i==(n.s<0?8:7)),1>t||!e[0])return e.length=0,w?(t-=n.e+1,e[0]=h(10,(r-t%r)%r),n.e=-t||0):e[0]=n.e=0,n;if(0==c?(e.length=l,s=1,l--):(e.length=l+1,s=h(10,r-c),e[l]=o>0?(v/h(10,a-o)%h(10,o)|0)*s:0),w)for(;;){if(0==l){for(c=1,o=e[0];o>=10;o/=10)c++;for(o=e[0]+=s,s=1;o>=10;o/=10)s++;c!=s&&(n.e++,e[0]==y&&(e[0]=1));break}if(e[l]+=s,e[l]!=y)break;e[l--]=0;s=1}for(c=e.length;0===e[--c];)e.pop()}return u&&(n.e>b.maxE?(n.d=null,n.e=NaN):n.e<b.minE&&(n.e=0,n.d=[0])),n}function p(n,t,i){if(!n.isFinite())return wt(n);var u,o=n.e,r=e(n.d),f=r.length;return t?(i&&(u=i-f)>0?r=r.charAt(0)+"."+r.slice(1)+k(u):f>1&&(r=r.charAt(0)+"."+r.slice(1)),r=r+(n.e<0?"e":"e+")+n.e):0>o?(r="0."+k(-o-1)+r,i&&(u=i-f)>0&&(r+=k(u))):o>=f?(r+=k(o+1-f),i&&(u=i-o-1)>0&&(r=r+"."+k(u))):((u=o+1)<f&&(r=r.slice(0,u)+"."+r.slice(u)),i&&(u=i-f)>0&&(o+1===f&&(r+="."),r+=k(u))),r}function ut(n,t){for(var i=1,u=n[0];u>=10;u/=10)i++;return i+t*r-1}function ft(n,t,r){if(t>ou)throw u=!0,r&&(n.precision=r),Error(ii);return i(new n(et),t,1,!0)}function a(n,t,r){if(t>lt)throw Error(ii);return i(new n(ot),t,r,!0)}function at(n){var t=n.length-1,i=t*r+1;if(t=n[t]){for(;t%10==0;t/=10)i--;for(t=n[0];t>=10;t/=10)i++}return i}function k(n){for(var t="";n--;)t+="0";return t}function vt(n,t,i,f){var o,e=new n(1),h=Math.ceil(f/r+4);for(u=!1;;){if(i%2&&(e=e.times(t),dt(e.d,h)&&(o=!0)),i=s(i/2),0===i){i=e.d.length-1;o&&0===e.d[i]&&++e.d[i];break}t=t.times(t);dt(t.d,h)}return u=!0,e}function yt(n){return 1&n.d[n.d.length-1]}function pt(n,t,i){for(var r,u=new n(t[0]),f=0;++f<t.length;){if(r=new n(t[f]),!r.s){u=r;break}u[i](r)&&(u=r)}return u}function ht(n,t){var l,v,b,a,o,c,r,y=0,k=0,p=0,s=n.constructor,d=s.rounding,w=s.precision;if(!n.d||!n.d[0]||n.e>17)return new s(n.d?n.d[0]?n.s<0?0:1/0:1:n.s?n.s<0?0:n:NaN);for(null==t?(u=!1,r=w):r=t,c=new s(.03125);n.e>-2;)n=n.times(c),p+=5;for(v=Math.log(h(2,p))/Math.LN10*2+5|0,r+=v,l=a=o=new s(1),s.precision=r;;){if(a=i(a.times(n),r,1),l=l.times(++k),c=o.plus(f(a,l,r,1)),e(c.d).slice(0,r)===e(o.d).slice(0,r)){for(b=p;b--;)o=i(o.times(o),r,1);if(null!=t)return s.precision=w,o;if(!(3>y&&rt(o.d,r-v,d,y)))return i(o,s.precision=w,d,u=!0);s.precision=r+=10;l=a=c=new s(1);k=0;y++}o=c}}function d(n,t){var c,l,b,y,w,it,h,p,o,g,nt,ut=1,k=10,r=n,a=r.d,s=r.constructor,tt=s.rounding,v=s.precision;if(r.s<0||!a||!a[0]||!r.e&&1==a[0]&&1==a.length)return new s(a&&!a[0]?-1/0:1!=r.s?NaN:a?0:r);if(null==t?(u=!1,o=v):o=t,s.precision=o+=k,c=e(a),l=c.charAt(0),!(Math.abs(y=r.e)<15e14))return p=ft(s,o+2,v).times(y+""),r=d(new s(l+"."+c.slice(1)),o-k).plus(p),s.precision=v,null==t?i(r,v,tt,u=!0):r;for(;7>l&&1!=l||1==l&&c.charAt(1)>3;)r=r.times(n),c=e(r.d),l=c.charAt(0),ut++;for(y=r.e,l>1?(r=new s("0."+c),y++):r=new s(l+"."+c.slice(1)),g=r,h=w=r=f(r.minus(1),r.plus(1),o,1),nt=i(r.times(r),o,1),b=3;;){if(w=i(w.times(nt),o,1),p=h.plus(f(w,new s(b),o,1)),e(p.d).slice(0,o)===e(h.d).slice(0,o)){if(h=h.times(2),0!==y&&(h=h.plus(ft(s,o+2,v).times(y+""))),h=f(h,new s(ut),o,1),null!=t)return s.precision=v,h;if(!rt(h.d,o-k,tt,it))return i(h,s.precision=v,tt,u=!0);s.precision=o+=k;p=w=r=f(g.minus(1),g.plus(1),o,1);nt=i(r.times(r),o,1);b=it=1}h=p;b+=2}}function wt(n){return String(n.s*n.s/0)}function bt(n,t){var f,i,e;for((f=t.indexOf("."))>-1&&(t=t.replace(".","")),(i=t.search(/e/i))>0?(0>f&&(f=i),f+=+t.slice(i+1),t=t.substring(0,i)):0>f&&(f=t.length),i=0;48===t.charCodeAt(i);i++);for(e=t.length;48===t.charCodeAt(e-1);--e);if(t=t.slice(i,e)){if(e-=i,n.e=f=f-i-1,n.d=[],i=(f+1)%r,0>f&&(i+=r),e>i){for(i&&n.d.push(+t.slice(0,i)),e-=r;e>i;)n.d.push(+t.slice(i,i+=r));t=t.slice(i);i=r-t.length}else i-=e;for(;i--;)t+="0";n.d.push(+t);u&&(n.e>n.constructor.maxE?(n.d=null,n.e=NaN):n.e<n.constructor.minE&&(n.e=0,n.d=[0]))}else n.e=0,n.d=[0];return n}function ui(n,t){var e,s,a,i,h,c,o,r,l;if("Infinity"===t||"NaN"===t)return+t||(n.s=NaN),n.e=NaN,n.d=null,n;if(ru.test(t))e=16,t=t.toLowerCase();else if(iu.test(t))e=2;else{if(!uu.test(t))throw Error(nt+t);e=8}for(i=t.search(/p/i),i>0?(o=+t.slice(i+1),t=t.substring(2,i)):t=t.slice(2),i=t.indexOf("."),h=i>=0,s=n.constructor,h&&(t=t.replace(".",""),c=t.length,i=c-i,a=vt(s,new s(e),i,2*i)),r=w(t,e,y),l=r.length-1,i=l;0===r[i];--i)r.pop();return 0>i?new s(0*n.s):(n.e=ut(r,l),n.d=r,u=!1,h&&(n=f(n,a,4*c)),o&&(n=n.times(Math.abs(o)<54?Math.pow(2,o):v.pow(2,o))),u=!0,n)}function fi(n,t){var i,u=t.d.length;if(3>u)return tt(n,2,t,t);i=1.4*Math.sqrt(u);i=i>16?16:0|i;t=t.times(Math.pow(5,-i));t=tt(n,2,t,t);for(var r,f=new n(5),e=new n(16),o=new n(20);i--;)r=t.times(t),t=t.times(f.plus(r.times(e.times(r).minus(o))));return t}function tt(n,t,i,e,o){var h,s,c,l,y=1,a=n.precision,v=Math.ceil(a/r);for(u=!1,l=i.times(i),c=new n(e);;){if(s=f(c.times(l),new n(t++*t++),a,1),c=o?e.plus(s):e.minus(s),e=f(s.times(l),new n(t++*t++),a,1),s=c.plus(e),void 0!==s.d[v]){for(h=v;s.d[h]===c.d[h]&&h--;);if(-1==h)break}h=c;c=e;e=s;s=h;y++}return u=!0,s.d.length=v+1,s}function kt(n,t){var r,i=t.s<0,u=a(n,n.precision,1),f=u.times(.5);if(t=t.abs(),t.lte(f))return b=i?4:1,t;if(r=t.divToInt(u),r.isZero())b=i?3:2;else{if(t=t.minus(r.times(u)),t.lte(f))return b=yt(r)?i?2:3:i?4:1,t;b=yt(r)?i?1:4:i?3:2}return t.minus(u).abs()}function ct(n,t,i,r){var a,l,s,d,h,v,u,e,y,b=n.constructor,k=void 0!==i;if(k?(c(i,1,g),void 0===r?r=b.rounding:c(r,0,8)):(i=b.precision,r=b.rounding),n.isFinite()){for(u=p(n),s=u.indexOf("."),k?(a=2,16==t?i=4*i-3:8==t&&(i=3*i-2)):a=t,s>=0&&(u=u.replace(".",""),y=new b(1),y.e=u.length-s,y.d=w(p(y),10,a),y.e=y.d.length),e=w(u,10,a),l=h=e.length;0==e[--h];)e.pop();if(e[0]){if(0>s?l--:(n=new b(n),n.d=e,n.e=l,n=f(n,y,i,r,0,a),e=n.d,l=n.e,v=ni),s=e[i],d=a/2,v=v||void 0!==e[i+1],v=4>r?(void 0!==s||v)&&(0===r||r===(n.s<0?3:2)):s>d||s===d&&(4===r||v||6===r&&1&e[i-1]||r===(n.s<0?8:7)),e.length=i,v)for(;++e[--i]>a-1;)e[i]=0,i||(++l,e.unshift(1));for(h=e.length;!e[h-1];--h);for(s=0,u="";h>s;s++)u+=o.charAt(e[s]);if(k){if(h>1)if(16==t||8==t){for(s=16==t?4:3,--h;h%s;h++)u+="0";for(e=w(u,a,t),h=e.length;!e[h-1];--h);for(s=1,u="1.";h>s;s++)u+=o.charAt(e[s])}else u=u.charAt(0)+"."+u.slice(1);u=u+(0>l?"p":"p+")+l}else if(0>l){for(;++l;)u="0"+u;u="0."+u}else if(++l>h)for(l-=h;l--;)u+="0";else h>l&&(u=u.slice(0,l)+"."+u.slice(l))}else u=k?"0p+0":"0";u=(16==t?"0x":2==t?"0b":8==t?"0o":"")+u}else u=wt(n);return n.s<0?"-"+u:u}function dt(n,t){if(n.length>t)return(n.length=t,!0)}function ei(n){return new this(n).abs()}function oi(n){return new this(n).acos()}function si(n){return new this(n).acosh()}function hi(n,t){return new this(n).plus(t)}function ci(n){return new this(n).asin()}function li(n){return new this(n).asinh()}function ai(n){return new this(n).atan()}function vi(n){return new this(n).atanh()}function yi(n,t){n=new this(n);t=new this(t);var i,u=this.precision,e=this.rounding,r=u+4;return n.s&&t.s?n.d||t.d?!t.d||n.isZero()?(i=t.s<0?a(this,u,e):new this(0),i.s=n.s):!n.d||t.isZero()?(i=a(this,r,1).times(.5),i.s=n.s):t.s<0?(this.precision=r,this.rounding=1,i=this.atan(f(n,t,r,1)),t=a(this,r,1),this.precision=u,this.rounding=e,i=n.s<0?i.minus(t):i.plus(t)):i=this.atan(f(n,t,r,1)):(i=a(this,r,1).times(t.s>0?.25:.75),i.s=n.s):i=new this(NaN),i}function pi(n){return new this(n).cbrt()}function wi(n){return i(n=new this(n),n.e+1,2)}function bi(n){if(!n||"object"!=typeof n)throw Error(st+"Object expected");for(var i,t,u=["precision",1,g,"rounding",0,8,"toExpNeg",-it,0,"toExpPos",0,it,"maxE",0,it,"minE",-it,0,"modulo",0,9],r=0;r<u.length;r+=3)if(void 0!==(t=n[i=u[r]])){if(!(s(t)===t&&t>=u[r+1]&&t<=u[r+2]))throw Error(nt+i+": "+t);this[i]=t}if(n.hasOwnProperty(i="crypto"))if(void 0===(t=n[i]))this[i]=t;else{if(t!==!0&&t!==!1&&0!==t&&1!==t)throw Error(nt+i+": "+t);this[i]=!(!t||!l||!l.getRandomValues&&!l.randomBytes)}return this}function ki(n){return new this(n).cos()}function di(n){return new this(n).cosh()}function gt(n){function i(n){var r,u,f,t=this;if(!(t instanceof i))return new i(n);if(t.constructor=i,n instanceof i)return t.s=n.s,t.e=n.e,void(t.d=(n=n.d)?n.slice():n);if(f=typeof n,"number"===f){if(0===n)return t.s=0>1/n?-1:1,t.e=0,void(t.d=[0]);if(0>n?(n=-n,t.s=-1):t.s=1,n===~~n&&1e7>n){for(r=0,u=n;u>=10;u/=10)r++;return t.e=r,void(t.d=[n])}return 0*n!=0?(n||(t.s=NaN),t.e=NaN,void(t.d=null)):bt(t,n.toString())}if("string"!==f)throw Error(nt+n);return 45===n.charCodeAt(0)?(n=n.slice(1),t.s=-1):t.s=1,fu.test(n)?bt(t,n):ui(t,n)}var r,u,f;if(i.prototype=t,i.ROUND_UP=0,i.ROUND_DOWN=1,i.ROUND_CEIL=2,i.ROUND_FLOOR=3,i.ROUND_HALF_UP=4,i.ROUND_HALF_DOWN=5,i.ROUND_HALF_EVEN=6,i.ROUND_HALF_CEIL=7,i.ROUND_HALF_FLOOR=8,i.EUCLID=9,i.config=bi,i.clone=gt,i.abs=ei,i.acos=oi,i.acosh=si,i.add=hi,i.asin=ci,i.asinh=li,i.atan=ai,i.atanh=vi,i.atan2=yi,i.cbrt=pi,i.ceil=wi,i.cos=ki,i.cosh=di,i.div=gi,i.exp=nr,i.floor=tr,i.fromJSON=ir,i.hypot=rr,i.ln=ur,i.log=fr,i.log10=or,i.log2=er,i.max=sr,i.min=hr,i.mod=cr,i.mul=lr,i.pow=ar,i.random=vr,i.round=yr,i.sign=pr,i.sin=wr,i.sinh=br,i.sqrt=kr,i.sub=dr,i.tan=gr,i.tanh=nu,i.trunc=tu,void 0===n&&(n={}),n)for(f=["precision","rounding","toExpNeg","toExpPos","maxE","minE","modulo","crypto"],r=0;r<f.length;)n.hasOwnProperty(u=f[r++])||(n[u]=this[u]);return i.config(n),i}function gi(n,t){return new this(n).div(t)}function nr(n){return new this(n).exp()}function tr(n){return i(n=new this(n),n.e+1,3)}function ir(n){var i,u,r,t;if("string"!=typeof n||!n)throw Error(nt+n);if(r=n.length,t=o.indexOf(n.charAt(0)),1===r)return new this(t>81?[-1/0,1/0,NaN][t-82]:t>40?-(t-41):t);if(64&t)u=16&t,i=u?(7&t)-3:(15&t)-7,r=1;else{if(2===r)return t=88*t+o.indexOf(n.charAt(1)),new this(t>=2816?-(t-2816)-41:t+41);if(u=32&t,!(31&t))return n=w(n.slice(1),88,10).join(""),new this(u?"-"+n:n);i=15&t;r=i+1;i=1===i?o.indexOf(n.charAt(1)):2===i?88*o.indexOf(n.charAt(1))+o.indexOf(n.charAt(2)):+w(n.slice(1,r),88,10).join("");16&t&&(i=-i)}return n=w(n.slice(r),88,10).join(""),i=i-n.length+1,n=n+"e"+i,new this(u?"-"+n:n)}function rr(){var i,n,t=new this(0);for(u=!1,i=0;i<arguments.length;)if(n=new this(arguments[i++]),n.d)t.d&&(t=t.plus(n.times(n)));else{if(n.s)return u=!0,new this(1/0);t=n}return u=!0,t.sqrt()}function ur(n){return new this(n).ln()}function fr(n,t){return new this(n).log(t)}function er(n){return new this(n).log(2)}function or(n){return new this(n).log(10)}function sr(){return pt(this,arguments,"lt")}function hr(){return pt(this,arguments,"gt")}function cr(n,t){return new this(n).mod(t)}function lr(n,t){return new this(n).mul(t)}function ar(n,t){return new this(n).pow(t)}function vr(n){var e,o,i,f,t=0,s=new this(1),u=[];if(void 0===n?n=this.precision:c(n,1,g),i=Math.ceil(n/r),this.crypto===!1)for(;i>t;)u[t++]=1e7*Math.random()|0;else if(l&&l.getRandomValues)for(e=l.getRandomValues(new Uint32Array(i));i>t;)f=e[t],f>=429e7?e[t]=l.getRandomValues(new Uint32Array(1))[0]:u[t++]=f%1e7;else if(l&&l.randomBytes){for(e=l.randomBytes(i*=4);i>t;)f=e[t]+(e[t+1]<<8)+(e[t+2]<<16)+((127&e[t+3])<<24),f>=214e7?l.randomBytes(4).copy(e,t):(u.push(f%1e7),t+=4);t=i/4}else{if(this.crypto)throw Error(st+"crypto unavailable");for(;i>t;)u[t++]=1e7*Math.random()|0}for(i=u[--t],n%=r,i&&n&&(f=h(10,r-n),u[t]=(i/f|0)*f);0===u[t];t--)u.pop();if(0>t)o=0,u=[0];else{for(o=-1;0===u[0];o-=r)u.shift();for(i=1,f=u[0];f>=10;f/=10)i++;r>i&&(o-=r-i)}return s.e=o,s.d=u,s}function yr(n){return i(n=new this(n),n.e+1,this.rounding)}function pr(n){return n=new this(n),n.d?n.d[0]?n.s:0*n.s:n.s||NaN}function wr(n){return new this(n).sin()}function br(n){return new this(n).sinh()}function kr(n){return new this(n).sqrt()}function dr(n,t){return new this(n).sub(t)}function gr(n){return new this(n).tan()}function nu(n){return new this(n).tanh()}function tu(n){return i(n=new this(n),n.e+1,1)}var ni,ti,b,it=9e15,g=1e9,o="0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!#$%()*+,-./:;=?@[]^_`{|}~",et="2.3025850929940456840179914546843642076011014886287729760333279009675726096773524802359972050895982983419677840422862486334095254650828067566662873690987816894829072083255546808437998948262331985283935053089653777326288461633662222876982198867465436674744042432743651550489343149393914796194044002221051017141748003688084012647080685567743216228355220114804663715659121373450747856947683463616792101806445070648000277502684916746550586856935673420670581136429224554405758925724208241314695689016758940256776311356919292033376587141660230105703089634572075440370847469940168269282808481184289314848524948644871927809676271275775397027668605952496716674183485704422507197965004714951050492214776567636938662976979522110718264549734772662425709429322582798502585509785265383207606726317164309505995087807523710333101197857547331541421808427543863591778117054309827482385045648019095610299291824318237525357709750539565187697510374970888692180205189339507238539205144634197265287286965110862571492198849978748873771345686209167058",ot="3.1415926535897932384626433832795028841971693993751058209749445923078164062862089986280348253421170679821480865132823066470938446095505822317253594081284811174502841027019385211055596446229489549303819644288109756659334461284756482337867831652712019091456485669234603486104543266482133936072602491412737245870066063155881748815209209628292540917153643678925903600113305305488204665213841469519415116094330572703657595919530921861173819326117931051185480744623799627495673518857527248912279381830119491298336733624406566430860213949463952247371907021798609437027705392171762931767523846748184676694051320005681271452635608277857713427577896091736371787214684409012249534301465495853710507922796892589235420199561121290219608640344181598136297747713099605187072113499999983729780499510597317328160963185950244594553469083026425223082533446850352619311881710100031378387528865875332083814206171776691473035982534904287554687311595628638823537875937519577818577805321712268066130019278766111959092164201989380952572010654858632789",v={precision:20,rounding:4,modulo:1,toExpNeg:-7,toExpPos:21,minE:-it,maxE:it,crypto:void 0},l="undefined"!=typeof crypto?crypto:null,u=!0,st="[DecimalError] ",nt=st+"Invalid argument: ",ii=st+"Precision limit exceeded",s=Math.floor,h=Math.pow,iu=/^0b([01]+(\.[01]*)?|\.[01]+)(p[+-]?\d+)?$/i,ru=/^0x([0-9a-f]+(\.[0-9a-f]*)?|\.[0-9a-f]+)(p[+-]?\d+)?$/i,uu=/^0o([0-7]+(\.[0-7]*)?|\.[0-7]+)(p[+-]?\d+)?$/i,fu=/^(\d+(\.\d*)?|\.\d+)(e[+-]?\d+)?$/i,y=1e7,r=7,eu=9007199254740991,ou=et.length-1,lt=ot.length-1,t={},f;if(t.absoluteValue=t.abs=function(){var n=new this.constructor(this);return n.s<0&&(n.s=1),i(n)},t.ceil=function(){return i(new this.constructor(this),this.e+1,2)},t.comparedTo=t.cmp=function(n){var r,h,f,e,o=this,i=o.d,u=(n=new o.constructor(n)).d,t=o.s,s=n.s;if(!i||!u)return t&&s?t!==s?t:i===u?0:!i^0>t?1:-1:NaN;if(!i[0]||!u[0])return i[0]?t:u[0]?-s:0;if(t!==s)return t;if(o.e!==n.e)return o.e>n.e^0>t?1:-1;for(f=i.length,e=u.length,r=0,h=e>f?f:e;h>r;++r)if(i[r]!==u[r])return i[r]>u[r]^0>t?1:-1;return f===e?0:f>e^0>t?1:-1},t.cosine=t.cos=function(){var u,f,t=this,n=t.constructor;return t.d?t.d[0]?(u=n.precision,f=n.rounding,n.precision=u+Math.max(t.e,t.sd())+r,n.rounding=1,t=ri(n,kt(n,t)),n.precision=u,n.rounding=f,i(2==b||3==b?t.neg():t,u,f,!0)):new n(1):new n(NaN)},t.cubeRoot=t.cbrt=function(){var r,w,n,o,v,c,l,h,y,p,t=this,a=t.constructor;if(!t.isFinite()||t.isZero())return new a(t);for(u=!1,c=t.s*Math.pow(t.s*t,1/3),c&&Math.abs(c)!=1/0?o=new a(c.toString()):(n=e(t.d),r=t.e,(c=(r-n.length+1)%3)&&(n+=1==c||-2==c?"0":"00"),c=Math.pow(n,1/3),r=s((r+1)/3)-(r%3==(0>r?-1:2)),c==1/0?n="5e"+r:(n=c.toExponential(),n=n.slice(0,n.indexOf("e")+1)+r),o=new a(n),o.s=t.s),l=(r=a.precision)+3;;)if(h=o,y=h.times(h).times(h),p=y.plus(t),o=f(p.plus(t).times(h),p.plus(y),l+2,1),e(h.d).slice(0,l)===(n=e(o.d)).slice(0,l)){if(n=n.slice(l-3,l+1),"9999"!=n&&(v||"4999"!=n)){+n&&(+n.slice(1)||"5"!=n.charAt(0))||(i(o,r+1,1),w=!o.times(o).times(o).eq(t));break}if(!v&&(i(h,r+1,0),h.times(h).times(h).eq(t))){o=h;break}l+=4;v=1}return u=!0,i(o,r,a.rounding,w)},t.decimalPlaces=t.dp=function(){var n,i=this.d,t=NaN;if(i){if(n=i.length-1,t=(n-s(this.e/r))*r,n=i[n])for(;n%10==0;n/=10)t--;0>t&&(t=0)}return t},t.dividedBy=t.div=function(n){return f(this,new this.constructor(n))},t.dividedToIntegerBy=t.divToInt=function(n){var r=this,t=r.constructor;return i(f(r,new t(n),0,1,1),t.precision,t.rounding)},t.equals=t.eq=function(n){return 0===this.cmp(n)},t.floor=function(){return i(new this.constructor(this),this.e+1,3)},t.greaterThan=t.gt=function(n){return this.cmp(n)>0},t.greaterThanOrEqualTo=t.gte=function(n){var t=this.cmp(n);return 1==t||0===t},t.hyperbolicCosine=t.cosh=function(){var r,u,f,h,e,n=this,t=n.constructor,c=new t(1),o,l,s;if(!n.isFinite())return new t(n.s?1/0:NaN);if(n.isZero())return c;for(f=t.precision,h=t.rounding,t.precision=f+Math.max(n.e,n.sd())+4,t.rounding=1,e=n.d.length,32>e?(r=Math.ceil(e/3),u=Math.pow(4,-r).toString()):(r=16,u="2.3283064365386962890625e-10"),n=tt(t,1,n.times(u),new t(1),!0),l=r,s=new t(8);l--;)o=n.times(n),n=c.minus(o.times(s.minus(o.times(s))));return i(n,t.precision=f,t.rounding=h,!0)},t.hyperbolicSine=t.sinh=function(){var r,u,f,e,n=this,t=n.constructor;if(!n.isFinite()||n.isZero())return new t(n);if(u=t.precision,f=t.rounding,t.precision=u+Math.max(n.e,n.sd())+4,t.rounding=1,e=n.d.length,3>e)n=tt(t,2,n,n,!0);else{r=1.4*Math.sqrt(e);r=r>16?16:0|r;n=n.times(Math.pow(5,-r));n=tt(t,2,n,n,!0);for(var o,s=new t(5),h=new t(16),c=new t(20);r--;)o=n.times(n),n=n.times(s.plus(o.times(h.times(o).plus(c))))}return t.precision=u,t.rounding=f,i(n,u,f,!0)},t.hyperbolicTangent=t.tanh=function(){var i,r,t=this,n=t.constructor;return t.isFinite()?t.isZero()?new n(t):(i=n.precision,r=n.rounding,n.precision=i+7,n.rounding=1,f(t.sinh(),t.cosh(),n.precision=i,n.rounding=r)):new n(t.s)},t.inverseCosine=t.acos=function(){var u,t=this,n=t.constructor,f=t.abs().cmp(1),i=n.precision,r=n.rounding;return-1!==f?0===f?t.isNeg()?a(n,i,r):new n(0):new n(NaN):t.isZero()?a(n,i+4,r).times(.5):(n.precision=i+6,n.rounding=1,t=t.asin(),u=a(n,i+4,r).times(.5),n.precision=i,n.rounding=r,u.minus(t))},t.inverseHyperbolicCosine=t.acosh=function(){var i,r,n=this,t=n.constructor;return n.lte(1)?new t(n.eq(1)?0:NaN):n.isFinite()?(i=t.precision,r=t.rounding,t.precision=i+Math.max(Math.abs(n.e),n.sd())+4,t.rounding=1,u=!1,n=n.times(n).minus(1).sqrt().plus(n),u=!0,t.precision=i,t.rounding=r,n.ln()):new t(n)},t.inverseHyperbolicSine=t.asinh=function(){var i,r,n=this,t=n.constructor;return!n.isFinite()||n.isZero()?new t(n):(i=t.precision,r=t.rounding,t.precision=i+2*Math.max(Math.abs(n.e),n.sd())+6,t.rounding=1,u=!1,n=n.times(n).plus(1).sqrt().plus(n),u=!0,t.precision=i,t.rounding=r,n.ln())},t.inverseHyperbolicTangent=t.atanh=function(){var r,u,o,e,n=this,t=n.constructor;return n.isFinite()?n.e>=0?new t(n.abs().eq(1)?n.s/0:n.isZero()?n:NaN):(r=t.precision,u=t.rounding,e=n.sd(),Math.max(e,r)<2*-n.e-1?i(new t(n),r,u,!0):(t.precision=o=e-n.e,n=f(n.plus(1),new t(1).minus(n),o+r,1),t.precision=r+4,t.rounding=1,n=n.ln(),t.precision=r,t.rounding=u,n.times(.5))):new t(NaN)},t.inverseSine=t.asin=function(){var r,u,i,f,n=this,t=n.constructor;return n.isZero()?new t(n):(u=n.abs().cmp(1),i=t.precision,f=t.rounding,-1!==u?0===u?(r=a(t,i+4,f).times(.5),r.s=n.s,r):new t(NaN):(t.precision=i+6,t.rounding=1,n=n.div(new t(1).minus(n.times(n)).sqrt().plus(1)).atan(),t.precision=i,t.rounding=f,n.times(2)))},t.inverseTangent=t.atan=function(){var e,c,h,l,o,v,t,y,p,n=this,f=n.constructor,s=f.precision,w=f.rounding;if(n.isFinite()){if(n.isZero())return new f(n);if(n.abs().eq(1)&&lt>=s+4)return t=a(f,s+4,w).times(.25),t.s=n.s,t}else{if(!n.s)return new f(NaN);if(lt>=s+4)return t=a(f,s+4,w).times(.5),t.s=n.s,t}for(f.precision=y=s+10,f.rounding=1,h=Math.min(28,y/r+2|0),e=h;e;--e)n=n.div(n.times(n).plus(1).sqrt().plus(1));for(u=!1,c=Math.ceil(y/r),l=1,p=n.times(n),t=new f(n),o=n;-1!==e;)if(o=o.times(p),v=t.minus(o.div(l+=2)),o=o.times(p),t=v.plus(o.div(l+=2)),void 0!==t.d[c])for(e=c;t.d[e]===v.d[e]&&e--;);return h&&(t=t.times(2<<h-1)),u=!0,i(t,f.precision=s,f.rounding=w,!0)},t.isFinite=function(){return!!this.d},t.isInteger=t.isInt=function(){return!!this.d&&s(this.e/r)>this.d.length-2},t.isNaN=function(){return!this.s},t.isNegative=t.isNeg=function(){return this.s<0},t.isPositive=t.isPos=function(){return this.s>0},t.isZero=function(){return!!this.d&&0===this.d[0]},t.lessThan=t.lt=function(n){return this.cmp(n)<0},t.lessThanOrEqualTo=t.lte=function(n){return this.cmp(n)<1},t.logarithm=t.log=function(n){var l,t,a,o,p,v,r,s,c=this,h=c.constructor,y=h.precision,w=h.rounding;if(null==n)n=new h(10),l=!0;else{if(n=new h(n),t=n.d,n.s<0||!t||!t[0]||n.eq(1))return new h(NaN);l=n.eq(10)}if(t=c.d,c.s<0||!t||!t[0]||c.eq(1))return new h(t&&!t[0]?-1/0:1!=c.s?NaN:t?0:1/0);if(l)if(t.length>1)p=!0;else{for(o=t[0];o%10==0;)o/=10;p=1!==o}if(u=!1,r=y+5,v=d(c,r),a=l?ft(h,r+10):d(n,r),s=f(v,a,r,1),rt(s.d,o=y,w))do if(r+=10,v=d(c,r),a=l?ft(h,r+10):d(n,r),s=f(v,a,r,1),!p){+e(s.d).slice(o+1,o+15)+1==1e14&&(s=i(s,y+1,0));break}while(rt(s.d,o+=10,w));return u=!0,i(s,y,w)},t.minus=t.sub=function(n){var l,p,f,w,c,o,k,b,t,d,v,e,h=this,a=h.constructor;if(n=new a(n),!h.d||!n.d)return h.s&&n.s?h.d?n.s=-n.s:n=new a(n.d||h.s!==n.s?h:NaN):n=new a(NaN),n;if(h.s!=n.s)return n.s=-n.s,h.plus(n);if(t=h.d,e=n.d,k=a.precision,b=a.rounding,!t[0]||!e[0]){if(e[0])n.s=-n.s;else{if(!t[0])return new a(3===b?-0:0);n=new a(h)}return u?i(n,k,b):n}if(p=s(n.e/r),d=s(h.e/r),t=t.slice(),c=d-p){for(v=0>c,v?(l=t,c=-c,o=e.length):(l=e,p=d,o=t.length),f=Math.max(Math.ceil(k/r),o)+2,c>f&&(c=f,l.length=1),l.reverse(),f=c;f--;)l.push(0);l.reverse()}else{for(f=t.length,o=e.length,v=o>f,v&&(o=f),f=0;o>f;f++)if(t[f]!=e[f]){v=t[f]<e[f];break}c=0}for(v&&(l=t,t=e,e=l,n.s=-n.s),o=t.length,f=e.length-o;f>0;--f)t[o++]=0;for(f=e.length;f>c;){if(t[--f]<e[f]){for(w=f;w&&0===t[--w];)t[w]=y-1;--t[w];t[f]+=y}t[f]-=e[f]}for(;0===t[--o];)t.pop();for(;0===t[0];t.shift())--p;return t[0]?(n.d=t,n.e=ut(t,p),u?i(n,k,b):n):new a(3===b?-0:0)},t.modulo=t.mod=function(n){var e,t=this,r=t.constructor;return n=new r(n),!t.d||!n.s||n.d&&!n.d[0]?new r(NaN):!n.d||t.d&&!t.d[0]?i(new r(t),r.precision,r.rounding):(u=!1,9==r.modulo?(e=f(t,n.abs(),0,3,1),e.s*=n.s):e=f(t,n,0,r.modulo,1),e=e.times(n),u=!0,t.minus(e))},t.naturalExponential=t.exp=function(){return ht(this)},t.naturalLogarithm=t.ln=function(){return d(this)},t.negated=t.neg=function(){var n=new this.constructor(this);return n.s=-n.s,i(n)},t.plus=t.add=function(n){var v,c,p,f,l,e,w,b,t,h,o=this,a=o.constructor;if(n=new a(n),!o.d||!n.d)return o.s&&n.s?o.d||(n=new a(n.d||o.s===n.s?o:NaN)):n=new a(NaN),n;if(o.s!=n.s)return n.s=-n.s,o.minus(n);if(t=o.d,h=n.d,w=a.precision,b=a.rounding,!t[0]||!h[0])return h[0]||(n=new a(o)),u?i(n,w,b):n;if(l=s(o.e/r),p=s(n.e/r),t=t.slice(),f=l-p){for(0>f?(c=t,f=-f,e=h.length):(c=h,p=l,e=t.length),l=Math.ceil(w/r),e=l>e?l+1:e+1,f>e&&(f=e,c.length=1),c.reverse();f--;)c.push(0);c.reverse()}for(e=t.length,f=h.length,0>e-f&&(f=e,c=h,h=t,t=c),v=0;f;)v=(t[--f]=t[f]+h[f]+v)/y|0,t[f]%=y;for(v&&(t.unshift(v),++p),e=t.length;0==t[--e];)t.pop();return n.d=t,n.e=ut(t,p),u?i(n,w,b):n},t.precision=t.sd=function(n){var t,i=this;if(void 0!==n&&n!==!!n&&1!==n&&0!==n)throw Error(nt+n);return i.d?(t=at(i.d),n&&i.e+1>t&&(t=i.e+1)):t=NaN,t},t.round=function(){var n=this,t=n.constructor;return i(new t(n),n.e+1,t.rounding)},t.sine=t.sin=function(){var u,f,n=this,t=n.constructor;return n.isFinite()?n.isZero()?new t(n):(u=t.precision,f=t.rounding,t.precision=u+Math.max(n.e,n.sd())+r,t.rounding=1,n=fi(t,kt(t,n)),t.precision=u,t.rounding=f,i(b>2?n.neg():n,u,f,!0)):new t(NaN)},t.squareRoot=t.sqrt=function(){var p,n,l,r,y,c,h=this,a=h.d,t=h.e,o=h.s,v=h.constructor;if(1!==o||!a||!a[0])return new v(!o||0>o&&(!a||a[0])?NaN:a?h:1/0);for(u=!1,o=Math.sqrt(+h),0==o||o==1/0?(n=e(a),(n.length+t)%2==0&&(n+="0"),o=Math.sqrt(n),t=s((t+1)/2)-(0>t||t%2),o==1/0?n="1e"+t:(n=o.toExponential(),n=n.slice(0,n.indexOf("e")+1)+t),r=new v(n)):r=new v(o.toString()),l=(t=v.precision)+3;;)if(c=r,r=c.plus(f(h,c,l+2,1)).times(.5),e(c.d).slice(0,l)===(n=e(r.d)).slice(0,l)){if(n=n.slice(l-3,l+1),"9999"!=n&&(y||"4999"!=n)){+n&&(+n.slice(1)||"5"!=n.charAt(0))||(i(r,t+1,1),p=!r.times(r).eq(h));break}if(!y&&(i(c,t+1,0),c.times(c).eq(h))){r=c;break}l+=4;y=1}return u=!0,i(r,t,v.rounding,p)},t.tangent=t.tan=function(){var r,u,n=this,t=n.constructor;return n.isFinite()?n.isZero()?new t(n):(r=t.precision,u=t.rounding,t.precision=r+10,t.rounding=1,n=n.sin(),n.s=1,n=f(n,new t(1).minus(n.times(n)).sqrt(),r+10,0),t.precision=r,t.rounding=u,i(2==b||4==b?n.neg():n,r,u,!0)):new t(NaN)},t.times=t.mul=function(n){var a,b,f,h,t,v,k,c,l,p=this,w=p.constructor,e=p.d,o=(n=new w(n)).d;if(n.s*=p.s,!(e&&e[0]&&o&&o[0]))return new w(!n.s||e&&!e[0]&&!o||o&&!o[0]&&!e?NaN:e&&o?0*n.s:n.s/0);for(b=s(p.e/r)+s(n.e/r),c=e.length,l=o.length,l>c&&(t=e,e=o,o=t,v=c,c=l,l=v),t=[],v=c+l,f=v;f--;)t.push(0);for(f=l;--f>=0;){for(a=0,h=c+f;h>f;)k=t[h]+o[f]*e[h-f-1]+a,t[h--]=k%y|0,a=k/y|0;t[h]=(t[h]+a)%y|0}for(;!t[--v];)t.pop();for(a?++b:t.shift(),f=t.length;!t[--f];)t.pop();return n.d=t,n.e=ut(t,b),u?i(n,w.precision,w.rounding):n},t.toBinary=function(n,t){return ct(this,2,n,t)},t.toDecimalPlaces=t.toDP=function(n,t){var r=this,u=r.constructor;return r=new u(r),void 0===n?r:(c(n,0,g),void 0===t?t=u.rounding:c(t,0,8),i(r,n+r.e+1,t))},t.toExponential=function(n,t){var u,r=this,f=r.constructor;return void 0===n?u=p(r,!0):(c(n,0,g),void 0===t?t=f.rounding:c(t,0,8),r=i(new f(r),n+1,t),u=p(r,!0,n+1)),r.isNeg()&&!r.isZero()?"-"+u:u},t.toFixed=function(n,t){var u,f,r=this,e=r.constructor;return void 0===n?u=p(r):(c(n,0,g),void 0===t?t=e.rounding:c(t,0,8),f=i(new e(r),n+r.e+1,t),u=p(f,!1,n+f.e+1)),r.isNeg()&&!r.isZero()?"-"+u:u},t.toFraction=function(n){var s,a,c,t,y,w,i,v,o,d,b,g,p=this,k=p.d,l=p.constructor;if(!k)return new l(p);if(o=a=new l(1),c=v=new l(0),s=new l(c),y=s.e=at(k)-p.e-1,w=y%r,s.d[0]=h(10,0>w?r+w:w),null==n)n=y>0?s:o;else{if(i=new l(n),!i.isInt()||i.lt(o))throw Error(nt+i);n=i.gt(s)?y>0?s:o:i}for(u=!1,i=new l(e(k)),d=l.precision,l.precision=y=k.length*r*2;b=f(i,s,0,1,1),t=a.plus(b.times(c)),1!=t.cmp(n);)a=c,c=t,t=o,o=v.plus(b.times(t)),v=t,t=s,s=i.minus(b.times(t)),i=t;return t=f(n.minus(a),c,0,1,1),v=v.plus(t.times(o)),a=a.plus(t.times(c)),v.s=o.s=p.s,g=f(o,c,y,1).minus(p).abs().cmp(f(v,a,y,1).minus(p).abs())<1?[o,c]:[v,a],l.precision=d,u=!0,g},t.toHexadecimal=t.toHex=function(n,t){return ct(this,16,n,t)},t.toJSON=function(){var h,n,r,i,c,t,u,l,f=this,s=f.s<0;if(!f.d)return o.charAt(f.s?s?82:83:84);if(n=f.e,1===f.d.length&&4>n&&n>=0&&(t=f.d[0],2857>t))return 41>t?o.charAt(s?t+41:t):(t-=41,s&&(t+=2816),i=t/88|0,o.charAt(i)+o.charAt(t-88*i));if(l=e(f.d),u="",!s&&8>=n&&n>=-7)i=64+n+7;else if(s&&4>=n&&n>=-3)i=80+n+3;else if(l.length===n+1)i=32*s;else if(i=32*s+16*(0>n),n=Math.abs(n),88>n)i+=1,u=o.charAt(n);else if(7744>n)i+=2,t=n/88|0,u=o.charAt(t)+o.charAt(n-88*t);else for(h=w(String(n),10,88),c=h.length,i+=c,r=0;c>r;r++)u+=o.charAt(h[r]);for(u=o.charAt(i)+u,h=w(l,10,88),c=h.length,r=0;c>r;r++)u+=o.charAt(h[r]);return u},t.toNearest=function(n,t){var r=this,e=r.constructor;if(r=new e(r),null==n){if(!r.d)return r;n=new e(1);t=e.rounding}else{if(n=new e(n),void 0!==t&&c(t,0,8),!r.d)return n.s?r:n;if(!n.d)return n.s&&(n.s=r.s),n}return n.d[0]?(u=!1,4>t&&(t=[4,5,7,8][t]),r=f(r,n,0,t,1).times(n),u=!0,i(r)):(n.s=r.s,r=n),r},t.toNumber=function(){return+this},t.toOctal=function(n,t){return ct(this,8,n,t)},t.toPower=t.pow=function(n){var l,a,o,c,v,y,w,t=this,f=t.constructor,p=+(n=new f(n));if(!(t.d&&n.d&&t.d[0]&&n.d[0]))return new f(h(+t,p));if(t=new f(t),t.eq(1))return t;if(o=f.precision,v=f.rounding,n.eq(1))return i(t,o,v);if(l=s(n.e/r),a=n.d.length-1,w=l>=a,y=t.s,w){if((a=0>p?-p:p)<=eu)return c=vt(f,t,a,o),n.s<0?new f(1).div(c):i(c,o,v)}else if(0>y)return new f(NaN);return y=0>y&&1&n.d[Math.max(l,a)]?-1:1,a=h(+t,p),l=0!=a&&isFinite(a)?new f(a+"").e:s(p*(Math.log("0."+e(t.d))/Math.LN10+t.e+1)),l>f.maxE+1||l<f.minE-1?new f(l>0?y/0:0):(u=!1,f.rounding=t.s=1,a=Math.min(12,(l+"").length),c=ht(n.times(d(t,o+a)),o),c=i(c,o+5,1),rt(c.d,o,v)&&(l=o+10,c=i(ht(n.times(d(t,l+a)),l),l+5,1),+e(c.d).slice(o+1,o+15)+1==1e14&&(c=i(c,o+1,0))),c.s=y,u=!0,f.rounding=v,i(c,o,v))},t.toPrecision=function(n,t){var f,r=this,u=r.constructor;return void 0===n?f=p(r,r.e<=u.toExpNeg||r.e>=u.toExpPos):(c(n,1,g),void 0===t?t=u.rounding:c(t,0,8),r=i(new u(r),n,t),f=p(r,n<=r.e||r.e<=u.toExpNeg,n)),r.isNeg()&&!r.isZero()?"-"+f:f},t.toSignificantDigits=t.toSD=function(n,t){var u=this,r=u.constructor;return void 0===n?(n=r.precision,t=r.rounding):(c(n,1,g),void 0===t?t=r.rounding:c(t,0,8)),i(new r(u),n,t)},t.toString=function(){var n=this,t=n.constructor,i=p(n,n.e<=t.toExpNeg||n.e>=t.toExpPos);return n.isNeg()&&!n.isZero()?"-"+i:i},t.truncated=t.trunc=function(){return i(new this.constructor(this),this.e+1,1)},t.valueOf=function(){var n=this,t=n.constructor,i=p(n,n.e<=t.toExpNeg||n.e>=t.toExpPos);return n.isNeg()?"-"+i:i},f=function(){function n(n,t,i){var u,r=0,f=n.length;for(n=n.slice();f--;)u=n[f]*t+r,n[f]=u%i|0,r=u/i|0;return r&&n.unshift(r),n}function t(n,t,i,r){var u,f;if(i!=r)f=i>r?1:-1;else for(u=f=0;i>u;u++)if(n[u]!=t[u]){f=n[u]>t[u]?1:-1;break}return f}function u(n,t,i,r){for(var u=0;i--;)n[i]-=u,u=n[i]<t[i]?1:0,n[i]=u*r+n[i]-t[i];for(;!n[0]&&n.length>1;)n.shift()}return function(f,e,o,h,c,l){var g,et,w,v,it,ot,nt,ft,rt,ut,p,b,ht,tt,vt,ct,st,yt,d,lt,at=f.constructor,pt=f.s==e.s?1:-1,k=f.d,a=e.d;if(!(k&&k[0]&&a&&a[0]))return new at(f.s&&e.s&&(k?!a||k[0]!=a[0]:a)?k&&0==k[0]||!a?0*pt:pt/0:NaN);for(l?(it=1,et=f.e-e.e):(l=y,it=r,et=s(f.e/it)-s(e.e/it)),d=a.length,st=k.length,rt=new at(pt),ut=rt.d=[],w=0;a[w]==(k[w]||0);w++);if(a[w]>(k[w]||0)&&et--,null==o?(tt=o=at.precision,h=at.rounding):tt=c?o+(f.e-e.e)+1:o,0>tt)ut.push(1),ot=!0;else{if(tt=tt/it+2|0,w=0,1==d){for(v=0,a=a[0],tt++;(st>w||v)&&tt--;w++)vt=v*l+(k[w]||0),ut[w]=vt/a|0,v=vt%a|0;ot=v||st>w}else{for(v=l/(a[0]+1)|0,v>1&&(a=n(a,v,l),k=n(k,v,l),d=a.length,st=k.length),ct=d,p=k.slice(0,d),b=p.length;d>b;)p[b++]=0;lt=a.slice();lt.unshift(0);yt=a[0];a[1]>=l/2&&++yt;do v=0,g=t(a,p,d,b),0>g?(ht=p[0],d!=b&&(ht=ht*l+(p[1]||0)),v=ht/yt|0,v>1?(v>=l&&(v=l-1),nt=n(a,v,l),ft=nt.length,b=p.length,g=t(nt,p,ft,b),1==g&&(v--,u(nt,ft>d?lt:a,ft,l))):(0==v&&(g=v=1),nt=a.slice()),ft=nt.length,b>ft&&nt.unshift(0),u(p,nt,b,l),-1==g&&(b=p.length,g=t(a,p,d,b),1>g&&(v++,u(p,b>d?lt:a,b,l))),b=p.length):0===g&&(v++,p=[0]),ut[w++]=v,g&&p[0]?p[b++]=k[ct]||0:(p=[k[ct]],b=1);while((ct++<st||void 0!==p[0])&&tt--);ot=void 0!==p[0]}ut[0]||ut.shift()}if(1==it)rt.e=et,ni=ot;else{for(w=1,v=ut[0];v>=10;v/=10)w++;rt.e=w+et*it-1;i(rt,c?o+rt.e+1:o,h,ot)}return rt}}(),v=gt(v),et=new v(et),ot=new v(ot),Bridge.$Decimal=v,"function"==typeof define&&define.amd)define(function(){return v});else if("undefined"!=typeof module&&module.exports){if(module.exports=v,!l)try{l=require("crypto")}catch(su){}}else n||(n="undefined"!=typeof self&&self&&self.self==self?self:Function("return this")()),ti=n.Decimal,v.noConflict=function(){return n.Decimal=ti,v},n.Decimal=v}(Bridge.global);System.Decimal=function(n,t){if(this.constructor!==System.Decimal)return new System.Decimal(n);if(typeof n=="string"){t=t||System.Globalization.CultureInfo.getCurrentCulture();var i=t&&t.getFormat(System.Globalization.NumberFormatInfo);if(i&&i.numberDecimalSeparator!=="."&&(n=n.replace(i.numberDecimalSeparator,".")),!/^\s*[+-]?(\d+|\d*\.\d+)((e|E)[+-]?\d+)?\s*$/.test(n))throw new System.FormatException;n=n.replace(/\s/g,"")}this.value=System.Decimal.getValue(n)};System.Decimal.$$name="System.Decimal";System.Decimal.prototype.$$name="System.Decimal";System.Decimal.$$inherits=[];Bridge.Class.addExtend(System.Decimal,[System.IComparable,System.IFormattable,System.IComparable$1(System.Decimal),System.IEquatable$1(System.Decimal)]);System.Decimal.instanceOf=function(n){return n instanceof System.Decimal};System.Decimal.getDefaultValue=function(){return new System.Decimal(0)};System.Decimal.getValue=function(n){return Bridge.hasValue(n)?n instanceof System.Decimal?n.value:n instanceof System.Int64||n instanceof System.UInt64?new Bridge.$Decimal(n.toString()):new Bridge.$Decimal(n):this.getDefaultValue()};System.Decimal.create=function(n){return Bridge.hasValue(n)?n instanceof System.Decimal?n:new System.Decimal(n):null};System.Decimal.lift=function(n){return n==null?null:System.Decimal.create(n)};System.Decimal.prototype.toString=function(n,t){return!n&&!t?this.value.toString():Bridge.Int.format(this,n,t)};System.Decimal.prototype.toFloat=function(){return this.value.toNumber()};System.Decimal.prototype.toJSON=function(){return this.value.toNumber()};System.Decimal.prototype.format=function(n,t){return Bridge.Int.format(this.toFloat(),n,t)};System.Decimal.prototype.decimalPlaces=function(){return this.value.decimalPlaces()};System.Decimal.prototype.dividedToIntegerBy=function(n){return new System.Decimal(this.value.dividedToIntegerBy(System.Decimal.getValue(n)))};System.Decimal.prototype.exponential=function(){return new System.Decimal(this.value.exponential())};System.Decimal.prototype.abs=function(){return new System.Decimal(this.value.abs())};System.Decimal.prototype.floor=function(){return new System.Decimal(this.value.floor())};System.Decimal.prototype.ceil=function(){return new System.Decimal(this.value.ceil())};System.Decimal.prototype.trunc=function(){return new System.Decimal(this.value.trunc())};System.Decimal.round=function(n,t){var i,r;return n=System.Decimal.create(n),i=Bridge.$Decimal.rounding,Bridge.$Decimal.rounding=t,r=new System.Decimal(n.value.round()),Bridge.$Decimal.rounding=i,r};System.Decimal.toDecimalPlaces=function(n,t,i){n=System.Decimal.create(n);return new System.Decimal(n.value.toDecimalPlaces(t,i))};System.Decimal.prototype.compareTo=function(n){return this.value.comparedTo(System.Decimal.getValue(n))};System.Decimal.prototype.add=function(n){return new System.Decimal(this.value.plus(System.Decimal.getValue(n)))};System.Decimal.prototype.sub=function(n){return new System.Decimal(this.value.minus(System.Decimal.getValue(n)))};System.Decimal.prototype.isZero=function(){return this.value.isZero};System.Decimal.prototype.mul=function(n){return new System.Decimal(this.value.times(System.Decimal.getValue(n)))};System.Decimal.prototype.div=function(n){return new System.Decimal(this.value.dividedBy(System.Decimal.getValue(n)))};System.Decimal.prototype.mod=function(n){return new System.Decimal(this.value.modulo(System.Decimal.getValue(n)))};System.Decimal.prototype.neg=function(){return new System.Decimal(this.value.negated())};System.Decimal.prototype.inc=function(){return new System.Decimal(this.value.plus(System.Decimal.getValue(1)))};System.Decimal.prototype.dec=function(){return new System.Decimal(this.value.minus(System.Decimal.getValue(1)))};System.Decimal.prototype.sign=function(){return this.value.isZero()?0:this.value.isNegative()?-1:1};System.Decimal.prototype.clone=function(){return new System.Decimal(this)};System.Decimal.prototype.ne=function(n){return!!this.compareTo(n)};System.Decimal.prototype.lt=function(n){return this.compareTo(n)<0};System.Decimal.prototype.lte=function(n){return this.compareTo(n)<=0};System.Decimal.prototype.gt=function(n){return this.compareTo(n)>0};System.Decimal.prototype.gte=function(n){return this.compareTo(n)>=0};System.Decimal.prototype.equals=function(n){return!this.compareTo(n)};System.Decimal.prototype.equalsT=function(n){return!this.compareTo(n)};System.Decimal.prototype.getHashCode=function(){for(var n=this.sign()*397+this.value.e|0,t=0;t<this.value.d.length;t++)n=n*397+this.value.d[t]|0;return n};System.Decimal.toInt=function(n,t){var i,r,u;if(!n)return null;if(t){if(t===System.Int64){if(i=n.value.trunc().toString(),r=new System.Int64(i),i!==r.value.toString())throw new System.OverflowException;return r}if(t===System.UInt64){if(n.value.isNegative())throw new System.OverflowException;if(i=n.value.trunc().toString(),r=new System.UInt64(i),i!==r.value.toString())throw new System.OverflowException;return r}return Bridge.Int.check(Bridge.Int.trunc(n.value.toNumber()),t)}if(u=Bridge.Int.trunc(System.Decimal.getValue(n).toNumber()),!Bridge.Int.instanceOf(u))throw new System.OverflowException;return u};System.Decimal.tryParse=function(n,t,i){try{return i.v=new System.Decimal(n,t),!0}catch(r){return i.v=new System.Decimal(0),!1}};System.Decimal.toFloat=function(n){return n?System.Decimal.getValue(n).toNumber():null};System.Decimal.setConfig=function(n){Bridge.$Decimal.config(n)};System.Decimal.min=function(){for(var t=[],n=0,i=arguments.length;n<i;n++)t.push(System.Decimal.getValue(arguments[n]));return new System.Decimal(Bridge.$Decimal.min.apply(Bridge.$Decimal,t))};System.Decimal.max=function(){for(var t=[],n=0,i=arguments.length;n<i;n++)t.push(System.Decimal.getValue(arguments[n]));return new System.Decimal(Bridge.$Decimal.max.apply(Bridge.$Decimal,t))};System.Decimal.random=function(n){return new System.Decimal(Bridge.$Decimal.random(n))};System.Decimal.exp=function(n){return new System.Decimal(System.Decimal.getValue(n).exp())};System.Decimal.exp=function(n){return new System.Decimal(System.Decimal.getValue(n).exp())};System.Decimal.ln=function(n){return new System.Decimal(System.Decimal.getValue(n).ln())};System.Decimal.log=function(n,t){return new System.Decimal(System.Decimal.getValue(n).log(t))};System.Decimal.pow=function(n,t){return new System.Decimal(System.Decimal.getValue(n).pow(t))};System.Decimal.sqrt=function(n){return new System.Decimal(System.Decimal.getValue(n).sqrt())};System.Decimal.prototype.isFinite=function(){return this.value.isFinite()};System.Decimal.prototype.isInteger=function(){return this.value.isInteger()};System.Decimal.prototype.isNaN=function(){return this.value.isNaN()};System.Decimal.prototype.isNegative=function(){return this.value.isNegative()};System.Decimal.prototype.isZero=function(){return this.value.isZero()};System.Decimal.prototype.log=function(n){return new System.Decimal(this.value.log(n))};System.Decimal.prototype.ln=function(){return new System.Decimal(this.value.ln())};System.Decimal.prototype.precision=function(){return this.value.precision()};System.Decimal.prototype.round=function(){var t=Bridge.$Decimal.rounding,n;return Bridge.$Decimal.rounding=6,n=new System.Decimal(this.value.round()),Bridge.$Decimal.rounding=t,n};System.Decimal.prototype.sqrt=function(){return new System.Decimal(this.value.sqrt())};System.Decimal.prototype.toDecimalPlaces=function(n,t){return new System.Decimal(this.value.toDecimalPlaces(n,t))};System.Decimal.prototype.toExponential=function(n,t){return this.value.toExponential(n,t)};System.Decimal.prototype.toFixed=function(n,t){return this.value.toFixed(n,t)};System.Decimal.prototype.pow=function(n){return new System.Decimal(this.value.pow(n))};System.Decimal.prototype.toPrecision=function(n,t){return this.value.toPrecision(n,t)};System.Decimal.prototype.toSignificantDigits=function(n,t){return new System.Decimal(this.value.toSignificantDigits(n,t))};System.Decimal.prototype.valueOf=function(){return this.value.valueOf()};System.Decimal.prototype.toFormat=function(n,t,i){var f=Bridge.$Decimal.format,u,e,r;return i&&!i.getFormat?(e=Bridge.merge({},f||{}),Bridge.$Decimal.format=Bridge.merge(e,i),u=this.value.toFormat(n,t)):(i=i||System.Globalization.CultureInfo.getCurrentCulture(),r=i&&i.getFormat(System.Globalization.NumberFormatInfo),r&&(Bridge.$Decimal.format.decimalSeparator=r.numberDecimalSeparator,Bridge.$Decimal.format.groupSeparator=r.numberGroupSeparator,Bridge.$Decimal.format.groupSize=r.numberGroupSizes[0]),u=this.value.toFormat(n,t)),Bridge.$Decimal.format=f,u};Bridge.$Decimal.config({precision:29});System.Decimal.Zero=System.Decimal(0);System.Decimal.One=System.Decimal(1);System.Decimal.MinusOne=System.Decimal(-1);System.Decimal.MinValue=System.Decimal("-79228162514264337593543950335");System.Decimal.MaxValue=System.Decimal("79228162514264337593543950335");Bridge.define("System.DayOfWeek",{$enum:!0,$statics:{sunday:0,monday:1,tuesday:2,wednesday:3,thursday:4,friday:5,saturday:6}});pt={getDefaultValue:function(){return new Date(-864e13)},utcNow:function(){var n=new Date;return new Date(n.getUTCFullYear(),n.getUTCMonth(),n.getUTCDate(),n.getUTCHours(),n.getUTCMinutes(),n.getUTCSeconds(),n.getUTCMilliseconds())},today:function(){var n=new Date;return new Date(n.getFullYear(),n.getMonth(),n.getDate())},timeOfDay:function(n){return new System.TimeSpan((n-new Date(n.getFullYear(),n.getMonth(),n.getDate()))*1e4)},isUseGenitiveForm:function(n,t,i,r){for(var f=0,u=t-1;u>=0&&n[u]!==r;u--);if(u>=0){while(--u>=0&&n[u]===r)f++;if(f<=1)return!0}for(u=t+i;u<n.length&&n[u]!==r;u++);if(u<n.length){for(f=0;++u<n.length&&n[u]===r;)f++;if(f<=1)return!0}return!1},format:function(n,t,i){var c=this,r=(i||System.Globalization.CultureInfo.getCurrentCulture()).getFormat(System.Globalization.DateTimeFormatInfo),o=n.getFullYear(),u=n.getMonth(),f=n.getDate(),l=n.getDay(),e=n.getHours(),a=n.getMinutes(),v=n.getSeconds(),y=n.getMilliseconds(),s=n.getTimezoneOffset(),h;return t=t||"G",t.length===1?(h=r.getAllDateTimePatterns(t,!0),t=h?h[0]:t):t.length===2&&t.charAt(0)==="%"&&(t=t.charAt(1)),t.replace(/(\\.|'[^']*'|"[^"]*"|d{1,4}|M{1,4}|yyyy|yy|y|HH?|hh?|mm?|ss?|tt?|f{1,3}|z{1,3}|\:|\/)/g,function(n,i,h){var p=n;switch(n){case"dddd":p=r.dayNames[l];break;case"ddd":p=r.abbreviatedDayNames[l];break;case"dd":p=f<10?"0"+f:f;break;case"d":p=f;break;case"MMMM":p=c.isUseGenitiveForm(t,h,4,"d")?r.monthGenitiveNames[u]:r.monthNames[u];break;case"MMM":p=c.isUseGenitiveForm(t,h,3,"d")?r.abbreviatedMonthGenitiveNames[u]:r.abbreviatedMonthNames[u];break;case"MM":p=u+1<10?"0"+(u+1):u+1;break;case"M":p=u+1;break;case"yyyy":p=o;break;case"yy":p=(o%100).toString();p.length===1&&(p="0"+p);break;case"y":p=o%100;break;case"h":case"hh":p=e%12;p?n==="hh"&&p.length===1&&(p="0"+p):p="12";break;case"HH":p=e.toString();p.length===1&&(p="0"+p);break;case"H":p=e;break;case"mm":p=a.toString();p.length===1&&(p="0"+p);break;case"m":p=a;break;case"ss":p=v.toString();p.length===1&&(p="0"+p);break;case"s":p=v;break;case"t":case"tt":p=e<12?r.amDesignator:r.pmDesignator;n==="t"&&(p=p.charAt(0));break;case"f":case"ff":case"fff":p=y.toString();p.length<3&&(p=Array(3-p.length).join("0")+p);n==="ff"?p=p.substr(0,2):n==="f"&&(p=p.charAt(0));break;case"z":p=s/60;p=(p>=0?"-":"+")+Math.floor(Math.abs(p));break;case"zz":case"zzz":p=s/60;p=(p>=0?"-":"+")+System.String.alignString(Math.floor(Math.abs(p)).toString(),2,"0",2);n==="zzz"&&(p+=r.timeSeparator+System.String.alignString(Math.floor(Math.abs(s%60)).toString(),2,"0",2));break;case":":p=r.timeSeparator;break;case"/":p=r.dateSeparator;break;default:p=n.substr(1,n.length-1-(n.charAt(0)!=="\\"))}return p})},parse:function(n,t,i,r){var u=this.parseExact(n,null,t,i,!0);if(u!==null)return u;if(u=Date.parse(n),isNaN(u)){if(!r)throw new System.FormatException("String does not contain a valid string representation of a date and time.");}else return new Date(u)},parseExact:function(n,t,i,r,u){var ft,ct;if(t||(t=["G","g","F","f","D","d","R","r","s","S","U","u","O","o","Y","y","M","m","T","t"]),Bridge.isArray(t)){for(ft=0,ft;ft<t.length;ft++)if(ct=Bridge.Date.parseExact(n,t[ft],i,r,!0),ct!=null)return ct;if(u)return null;throw new System.FormatException("String does not contain a valid string representation of a date and time.");}var y=(i||System.Globalization.CultureInfo.getCurrentCulture()).getFormat(System.Globalization.DateTimeFormatInfo),et=y.amDesignator,ot=y.pmDesignator,e=0,k=0,b=0,st,f,c=0,l=1,w=1,h=0,d=0,g=0,v=0,nt="",a=0,ht=0,tt,it,rt,ut,s,o=!1,p=!1,lt,at;if(n==null)throw new System.ArgumentNullException("str");for(t=t||"G",t.length===1?(at=y.getAllDateTimePatterns(t,!0),t=at?at[0]:t):t.length===2&&t.charAt(0)==="%"&&(t=t.charAt(1));k<t.length;){if(st=t.charAt(k),f="",p==="\\")f+=st,k++;else while(t.charAt(k)===st&&k<t.length)f+=st,k++;if(lt=!0,!p)if(f==="yyyy"||f==="yy"||f==="y"){if(f==="yyyy"?c=this.subparseInt(n,e,4,4):f==="yy"?c=this.subparseInt(n,e,2,2):f==="y"&&(c=this.subparseInt(n,e,2,4)),c==null){o=!0;break}e+=c.length;c.length===2&&(c=~~c,c=(c>30?1900:2e3)+c)}else if(f==="MMM"||f==="MMMM"){for(l=0,ut=f==="MMM"?this.isUseGenitiveForm(t,k,3,"d")?y.abbreviatedMonthGenitiveNames:y.abbreviatedMonthNames:this.isUseGenitiveForm(t,k,4,"d")?y.monthGenitiveNames:y.monthNames,b=0;b<ut.length;b++)if(s=ut[b],n.substring(e,e+s.length).toLowerCase()===s.toLowerCase()){l=b%12+1;e+=s.length;break}if(l<1||l>12){o=!0;break}}else if(f==="MM"||f==="M"){if(l=this.subparseInt(n,e,f.length,2),l==null||l<1||l>12){o=!0;break}e+=l.length}else if(f==="dddd"||f==="ddd"){for(ut=f==="ddd"?y.abbreviatedDayNames:y.dayNames,b=0;b<ut.length;b++)if(s=ut[b],n.substring(e,e+s.length).toLowerCase()===s.toLowerCase()){e+=s.length;break}}else if(f==="dd"||f==="d"){if(w=this.subparseInt(n,e,f.length,2),w==null||w<1||w>31){o=!0;break}e+=w.length}else if(f==="hh"||f==="h"){if(h=this.subparseInt(n,e,f.length,2),h==null||h<1||h>12){o=!0;break}e+=h.length}else if(f==="HH"||f==="H"){if(h=this.subparseInt(n,e,f.length,2),h==null||h<0||h>23){o=!0;break}e+=h.length}else if(f==="mm"||f==="m"){if(d=this.subparseInt(n,e,f.length,2),d==null||d<0||d>59)return null;e+=d.length}else if(f==="ss"||f==="s"){if(g=this.subparseInt(n,e,f.length,2),g==null||g<0||g>59){o=!0;break}e+=g.length}else if(f==="u"){if(v=this.subparseInt(n,e,1,7),v==null){o=!0;break}e+=v.length;v.length>3&&(v=v.substring(0,3))}else if(f==="fffffff"||f==="ffffff"||f==="fffff"||f==="ffff"||f==="fff"||f==="ff"||f==="f"){if(v=this.subparseInt(n,e,f.length,7),v==null){o=!0;break}e+=v.length;v.length>3&&(v=v.substring(0,3))}else if(f==="t"){if(n.substring(e,e+1).toLowerCase()===et.charAt(0).toLowerCase())nt=et;else if(n.substring(e,e+1).toLowerCase()===ot.charAt(0).toLowerCase())nt=ot;else{o=!0;break}e+=1}else if(f==="tt"){if(n.substring(e,e+2).toLowerCase()===et.toLowerCase())nt=et;else if(n.substring(e,e+2).toLowerCase()===ot.toLowerCase())nt=ot;else{o=!0;break}e+=2}else if(f==="z"||f==="zz"){if(it=n.charAt(e),it==="-")rt=!0;else if(it==="+")rt=!1;else{o=!0;break}if(e++,a=this.subparseInt(n,e,1,2),a==null||a>14){o=!0;break}e+=a.length;rt&&(a=-a)}else if(f==="zzz"){if(s=n.substring(e,e+6),e+=6,s.length!==6){o=!0;break}if(it=s.charAt(0),it==="-")rt=!0;else if(it==="+")rt=!1;else{o=!0;break}if(tt=1,a=this.subparseInt(s,tt,1,2),a==null||a>14){o=!0;break}if(tt+=a.length,rt&&(a=-a),s.charAt(tt)!==y.timeSeparator){o=!0;break}if(tt++,ht=this.subparseInt(s,tt,1,2),ht==null||a>59){o=!0;break}}else lt=!1;if(p||!lt){if(s=n.substring(e,e+f.length),!p&&(f===":"&&s!==y.timeSeparator||f==="/"&&s!==y.dateSeparator)||s!==f&&f!=="'"&&f!=='"'&&f!=="\\"){o=!0;break}if(p==="\\"&&(p=!1),f!=="'"&&f!=='"'&&f!=="\\")e+=f.length;else if(p===!1)p=f;else{if(p!==f){o=!0;break}p=!1}}}if(p&&(o=!0),o||(e!==n.length?o=!0:l===2?c%4==0&&c%100!=0||c%400==0?w>29&&(o=!0):w>28&&(o=!0):(l===4||l===6||l===9||l===11)&&w>30&&(o=!0)),o){if(u)return null;throw new System.FormatException("String does not contain a valid string representation of a date and time.");}return(h<12&&nt===ot?h=+h+12:h>11&&nt===et&&(h-=12),a===0&&ht===0&&!r)?new Date(c,l-1,w,h,d,g,v):new Date(Date.UTC(c,l-1,w,h-a,d-ht,g,v))},subparseInt:function(n,t,i,r){for(var f,u=r;u>=i;u--){if(f=n.substring(t,t+u),f.length<i)return null;if(/^\d+$/.test(f))return f}return null},tryParse:function(n,t,i,r){return(i.v=this.parse(n,t,r,!0),i.v==null)?(i.v=new Date(-864e13),!1):!0},tryParseExact:function(n,t,i,r,u){return(r.v=this.parseExact(n,t,i,u,!0),r.v==null)?(r.v=new Date(-864e13),!1):!0},isDaylightSavingTime:function(n){var t=Bridge.Date.today();return t.setMonth(0),t.setDate(1),t.getTimezoneOffset()!==n.getTimezoneOffset()},toUTC:function(n){return new Date(n.getUTCFullYear(),n.getUTCMonth(),n.getUTCDate(),n.getUTCHours(),n.getUTCMinutes(),n.getUTCSeconds(),n.getUTCMilliseconds())},toLocal:function(n){return new Date(Date.UTC(n.getFullYear(),n.getMonth(),n.getDate(),n.getHours(),n.getMinutes(),n.getSeconds(),n.getMilliseconds()))},dateDiff:function(n,t){var i=Date.UTC(n.getFullYear(),n.getMonth(),n.getDate(),n.getHours(),n.getMinutes(),n.getSeconds(),n.getMilliseconds()),r=Date.UTC(t.getFullYear(),t.getMonth(),t.getDate(),t.getHours(),t.getMinutes(),t.getSeconds(),t.getMilliseconds());return i-r},dateAddSubTimespan:function(n,t,i){var r=new Date(n.getTime());return r.setDate(r.getDate()+i*t.getDays()),r.setHours(r.getHours()+i*t.getHours()),r.setMinutes(r.getMinutes()+i*t.getMinutes()),r.setSeconds(r.getSeconds()+i*t.getSeconds()),r.setMilliseconds(r.getMilliseconds()+i*t.getMilliseconds()),r},subdt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?this.dateAddSubTimespan(n,t,-1):null},adddt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?this.dateAddSubTimespan(n,t,1):null},subdd:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?new System.TimeSpan(Bridge.Date.dateDiff(n,t)*1e4):null},gt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n>t:!1},gte:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n>=t:!1},lt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n<t:!1},lte:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n<=t:!1}};Bridge.Date=pt;Bridge.define("System.TimeSpan",{inherits:[System.IComparable],$struct:!0,statics:{fromDays:function(n){return new System.TimeSpan(n*864e9)},fromHours:function(n){return new System.TimeSpan(n*36e9)},fromMilliseconds:function(n){return new System.TimeSpan(n*1e4)},fromMinutes:function(n){return new System.TimeSpan(n*6e8)},fromSeconds:function(n){return new System.TimeSpan(n*1e7)},fromTicks:function(n){return new System.TimeSpan(n)},constructor:function(){this.zero=new System.TimeSpan(System.Int64.Zero);this.maxValue=new System.TimeSpan(System.Int64.MaxValue);this.minValue=new System.TimeSpan(System.Int64.MinValue)},getDefaultValue:function(){return new System.TimeSpan(System.Int64.Zero)},neg:function(n){return Bridge.hasValue(n)?new System.TimeSpan(n.ticks.neg()):null},sub:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?new System.TimeSpan(n.ticks.sub(t.ticks)):null},eq:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n.ticks.eq(t.ticks):null},neq:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n.ticks.ne(t.ticks):null},plus:function(n){return Bridge.hasValue(n)?new System.TimeSpan(n.ticks):null},add:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?new System.TimeSpan(n.ticks.add(t.ticks)):null},gt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n.ticks.gt(t.ticks):!1},gte:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n.ticks.gte(t.ticks):!1},lt:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n.ticks.lt(t.ticks):!1},lte:function(n,t){return Bridge.hasValue(n)&&Bridge.hasValue(t)?n.ticks.lte(t.ticks):!1}},constructor:function(){this.ticks=System.Int64.Zero;arguments.length===1?this.ticks=arguments[0]instanceof System.Int64?arguments[0]:new System.Int64(arguments[0]):arguments.length===3?this.ticks=new System.Int64(arguments[0]).mul(60).add(arguments[1]).mul(60).add(arguments[2]).mul(1e7):arguments.length===4?this.ticks=new System.Int64(arguments[0]).mul(24).add(arguments[1]).mul(60).add(arguments[2]).mul(60).add(arguments[3]).mul(1e7):arguments.length===5&&(this.ticks=new System.Int64(arguments[0]).mul(24).add(arguments[1]).mul(60).add(arguments[2]).mul(60).add(arguments[3]).mul(1e3).add(arguments[4]).mul(1e4))},getTicks:function(){return this.ticks},getDays:function(){return this.ticks.div(864e9).toNumber()},getHours:function(){return this.ticks.div(36e9).mod(24).toNumber()},getMilliseconds:function(){return this.ticks.div(1e4).mod(1e3).toNumber()},getMinutes:function(){return this.ticks.div(6e8).mod(60).toNumber()},getSeconds:function(){return this.ticks.div(1e7).mod(60).toNumber()},getTotalDays:function(){return this.ticks.toNumberDivided(864e9)},getTotalHours:function(){return this.ticks.toNumberDivided(36e9)},getTotalMilliseconds:function(){return this.ticks.toNumberDivided(1e4)},getTotalMinutes:function(){return this.ticks.toNumberDivided(6e8)},getTotalSeconds:function(){return this.ticks.toNumberDivided(1e7)},get12HourHour:function(){return this.getHours()>12?this.getHours()-12:this.getHours()===0?12:this.getHours()},add:function(n){return new System.TimeSpan(this.ticks.add(n.ticks))},subtract:function(n){return new System.TimeSpan(this.ticks.sub(n.ticks))},duration:function(){return new System.TimeSpan(this.ticks.abs())},negate:function(){return new System.TimeSpan(this.ticks.neg())},compareTo:function(n){return this.ticks.compareTo(n.ticks)},equals:function(n){return n.ticks.eq(this.ticks)},equalsT:function(n){return n.ticks.eq(this.ticks)},format:function(n,t){return this.toString(n,t)},getHashCode:function(){return this.ticks.getHashCode()},toString:function(n,t){var i=this.ticks,f="",r=this,e=(t||System.Globalization.CultureInfo.getCurrentCulture()).getFormat(System.Globalization.DateTimeFormatInfo),u=function(n,t){return System.String.alignString((n|0).toString(),t||2,"0",2)};return n?n.replace(/dd?|HH?|hh?|mm?|ss?|tt?/g,function(n){switch(n){case"d":return r.getDays();case"dd":return u(r.getDays());case"H":return r.getHours();case"HH":return u(r.getHours());case"h":return r.get12HourHour();case"hh":return u(r.get12HourHour());case"m":return r.getMinutes();case"mm":return u(r.getMinutes());case"s":return r.getSeconds();case"ss":return u(r.getSeconds());case"t":return(r.getHours()<12?e.amDesignator:e.pmDesignator).substring(0,1);case"tt":return r.getHours()<12?e.amDesignator:e.pmDesignator}}):(i.abs().gte(864e9)&&(f+=u(i.toNumberDivided(864e9))+".",i=i.mod(864e9)),f+=u(i.toNumberDivided(36e9))+":",i=i.mod(36e9),f+=u(i.toNumberDivided(6e8)|0)+":",i=i.mod(6e8),f+=u(i.toNumberDivided(1e7)),i=i.mod(1e7),i.gt(0)&&(f+="."+u(i.toNumber(),7)),f)}});Bridge.Class.addExtend(System.TimeSpan,[System.IComparable$1(System.TimeSpan),System.IEquatable$1(System.TimeSpan)]);Bridge.define("System.Text.StringBuilder",{constructor:function(){this.buffer=[];this.capacity=16;arguments.length===1?this.append(arguments[0]):arguments.length===2?(this.append(arguments[0]),this.setCapacity(arguments[1])):arguments.length===3&&this.append(arguments[0],arguments[1],arguments[2])},getLength:function(){if(this.buffer.length<2)return this.buffer[0]?this.buffer[0].length:0;var n=this.buffer.join("");return this.buffer=[],this.buffer[0]=n,n.length},getCapacity:function(){var n=this.getLength();return this.capacity>n?this.capacity:n},setCapacity:function(n){var t=this.getLength();n>t&&(this.capacity=n)},toString:function(){var n=this.buffer.join(""),t,i;return(this.buffer=[],this.buffer[0]=n,arguments.length===2)?(t=arguments[0],i=arguments[1],this.checkLimits(n,t,i),n.substr(t,i)):n},append:function(n){var i,t;if(n==null)return this;if(arguments.length===2){if(t=arguments[1],t===0)return this;if(t<0)throw new System.ArgumentOutOfRangeException("count","cannot be less than zero");n=Array(t+1).join(n).toString()}else if(arguments.length===3){if(i=arguments[1],t=arguments[2],t===0)return this;this.checkLimits(n,i,t);n=n.substr(i,t)}return this.buffer[this.buffer.length]=n,this},appendFormat:function(){return this.append(System.String.format.apply(System.String,arguments))},clear:function(){return this.buffer=[],this},appendLine:function(){return arguments.length===1&&this.append(arguments[0]),this.append("\r\n")},equals:function(n){return n==null?!1:n===this?!0:this.toString()===n.toString()},remove:function(n,t){var i=this.buffer.join("");return(this.checkLimits(i,n,t),i.length===t&&n===0)?this.clear():(t>0&&(this.buffer=[],this.buffer[0]=i.substring(0,n),this.buffer[1]=i.substring(n+t,i.length)),this)},insert:function(n,t){var r,i;if(t==null)return this;if(arguments.length===3){if(r=arguments[2],r===0)return this;if(r<0)throw new System.ArgumentOutOfRangeException("count","cannot be less than zero");t=Array(r+1).join(t).toString()}return i=this.buffer.join(""),this.buffer=[],n<1?(this.buffer[0]=t,this.buffer[1]=i):n>=i.length?(this.buffer[0]=i,this.buffer[1]=t):(this.buffer[0]=i.substring(0,n),this.buffer[1]=t,this.buffer[2]=i.substring(n,i.length)),this},replace:function(n,t){var f=new RegExp(n,"g"),i=this.buffer.join("");if(this.buffer=[],arguments.length===4){var r=arguments[2],u=arguments[3],e=i.substr(r,u);this.checkLimits(i,r,u);this.buffer[0]=i.substring(0,r);this.buffer[1]=e.replace(f,t);this.buffer[2]=i.substring(r+u,i.length)}else this.buffer[0]=i.replace(f,t);return this},checkLimits:function(n,t,i){if(i<0)throw new System.ArgumentOutOfRangeException("length","must be non-negative");if(t<0)throw new System.ArgumentOutOfRangeException("startIndex","startIndex cannot be less than zero");if(i>n.length-t)throw new System.ArgumentOutOfRangeException("Index and length must refer to a location within the string");}}),function(){var n=RegExp("[-\\[\\]\\/\\{\\}\\(\\)\\*\\+\\?\\.\\\\\\^\\$\\|]","g"),t=function(t){return t.replace(n,"\\$&")};Bridge.regexpEscape=t}();System.Diagnostics.Debug={writeln:function(n){var t=Bridge.global;if(t.console){if(t.console.debug){t.console.debug(n);return}if(t.console.log){t.console.log(n);return}}else if(t.opera&&t.opera.postError){t.opera.postError(n);return}},_fail:function(n){System.Diagnostics.Debug.writeln(n)},assert:function(n,t){n||(t="Assert failed: "+t,confirm(t+"\r\n\r\nBreak into debugger?")&&System.Diagnostics.Debug._fail(t))},fail:function(n){System.Diagnostics.Debug._fail(n)}};Bridge.define("System.Diagnostics.Stopwatch",{constructor:function(){this._stopTime=System.Int64.Zero;this._startTime=System.Int64.Zero;this.isRunning=!1},reset:function(){this._stopTime=this._startTime=System.Diagnostics.Stopwatch.getTimestamp();this.isRunning=!1},ticks:function(){return(this.isRunning?System.Diagnostics.Stopwatch.getTimestamp():this._stopTime).sub(this._startTime)},milliseconds:function(){return this.ticks().mul(1e3).div(System.Diagnostics.Stopwatch.frequency)},timeSpan:function(){return new System.TimeSpan(this.milliseconds().mul(1e4))},start:function(){this.isRunning||(this._startTime=System.Diagnostics.Stopwatch.getTimestamp(),this.isRunning=!0)},stop:function(){this.isRunning&&(this._stopTime=System.Diagnostics.Stopwatch.getTimestamp(),this.isRunning=!1)},restart:function(){this.isRunning=!1;this.start()},statics:{startNew:function(){var n=new System.Diagnostics.Stopwatch;return n.start(),n}}});typeof window!="undefined"&&window.performance&&window.performance.now?(System.Diagnostics.Stopwatch.frequency=new System.Int64(1e6),System.Diagnostics.Stopwatch.isHighResolution=!0,System.Diagnostics.Stopwatch.getTimestamp=function(){return new System.Int64(Math.round(window.performance.now()*1e3))}):typeof process!="undefined"&&process.hrtime?(System.Diagnostics.Stopwatch.frequency=new System.Int64(1e9),System.Diagnostics.Stopwatch.isHighResolution=!0,System.Diagnostics.Stopwatch.getTimestamp=function(){var n=process.hrtime();return new System.Int64(n[0]).mul(1e9).add(n[1])}):(System.Diagnostics.Stopwatch.frequency=new System.Int64(1e3),System.Diagnostics.Stopwatch.isHighResolution=!1,System.Diagnostics.Stopwatch.getTimestamp=function(){return new System.Int64((new Date).valueOf())});System.Diagnostics.Contracts.Contract={reportFailure:function(n,t,i,r,u){var f=i.toString(),e,o;if(f=f.substring(f.indexOf("return")+7),f=f.substr(0,f.lastIndexOf(";")),e=f?"Contract '"+f+"' failed":"Contract failed",o=t?e+": "+t:e,u)throw new u(f,t);else throw new System.Diagnostics.Contracts.ContractException(n,o,t,f,r);},assert:function(n,t,i){t()||System.Diagnostics.Contracts.Contract.reportFailure(n,i,t,null)},requires:function(n,t,i){t()||System.Diagnostics.Contracts.Contract.reportFailure(0,i,t,null,n)},forAll:function(n,t,i){if(!i)throw new System.ArgumentNullException("predicate");for(;n<t;n++)if(!i(n))return!1;return!0},forAll$1:function(n,t){if(!n)throw new System.ArgumentNullException("collection");if(!t)throw new System.ArgumentNullException("predicate");var i=Bridge.getEnumerator(n);try{while(i.moveNext())if(!t(i.getCurrent()))return!1;return!0}finally{i.dispose()}},exists:function(n,t,i){if(!i)throw new System.ArgumentNullException("predicate");for(;n<t;n++)if(i(n))return!0;return!1},exists$1:function(n,t){if(!n)throw new System.ArgumentNullException("collection");if(!t)throw new System.ArgumentNullException("predicate");var i=Bridge.getEnumerator(n);try{while(i.moveNext())if(t(i.getCurrent()))return!0;return!1}finally{i.dispose()}}};Bridge.define("System.Diagnostics.Contracts.ContractFailureKind",{$enum:!0,$statics:{precondition:0,postcondition:1,postconditionOnException:2,invarian:3,assert:4,assume:5}});Bridge.define("System.Diagnostics.Contracts.ContractException",{inherits:[System.Exception],constructor:function(n,t,i,r,u){System.Exception.prototype.$constructor.call(this,t,u);this._kind=n;this._failureMessage=t||null;this._userMessage=i||null;this._condition=r||null},getKind:function(){return this._kind},getFailure:function(){return this._failureMessage},getUserMessage:function(){return this._userMessage},getCondition:function(){return this._condition}});k={toIndex:function(n,t){if(t.length!==(n.$s?n.$s.length:1))throw new System.ArgumentException("Invalid number of indices");if(t[0]<0||t[0]>=(n.$s?n.$s[0]:n.length))throw new System.ArgumentException("Index 0 out of range");var r=t[0],i;if(n.$s)for(i=1;i<n.$s.length;i++){if(t[i]<0||t[i]>=n.$s[i])throw new System.ArgumentException("Index "+i+" out of range");r=r*n.$s[i]+t[i]}return r},$get:function(n){var t=this[System.Array.toIndex(this,n)];return typeof t!="undefined"?t:this.$v},get:function(n){var t,i,r;if(arguments.length<2)throw new System.ArgumentNullException("indices");for(t=Array.prototype.slice.call(arguments,1),i=0;i<t.length;i++)if(!Bridge.hasValue(t[i]))throw new System.ArgumentNullException("indices");return r=n[System.Array.toIndex(n,t)],typeof r!="undefined"?r:n.$v},$set:function(n,t){this[System.Array.toIndex(this,Array.prototype.slice.call(n,0))]=t},set:function(n,t){var i=Array.prototype.slice.call(arguments,2);n[System.Array.toIndex(n,i)]=t},getLength:function(n,t){if(t<0||t>=(n.$s?n.$s.length:1))throw new System.IndexOutOfRangeException;return n.$s?n.$s[t]:n.length},getRank:function(n){return n.$s?n.$s.length:1},getLower:function(n,t){return System.Array.getLength(n,t),0},create:function(n,t){var i=[],h=arguments.length>2?1:0,r,f,e,u,o,s;for(i.$v=n,i.$s=[],i.get=System.Array.$get,i.set=System.Array.$set,r=2;r<arguments.length;r++)h*=arguments[r],i.$s[r-2]=arguments[r];if(i.length=h,t)for(r=0;r<i.length;r++){for(o=[],s=r,f=i.$s.length-1;f>=0;f--)u=s%i.$s[f],o.unshift(u),s=Bridge.Int.div(s-u,i.$s[f]);for(e=t,u=0;u<o.length;u++)e=e[o[u]];i[r]=e}return i},init:function(n,t){for(var r=new Array(n),u=Bridge.isFunction(t),i=0;i<n;i++)r[i]=u?t():t;return r},toEnumerable:function(n){return new Bridge.ArrayEnumerable(n)},toEnumerator:function(n){return new Bridge.ArrayEnumerator(n)},_typedArrays:{Float32Array:!0,Float64Array:!0,Int8Array:!0,Int16Array:!0,Int32Array:!0,Uint8Array:!0,Uint8ClampedArray:!0,Uint16Array:!0,Uint32Array:!0},is:function(n,t){return n instanceof Bridge.ArrayEnumerator?n.constructor===t||n instanceof t||t===Bridge.ArrayEnumerator||t.$$name&&System.String.startsWith(t.$$name,"System.Collections.IEnumerator")||t.$$name&&System.String.startsWith(t.$$name,"System.Collections.Generic.IEnumerator")?!0:!1:Bridge.isArray(n)?n.constructor===t||n instanceof t?!0:t===System.Collections.IEnumerable||t===System.Collections.ICollection||t===System.ICloneable||t.$$name&&System.String.startsWith(t.$$name,"System.Collections.Generic.IEnumerable$1")||t.$$name&&System.String.startsWith(t.$$name,"System.Collections.Generic.ICollection$1")||t.$$name&&System.String.startsWith(t.$$name,"System.Collections.Generic.IList$1")?!0:!!System.Array._typedArrays[String.prototype.slice.call(Object.prototype.toString.call(n),8,-1)]:!1},clone:function(n){return n.length===1?[n[0]]:n.slice(0)},getCount:function(n){return Bridge.isArray(n)?n.length:Bridge.isFunction(n.getCount)?n.getCount():0},add:function(n,t){Bridge.isArray(n)?n.push(t):Bridge.isFunction(n.add)&&n.add(t)},clear:function(n,t){Bridge.isArray(n)?System.Array.fill(n,t?t.getDefaultValue||Bridge.getDefaultValue(t):null,0,n.length):Bridge.isFunction(n.clear)&&n.clear()},fill:function(n,t,i,r){if(!Bridge.hasValue(n))throw new System.ArgumentNullException("dst");if(i<0||r<0||i+r>n.length)throw new System.IndexOutOfRangeException;for(var u=Bridge.isFunction(t);--r>=0;)n[i+r]=u?t():t},copy:function(n,t,i,r,u){if(t<0||r<0||u<0)throw new System.ArgumentOutOfRangeException;if(u>n.length-t||u>i.length-r)throw new System.IndexOutOfRangeException;if(t<r&&n===i)while(--u>=0)i[r+u]=n[t+u];else for(var f=0;f<u;f++)i[r+f]=n[t+f]},indexOf:function(n,t,i,r){if(Bridge.isArray(n)){var u,f,e;for(i=i||0,r=r||n.length,e=i+r,u=i;u<e;u++)if(f=n[u],f===t||System.Collections.Generic.EqualityComparer$1.$default.equals2(f,t))return u}else if(Bridge.isFunction(n.indexOf))return n.indexOf(t);return-1},contains:function(n,t){return Bridge.isArray(n)?System.Array.indexOf(n,t)>-1:Bridge.isFunction(n.contains)?n.contains(t):!1},remove:function(n,t){if(Bridge.isArray(n)){var i=System.Array.indexOf(n,t);if(i>-1)return n.splice(i,1),!0}else if(Bridge.isFunction(n.remove))return n.remove(t);return!1},insert:function(n,t,i){Bridge.isArray(n)?n.splice(t,0,i):Bridge.isFunction(n.insert)&&n.insert(t,i)},removeAt:function(n,t){Bridge.isArray(n)?n.splice(t,1):Bridge.isFunction(n.removeAt)&&n.removeAt(t)},getItem:function(n,t){return Bridge.isArray(n)?n[t]:Bridge.isFunction(n.get)?n.get(t):Bridge.isFunction(n.getItem)?n.getItem(t):Bridge.isFunction(n.get_Item)?n.get_Item(t):void 0},setItem:function(n,t,i){Bridge.isArray(n)?n[t]=i:Bridge.isFunction(n.set)?n.set(t,i):Bridge.isFunction(n.setItem)?n.setItem(t,i):Bridge.isFunction(n.set_Item)&&n.set_Item(t,i)},resize:function(n,t,i){var u;if(t<0)throw new System.ArgumentOutOfRangeException("newSize",null,null,t);var f=0,e=Bridge.isFunction(i),r=n.v;for(r?(f=r.length,r.length=t):r=new Array(t),u=f;u<t;u++)r[u]=e?i():i;n.v=r},reverse:function(n,t,i){var r,u,f;if(!k)throw new System.ArgumentNullException("arr");if(t||t===0||(t=0,i=n.length),t<0||i<0)throw new System.ArgumentOutOfRangeException(t<0?"index":"length","Non-negative number required.");if(k.length-t<i)throw new System.ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");if(System.Array.getRank(n)!==1)throw new System.Exception("Only single dimension arrays are supported here.");for(r=t,u=t+i-1;r<u;)f=n[r],n[r]=n[u],n[u]=f,r++,u--},binarySearch:function(n,t,i,r,u){var o,f,s,e,h;if(!n)throw new System.ArgumentNullException("array");if(o=0,t<o||i<0)throw new System.ArgumentOutOfRangeException(t<o?"index":"length","Non-negative number required.");if(n.length-(t-o)<i)throw new System.ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");if(System.Array.getRank(n)!==1)throw new System.RankException("Only single dimensional arrays are supported for the requested action.");for(u||(u=System.Collections.Generic.Comparer$1.$default),f=t,s=t+i-1;f<=s;){e=f+(s-f>>1);try{h=u.compare(n[e],r)}catch(c){throw new System.InvalidOperationException("Failed to compare two elements in the array.",c);}if(h===0)return e;h<0?f=e+1:s=e-1}return~f},sort:function(n,t,i,r){var f,u;if(!n)throw new System.ArgumentNullException("array");if(arguments.length===2&&typeof t=="object"&&(r=t,t=null),Bridge.isNumber(t)||(t=0),Bridge.isNumber(i)||(i=n.length),r||(r=System.Collections.Generic.Comparer$1.$default),t===0&&i===n.length)n.sort(Bridge.fn.bind(r,r.compare));else for(f=n.slice(t,t+i),f.sort(Bridge.fn.bind(r,r.compare)),u=t;u<t+i;u++)n[u]=f[u-t]},min:function(n,t){for(var r=n[0],u=n.length,i=0;i<u;i++)!(n[i]<r||r<t)||n[i]<t||(r=n[i]);return r},max:function(n,t){for(var r=n[0],u=n.length,i=0;i<u;i++)!(n[i]>r||r>t)||n[i]>t||(r=n[i]);return r},addRange:function(n,t){if(Bridge.isArray(t))n.push.apply(n,t);else{var i=Bridge.getEnumerator(t);try{while(i.moveNext())n.push(i.getCurrent())}finally{Bridge.is(i,System.IDisposable)&&i.dispose()}}},convertAll:function(n,t){if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(!Bridge.hasValue(t))throw new System.ArgumentNullException("converter");return n.map(t)},find:function(n,t,i){if(!Bridge.hasValue(t))throw new System.ArgumentNullException("array");if(!Bridge.hasValue(i))throw new System.ArgumentNullException("match");for(var r=0;r<t.length;r++)if(i(t[r]))return t[r];return Bridge.getDefaultValue(n)},findAll:function(n,t){var r,i;if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(!Bridge.hasValue(t))throw new System.ArgumentNullException("match");for(r=[],i=0;i<n.length;i++)t(n[i])&&r.push(n[i]);return r},findIndex:function(n,t,i,r){var f,u;if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(arguments.length===2?(r=t,t=0,i=n.length):arguments.length===3&&(r=i,i=n.length-t),t<0||t>n.length)throw new System.ArgumentOutOfRangeException("startIndex");if(i<0||t>n.length-i)throw new System.ArgumentOutOfRangeException("count");if(!Bridge.hasValue(r))throw new System.ArgumentNullException("match");for(f=t+i,u=t;u<f;u++)if(r(n[u]))return u;return-1},findLast:function(n,t,i){if(!Bridge.hasValue(t))throw new System.ArgumentNullException("array");if(!Bridge.hasValue(i))throw new System.ArgumentNullException("match");for(var r=t.length-1;r>=0;r--)if(i(t[r]))return t[r];return Bridge.getDefaultValue(n)},findLastIndex:function(n,t,i,r){var f,u;if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(arguments.length===2?(r=t,t=n.length-1,i=n.length):arguments.length===3&&(r=i,i=t+1),!Bridge.hasValue(r))throw new System.ArgumentNullException("match");if(n.length===0){if(t!==-1)throw new System.ArgumentOutOfRangeException("startIndex");}else if(t<0||t>=n.length)throw new System.ArgumentOutOfRangeException("startIndex");if(i<0||t-i+1<0)throw new System.ArgumentOutOfRangeException("count");for(f=t-i,u=t;u>f;u--)if(r(n[u]))return u;return-1},forEach:function(n,t){if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(!Bridge.hasValue(t))throw new System.ArgumentNullException("action");for(var i=0;i<n.length;i++)t(n[i])},indexOfT:function(n,t,i,r){if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(arguments.length===2?(i=0,r=n.length):arguments.length===3&&(r=n.length-i),i<0||i>=n.length&&n.length>0)throw new System.ArgumentOutOfRangeException("startIndex","out of range");if(r<0||r>n.length-i)throw new System.ArgumentOutOfRangeException("count","out of range");return System.Array.indexOf(n,t,i,r)},lastIndexOfT:function(n,t,i,r){var e,u,f;if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(arguments.length===2?(i=n.length-1,r=n.length):arguments.length===3&&(r=n.length===0?0:i+1),i<0||i>=n.length&&n.length>0)throw new System.ArgumentOutOfRangeException("startIndex","out of range");if(r<0||i-r+1<0)throw new System.ArgumentOutOfRangeException("count","out of range");for(e=i-r+1,u=i;u>=e;u--)if(f=n[u],f===t||System.Collections.Generic.EqualityComparer$1.$default.equals2(f,t))return u;return-1},trueForAll:function(n,t){if(!Bridge.hasValue(n))throw new System.ArgumentNullException("array");if(!Bridge.hasValue(t))throw new System.ArgumentNullException("match");for(var i=0;i<n.length;i++)if(!t(n[i]))return!1;return!0}};System.Array=k;Bridge.Class.generic("System.ArraySegment$1",function(n){var t=Bridge.Class.genericName("System.ArraySegment$1",n);return Bridge.Class.cache[t]||(Bridge.Class.cache[t]=Bridge.define(t,{constructor:function(n,t,i){this.array=n;this.offset=t||0;this.count=i||n.length},getArray:function(){return this.array},getCount:function(){return this.count},getOffset:function(){return this.offset}}))});Bridge.define("System.Collections.IEnumerable");Bridge.define("System.Collections.IEnumerator");Bridge.define("System.Collections.IEqualityComparer");Bridge.define("System.Collections.ICollection",{inherits:[System.Collections.IEnumerable]});Bridge.define("System.Collections.Generic.IEnumerator$1",function(){return{inherits:[System.Collections.IEnumerator]}});Bridge.define("System.Collections.Generic.IEnumerable$1",function(){return{inherits:[System.Collections.IEnumerable]}});Bridge.define("System.Collections.Generic.ICollection$1",function(n){return{inherits:[System.Collections.Generic.IEnumerable$1(n)]}});Bridge.define("System.Collections.Generic.IEqualityComparer$1",function(){return{}});Bridge.define("System.Collections.Generic.IDictionary$2",function(n,t){return{inherits:[System.Collections.Generic.IEnumerable$1(System.Collections.Generic.KeyValuePair$2(n,t))]}});Bridge.define("System.Collections.Generic.IList$1",function(n){return{inherits:[System.Collections.Generic.ICollection$1(n)]}});Bridge.define("System.Collections.Generic.IComparer$1",function(){return{}});Bridge.define("System.Collections.Generic.ISet$1",function(n){return{inherits:[System.Collections.Generic.ICollection$1(n)]}});Bridge.define("Bridge.CustomEnumerator",{inherits:[System.Collections.IEnumerator],constructor:function(n,t,i,r,u){this.$moveNext=n;this.$getCurrent=t;this.$dispose=r;this.$reset=i;this.scope=u},moveNext:function(){try{return this.$moveNext.call(this.scope)}catch(n){this.dispose.call(this.scope);throw n;}},getCurrent:function(){return this.$getCurrent.call(this.scope)},getCurrent$1:function(){return this.$getCurrent.call(this.scope)},reset:function(){this.$reset&&this.$reset.call(this.scope)},dispose:function(){this.$dispose&&this.$dispose.call(this.scope)}});Bridge.define("Bridge.ArrayEnumerator",{inherits:[System.Collections.IEnumerator],constructor:function(n){this.array=n;this.reset()},moveNext:function(){return this.index++,this.index<this.array.length},getCurrent:function(){return this.array[this.index]},getCurrent$1:function(){return this.array[this.index]},reset:function(){this.index=-1},dispose:Bridge.emptyFn});Bridge.define("Bridge.ArrayEnumerable",{inherits:[System.Collections.IEnumerable],constructor:function(n){this.array=n},getEnumerator:function(){return new Bridge.ArrayEnumerator(this.array)}});Bridge.define("System.Collections.Generic.EqualityComparer$1",function(n){return{inherits:[System.Collections.Generic.IEqualityComparer$1(n)],equals2:function(n,t){if(Bridge.isDefined(n,!0)){if(Bridge.isDefined(t,!0)){var i=n&&n.$$name;if(i){if(Bridge.isFunction(n.equalsT))return Bridge.equalsT(n,t);if(Bridge.isFunction(n.equals))return Bridge.equals(n,t)}else return Bridge.equals(n,t);return n===t}}else return!Bridge.isDefined(t,!0);return!1},getHashCode2:function(n){return Bridge.isDefined(n,!0)?Bridge.getHashCode(n):0}}});System.Collections.Generic.EqualityComparer$1.$default=new System.Collections.Generic.EqualityComparer$1(Object)();Bridge.define("System.Collections.Generic.Comparer$1",function(n){return{inherits:[System.Collections.Generic.IComparer$1(n)],constructor:function(n){this.fn=n;this.compare=n}}});System.Collections.Generic.Comparer$1.$default=new System.Collections.Generic.Comparer$1(Object)(function(n,t){if(Bridge.hasValue(n)){if(!Bridge.hasValue(t))return 1}else return Bridge.hasValue(t)?-1:0;return Bridge.compare(n,t)});Bridge.define("System.Collections.Generic.KeyValuePair$2",function(){return{constructor:function(n,t){this.key=n;this.value=t},toString:function(){var n="[";return this.key!=null&&(n+=this.key.toString()),n+=", ",this.value!=null&&(n+=this.value.toString()),n+"]"}}});Bridge.define("System.Collections.Generic.Dictionary$2",function(n,t){return{inherits:[System.Collections.Generic.IDictionary$2(n,t)],constructor:function(i,r){var f,e,o,s,u;if(this.comparer=r||System.Collections.Generic.EqualityComparer$1.$default,this.clear(),Bridge.is(i,System.Collections.Generic.Dictionary$2(n,t)))for(f=Bridge.getEnumerator(i);f.moveNext();)e=f.getCurrent(),this.add(e.key,e.value);else if(Object.prototype.toString.call(i)==="[object Object]")for(o=Bridge.getPropertyNames(i),u=0;u<o.length;u++)s=o[u],this.add(s,i[s])},getKeys:function(){return new System.Collections.Generic.DictionaryCollection$1(n)(this,!0)},getValues:function(){return new System.Collections.Generic.DictionaryCollection$1(t)(this,!1)},clear:function(){this.entries={};this.count=0},findEntry:function(n){var r=this.comparer.getHashCode2(n),i,t;if(Bridge.isDefined(this.entries[r]))for(i=this.entries[r],t=0;t<i.length;t++)if(this.comparer.equals2(i[t].key,n))return i[t]},containsKey:function(n){return!!this.findEntry(n)},containsValue:function(n){var i,t,r;for(i in this.entries)if(this.entries.hasOwnProperty(i))for(r=this.entries[i],t=0;t<r.length;t++)if(this.comparer.equals2(r[t].value,n))return!0;return!1},get:function(n){var t=this.findEntry(n);if(!t)throw new System.Collections.Generic.KeyNotFoundException("Key "+n+" does not exist.");return t.value},getItem:function(n){return this.get(n)},set:function(i,r,u){var f=this.findEntry(i),e;if(f){if(u)throw new System.ArgumentException("Key "+i+" already exists.");f.value=r;return}e=this.comparer.getHashCode2(i);f=new System.Collections.Generic.KeyValuePair$2(n,t)(i,r);this.entries[e]?this.entries[e].push(f):this.entries[e]=[f];this.count++},setItem:function(n,t,i){this.set(n,t,i)},add:function(n,t){this.set(n,t,!0)},remove:function(n){var r=this.comparer.getHashCode2(n),t,i;if(!this.entries[r])return!1;for(t=this.entries[r],i=0;i<t.length;i++)if(this.comparer.equals2(t[i].key,n))return t.splice(i,1),t.length==0&&delete this.entries[r],this.count--,!0;return!1},getCount:function(){return this.count},getComparer:function(){return this.comparer},tryGetValue:function(n,i){var r=this.findEntry(n);return i.v=r?r.value:Bridge.getDefaultValue(t),!!r},getCustomEnumerator:function(n){var r=Bridge.getPropertyNames(this.entries),t=-1,i;return new Bridge.CustomEnumerator(function(){return((t<0||i>=this.entries[r[t]].length-1)&&(i=-1,t++),t>=r.length)?!1:(i++,!0)},function(){return n(this.entries[r[t]][i])},function(){t=-1},null,this)},getEnumerator:function(){return this.getCustomEnumerator(function(n){return n})}}});Bridge.define("System.Collections.Generic.DictionaryCollection$1",function(n){return{inherits:[System.Collections.Generic.ICollection$1(n)],constructor:function(n,t){this.dictionary=n;this.keys=t},getCount:function(){return this.dictionary.getCount()},getEnumerator:function(){return this.dictionary.getCustomEnumerator(this.keys?function(n){return n.key}:function(n){return n.value})},contains:function(n){return this.keys?this.dictionary.containsKey(n):this.dictionary.containsValue(n)},add:function(){throw new System.NotSupportedException;},clear:function(){throw new System.NotSupportedException;},remove:function(){throw new System.NotSupportedException;}}});Bridge.define("System.Collections.Generic.List$1",function(n){return{inherits:[System.Collections.Generic.ICollection$1(n),System.Collections.ICollection,System.Collections.Generic.IList$1(n)],constructor:function(n){this.items=Object.prototype.toString.call(n)==="[object Array]"?System.Array.clone(n):Bridge.is(n,System.Collections.IEnumerable)?Bridge.toArray(n):[]},checkIndex:function(n){if(n<0||n>this.items.length-1)throw new System.ArgumentOutOfRangeException("Index out of range");},getCount:function(){return this.items.length},get:function(n){return this.checkIndex(n),this.items[n]},getItem:function(n){return this.get(n)},set:function(n,t){this.checkReadOnly();this.checkIndex(n);this.items[n]=t},setItem:function(n,t){this.set(n,t)},add:function(n){this.checkReadOnly();this.items.push(n)},addRange:function(n){this.checkReadOnly();for(var i=Bridge.toArray(n),t=0,r=i.length;t<r;++t)this.items.push(i[t])},clear:function(){this.checkReadOnly();this.items=[]},indexOf:function(n,t){var i,r;for(Bridge.isDefined(t)||(t=0),t!==0&&this.checkIndex(t),i=t;i<this.items.length;i++)if(r=this.items[i],r===n||System.Collections.Generic.EqualityComparer$1.$default.equals2(r,n))return i;return-1},insertRange:function(n,t){var r,i;for(this.checkReadOnly(),n!==this.items.length&&this.checkIndex(n),r=Bridge.toArray(t),i=0;i<r.length;i++)this.insert(n++,r[i])},contains:function(n){return this.indexOf(n)>-1},getEnumerator:function(){return new Bridge.ArrayEnumerator(this.items)},getRange:function(t,i){Bridge.isDefined(t)||(t=0);Bridge.isDefined(i)||(i=this.items.length);t!==0&&this.checkIndex(t);this.checkIndex(t+i-1);for(var u=[],f=t+i,r=t;r<f;r++)u.push(this.items[r]);return new System.Collections.Generic.List$1(n)(u)},insert:function(n,t){if(this.checkReadOnly(),n!==this.items.length&&this.checkIndex(n),Bridge.isArray(t))for(var i=0;i<t.length;i++)this.insert(n++,t[i]);else this.items.splice(n,0,t)},join:function(n){return this.items.join(n)},lastIndexOf:function(n,t){Bridge.isDefined(t)||(t=this.items.length-1);t!==0&&this.checkIndex(t);for(var i=t;i>=0;i--)if(n===this.items[i])return i;return-1},remove:function(n){this.checkReadOnly();var t=this.indexOf(n);return t<0?!1:(this.checkIndex(t),this.items.splice(t,1),!0)},removeAt:function(n){this.checkReadOnly();this.checkIndex(n);this.items.splice(n,1)},removeRange:function(n,t){this.checkReadOnly();this.checkIndex(n);this.items.splice(n,t)},reverse:function(){this.checkReadOnly();this.items.reverse()},slice:function(n,t){return this.checkReadOnly(),new System.Collections.Generic.List$1(this.$$name.substr(this.$$name.lastIndexOf("$")+1))(this.items.slice(n,t))},sort:function(n){this.checkReadOnly();this.items.sort(n||System.Collections.Generic.Comparer$1.$default.compare)},splice:function(n,t,i){this.checkReadOnly();this.items.splice(n,t,i)},unshift:function(){this.checkReadOnly();this.items.unshift()},toArray:function(){return Bridge.toArray(this)},checkReadOnly:function(){if(this.readOnly)throw new System.NotSupportedException;},binarySearch:function(n,t,i,r){return arguments.length===1&&(i=n,n=null),arguments.length===2&&(i=n,r=t,n=null,t=null),Bridge.isNumber(n)||(n=0),Bridge.isNumber(t)||(t=this.items.length),r||(r=System.Collections.Generic.Comparer$1.$default),System.Array.binarySearch(this.items,n,t,i,r)},convertAll:function(n,t){var r,i;if(!Bridge.hasValue(t))throw new System.ArgumentNullException("converter is null.");for(r=new System.Collections.Generic.List$1(n)(this.items.length),i=0;i<this.items.length;i++)r.items[i]=t(this.items[i]);return r}}});Bridge.define("System.Collections.ObjectModel.ReadOnlyCollection$1",function(n){return{inherits:[System.Collections.Generic.List$1(n)],constructor:function(t){if(t==null)throw new System.ArgumentNullException("list");System.Collections.Generic.List$1(n).prototype.$constructor.call(this,t);this.readOnly=!0}}});Bridge.define("System.Threading.Tasks.Task",{inherits:[System.IDisposable],constructor:function(n,t){this.action=n;this.state=t;this.exception=null;this.status=System.Threading.Tasks.TaskStatus.created;this.callbacks=[];this.result=null},statics:{delay:function(n,t){var i=new System.Threading.Tasks.TaskCompletionSource;return setTimeout(function(){i.setResult(t)},n),i.task},fromResult:function(n){var t=new System.Threading.Tasks.Task;return t.status=System.Threading.Tasks.TaskStatus.ranToCompletion,t.result=n,t},run:function(n){var t=new System.Threading.Tasks.TaskCompletionSource;return setTimeout(function(){try{t.setResult(n())}catch(i){t.setException(System.Exception.create(i))}},0),t.task},whenAll:function(n){var t=new System.Threading.Tasks.TaskCompletionSource,r,f,e=!1,u=[],i;if(Bridge.is(n,System.Collections.IEnumerable)?n=Bridge.toArray(n):Bridge.isArray(n)||(n=Array.prototype.slice.call(arguments,0)),n.length===0)return t.setResult([]),t.task;for(f=n.length,r=new Array(n.length),i=0;i<n.length;i++)(function(i){n[i].continueWith(function(n){switch(n.status){case System.Threading.Tasks.TaskStatus.ranToCompletion:r[i]=n.getResult();break;case System.Threading.Tasks.TaskStatus.canceled:e=!0;break;case System.Threading.Tasks.TaskStatus.faulted:System.Array.addRange(u,n.exception.innerExceptions);break;default:throw new System.InvalidOperationException("Invalid task status: "+n.status);}--f==0&&(u.length>0?t.setException(u):e?t.setCanceled():t.setResult(r))})})(i);return t.task},whenAny:function(n){if(Bridge.is(n,System.Collections.IEnumerable)?n=Bridge.toArray(n):Bridge.isArray(n)||(n=Array.prototype.slice.call(arguments,0)),!n.length)throw new System.ArgumentException("At least one task is required");for(var t=new System.Threading.Tasks.TaskCompletionSource,i=0;i<n.length;i++)n[i].continueWith(function(n){switch(n.status){case System.Threading.Tasks.TaskStatus.ranToCompletion:t.trySetResult(n);break;case System.Threading.Tasks.TaskStatus.canceled:t.trySetCanceled();break;case System.Threading.Tasks.TaskStatus.faulted:t.trySetException(n.exception.innerExceptions);break;default:throw new System.InvalidOperationException("Invalid task status: "+n.status);}});return t.task},fromCallback:function(n,t){var i=new System.Threading.Tasks.TaskCompletionSource,r=Array.prototype.slice.call(arguments,2),u;return u=function(n){i.setResult(n)},r.push(u),n[t].apply(n,r),i.task},fromCallbackResult:function(n,t,i){var r=new System.Threading.Tasks.TaskCompletionSource,u=Array.prototype.slice.call(arguments,3),f;return f=function(n){r.setResult(n)},i(u,f),n[t].apply(n,u),r.task},fromCallbackOptions:function(n,t,i){var u=new System.Threading.Tasks.TaskCompletionSource,r=Array.prototype.slice.call(arguments,3),f;return f=function(n){u.setResult(n)},r[0]=r[0]||{},r[0][i]=f,n[t].apply(n,r),u.task},fromPromise:function(n,t,i,r){var u=new System.Threading.Tasks.TaskCompletionSource;return n.then||(n=n.promise()),typeof t=="number"?t=function(n){return function(){return arguments[n>=0?n:arguments.length+n]}}(t):typeof t!="function"&&(t=function(){return Array.prototype.slice.call(arguments,0)}),n.then(function(){u.setResult(t?t.apply(null,arguments):Array.prototype.slice.call(arguments,0))},function(){u.setException(i?i.apply(null,arguments):new Bridge.PromiseException(Array.prototype.slice.call(arguments,0)))},r),u.task}},continueWith:function(n,t){var i=new System.Threading.Tasks.TaskCompletionSource,r=this,u=t?function(){i.setResult(n(r))}:function(){try{i.setResult(n(r))}catch(t){i.setException(System.Exception.create(t))}};return this.isCompleted()?setTimeout(u,0):this.callbacks.push(u),i.task},start:function(){if(this.status!==System.Threading.Tasks.TaskStatus.created)throw new System.InvalidOperationException("Task was already started.");var n=this;this.status=System.Threading.Tasks.TaskStatus.running;setTimeout(function(){try{var t=n.action(n.state);delete n.action;delete n.state;n.complete(t)}catch(i){n.fail(new System.AggregateException(null,[System.Exception.create(i)]))}},0)},runCallbacks:function(){var n=this;setTimeout(function(){for(var t=0;t<n.callbacks.length;t++)n.callbacks[t](n);delete n.callbacks},0)},complete:function(n){return this.isCompleted()?!1:(this.result=n,this.status=System.Threading.Tasks.TaskStatus.ranToCompletion,this.runCallbacks(),!0)},fail:function(n){return this.isCompleted()?!1:(this.exception=n,this.status=System.Threading.Tasks.TaskStatus.faulted,this.runCallbacks(),!0)},cancel:function(){return this.isCompleted()?!1:(this.status=System.Threading.Tasks.TaskStatus.canceled,this.runCallbacks(),!0)},isCanceled:function(){return this.status===System.Threading.Tasks.TaskStatus.canceled},isCompleted:function(){return this.status===System.Threading.Tasks.TaskStatus.ranToCompletion||this.status===System.Threading.Tasks.TaskStatus.canceled||this.status===System.Threading.Tasks.TaskStatus.faulted},isFaulted:function(){return this.status===System.Threading.Tasks.TaskStatus.faulted},_getResult:function(n){switch(this.status){case System.Threading.Tasks.TaskStatus.ranToCompletion:return this.result;case System.Threading.Tasks.TaskStatus.canceled:var t=new System.Threading.Tasks.TaskCanceledException(null,this);throw n?t:new System.AggregateException(null,[t]);case System.Threading.Tasks.TaskStatus.faulted:throw n?this.exception.innerExceptions.getCount()>0?this.exception.innerExceptions.get(0):null:this.exception;default:throw new System.InvalidOperationException("Task is not yet completed.");}},getResult:function(){return this._getResult(!1)},dispose:function(){},getAwaiter:function(){return this},getAwaitedResult:function(){return this._getResult(!0)}});Bridge.define("System.Threading.Tasks.TaskStatus",{$enum:!0,$statics:{created:0,waitingForActivation:1,waitingToRun:2,running:3,waitingForChildrenToComplete:4,ranToCompletion:5,canceled:6,faulted:7}});Bridge.define("System.Threading.Tasks.TaskCompletionSource",{constructor:function(){this.task=new System.Threading.Tasks.Task;this.task.status=System.Threading.Tasks.TaskStatus.running},setCanceled:function(){if(!this.task.cancel())throw new System.InvalidOperationException("Task was already completed.");},setResult:function(n){if(!this.task.complete(n))throw new System.InvalidOperationException("Task was already completed.");},setException:function(n){if(!this.trySetException(n))throw new System.InvalidOperationException("Task was already completed.");},trySetCanceled:function(){return this.task.cancel()},trySetResult:function(n){return this.task.complete(n)},trySetException:function(n){return Bridge.is(n,System.Exception)&&(n=[n]),this.task.fail(new System.AggregateException(null,n))}});Bridge.define("System.Threading.CancellationToken",{$struct:!0,constructor:function(n){Bridge.is(n,System.Threading.CancellationTokenSource)||(n=n?System.Threading.CancellationToken.sourceTrue:System.Threading.CancellationToken.sourceFalse);this.source=n},getCanBeCanceled:function(){return!this.source.uncancellable},getIsCancellationRequested:function(){return this.source.isCancellationRequested},throwIfCancellationRequested:function(){if(this.source.isCancellationRequested)throw new System.OperationCanceledException(this);},register:function(n,t){return this.source.register(n,t)},getHashCode:function(){return Bridge.getHashCode(this.source)},equals:function(n){return n.source===this.source},equalsT:function(n){return n.source===this.source},statics:{sourceTrue:{isCancellationRequested:!0,register:function(n,t){return n(t),new System.Threading.CancellationTokenRegistration}},sourceFalse:{uncancellable:!0,isCancellationRequested:!1,register:function(){return new System.Threading.CancellationTokenRegistration}},getDefaultValue:function(){return new System.Threading.CancellationToken}}});System.Threading.CancellationToken.none=new System.Threading.CancellationToken;Bridge.define("System.Threading.CancellationTokenRegistration",{inherits:function(){return[System.IDisposable,System.IEquatable$1(System.Threading.CancellationTokenRegistration)]},constructor:function(n,t){this.cts=n;this.o=t},dispose:function(){this.cts&&(this.cts.deregister(this.o),this.cts=this.o=null)},equalsT:function(n){return this===n},equals:function(n){return this===n},statics:{getDefaultValue:function(){return new System.Threading.CancellationTokenRegistration}}});Bridge.define("System.Threading.CancellationTokenSource",{inherits:[System.IDisposable],constructor:function(n){this.timeout=typeof n=="number"&&n>=0?setTimeout(Bridge.fn.bind(this,this.cancel),n,-1):null;this.isCancellationRequested=!1;this.token=new System.Threading.CancellationToken(this);this.handlers=[]},cancel:function(n){var i,r,t;if(!this.isCancellationRequested){for(this.isCancellationRequested=!0,i=[],r=this.handlers,this.clean(),t=0;t<r.length;t++)try{r[t].f(r[t].s)}catch(u){if(n&&n!==-1)throw u;i.push(u)}if(i.length>0&&n!==-1)throw new System.AggregateException(null,i);}},cancelAfter:function(n){this.isCancellationRequested||(this.timeout&&clearTimeout(this.timeout),this.timeout=setTimeout(Bridge.fn.bind(this,this.cancel),n,-1))},register:function(n,t){if(this.isCancellationRequested)return n(t),new System.Threading.CancellationTokenRegistration;var i={f:n,s:t};return this.handlers.push(i),new System.Threading.CancellationTokenRegistration(this,i)},deregister:function(n){var t=this.handlers.indexOf(n);t>=0&&this.handlers.splice(t,1)},dispose:function(){this.clean()},clean:function(){if(this.timeout&&clearTimeout(this.timeout),this.timeout=null,this.handlers=[],this.links){for(var n=0;n<this.links.length;n++)this.links[n].dispose();this.links=null}},statics:{createLinked:function(){var n=new System.Threading.CancellationTokenSource,i,t;for(n.links=[],i=Bridge.fn.bind(n,n.cancel),t=0;t<arguments.length;t++)n.links.push(arguments[t].register(i));return n}}});wt={isNull:function(n){return!Bridge.isDefined(n,!0)},isEmpty:function(n){return n==null||n.length===0||Bridge.is(n,System.Collections.ICollection)?n.getCount()===0:!1},isNotEmptyOrWhitespace:function(n){return Bridge.isDefined(n,!0)&&!/^$|\s+/.test(n)},isNotNull:function(n){return Bridge.isDefined(n,!0)},isNotEmpty:function(n){return!Bridge.Validation.isEmpty(n)},email:function(n){return/^(")?(?:[^\."])(?:(?:[\.])?(?:[\w\-!#$%&'*+/=?^_`{|}~]))*\1@(\w[\-\w]*\.){1,5}([A-Za-z]){2,6}$/.test(n)},url:function(n){return/(?:(?:https?|ftp):\/\/)(?:\S+(?::\S*)?@)?(?:(?!(?:\.\d{1,3}){3})(?!(?:\.\d{1,3}){2})(?!\.(?:1[6-9]|2\d|3[0-1])(?:\.\d{1,3}){2})(?:[1-9]\d?|1\d\d|2[01]\d|22[0-3])(?:\.(?:1?\d{1,2}|2[0-4]\d|25[0-5])){2}(?:\.(?:[1-9]\d?|1\d\d|2[0-4]\d|25[0-4]))|(?:(?:[a-z\u00a1-\uffff0-9]-*)*[a-z\u00a1-\uffff0-9]+)(?:\.(?:[a-z\u00a1-\uffff0-9]-*)*[a-z\u00a1-\uffff0-9]+)*(?:\.(?:[a-z\u00a1-\uffff]{2,}))\.?)(?::\d{2,5})?(?:[/?#]\S*)?$/.test(n)},alpha:function(n){return/^[a-zA-Z_]+$/.test(n)},alphaNum:function(n){return/^[a-zA-Z_]+$/.test(n)},creditCard:function(n,t){var r,u,i,f,e=!1;if(t==="Visa")r=/^4\d{3}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}$/;else if(t==="MasterCard")r=/^5[1-5]\d{2}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}$/;else if(t==="Discover")r=/^6011[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}$/;else if(t==="AmericanExpress")r=/^3[4,7]\d{13}$/;else if(t==="DinersClub")r=/^(3[0,6,8]\d{12})|(5[45]\d{14})$/;else{if(!n||n.length<13||n.length>19)return!1;r=/[^0-9 \-]+/;e=!0}if(!r.test(n))return!1;for(n=n.split(e?"-":/[- ]/).join(""),u=0,i=2-n.length%2;i<=n.length;i+=2)u+=parseInt(n.charAt(i-1));for(i=n.length%2+1;i<n.length;i+=2)f=parseInt(n.charAt(i-1))*2,u+=f<10?f:f-9;return u%10==0}};Bridge.Validation=wt;Bridge.define("System.Version",{inherits:function(){return[System.ICloneable,System.IComparable$1(System.Version),System.IEquatable$1(System.Version)]},statics:{separatorsArray:".",config:{init:function(){this.ZERO_CHAR_VALUE=Bridge.cast(48,Bridge.Int)}},appendPositiveNumber:function(n,t){var r=t.getLength(),i;do i=n%10,n=Bridge.Int.div(n,10),t.insert(r,String.fromCharCode(Bridge.cast(System.Version.ZERO_CHAR_VALUE+i,Bridge.Int)));while(n>0)},parse:function(n){if(n===null)throw new System.ArgumentNullException("input");var t={v:new System.Version.VersionResult};if(t.v.init("input",!0),!System.Version.tryParseVersion(n,t))throw t.v.getVersionParseException();return t.v.m_parsedVersion},tryParse:function(n,t){var i={v:new System.Version.VersionResult},r;return i.v.init("input",!1),r=System.Version.tryParseVersion(n,i),t.v=i.v.m_parsedVersion,r},tryParseVersion:function(n,t){var u={},f={},e={},o={},r,i;if(n===null)return t.v.setFailure(System.Version.ParseFailureKind.argumentNullException),!1;if(r=n.split(System.Version.separatorsArray),i=r.length,i<2||i>4)return t.v.setFailure(System.Version.ParseFailureKind.argumentException),!1;if(!System.Version.tryParseComponent(r[0],"version",t,u)||!System.Version.tryParseComponent(r[1],"version",t,f))return!1;if(i-=2,i>0){if(!System.Version.tryParseComponent(r[2],"build",t,e))return!1;if(i--,i>0)if(System.Version.tryParseComponent(r[3],"revision",t,o))t.v.m_parsedVersion=new System.Version("constructor$3",u.v,f.v,e.v,o.v);else return!1;else t.v.m_parsedVersion=new System.Version("constructor$2",u.v,f.v,e.v)}else t.v.m_parsedVersion=new System.Version("constructor$1",u.v,f.v);return!0},tryParseComponent:function(n,t,i,r){return Bridge.Int.tryParseInt(n,r,-2147483648,2147483647)?r.v<0?(i.v.setFailure$1(System.Version.ParseFailureKind.argumentOutOfRangeException,t),!1):!0:(i.v.setFailure$1(System.Version.ParseFailureKind.formatException,n),!1)},op_Equality:function(n,t){return n===null?t===null:n.equals(t)},op_Inequality:function(n,t){return!System.Version.op_Equality(n,t)},op_LessThan:function(n,t){return n===null&&t===null?!1:t===null?n.compareTo(t)<0:t.compareTo(n)>0},op_LessThanOrEqual:function(n,t){return n===null&&t===null?!1:t===null?n.compareTo(t)<=0:t.compareTo(n)>=0},op_GreaterThan:function(n,t){return System.Version.op_LessThan(t,n)},op_GreaterThanOrEqual:function(n,t){return System.Version.op_LessThanOrEqual(t,n)}},_Major:0,_Minor:0,config:{init:function(){this._Build=-1;this._Revision=-1}},constructor$3:function(n,t,i,r){if(n<0)throw new System.ArgumentOutOfRangeException("major","Cannot be < 0");if(t<0)throw new System.ArgumentOutOfRangeException("minor","Cannot be < 0");if(i<0)throw new System.ArgumentOutOfRangeException("build","Cannot be < 0");if(r<0)throw new System.ArgumentOutOfRangeException("revision","Cannot be < 0");this._Major=n;this._Minor=t;this._Build=i;this._Revision=r},constructor$2:function(n,t,i){if(n<0)throw new System.ArgumentOutOfRangeException("major","Cannot be < 0");if(t<0)throw new System.ArgumentOutOfRangeException("minor","Cannot be < 0");if(i<0)throw new System.ArgumentOutOfRangeException("build","Cannot be < 0");this._Major=n;this._Minor=t;this._Build=i},constructor$1:function(n,t){if(n<0)throw new System.ArgumentOutOfRangeException("major","Cannot be < 0");if(t<0)throw new System.ArgumentOutOfRangeException("minor","Cannot be < 0");this._Major=n;this._Minor=t},constructor$4:function(n){var t=System.Version.parse(n);this._Major=t.getMajor();this._Minor=t.getMinor();this._Build=t.getBuild();this._Revision=t.getRevision()},constructor:function(){this._Major=0;this._Minor=0},getMajor:function(){return this._Major},getMinor:function(){return this._Minor},getBuild:function(){return this._Build},getRevision:function(){return this._Revision},getMajorRevision:function(){return this._Revision>>16},getMinorRevision:function(){var n=this._Revision&65535;return n>32767&&(n=-(n&32767^32767)-1),n},clone:function(){var n=new System.Version("constructor");return n._Major=this._Major,n._Minor=this._Minor,n._Build=this._Build,n._Revision=this._Revision,n},compareInternal:function(n){return this._Major!==n._Major?this._Major>n._Major?1:-1:this._Minor!==n._Minor?this._Minor>n._Minor?1:-1:this._Build!==n._Build?this._Build>n._Build?1:-1:this._Revision!==n._Revision?this._Revision>n._Revision?1:-1:0},compareTo$1:function(n){if(n===null)return 1;var t=Bridge.as(n,System.Version);if(t===null)throw new System.ArgumentException("version should be of System.Version type");return this.compareInternal(t)},compareTo:function(n){return n===null?1:this.compareInternal(n)},equals$1:function(n){var t=Bridge.as(n,System.Version);return t===null?!1:this._Major!==t._Major||this._Minor!==t._Minor||this._Build!==t._Build||this._Revision!==t._Revision?!1:!0},equals:function(n){return this.equals$1(n)},equalsT:function(n){return this.equals$1(n)},getHashCode:function(){var n=0;return n|=(this._Major&15)<<28,n|=(this._Minor&255)<<20,n|=(this._Build&255)<<12,n|this._Revision&4095},toString:function(){return this._Build===-1?this.toString$1(2):this._Revision===-1?this.toString$1(3):this.toString$1(4)},toString$1:function(n){var t;switch(n){case 0:return"";case 1:return this._Major.toString();case 2:return t=new System.Text.StringBuilder,System.Version.appendPositiveNumber(this._Major,t),t.append(String.fromCharCode(46)),System.Version.appendPositiveNumber(this._Minor,t),t.toString();default:if(this._Build===-1)throw new System.ArgumentException("Build should be > 0 if fieldCount > 2","fieldCount");if(n===3)return t=new System.Text.StringBuilder,System.Version.appendPositiveNumber(this._Major,t),t.append(String.fromCharCode(46)),System.Version.appendPositiveNumber(this._Minor,t),t.append(String.fromCharCode(46)),System.Version.appendPositiveNumber(this._Build,t),t.toString();if(this._Revision===-1)throw new System.ArgumentException("Revision should be > 0 if fieldCount > 3","fieldCount");if(n===4)return t=new System.Text.StringBuilder,System.Version.appendPositiveNumber(this._Major,t),t.append(String.fromCharCode(46)),System.Version.appendPositiveNumber(this._Minor,t),t.append(String.fromCharCode(46)),System.Version.appendPositiveNumber(this._Build,t),t.append(String.fromCharCode(46)),System.Version.appendPositiveNumber(this._Revision,t),t.toString();throw new System.ArgumentException("Should be < 5","fieldCount");}}});Bridge.define("System.Version.ParseFailureKind",{statics:{argumentNullException:0,argumentException:1,argumentOutOfRangeException:2,formatException:3}});Bridge.define("System.Version.VersionResult",{m_parsedVersion:null,m_failure:0,m_exceptionArgument:null,m_argumentName:null,m_canThrow:!1,constructor:function(){},init:function(n,t){this.m_canThrow=t;this.m_argumentName=n},setFailure:function(n){this.setFailure$1(n,"")},setFailure$1:function(n,t){if(this.m_failure=n,this.m_exceptionArgument=t,this.m_canThrow)throw this.getVersionParseException();},getVersionParseException:function(){switch(this.m_failure){case System.Version.ParseFailureKind.argumentNullException:return new System.ArgumentNullException(this.m_argumentName);case System.Version.ParseFailureKind.argumentException:return new System.ArgumentException("VersionString");case System.Version.ParseFailureKind.argumentOutOfRangeException:return new System.ArgumentOutOfRangeException(this.m_exceptionArgument,"Cannot be < 0");case System.Version.ParseFailureKind.formatException:try{Bridge.Int.parseInt(this.m_exceptionArgument,-2147483648,2147483647)}catch(n){n=System.Exception.create(n);var t;if(Bridge.is(n,System.FormatException)||Bridge.is(n,System.OverflowException))return t=n;throw n;}return new System.FormatException("InvalidString");default:return new System.ArgumentException("VersionString")}},getHashCode:function(){var n=17;return n=n*23+(this.m_parsedVersion==null?0:Bridge.getHashCode(this.m_parsedVersion)),n=n*23+(this.m_failure==null?0:Bridge.getHashCode(this.m_failure)),n=n*23+(this.m_exceptionArgument==null?0:Bridge.getHashCode(this.m_exceptionArgument)),n=n*23+(this.m_argumentName==null?0:Bridge.getHashCode(this.m_argumentName)),n*23+(this.m_canThrow==null?0:Bridge.getHashCode(this.m_canThrow))},equals:function(n){return Bridge.is(n,System.Version.VersionResult)?Bridge.equals(this.m_parsedVersion,n.m_parsedVersion)&&Bridge.equals(this.m_failure,n.m_failure)&&Bridge.equals(this.m_exceptionArgument,n.m_exceptionArgument)&&Bridge.equals(this.m_argumentName,n.m_argumentName)&&Bridge.equals(this.m_canThrow,n.m_canThrow):!1},$clone:function(n){var t=n||new System.Version.VersionResult;return t.m_parsedVersion=this.m_parsedVersion,t.m_failure=this.m_failure,t.m_exceptionArgument=this.m_exceptionArgument,t.m_argumentName=this.m_argumentName,t.m_canThrow=this.m_canThrow,t}});Bridge.define("System.Attribute");Bridge.define("System.ComponentModel.INotifyPropertyChanged");Bridge.define("System.ComponentModel.PropertyChangedEventArgs",{constructor:function(n){this.propertyName=n}});t={};t.convert={typeCodes:{Empty:0,Object:1,DBNull:2,Boolean:3,Char:4,SByte:5,Byte:6,Int16:7,UInt16:8,Int32:9,UInt32:10,Int64:11,UInt64:12,Single:13,Double:14,Decimal:15,DateTime:16,String:18},toBoolean:function(n,i){var r,u;switch(typeof n){case"boolean":return n;case"number":return n!==0;case"string":if(r=n.toLowerCase().trim(),r==="true")return!0;if(r==="false")return!1;throw new System.FormatException("String was not recognized as a valid Boolean.");case"object":if(n==null)return!1;if(n instanceof System.Decimal)return!n.isZero();if(System.Int64.is64Bit(n))return n.ne(0)}return u=t.internal.suggestTypeCode(n),t.internal.throwInvalidCastEx(u,t.convert.typeCodes.Boolean),t.convert.convertToType(t.convert.typeCodes.Boolean,n,i||null)},toChar:function(n,i,r){var u=t.convert.typeCodes,f,e;if(n instanceof System.Decimal&&(n=n.toFloat()),(n instanceof System.Int64||n instanceof System.UInt64)&&(n=n.toNumber()),f=typeof n,r=r||t.internal.suggestTypeCode(n),r===u.String&&n==null&&(f="string"),r!==u.Object)switch(f){case"boolean":t.internal.throwInvalidCastEx(u.Boolean,u.Char);case"number":return e=t.internal.isFloatingType(r),(e||n%1!=0)&&t.internal.throwInvalidCastEx(r,u.Char),t.internal.validateNumberRange(n,u.Char,!0),n;case"string":if(n==null)throw new System.ArgumentNullException("value");if(n.length!==1)throw new System.FormatException("String must be exactly one character long.");return n.charCodeAt(0)}if(r===u.Object||f==="object"){if(n==null)return 0;Bridge.isDate(n)&&t.internal.throwInvalidCastEx(u.DateTime,u.Char)}return t.internal.throwInvalidCastEx(r,t.convert.typeCodes.Char),t.convert.convertToType(u.Char,n,i||null)},toSByte:function(n,i,r){return t.internal.toNumber(n,i||null,t.convert.typeCodes.SByte,r||null)},toByte:function(n,i){return t.internal.toNumber(n,i||null,t.convert.typeCodes.Byte)},toInt16:function(n,i){return t.internal.toNumber(n,i||null,t.convert.typeCodes.Int16)},toUInt16:function(n,i){return t.internal.toNumber(n,i||null,t.convert.typeCodes.UInt16)},toInt32:function(n,i){return t.internal.toNumber(n,i||null,t.convert.typeCodes.Int32)},toUInt32:function(n,i){return t.internal.toNumber(n,i||null,t.convert.typeCodes.UInt32)},toInt64:function(n,i){var r=t.internal.toNumber(n,i||null,t.convert.typeCodes.Int64);return new System.Int64(r)},toUInt64:function(n,i){var r=t.internal.toNumber(n,i||null,t.convert.typeCodes.UInt64);return new System.UInt64(r)},toSingle:function(n,i){return t.internal.toNumber(n,i||null,t.convert.typeCodes.Single)},toDouble:function(n,i){return t.internal.toNumber(n,i||null,t.convert.typeCodes.Double)},toDecimal:function(n,i){return n instanceof System.Decimal?n:new System.Decimal(t.internal.toNumber(n,i||null,t.convert.typeCodes.Decimal))},toDateTime:function(n,i){var r=t.convert.typeCodes,u,f;switch(typeof n){case"boolean":t.internal.throwInvalidCastEx(r.Boolean,r.DateTime);case"number":u=t.internal.suggestTypeCode(n);t.internal.throwInvalidCastEx(u,r.DateTime);case"string":return Bridge.Date.parse(n,i||null);case"object":if(n==null)return t.internal.getMinValue(r.DateTime);if(Bridge.isDate(n))return n;n instanceof System.Decimal&&t.internal.throwInvalidCastEx(r.Decimal,r.DateTime);n instanceof System.Int64&&t.internal.throwInvalidCastEx(r.Int64,r.DateTime);n instanceof System.UInt64&&t.internal.throwInvalidCastEx(r.UInt64,r.DateTime)}return f=t.internal.suggestTypeCode(n),t.internal.throwInvalidCastEx(f,t.convert.typeCodes.DateTime),t.convert.convertToType(r.DateTime,n,i||null)},toString:function(n,i,r){var u=t.convert.typeCodes,f=typeof n;switch(f){case"boolean":return n?"True":"False";case"number":return(r||null)===u.Char?String.fromCharCode(n):isNaN(n)?"NaN":(n%1!=0&&(n=parseFloat(n.toPrecision(15))),n.toString());case"string":return n;case"object":return n==null?"":Bridge.isDate(n)?Bridge.Date.format(n,null,i||null):n instanceof System.Decimal?n.isInteger()?n.toFixed(0,4):n.toPrecision(n.precision()):System.Int64.is64Bit(n)?n.toString():n.format?n.format(null,i||null):Bridge.getTypeName(n)}return t.convert.convertToType(t.convert.typeCodes.String,n,i||null)},toNumberInBase:function(n,i,r){var h,o,v,c,b,y,p,u,k,s,e,d;if(i!==2&&i!==8&&i!==10&&i!==16)throw new System.ArgumentException("Invalid Base.");if(h=t.convert.typeCodes,n==null)return r===h.Int64?System.Int64.Zero:r===h.UInt64?System.UInt64.Zero:0;if(n.length===0)throw new System.ArgumentOutOfRangeException("Index was out of range. Must be non-negative and less than the size of the collection.");n=n.toLowerCase();var l=t.internal.getMinValue(r),a=t.internal.getMaxValue(r),w=!1,f=0;if(n[f]==="-"){if(i!==10)throw new System.ArgumentException("String cannot contain a minus sign if the base is not 10.");if(l>=0)throw new System.OverflowException("The string was being parsed as an unsigned number and could not have a negative sign.");w=!0;++f}else n[f]==="+"&&++f;if(i===16&&n.length>=2&&n[f]==="0"&&n[f+1]==="x"&&(f+=2),i===2)o=t.internal.charsToCodes("01");else if(i===8)o=t.internal.charsToCodes("01234567");else if(i===10)o=t.internal.charsToCodes("0123456789");else if(i===16)o=t.internal.charsToCodes("0123456789abcdef");else throw new System.ArgumentException("Invalid Base.");for(v={},c=0;c<o.length;c++)b=o[c],v[b]=c;if(y=o[0],p=o[o.length-1],r===h.Int64||r===h.UInt64){for(e=f;e<n.length;e++)if(s=n[e].charCodeAt(0),!(s>=y&&s<=p))if(e===f)throw new System.FormatException("Could not find any recognizable digits.");else throw new System.FormatException("Additional non-parsable characters are at the end of the string.");if(d=r===h.Int64,u=d?new System.Int64(Bridge.$Long.fromString(n,!1,i)):new System.UInt64(Bridge.$Long.fromString(n,!0,i)),u.toString(i)!==n)throw new System.OverflowException("Value was either too large or too small.");return u}for(u=0,k=a-l+1,e=f;e<n.length;e++)if(s=n[e].charCodeAt(0),s>=y&&s<=p){if(u*=i,u+=v[s],u>t.internal.typeRanges.Int64_MaxValue)throw new System.OverflowException("Value was either too large or too small.");}else if(e===f)throw new System.FormatException("Could not find any recognizable digits.");else throw new System.FormatException("Additional non-parsable characters are at the end of the string.");if(w&&(u*=-1),u>a&&i!==10&&l<0&&(u=u-k),u<l||u>a)throw new System.OverflowException("Value was either too large or too small.");return u},toStringInBase:function(n,i,r){var w=t.convert.typeCodes,v,e,h,y,p,o,u,f,c;if(i!==2&&i!==8&&i!==10&&i!==16)throw new System.ArgumentException("Invalid Base.");var l=t.internal.getMinValue(r),a=t.internal.getMaxValue(r),s=System.Int64.is64Bit(n);if(s){if(n.lt(l)||n.gt(a))throw new System.OverflowException("Value was either too large or too small for an unsigned byte.");}else if(n<l||n>a)throw new System.OverflowException("Value was either too large or too small for an unsigned byte.");if(v=!1,s)return i===10?n.toString():n.value.toUnsigned().toString(i);if(n<0&&(i===10?(v=!0,n*=-1):n=a+1-l+n),i===2)e="01";else if(i===8)e="01234567";else if(i===10)e="0123456789";else if(i===16)e="0123456789abcdef";else throw new System.ArgumentException("Invalid Base.");for(h={},y=e.split(""),o=0;o<y.length;o++)p=y[o],h[o]=p;if(u="",n===0||s&&n.eq(0))u="0";else if(s)while(n.gt(0))f=n.mod(i),n=n.sub(f).div(i),c=h[f.toNumber()],u+=c;else while(n>0)f=n%i,n=(n-f)/i,c=h[f],u+=c;return v&&(u+="-"),u.split("").reverse().join("")},toBase64String:function(n,i,r,u){var f;if(n==null)throw new System.ArgumentNullException("inArray");if(i=i||0,r=r!=null?r:n.length,u=u||0,r<0)throw new System.ArgumentOutOfRangeException("length","Index was out of range. Must be non-negative and less than the size of the collection.");if(i<0)throw new System.ArgumentOutOfRangeException("offset","Value must be positive.");if(u<0||u>1)throw new System.ArgumentException("Illegal enum value.");if(f=n.length,i>f-r)throw new System.ArgumentOutOfRangeException("offset","Offset and length must refer to a position in the string.");if(f===0)return"";var o=u===1,s=t.internal.toBase64_CalculateAndValidateOutputLength(r,o),e=[];return e.length=s,t.internal.convertToBase64Array(e,n,i,r,o),e.join("")},toBase64CharArray:function(n,i,r,u,f,e){var o,s,c;if(n==null)throw new System.ArgumentNullException("inArray");if(u==null)throw new System.ArgumentNullException("outArray");if(r<0)throw new System.ArgumentOutOfRangeException("length","Index was out of range. Must be non-negative and less than the size of the collection.");if(i<0)throw new System.ArgumentOutOfRangeException("offsetIn","Value must be positive.");if(f<0)throw new System.ArgumentOutOfRangeException("offsetOut","Value must be positive.");if(e=e||0,e<0||e>1)throw new System.ArgumentException("Illegal enum value.");if(o=n.length,i>o-r)throw new System.ArgumentOutOfRangeException("offsetIn","Offset and length must refer to a position in the string.");if(o===0)return 0;var h=e===1,l=u.length,a=t.internal.toBase64_CalculateAndValidateOutputLength(r,h);if(f>l-a)throw new System.ArgumentOutOfRangeException("offsetOut","Either offset did not refer to a position in the string, or there is an insufficient length of destination character array.");return s=[],c=t.internal.convertToBase64Array(s,n,i,r,h),t.internal.charsToCodes(s,u,f),c},fromBase64String:function(n){if(n==null)throw new System.ArgumentNullException("s");var i=n.split("");return t.internal.fromBase64CharPtr(i,0,i.length)},fromBase64CharArray:function(n,i,r){if(n==null)throw new System.ArgumentNullException("inArray");if(r<0)throw new System.ArgumentOutOfRangeException("length","Index was out of range. Must be non-negative and less than the size of the collection.");if(i<0)throw new System.ArgumentOutOfRangeException("offset","Value must be positive.");if(i>n.length-r)throw new System.ArgumentOutOfRangeException("offset","Offset and length must refer to a position in the string.");var u=t.internal.codesToChars(n);return t.internal.fromBase64CharPtr(u,i,r)},convertToType:function(){throw new System.NotSupportedException("IConvertible interface is not supported.");}};t.internal={base64Table:["A","B","C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z","a","b","c","d","e","f","g","h","i","j","k","l","m","n","o","p","q","r","s","t","u","v","w","x","y","z","0","1","2","3","4","5","6","7","8","9","+","/","="],typeRanges:{Char_MinValue:0,Char_MaxValue:65535,Byte_MinValue:0,Byte_MaxValue:255,SByte_MinValue:-128,SByte_MaxValue:127,Int16_MinValue:-32768,Int16_MaxValue:32767,UInt16_MinValue:0,UInt16_MaxValue:65535,Int32_MinValue:-2147483648,Int32_MaxValue:2147483647,UInt32_MinValue:0,UInt32_MaxValue:4294967295,Int64_MinValue:System.Int64.MinValue,Int64_MaxValue:System.Int64.MaxValue,UInt64_MinValue:System.UInt64.MinValue,UInt64_MaxValue:System.UInt64.MaxValue,Single_MinValue:-340282347e30,Single_MaxValue:340282347e30,Double_MinValue:-17976931348623157e292,Double_MaxValue:17976931348623157e292,Decimal_MinValue:System.Decimal.MinValue,Decimal_MaxValue:System.Decimal.MaxValue},base64LineBreakPosition:76,getTypeCodeName:function(n){var r=t.convert.typeCodes,u,i,e,f;if(t.internal.typeCodeNames==null){u={};for(i in r)r.hasOwnProperty(i)&&(e=r[i],u[e]=i);t.internal.typeCodeNames=u}if(f=t.internal.typeCodeNames[n],f==null)throw System.ArgumentOutOfRangeException("typeCode","The specified typeCode is undefined.");return f},suggestTypeCode:function(n){var i=t.convert.typeCodes,r=typeof n;switch(r){case"boolean":return i.Boolean;case"number":return n%1!=0?i.Double:i.Int32;case"string":return i.String;case"object":if(Bridge.isDate(n))return i.DateTime;if(n!=null)return i.Object}return null},getMinValue:function(n){var i=t.convert.typeCodes,r;switch(n){case i.Char:return t.internal.typeRanges.Char_MinValue;case i.SByte:return t.internal.typeRanges.SByte_MinValue;case i.Byte:return t.internal.typeRanges.Byte_MinValue;case i.Int16:return t.internal.typeRanges.Int16_MinValue;case i.UInt16:return t.internal.typeRanges.UInt16_MinValue;case i.Int32:return t.internal.typeRanges.Int32_MinValue;case i.UInt32:return t.internal.typeRanges.UInt32_MinValue;case i.Int64:return t.internal.typeRanges.Int64_MinValue;case i.UInt64:return t.internal.typeRanges.UInt64_MinValue;case i.Single:return t.internal.typeRanges.Single_MinValue;case i.Double:return t.internal.typeRanges.Double_MinValue;case i.Decimal:return t.internal.typeRanges.Decimal_MinValue;case i.DateTime:return r=new Date(0),r.setFullYear(1),r;default:return null}},getMaxValue:function(n){var i=t.convert.typeCodes;switch(n){case i.Char:return t.internal.typeRanges.Char_MaxValue;case i.SByte:return t.internal.typeRanges.SByte_MaxValue;case i.Byte:return t.internal.typeRanges.Byte_MaxValue;case i.Int16:return t.internal.typeRanges.Int16_MaxValue;case i.UInt16:return t.internal.typeRanges.UInt16_MaxValue;case i.Int32:return t.internal.typeRanges.Int32_MaxValue;case i.UInt32:return t.internal.typeRanges.UInt32_MaxValue;case i.Int64:return t.internal.typeRanges.Int64_MaxValue;case i.UInt64:return t.internal.typeRanges.UInt64_MaxValue;case i.Single:return t.internal.typeRanges.Single_MaxValue;case i.Double:return t.internal.typeRanges.Double_MaxValue;case i.Decimal:return t.internal.typeRanges.Decimal_MaxValue;default:throw new System.ArgumentOutOfRangeException("typeCode","The specified typeCode is undefined.");}},isFloatingType:function(n){var i=t.convert.typeCodes;return n===i.Single||n===i.Double||n===i.Decimal},toNumber:function(n,i,r,u){var f=t.convert.typeCodes,e=typeof n,o=t.internal.isFloatingType(r),h,c,s;u===f.String&&(e="string");(System.Int64.is64Bit(n)||n instanceof System.Decimal)&&(e="number");switch(e){case"boolean":return n?1:0;case"number":return r===f.Decimal?(t.internal.validateNumberRange(n,r,!0),new System.Decimal(n,i)):r===f.Int64?(t.internal.validateNumberRange(n,r,!0),new System.Int64(n)):r===f.UInt64?(t.internal.validateNumberRange(n,r,!0),new System.UInt64(n)):(System.Int64.is64Bit(n)?n=n.toNumber():n instanceof System.Decimal&&(n=n.toFloat()),o||n%1==0||(n=t.internal.roundToInt(n,r)),o&&(h=t.internal.getMinValue(r),c=t.internal.getMaxValue(r),n>c?n=Infinity:n<h&&(n=-Infinity)),t.internal.validateNumberRange(n,r,!1),n);case"string":if(n==null){if(i!=null)throw new System.ArgumentNullException("String","Value cannot be null.");return 0}if(o)if(r===f.Decimal){if(!/^[+-]?[0-9]+[.,]?[0-9]$/.test(n)&&!/^[+-]?[0-9]+$/.test(n))throw new System.FormatException("Input string was not in a correct format.");n=System.Decimal(n,i)}else{if(!/^[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?$/.test(n))throw new System.FormatException("Input string was not in a correct format.");n=parseFloat(n)}else{if(!/^[+-]?[0-9]+$/.test(n))throw new System.FormatException("Input string was not in a correct format.");s=n;r===f.Int64?(n=new System.Int64(n),s!==n.toString()&&this.throwOverflow(t.internal.getTypeCodeName(r))):r===f.UInt64?(n=new System.UInt64(n),s!==n.toString()&&this.throwOverflow(t.internal.getTypeCodeName(r))):n=parseInt(n,10)}if(isNaN(n))throw new System.FormatException("Input string was not in a correct format.");return t.internal.validateNumberRange(n,r,!0),n;case"object":if(n==null)return 0;Bridge.isDate(n)&&t.internal.throwInvalidCastEx(t.convert.typeCodes.DateTime,r)}return u=u||t.internal.suggestTypeCode(n),t.internal.throwInvalidCastEx(u,r),t.convert.convertToType(r,n,i)},validateNumberRange:function(n,i,r){var u=t.convert.typeCodes,e=t.internal.getMinValue(i),o=t.internal.getMaxValue(i),f=t.internal.getTypeCodeName(i);(i!==u.Single&&i!==u.Double||r||n!==Infinity&&n!==-Infinity)&&(i===u.Decimal||i===u.Int64||i===u.UInt64?i===u.Decimal?(System.Int64.is64Bit(n)||(e.gt(n)||o.lt(n))&&this.throwOverflow(f),n=new System.Decimal(n)):i===u.Int64?(n instanceof System.UInt64?n.gt(System.Int64.MaxValue)&&this.throwOverflow(f):n instanceof System.Decimal?(n.gt(new System.Decimal(o))||n.lt(new System.Decimal(e)))&&this.throwOverflow(f):n instanceof System.Int64||(e.toNumber()>n||o.toNumber()<n)&&this.throwOverflow(f),n=new System.Int64(n)):i===u.UInt64&&(n instanceof System.Int64?n.isNegative()&&this.throwOverflow(f):n instanceof System.Decimal?(n.gt(new System.Decimal(o))||n.lt(new System.Decimal(e)))&&this.throwOverflow(f):n instanceof System.UInt64||(e.toNumber()>n||o.toNumber()<n)&&this.throwOverflow(f),n=new System.UInt64(n)):(n<e||n>o)&&this.throwOverflow(f))},throwOverflow:function(n){throw new System.OverflowException("Value was either too large or too small for '"+n+"'.");},roundToInt:function(n,i){var r,f;if(n%1==0)return n;r=n>=0?Math.floor(n):-1*Math.floor(-n);var u=n-r,e=t.internal.getMinValue(i),o=t.internal.getMaxValue(i);if(n>=0){if(n<o+.5)return(u>.5||u===.5&&(r&1)!=0)&&++r,r}else if(n>=e-.5)return(u<-.5||u===-.5&&(r&1)!=0)&&--r,r;f=t.internal.getTypeCodeName(i);throw new System.OverflowException("Value was either too large or too small for an '"+f+"'.");},toBase64_CalculateAndValidateOutputLength:function(n,i){var f=t.internal.base64LineBreakPosition,r=~~(n/3)*4,u;if(r+=n%3!=0?4:0,r===0)return 0;if(i&&(u=~~(r/f),r%f==0&&--u,r+=u*2),r>2147483647)throw new System.OutOfMemoryException;return r},convertToBase64Array:function(n,i,r,u,f){for(var s=t.internal.base64Table,a=t.internal.base64LineBreakPosition,c=u%3,l=r+(u-c),h=0,e=0,o=r;o<l;o+=3)f&&(h===a&&(n[e++]="\r",n[e++]="\n",h=0),h+=4),n[e]=s[(i[o]&252)>>2],n[e+1]=s[(i[o]&3)<<4|(i[o+1]&240)>>4],n[e+2]=s[(i[o+1]&15)<<2|(i[o+2]&192)>>6],n[e+3]=s[i[o+2]&63],e+=4;o=l;f&&c!==0&&h===t.internal.base64LineBreakPosition&&(n[e++]="\r",n[e++]="\n");switch(c){case 2:n[e]=s[(i[o]&252)>>2];n[e+1]=s[(i[o]&3)<<4|(i[o+1]&240)>>4];n[e+2]=s[(i[o+1]&15)<<2];n[e+3]=s[64];e+=4;break;case 1:n[e]=s[(i[o]&252)>>2];n[e+1]=s[(i[o]&3)<<4];n[e+2]=s[64];n[e+3]=s[64];e+=4}return e},fromBase64CharPtr:function(n,i,r){var u,f,e;if(r<0)throw new System.ArgumentOutOfRangeException("inputLength","Index was out of range. Must be non-negative and less than the size of the collection.");if(i<0)throw new System.ArgumentOutOfRangeException("offset","Value must be positive.");while(r>0){if(u=n[i+r-1],u!==" "&&u!=="\n"&&u!=="\r"&&u!=="\t")break;r--}if(f=t.internal.fromBase64_ComputeResultLength(n,i,r),0>f)throw new System.InvalidOperationException("Contract voilation: 0 <= resultLength.");return e=[],e.length=f,t.internal.fromBase64_Decode(n,i,r,e,0,f),e},fromBase64_Decode:function(n,t,i,r,u,f){for(var k=u,a="A".charCodeAt(0),v="a".charCodeAt(0),y="0".charCodeAt(0),p="=".charCodeAt(0),d="+".charCodeAt(0),g="/".charCodeAt(0),nt=" ".charCodeAt(0),tt="\t".charCodeAt(0),it="\n".charCodeAt(0),rt="\r".charCodeAt(0),w="Z".charCodeAt(0)-"A".charCodeAt(0),ut="9".charCodeAt(0)-"0".charCodeAt(0),h=t+i,l=u+f,o,e=255,b=!1,c=!1,s;;){if(t>=h){b=!0;break}if(o=n[t].charCodeAt(0),t++,o-a>>>0<=w)o-=a;else if(o-v>>>0<=w)o-=v-26;else if(o-y>>>0<=ut)o-=y-52;else switch(o){case d:o=62;break;case g:o=63;break;case rt:case it:case nt:case tt:continue;case p:c=!0;break;default:throw new System.FormatException("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.");}if(c)break;if(e=e<<6|o,(e&2147483648)!=0){if(l-u<3)return-1;r[u]=255&e>>16;r[u+1]=255&e>>8;r[u+2]=255&e;u+=3;e=255}}if(!b&&!c)throw new System.InvalidOperationException("Contract violation: should never get here.");if(c){if(o!==p)throw new System.InvalidOperationException("Contract violation: currCode == intEq.");if(t===h){if(e<<=6,(e&2147483648)==0)throw new System.FormatException("Invalid length for a Base-64 char array or string.");if(l-u<2)return-1;r[u]=255&e>>16;r[u+1]=255&e>>8;u+=2;e=255}else{while(t<h-1){if(s=n[t],s!==" "&&s!=="\n"&&s!=="\r"&&s!=="\t")break;t++}if(t===h-1&&n[t]==="="){if(e<<=12,(e&2147483648)==0)throw new System.FormatException("Invalid length for a Base-64 char array or string.");if(l-u<1)return-1;r[u]=255&e>>16;u++;e=255}else throw new System.FormatException("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.");}}if(e!==255)throw new System.FormatException("Invalid length for a Base-64 char array or string.");return u-k},fromBase64_ComputeResultLength:function(n,t,i){var f;if(i<0)throw new System.ArgumentOutOfRangeException("inputLength","Index was out of range. Must be non-negative and less than the size of the collection.");for(var e=t+i,u=i,r=0;t<e;)f=n[t],t++,f<=" "?u--:f==="="&&(u--,r++);if(0>u)throw new System.InvalidOperationException("Contract violation: 0 <= usefulInputLength.");if(0>r)throw new System.InvalidOperationException("Contract violation: 0 <= padding.");if(r!==0)if(r===1)r=2;else if(r===2)r=1;else throw new System.FormatException("The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters.");return~~(u/4)*3+r},charsToCodes:function(n,t,i){if(n==null)return null;i=i||0;t==null&&(t=[],t.length=n.length);for(var r=0;r<n.length;r++)t[r+i]=n[r].charCodeAt(0);return t},codesToChars:function(n,t){var i,r;if(n==null)return null;for(t=t||[],i=0;i<n.length;i++)r=n[i],t[i]=String.fromCharCode(r);return t},throwInvalidCastEx:function(n,i){var r=t.internal.getTypeCodeName(n),u=t.internal.getTypeCodeName(i);throw new System.InvalidCastException("Invalid cast from '"+r+"' to '"+u+"'.");}};System.Convert=t.convert;Bridge.define("System.Net.WebSockets.ClientWebSocket",{inherits:[System.IDisposable],constructor:function(){this.messageBuffer=[];this.state="none";this.options=new System.Net.WebSockets.ClientWebSocketOptions;this.disposed=!1;this.closeStatus=null;this.closeStatusDescription=null},getCloseStatus:function(){return this.closeStatus},getState:function(){return this.state},getCloseStatusDescription:function(){return this.closeStatusDescription},getSubProtocol:function(){return this.socket?this.socket.protocol:null},connectAsync:function(n){if(this.state!=="none")throw new System.InvalidOperationException("Socket is not in initial state");this.options.setToReadOnly();this.state="connecting";var i=new System.Threading.Tasks.TaskCompletionSource,t=this;try{this.socket=new WebSocket(n.getAbsoluteUri(),this.options.requestedSubProtocols);this.socket.binaryType="arraybuffer";this.socket.onopen=function(){t.state="open";i.setResult(null)};this.socket.onmessage=function(n){var u=n.data,r={},i,f;if(r.bytes=[],typeof u=="string"){for(i=0;i<u.length;++i)r.bytes.push(u.charCodeAt(i));r.messageType="text";t.messageBuffer.push(r);return}if(u instanceof ArrayBuffer){for(f=new Uint8Array(u),i=0;i<f.length;i++)r.bytes.push(f[i]);r.messageType="binary";t.messageBuffer.push(r);return}throw new System.ArgumentException("Invalid message type.");};this.socket.onclose=function(n){t.state="closed";t.closeStatus=n.code;t.closeStatusDescription=n.reason}}catch(r){i.setException(System.Exception.create(r))}return i.task},sendAsync:function(n,t){var u,i,f,e,r;this.throwIfNotConnected();u=new System.Threading.Tasks.TaskCompletionSource;try{i=n.getArray();switch(t){case"binary":for(f=new ArrayBuffer(i.length),e=new Int8Array(f),r=0;r<i.length;r++)e[r]=i[r];break;case"text":f=String.fromCharCode.apply(null,i)}t==="close"?this.socket.close():this.socket.send(f);u.setResult(null)}catch(o){u.setException(System.Exception.create(o))}return u.task},receiveAsync:function(n,t){this.throwIfNotConnected();var u,i=new System.Threading.Tasks.TaskCompletionSource,r=this,f=Bridge.fn.bind(this,function(){var e,o,s,c,h;try{if(t.getIsCancellationRequested()){i.setException(new System.Threading.Tasks.TaskCanceledException("Receive has been cancelled.",i.task));return}if(r.messageBuffer.length===0){u=System.Threading.Tasks.Task.delay(0);u.continueWith(f);return}for(e=r.messageBuffer[0],o=n.getArray(),e.bytes.length<=o.length?(r.messageBuffer.shift(),s=e.bytes,c=!0):(s=e.bytes.slice(0,o.length),e.bytes=e.bytes.slice(o.length,e.bytes.length),c=!1),h=0;h<s.length;h++)o[h]=s[h];i.setResult(new System.Net.WebSockets.WebSocketReceiveResult(s.length,e.messageType,c))}catch(l){i.setException(System.Exception.create(l))}},arguments);return f(),i.task},closeAsync:function(n,t,i){if(this.throwIfNotConnected(),this.state!=="open")throw new System.InvalidOperationException("Socket is not in connected state");var r=new System.Threading.Tasks.TaskCompletionSource,e=this,u,f=function(){if(e.state==="closed"){r.setResult(null);return}if(i.getIsCancellationRequested()){r.setException(new System.Threading.Tasks.TaskCanceledException("Closing has been cancelled.",r.task));return}u=System.Threading.Tasks.Task.delay(0);u.continueWith(f)};try{this.state="closesent";this.socket.close(n,t)}catch(o){r.setException(System.Exception.create(o))}return f(),r.task},closeOutputAsync:function(n,t){if(this.throwIfNotConnected(),this.state!=="open")throw new System.InvalidOperationException("Socket is not in connected state");var i=new System.Threading.Tasks.TaskCompletionSource;try{this.state="closesent";this.socket.close(n,t);i.setResult(null)}catch(r){i.setException(System.Exception.create(r))}return i.task},abort:function(){this.dispose()},dispose:function(){this.disposed||(this.disposed=!0,this.messageBuffer=[],state==="open"&&(this.state="closesent",this.socket.close()))},throwIfNotConnected:function(){if(this.disposed)throw new System.InvalidOperationException("Socket is disposed.");if(this.socket.readyState!==1)throw new System.InvalidOperationException("Socket is not connected.");}});Bridge.define("System.Net.WebSockets.ClientWebSocketOptions",{constructor:function(){this.isReadOnly=!1;this.requestedSubProtocols=[]},setToReadOnly:function(){if(this.isReadOnly)throw new System.InvalidOperationException("Options are already readonly.");this.isReadOnly=!0},addSubProtocol:function(n){if(this.isReadOnly)throw new System.InvalidOperationException("Socket already started.");if(this.requestedSubProtocols.indexOf(n)>-1)throw new System.ArgumentException("Socket cannot have duplicate sub-protocols.","subProtocol");this.requestedSubProtocols.push(n)}});Bridge.define("System.Net.WebSockets.WebSocketReceiveResult",{constructor:function(n,t,i,r,u){this.count=n;this.messageType=t;this.endOfMessage=i;this.closeStatus=r;this.closeStatusDescription=u},getCount:function(){return this.count},getMessageType:function(){return this.messageType},getEndOfMessage:function(){return this.endOfMessage},getCloseStatus:function(){return this.closeStatus},getCloseStatusDescription:function(){return this.closeStatusDescription}});Bridge.define("System.Uri",{constructor:function(n){this.absoluteUri=n},getAbsoluteUri:function(){return this.absoluteUri}}),function(n,t){var f={Identity:function(n){return n},True:function(){return!0},Blank:function(){}},o={Boolean:"boolean",Number:"number",String:"string",Object:"object",Undefined:typeof t,Function:typeof function(){}},p={"":f.Identity},r={createLambda:function(n){var t,l,i,a,u,e,r,s,h,v,c;if(n==null)return f.Identity;if(typeof n===o.String){if(t=p[n],t!=null)return t;if(n.indexOf("=>")===-1){for(l=new RegExp("[$]+","g"),i=0;(a=l.exec(n))!=null;)u=a[0].length,u>i&&(i=u);for(e=[],r=1;r<=i;r++){for(s="",h=0;h<r;h++)s+="$";e.push(s)}return v=Array.prototype.join.call(e,","),t=new Function(v,"return "+n),p[n]=t,t}return c=n.match(/^[(\s]*([^()]*?)[)\s]*=>(.*)/),t=new Function(c[1],"return "+c[2]),p[n]=t,t}return n},isIEnumerable:function(n){if(typeof Enumerator!==o.Undefined)try{return new Enumerator(n),!0}catch(t){}return!1},defineProperty:Object.defineProperties!=null?function(n,t,i){Object.defineProperty(n,t,{enumerable:!1,configurable:!0,writable:!0,value:i})}:function(n,t,i){n[t]=i},compare:function(n,t){return n===t?0:n>t?1:-1},dispose:function(n){n!=null&&n.dispose()}},l={Before:0,Running:1,After:2},u=function(n,t,i){var u=new b,r=l.Before;this.getCurrent=u.getCurrent;this.reset=function(){throw new Error("Reset is not supported");};this.moveNext=function(){try{switch(r){case l.Before:r=l.Running;n();case l.Running:return t.apply(u)?!0:(this.dispose(),!1);case l.After:return!1}}catch(i){this.dispose();throw i;}};this.dispose=function(){if(r==l.Running)try{i()}finally{r=l.After}}},b,i,k,s,a,v,e,h,c,w,y;System.IDisposable.$$inheritors=System.IDisposable.$$inheritors||[];System.IDisposable.$$inheritors.push(u);b=function(){var n=null;this.getCurrent=function(){return n};this.yieldReturn=function(t){return n=t,!0};this.yieldBreak=function(){return!1}};i=function(n){this.getEnumerator=n};System.Collections.IEnumerable.$$inheritors=System.Collections.IEnumerable.$$inheritors||[];System.Collections.IEnumerable.$$inheritors.push(i);i.Utils={};i.Utils.createLambda=function(n){return r.createLambda(n)};i.Utils.createEnumerable=function(n){return new i(n)};i.Utils.createEnumerator=function(n,t,i){return new u(n,t,i)};i.Utils.extendTo=function(n){var u=n.prototype,o,t,f;n===Array?(o=e.prototype,r.defineProperty(u,"getSource",function(){return this})):(o=i.prototype,r.defineProperty(u,"getEnumerator",function(){return i.from(this).getEnumerator()}));for(t in o)(f=o[t],u[t]!=f)&&(u[t]==null||(t=t+"ByLinq",u[t]!=f))&&f instanceof Function&&r.defineProperty(u,t,f)};i.choice=function(){var n=arguments;return new i(function(){return new u(function(){n=n[0]instanceof Array?n[0]:n[0].getEnumerator!=null?n[0].toArray():n},function(){return this.yieldReturn(n[Math.floor(Math.random()*n.length)])},f.Blank)})};i.cycle=function(){var n=arguments;return new i(function(){var t=0;return new u(function(){n=n[0]instanceof Array?n[0]:n[0].getEnumerator!=null?n[0].toArray():n},function(){return t>=n.length&&(t=0),this.yieldReturn(n[t++])},f.Blank)})};k=new i(function(){return new u(f.Blank,function(){return!1},f.Blank)});i.empty=function(){return k};i.from=function(n){if(n==null)return i.empty();if(n instanceof i)return n;if(typeof n==o.Number||typeof n==o.Boolean)return i.repeat(n,1);if(typeof n==o.String)return new i(function(){var t=0;return new u(f.Blank,function(){return t<n.length?this.yieldReturn(n.charCodeAt(t++)):!1},f.Blank)});var t=Bridge.as(n,System.Collections.IEnumerable);if(t)return new i(function(){var n;return new u(function(){n=Bridge.getEnumerator(t)},function(){var t=n.moveNext();return t?this.yieldReturn(n.getCurrent()):!1},function(){var t=Bridge.as(n,System.IDisposable);t&&t.dispose()})});if(typeof n!=o.Function){if(typeof n.length==o.Number)return new e(n);if(!(n instanceof Object)&&r.isIEnumerable(n))return new i(function(){var i=!0,t;return new u(function(){t=new Enumerator(n)},function(){return i?i=!1:t.moveNext(),t.atEnd()?!1:this.yieldReturn(t.item())},f.Blank)});if(typeof Windows===o.Object&&typeof n.first===o.Function)return new i(function(){var i=!0,t;return new u(function(){t=n.first()},function(){return i?i=!1:t.moveNext(),t.hasCurrent?this.yieldReturn(t.current):this.yieldBreak()},f.Blank)})}return new i(function(){var t=[],i=0;return new u(function(){var i,r;for(i in n)r=n[i],r instanceof Function||!Object.prototype.hasOwnProperty.call(n,i)||t.push({key:i,value:r})},function(){return i<t.length?this.yieldReturn(t[i++]):!1},f.Blank)})};i.make=function(n){return i.repeat(n,1)};i.matches=function(n,t,r){return r==null&&(r=""),t instanceof RegExp&&(r+=t.ignoreCase?"i":"",r+=t.multiline?"m":"",t=t.source),r.indexOf("g")===-1&&(r+="g"),new i(function(){var i;return new u(function(){i=new RegExp(t,r)},function(){var t=i.exec(n);return t?this.yieldReturn(t):!1},f.Blank)})};i.range=function(n,t,r){return r==null&&(r=1),new i(function(){var i,e=0;return new u(function(){i=n-r},function(){return e++<t?this.yieldReturn(i+=r):this.yieldBreak()},f.Blank)})};i.rangeDown=function(n,t,r){return r==null&&(r=1),new i(function(){var i,e=0;return new u(function(){i=n+r},function(){return e++<t?this.yieldReturn(i-=r):this.yieldBreak()},f.Blank)})};i.rangeTo=function(n,t,r){return r==null&&(r=1),n<t?new i(function(){var i;return new u(function(){i=n-r},function(){var n=i+=r;return n<=t?this.yieldReturn(n):this.yieldBreak()},f.Blank)}):new i(function(){var i;return new u(function(){i=n+r},function(){var n=i-=r;return n>=t?this.yieldReturn(n):this.yieldBreak()},f.Blank)})};i.repeat=function(n,t){return t!=null?i.repeat(n).take(t):new i(function(){return new u(f.Blank,function(){return this.yieldReturn(n)},f.Blank)})};i.repeatWithFinalize=function(n,t){return n=r.createLambda(n),t=r.createLambda(t),new i(function(){var i;return new u(function(){i=n()},function(){return this.yieldReturn(i)},function(){i!=null&&(t(i),i=null)})})};i.generate=function(n,t){return t!=null?i.generate(n).take(t):(n=r.createLambda(n),new i(function(){return new u(f.Blank,function(){return this.yieldReturn(n())},f.Blank)}))};i.toInfinity=function(n,t){return n==null&&(n=0),t==null&&(t=1),new i(function(){var i;return new u(function(){i=n-t},function(){return this.yieldReturn(i+=t)},f.Blank)})};i.toNegativeInfinity=function(n,t){return n==null&&(n=0),t==null&&(t=1),new i(function(){var i;return new u(function(){i=n+t},function(){return this.yieldReturn(i-=t)},f.Blank)})};i.unfold=function(n,t){return t=r.createLambda(t),new i(function(){var r=!0,i;return new u(f.Blank,function(){return r?(r=!1,i=n,this.yieldReturn(i)):(i=t(i),this.yieldReturn(i))},f.Blank)})};i.defer=function(n){return new i(function(){var t;return new u(function(){t=i.from(n()).getEnumerator()},function(){return t.moveNext()?this.yieldReturn(t.getCurrent()):this.yieldBreak()},function(){r.dispose(t)})})};i.prototype.traverseBreadthFirst=function(n,t){var f=this;return n=r.createLambda(n),t=r.createLambda(t),new i(function(){var e,s=0,o=[];return new u(function(){e=f.getEnumerator()},function(){for(;;){if(e.moveNext())return o.push(e.getCurrent()),this.yieldReturn(t(e.getCurrent(),s));var u=i.from(o).selectMany(function(t){return n(t)});if(u.any())s++,o=[],r.dispose(e),e=u.getEnumerator();else return!1}},function(){r.dispose(e)})})};i.prototype.traverseDepthFirst=function(n,t){var f=this;return n=r.createLambda(n),t=r.createLambda(t),new i(function(){var o=[],e;return new u(function(){e=f.getEnumerator()},function(){for(;;){if(e.moveNext()){var u=t(e.getCurrent(),o.length);return o.push(e),e=i.from(n(e.getCurrent())).getEnumerator(),this.yieldReturn(u)}if(o.length<=0)return!1;r.dispose(e);e=o.pop()}},function(){try{r.dispose(e)}finally{i.from(o).forEach(function(n){n.dispose()})}})})};i.prototype.flatten=function(){var n=this;return new i(function(){var e,t=null;return new u(function(){e=n.getEnumerator()},function(){for(;;){if(t!=null){if(t.moveNext())return this.yieldReturn(t.getCurrent());t=null}if(e.moveNext())if(e.getCurrent()instanceof Array){r.dispose(t);t=i.from(e.getCurrent()).selectMany(f.Identity).flatten().getEnumerator();continue}else return this.yieldReturn(e.getCurrent());return!1}},function(){try{r.dispose(e)}finally{r.dispose(t)}})})};i.prototype.pairwise=function(n){var t=this;return n=r.createLambda(n),new i(function(){var i;return new u(function(){i=t.getEnumerator();i.moveNext()},function(){var t=i.getCurrent();return i.moveNext()?this.yieldReturn(n(t,i.getCurrent())):!1},function(){r.dispose(i)})})};i.prototype.scan=function(n,t){var f,e;return t==null?(t=r.createLambda(n),f=!1):(t=r.createLambda(t),f=!0),e=this,new i(function(){var i,o,s=!0;return new u(function(){i=e.getEnumerator()},function(){if(s){if(s=!1,f)return this.yieldReturn(o=n);if(i.moveNext())return this.yieldReturn(o=i.getCurrent())}return i.moveNext()?this.yieldReturn(o=t(o,i.getCurrent())):!1},function(){r.dispose(i)})})};i.prototype.select=function(n){if(n=r.createLambda(n),n.length<=1)return new c(this,null,n);var t=this;return new i(function(){var i,f=0;return new u(function(){i=t.getEnumerator()},function(){return i.moveNext()?this.yieldReturn(n(i.getCurrent(),f++)):!1},function(){r.dispose(i)})})};i.prototype.selectMany=function(n,f){var e=this;return n=r.createLambda(n),f==null&&(f=function(n,t){return t}),f=r.createLambda(f),new i(function(){var s,o=t,h=0;return new u(function(){s=e.getEnumerator()},function(){if(o===t&&!s.moveNext())return!1;do{if(o==null){var u=n(s.getCurrent(),h++);o=i.from(u).getEnumerator()}if(o.moveNext())return this.yieldReturn(f(s.getCurrent(),o.getCurrent()));r.dispose(o);o=null}while(s.moveNext());return!1},function(){try{r.dispose(s)}finally{r.dispose(o)}})})};i.prototype.where=function(n){if(n=r.createLambda(n),n.length<=1)return new h(this,n);var t=this;return new i(function(){var i,f=0;return new u(function(){i=t.getEnumerator()},function(){while(i.moveNext())if(n(i.getCurrent(),f++))return this.yieldReturn(i.getCurrent());return!1},function(){r.dispose(i)})})};i.prototype.choose=function(n){n=r.createLambda(n);var t=this;return new i(function(){var i,f=0;return new u(function(){i=t.getEnumerator()},function(){while(i.moveNext()){var t=n(i.getCurrent(),f++);if(t!=null)return this.yieldReturn(t)}return this.yieldBreak()},function(){r.dispose(i)})})};i.prototype.ofType=function(n){var t=this;return new i(function(){var i;return new u(function(){i=Bridge.getEnumerator(t)},function(){while(i.moveNext()){var t=Bridge.as(i.getCurrent(),n);if(Bridge.hasValue(t))return this.yieldReturn(t)}return!1},function(){r.dispose(i)})})};i.prototype.zip=function(){var e=arguments,n=r.createLambda(arguments[arguments.length-1]),t=this,f;return arguments.length==2?(f=arguments[0],new i(function(){var e,o,s=0;return new u(function(){e=t.getEnumerator();o=i.from(f).getEnumerator()},function(){return e.moveNext()&&o.moveNext()?this.yieldReturn(n(e.getCurrent(),o.getCurrent(),s++)):!1},function(){try{r.dispose(e)}finally{r.dispose(o)}})})):new i(function(){var f,o=0;return new u(function(){var n=i.make(t).concat(i.from(e).takeExceptLast().select(i.from)).select(function(n){return n.getEnumerator()}).toArray();f=i.from(n)},function(){if(f.all(function(n){return n.moveNext()})){var t=f.select(function(n){return n.getCurrent()}).toArray();return t.push(o++),this.yieldReturn(n.apply(null,t))}return this.yieldBreak()},function(){i.from(f).forEach(r.dispose)})})};i.prototype.merge=function(){var n=arguments,t=this;return new i(function(){var f,e=-1;return new u(function(){f=i.make(t).concat(i.from(n).select(i.from)).select(function(n){return n.getEnumerator()}).toArray()},function(){while(f.length>0){e=e>=f.length-1?0:e+1;var n=f[e];if(n.moveNext())return this.yieldReturn(n.getCurrent());n.dispose();f.splice(e--,1)}return this.yieldBreak()},function(){i.from(f).forEach(r.dispose)})})};i.prototype.join=function(n,e,o,s,h){e=r.createLambda(e);o=r.createLambda(o);s=r.createLambda(s);var c=this;return new i(function(){var l,v,a=null,y=0;return new u(function(){l=c.getEnumerator();v=i.from(n).toLookup(o,f.Identity,h)},function(){for(var n,i;;){if(a!=null){if(n=a[y++],n!==t)return this.yieldReturn(s(l.getCurrent(),n));n=null;y=0}if(l.moveNext())i=e(l.getCurrent()),a=v.get(i).toArray();else return!1}},function(){r.dispose(l)})})};i.prototype.groupJoin=function(n,t,e,o,s){t=r.createLambda(t);e=r.createLambda(e);o=r.createLambda(o);var h=this;return new i(function(){var c=h.getEnumerator(),l=null;return new u(function(){c=h.getEnumerator();l=i.from(n).toLookup(e,f.Identity,s)},function(){if(c.moveNext()){var n=l.get(t(c.getCurrent()));return this.yieldReturn(o(c.getCurrent(),n))}return!1},function(){r.dispose(c)})})};i.prototype.all=function(n){n=r.createLambda(n);var t=!0;return this.forEach(function(i){if(!n(i))return t=!1,!1}),t};i.prototype.any=function(n){n=r.createLambda(n);var t=this.getEnumerator();try{if(arguments.length==0)return t.moveNext();while(t.moveNext())if(n(t.getCurrent()))return!0;return!1}finally{r.dispose(t)}};i.prototype.isEmpty=function(){return!this.any()};i.prototype.concat=function(){var n=this,t,f;return arguments.length==1?(t=arguments[0],new i(function(){var e,f;return new u(function(){e=n.getEnumerator()},function(){if(f==null){if(e.moveNext())return this.yieldReturn(e.getCurrent());f=i.from(t).getEnumerator()}return f.moveNext()?this.yieldReturn(f.getCurrent()):!1},function(){try{r.dispose(e)}finally{r.dispose(f)}})})):(f=arguments,new i(function(){var t;return new u(function(){t=i.make(n).concat(i.from(f).select(i.from)).select(function(n){return n.getEnumerator()}).toArray()},function(){while(t.length>0){var n=t[0];if(n.moveNext())return this.yieldReturn(n.getCurrent());n.dispose();t.splice(0,1)}return this.yieldBreak()},function(){i.from(t).forEach(r.dispose)})}))};i.prototype.insert=function(n,t){var f=this;return new i(function(){var o,e,s=0,h=!1;return new u(function(){o=f.getEnumerator();e=i.from(t).getEnumerator()},function(){return s==n&&e.moveNext()?(h=!0,this.yieldReturn(e.getCurrent())):o.moveNext()?(s++,this.yieldReturn(o.getCurrent())):!h&&e.moveNext()?this.yieldReturn(e.getCurrent()):!1},function(){try{r.dispose(o)}finally{r.dispose(e)}})})};i.prototype.alternate=function(n){var t=this;return new i(function(){var f,e,s,o;return new u(function(){s=n instanceof Array||n.getEnumerator!=null?i.from(i.from(n).toArray()):i.make(n);e=t.getEnumerator();e.moveNext()&&(f=e.getCurrent())},function(){for(;;){if(o!=null){if(o.moveNext())return this.yieldReturn(o.getCurrent());o=null}if(f==null&&e.moveNext()){f=e.getCurrent();o=s.getEnumerator();continue}else if(f!=null){var n=f;return f=null,this.yieldReturn(n)}return this.yieldBreak()}},function(){try{r.dispose(e)}finally{r.dispose(o)}})})};i.prototype.contains=function(n,t){t=t||System.Collections.Generic.EqualityComparer$1.$default;var i=this.getEnumerator();try{while(i.moveNext())if(t.equals2(i.getCurrent(),n))return!0;return!1}finally{r.dispose(i)}};i.prototype.defaultIfEmpty=function(n){var f=this;return n===t&&(n=null),new i(function(){var t,i=!0;return new u(function(){t=f.getEnumerator()},function(){return t.moveNext()?(i=!1,this.yieldReturn(t.getCurrent())):i?(i=!1,this.yieldReturn(n)):!1},function(){r.dispose(t)})})};i.prototype.distinct=function(n){return this.except(i.empty(),n)};i.prototype.distinctUntilChanged=function(n){n=r.createLambda(n);var t=this;return new i(function(){var i,f,e;return new u(function(){i=t.getEnumerator()},function(){while(i.moveNext()){var t=n(i.getCurrent());if(e)return e=!1,f=t,this.yieldReturn(i.getCurrent());if(f!==t)return f=t,this.yieldReturn(i.getCurrent())}return this.yieldBreak()},function(){r.dispose(i)})})};i.prototype.except=function(n,t){var f=this;return new i(function(){var e,o;return new u(function(){e=f.getEnumerator();o=new System.Collections.Generic.Dictionary$2(Object,Object)(null,t);i.from(n).forEach(function(n){o.add(n)})},function(){while(e.moveNext()){var n=e.getCurrent();if(!o.containsKey(n))return o.add(n),this.yieldReturn(n)}return!1},function(){r.dispose(e)})})};i.prototype.intersect=function(n,t){var f=this;return new i(function(){var e,o,s;return new u(function(){e=f.getEnumerator();o=new System.Collections.Generic.Dictionary$2(Object,Object)(null,t);i.from(n).forEach(function(n){o.add(n)});s=new System.Collections.Generic.Dictionary$2(Object,Object)(null,t)},function(){while(e.moveNext()){var n=e.getCurrent();if(!s.containsKey(n)&&o.containsKey(n))return s.add(n),this.yieldReturn(n)}return!1},function(){r.dispose(e)})})};i.prototype.sequenceEqual=function(n,t){var f,u;t=t||System.Collections.Generic.EqualityComparer$1.$default;f=this.getEnumerator();try{u=i.from(n).getEnumerator();try{while(f.moveNext())if(!u.moveNext()||!t.equals2(f.getCurrent(),u.getCurrent()))return!1;return u.moveNext()?!1:!0}finally{r.dispose(u)}}finally{r.dispose(f)}};i.prototype.union=function(n,f){var e=this;return new i(function(){var h,o,s;return new u(function(){h=e.getEnumerator();s=new System.Collections.Generic.Dictionary$2(Object,Object)(null,f)},function(){var r;if(o===t){while(h.moveNext())if(r=h.getCurrent(),!s.containsKey(r))return s.add(r),this.yieldReturn(r);o=i.from(n).getEnumerator()}while(o.moveNext())if(r=o.getCurrent(),!s.containsKey(r))return s.add(r),this.yieldReturn(r);return!1},function(){try{r.dispose(h)}finally{r.dispose(o)}})})};i.prototype.orderBy=function(n,t){return new s(this,n,t,!1)};i.prototype.orderByDescending=function(n,t){return new s(this,n,t,!0)};i.prototype.reverse=function(){var n=this;return new i(function(){var t,i;return new u(function(){t=n.toArray();i=t.length},function(){return i>0?this.yieldReturn(t[--i]):!1},f.Blank)})};i.prototype.shuffle=function(){var n=this;return new i(function(){var t;return new u(function(){t=n.toArray()},function(){if(t.length>0){var n=Math.floor(Math.random()*t.length);return this.yieldReturn(t.splice(n,1)[0])}return!1},f.Blank)})};i.prototype.weightedSample=function(n){n=r.createLambda(n);var t=this;return new i(function(){var i,r=0;return new u(function(){i=t.choose(function(t){var i=n(t);return i<=0?null:(r+=i,{value:t,bound:r})}).toArray()},function(){var t;if(i.length>0){for(var f=Math.floor(Math.random()*r)+1,u=-1,n=i.length;n-u>1;)t=Math.floor((u+n)/2),i[t].bound>=f?n=t:u=t;return this.yieldReturn(i[n].value)}return this.yieldBreak()},f.Blank)})};i.prototype.groupBy=function(n,t,f,e){var o=this;return n=r.createLambda(n),t=r.createLambda(t),f!=null&&(f=r.createLambda(f)),new i(function(){var i;return new u(function(){i=o.toLookup(n,t,e).toEnumerable().getEnumerator()},function(){while(i.moveNext())return f==null?this.yieldReturn(i.getCurrent()):this.yieldReturn(f(i.getCurrent().key(),i.getCurrent()));return!1},function(){r.dispose(i)})})};i.prototype.partitionBy=function(n,t,f,e){var s=this,o;return n=r.createLambda(n),t=r.createLambda(t),e=e||System.Collections.Generic.EqualityComparer$1.$default,f==null?(o=!1,f=function(n,t){return new y(n,t)}):(o=!0,f=r.createLambda(f)),new i(function(){var h,l,c=[];return new u(function(){h=s.getEnumerator();h.moveNext()&&(l=n(h.getCurrent()),c.push(t(h.getCurrent())))},function(){for(var r,u;(r=h.moveNext())==!0;)if(e.equals2(l,n(h.getCurrent())))c.push(t(h.getCurrent()));else break;return c.length>0?(u=o?f(l,i.from(c)):f(l,c),r?(l=n(h.getCurrent()),c=[t(h.getCurrent())]):c=[],this.yieldReturn(u)):!1},function(){r.dispose(h)})})};i.prototype.buffer=function(n){var t=this;return new i(function(){var i;return new u(function(){i=t.getEnumerator()},function(){for(var t=[],r=0;i.moveNext();)if(t.push(i.getCurrent()),++r>=n)return this.yieldReturn(t);return t.length>0?this.yieldReturn(t):!1},function(){r.dispose(i)})})};i.prototype.aggregate=function(n,t,i){return i=r.createLambda(i),i(this.scan(n,t,i).last())};i.prototype.average=function(n){n=r.createLambda(n);var t=0,i=0;return this.forEach(function(r){r=n(r);r instanceof System.Decimal||System.Int64.is64Bit(r)?t=r.add(t):t instanceof System.Decimal||System.Int64.is64Bit(t)?t=t.add(r):t+=r;++i}),t instanceof System.Decimal||System.Int64.is64Bit(t)?t.div(i):t/i};i.prototype.nullableAverage=function(n){return this.any(Bridge.isNull)?null:this.average(n)};i.prototype.count=function(n){n=n==null?f.True:r.createLambda(n);var t=0;return this.forEach(function(i,r){n(i,r)&&++t}),t};i.prototype.max=function(n){return n==null&&(n=f.Identity),this.select(n).aggregate(function(n,t){return Bridge.compare(n,t,!0)===1?n:t})};i.prototype.nullableMax=function(n){return this.any(Bridge.isNull)?null:this.max(n)};i.prototype.min=function(n){return n==null&&(n=f.Identity),this.select(n).aggregate(function(n,t){return Bridge.compare(n,t,!0)===-1?n:t})};i.prototype.nullableMin=function(n){return this.any(Bridge.isNull)?null:this.min(n)};i.prototype.maxBy=function(n){return n=r.createLambda(n),this.aggregate(function(t,i){return Bridge.compare(n(t),n(i),!0)===1?t:i})};i.prototype.minBy=function(n){return n=r.createLambda(n),this.aggregate(function(t,i){return Bridge.compare(n(t),n(i),!0)===-1?t:i})};i.prototype.sum=function(n){return n==null&&(n=f.Identity),this.select(n).aggregate(0,function(n,t){return n instanceof System.Decimal||System.Int64.is64Bit(n)?n.add(t):t instanceof System.Decimal||System.Int64.is64Bit(t)?t.add(n):n+t})};i.prototype.nullableSum=function(n){return this.any(Bridge.isNull)?null:this.sum(n)};i.prototype.elementAt=function(n){var t,i=!1;if(this.forEach(function(r,u){if(u==n)return t=r,i=!0,!1}),!i)throw new Error("index is less than 0 or greater than or equal to the number of elements in source.");return t};i.prototype.elementAtOrDefault=function(n,i){i===t&&(i=null);var r,u=!1;return this.forEach(function(t,i){if(i==n)return r=t,u=!0,!1}),u?r:i};i.prototype.first=function(n){if(n!=null)return this.where(n).first();var t,i=!1;if(this.forEach(function(n){return t=n,i=!0,!1}),!i)throw new Error("first:No element satisfies the condition.");return t};i.prototype.firstOrDefault=function(n,i){if(i===t&&(i=null),n!=null)return this.where(n).firstOrDefault(null,i);var r,u=!1;return this.forEach(function(n){return r=n,u=!0,!1}),u?r:i};i.prototype.last=function(n){if(n!=null)return this.where(n).last();var t,i=!1;if(this.forEach(function(n){i=!0;t=n}),!i)throw new Error("last:No element satisfies the condition.");return t};i.prototype.lastOrDefault=function(n,i){if(i===t&&(i=null),n!=null)return this.where(n).lastOrDefault(null,i);var r,u=!1;return this.forEach(function(n){u=!0;r=n}),u?r:i};i.prototype.single=function(n){if(n!=null)return this.where(n).single();var i,t=!1;if(this.forEach(function(n){if(t)throw new Error("single:sequence contains more than one element.");else t=!0,i=n}),!t)throw new Error("single:No element satisfies the condition.");return i};i.prototype.singleOrDefault=function(n,i){if(i===t&&(i=null),n!=null)return this.where(n).singleOrDefault(null,i);var u,r=!1;return this.forEach(function(n){if(r)throw new Error("single:sequence contains more than one element.");else r=!0,u=n}),r?u:i};i.prototype.skip=function(n){var t=this;return new i(function(){var i,f=0;return new u(function(){for(i=t.getEnumerator();f++<n&&i.moveNext(););},function(){return i.moveNext()?this.yieldReturn(i.getCurrent()):!1},function(){r.dispose(i)})})};i.prototype.skipWhile=function(n){n=r.createLambda(n);var t=this;return new i(function(){var i,e=0,f=!1;return new u(function(){i=t.getEnumerator()},function(){while(!f)if(i.moveNext()){if(!n(i.getCurrent(),e++))return f=!0,this.yieldReturn(i.getCurrent());continue}else return!1;return i.moveNext()?this.yieldReturn(i.getCurrent()):!1},function(){r.dispose(i)})})};i.prototype.take=function(n){var t=this;return new i(function(){var i,f=0;return new u(function(){i=t.getEnumerator()},function(){return f++<n&&i.moveNext()?this.yieldReturn(i.getCurrent()):!1},function(){r.dispose(i)})})};i.prototype.takeWhile=function(n){n=r.createLambda(n);var t=this;return new i(function(){var i,f=0;return new u(function(){i=t.getEnumerator()},function(){return i.moveNext()&&n(i.getCurrent(),f++)?this.yieldReturn(i.getCurrent()):!1},function(){r.dispose(i)})})};i.prototype.takeExceptLast=function(n){n==null&&(n=1);var t=this;return new i(function(){if(n<=0)return t.getEnumerator();var i,f=[];return new u(function(){i=t.getEnumerator()},function(){while(i.moveNext()){if(f.length==n)return f.push(i.getCurrent()),this.yieldReturn(f.shift());f.push(i.getCurrent())}return!1},function(){r.dispose(i)})})};i.prototype.takeFromLast=function(n){if(n<=0||n==null)return i.empty();var t=this;return new i(function(){var o,f,e=[];return new u(function(){o=t.getEnumerator()},function(){if(f==null){while(o.moveNext())e.length==n&&e.shift(),e.push(o.getCurrent());f=i.from(e).getEnumerator()}return f.moveNext()?this.yieldReturn(f.getCurrent()):!1},function(){r.dispose(f)})})};i.prototype.indexOf=function(n,t){var i=null;return typeof n===o.Function?this.forEach(function(t,r){if(n(t,r))return i=r,!1}):(t=t||System.Collections.Generic.EqualityComparer$1.$default,this.forEach(function(r,u){if(t.equals2(r,n))return i=u,!1})),i!==null?i:-1};i.prototype.lastIndexOf=function(n,t){var i=-1;return typeof n===o.Function?this.forEach(function(t,r){n(t,r)&&(i=r)}):(t=t||System.Collections.Generic.EqualityComparer$1.$default,this.forEach(function(r,u){t.equals2(r,n)&&(i=u)})),i};i.prototype.asEnumerable=function(){return i.from(this)};i.prototype.toArray=function(){var n=[];return this.forEach(function(t){n.push(t)}),n};i.prototype.toList=function(n){var t=[];return this.forEach(function(n){t.push(n)}),new System.Collections.Generic.List$1(n||Object)(t)};i.prototype.toLookup=function(n,t,i){n=r.createLambda(n);t=r.createLambda(t);var u=new System.Collections.Generic.Dictionary$2(Object,Object)(null,i),f=[];return this.forEach(function(i){var r=n(i),e=t(i),o={v:null};u.tryGetValue(r,o)?o.v.push(e):(f.push(r),u.add(r,[e]))}),new w(u,f)};i.prototype.toObject=function(n,t){n=r.createLambda(n);t=r.createLambda(t);var i={};return this.forEach(function(r){i[n(r)]=t(r)}),i};i.prototype.toDictionary=function(n,t,i,u,f){n=r.createLambda(n);t=r.createLambda(t);var e=new System.Collections.Generic.Dictionary$2(i,u)(null,f);return this.forEach(function(i){e.add(n(i),t(i))}),e};i.prototype.toJSONString=function(n,t){if(typeof JSON===o.Undefined||JSON.stringify==null)throw new Error("toJSONString can't find JSON.stringify. This works native JSON support Browser or include json2.js");return JSON.stringify(this.toArray(),n,t)};i.prototype.toJoinedString=function(n,t){return n==null&&(n=""),t==null&&(t=f.Identity),this.select(t).toArray().join(n)};i.prototype.doAction=function(n){var t=this;return n=r.createLambda(n),new i(function(){var i,f=0;return new u(function(){i=t.getEnumerator()},function(){return i.moveNext()?(n(i.getCurrent(),f++),this.yieldReturn(i.getCurrent())):!1},function(){r.dispose(i)})})};i.prototype.forEach=function(n){n=r.createLambda(n);var i=0,t=this.getEnumerator();try{while(t.moveNext())if(n(t.getCurrent(),i++)===!1)break}finally{r.dispose(t)}};i.prototype.write=function(n,t){n==null&&(n="");t=r.createLambda(t);var i=!0;this.forEach(function(r){i?i=!1:document.write(n);document.write(t(r))})};i.prototype.writeLine=function(n){n=r.createLambda(n);this.forEach(function(t){document.writeln(n(t)+"<br />")})};i.prototype.force=function(){var n=this.getEnumerator();try{while(n.moveNext());}finally{r.dispose(n)}};i.prototype.letBind=function(n){n=r.createLambda(n);var t=this;return new i(function(){var f;return new u(function(){f=i.from(n(t)).getEnumerator()},function(){return f.moveNext()?this.yieldReturn(f.getCurrent()):!1},function(){r.dispose(f)})})};i.prototype.share=function(){var i=this,n,t=!1;return new v(function(){return new u(function(){n==null&&(n=i.getEnumerator())},function(){if(t)throw new Error("enumerator is disposed");return n.moveNext()?this.yieldReturn(n.getCurrent()):!1},f.Blank)},function(){t=!0;r.dispose(n)})};i.prototype.memoize=function(){var e=this,n,t,i=!1;return new v(function(){var r=-1;return new u(function(){t==null&&(t=e.getEnumerator(),n=[])},function(){if(i)throw new Error("enumerator is disposed");return(r++,n.length<=r)?t.moveNext()?this.yieldReturn(n[r]=t.getCurrent()):!1:this.yieldReturn(n[r])},f.Blank)},function(){i=!0;r.dispose(t);n=null})};i.prototype.catchError=function(n){n=r.createLambda(n);var t=this;return new i(function(){var i;return new u(function(){i=t.getEnumerator()},function(){try{return i.moveNext()?this.yieldReturn(i.getCurrent()):!1}catch(t){return n(t),!1}},function(){r.dispose(i)})})};i.prototype.finallyAction=function(n){n=r.createLambda(n);var t=this;return new i(function(){var i;return new u(function(){i=t.getEnumerator()},function(){return i.moveNext()?this.yieldReturn(i.getCurrent()):!1},function(){try{r.dispose(i)}finally{n()}})})};i.prototype.log=function(n){return n=r.createLambda(n),this.doAction(function(t){typeof console!==o.Undefined&&console.log(n(t))})};i.prototype.trace=function(n,t){return n==null&&(n="Trace"),t=r.createLambda(t),this.doAction(function(i){typeof console!==o.Undefined&&console.log(n,t(i))})};s=function(n,t,i,u,f){this.source=n;this.keySelector=r.createLambda(t);this.comparer=i||System.Collections.Generic.Comparer$1.$default;this.descending=u;this.parent=f};s.prototype=new i;s.prototype.createOrderedEnumerable=function(n,t,i){return new s(this.source,n,t,i,this)};s.prototype.thenBy=function(n,t){return this.createOrderedEnumerable(n,t,!1)};s.prototype.thenByDescending=function(n,t){return this.createOrderedEnumerable(n,t,!0)};s.prototype.getEnumerator=function(){var i=this,t,n,r=0;return new u(function(){t=[];n=[];i.source.forEach(function(i,r){t.push(i);n.push(r)});var r=a.create(i,null);r.GenerateKeys(t);n.sort(function(n,t){return r.compare(n,t)})},function(){return r<n.length?this.yieldReturn(t[n[r++]]):!1},f.Blank)};a=function(n,t,i,r){this.keySelector=n;this.comparer=t;this.descending=i;this.child=r;this.keys=null};a.create=function(n,t){var i=new a(n.keySelector,n.comparer,n.descending,t);return n.parent!=null?a.create(n.parent,i):i};a.prototype.GenerateKeys=function(n){for(var i=n.length,u=this.keySelector,r=new Array(i),t=0;t<i;t++)r[t]=u(n[t]);this.keys=r;this.child!=null&&this.child.GenerateKeys(n)};a.prototype.compare=function(n,t){var i=this.comparer.compare(this.keys[n],this.keys[t]);return i==0?this.child!=null?this.child.compare(n,t):r.compare(n,t):this.descending?-i:i};v=function(n,t){this.dispose=t;i.call(this,n)};v.prototype=new i;e=function(n){this.getSource=function(){return n}};e.prototype=new i;e.prototype.any=function(n){return n==null?this.getSource().length>0:i.prototype.any.apply(this,arguments)};e.prototype.count=function(n){return n==null?this.getSource().length:i.prototype.count.apply(this,arguments)};e.prototype.elementAt=function(n){var t=this.getSource();return 0<=n&&n<t.length?t[n]:i.prototype.elementAt.apply(this,arguments)};e.prototype.elementAtOrDefault=function(n,i){i===t&&(i=null);var r=this.getSource();return 0<=n&&n<r.length?r[n]:i};e.prototype.first=function(n){var t=this.getSource();return n==null&&t.length>0?t[0]:i.prototype.first.apply(this,arguments)};e.prototype.firstOrDefault=function(n,r){if(r===t&&(r=null),n!=null)return i.prototype.firstOrDefault.apply(this,arguments);var u=this.getSource();return u.length>0?u[0]:r};e.prototype.last=function(n){var t=this.getSource();return n==null&&t.length>0?t[t.length-1]:i.prototype.last.apply(this,arguments)};e.prototype.lastOrDefault=function(n,r){if(r===t&&(r=null),n!=null)return i.prototype.lastOrDefault.apply(this,arguments);var u=this.getSource();return u.length>0?u[u.length-1]:r};e.prototype.skip=function(n){var t=this.getSource();return new i(function(){var i;return new u(function(){i=n<0?0:n},function(){return i<t.length?this.yieldReturn(t[i++]):!1},f.Blank)})};e.prototype.takeExceptLast=function(n){return n==null&&(n=1),this.take(this.getSource().length-n)};e.prototype.takeFromLast=function(n){return this.skip(this.getSource().length-n)};e.prototype.reverse=function(){var n=this.getSource();return new i(function(){var t;return new u(function(){t=n.length},function(){return t>0?this.yieldReturn(n[--t]):!1},f.Blank)})};e.prototype.sequenceEqual=function(n,t){return(n instanceof e||n instanceof Array)&&t==null&&i.from(n).count()!=this.count()?!1:i.prototype.sequenceEqual.apply(this,arguments)};e.prototype.toJoinedString=function(n,t){var r=this.getSource();return t!=null||!(r instanceof Array)?i.prototype.toJoinedString.apply(this,arguments):(n==null&&(n=""),r.join(n))};e.prototype.getEnumerator=function(){return new Bridge.ArrayEnumerator(this.getSource())};h=function(n,t){this.prevSource=n;this.prevPredicate=t};h.prototype=new i;h.prototype.where=function(n){if(n=r.createLambda(n),n.length<=1){var t=this.prevPredicate,u=function(i){return t(i)&&n(i)};return new h(this.prevSource,u)}return i.prototype.where.call(this,n)};h.prototype.select=function(n){return n=r.createLambda(n),n.length<=1?new c(this.prevSource,this.prevPredicate,n):i.prototype.select.call(this,n)};h.prototype.getEnumerator=function(){var t=this.prevPredicate,i=this.prevSource,n;return new u(function(){n=i.getEnumerator()},function(){while(n.moveNext())if(t(n.getCurrent()))return this.yieldReturn(n.getCurrent());return!1},function(){r.dispose(n)})};c=function(n,t,i){this.prevSource=n;this.prevPredicate=t;this.prevSelector=i};c.prototype=new i;c.prototype.where=function(n){return n=r.createLambda(n),n.length<=1?new h(this,n):i.prototype.where.call(this,n)};c.prototype.select=function(n){if(n=r.createLambda(n),n.length<=1){var t=this.prevSelector,u=function(i){return n(t(i))};return new c(this.prevSource,this.prevPredicate,u)}return i.prototype.select.call(this,n)};c.prototype.getEnumerator=function(){var t=this.prevPredicate,i=this.prevSelector,f=this.prevSource,n;return new u(function(){n=f.getEnumerator()},function(){while(n.moveNext())if(t==null||t(n.getCurrent()))return this.yieldReturn(i(n.getCurrent()));return!1},function(){r.dispose(n)})};w=function(n,t){this.count=function(){return n.getCount()};this.get=function(t){var r={v:null},u=n.tryGetValue(t,r);return i.from(u?r.v:[])};this.contains=function(t){return n.containsKey(t)};this.toEnumerable=function(){return i.from(t).select(function(t){return new y(t,n.get(t))})};this.getEnumerator=function(){return this.toEnumerable().getEnumerator()}};System.Collections.IEnumerable.$$inheritors=System.Collections.IEnumerable.$$inheritors||[];System.Collections.IEnumerable.$$inheritors.push(w);y=function(n,t){this.key=function(){return n};e.call(this,t)};y.prototype=new e;typeof define===o.Function&&define.amd?define("linqjs",[],function(){return i}):typeof module!==o.Undefined&&module.exports?module.exports=i:n.Enumerable=i;Bridge.Linq={};Bridge.Linq.Enumerable=i;System.Linq={};System.Linq.Enumerable=i}(Bridge.global);Bridge.define("System.Random",{statics:{MBIG:2147483647,MSEED:161803398,MZ:0},inext:0,inextp:0,seedArray:null,config:{init:function(){this.seedArray=System.Array.init(56,0)}},constructor:function(){System.Random.prototype.constructor$1.call(this,System.Int64.clip32(System.Int64((new Date).getTime()).mul(1e4)))},constructor$1:function(n){var e,u,i,o=n===-2147483648?2147483647:Math.abs(n),r,f,t;for(u=System.Random.MSEED-o|0,this.seedArray[55]=u,i=1,r=1;r<55;r=r+1|0)e=(21*r|0)%55,this.seedArray[e]=i,i=u-i|0,i<0&&(i=i+System.Random.MBIG|0),u=this.seedArray[e];for(f=1;f<5;f=f+1|0)for(t=1;t<56;t=t+1|0)this.seedArray[t]=this.seedArray[t]-this.seedArray[1+(t+30|0)%55|0]|0,this.seedArray[t]<0&&(this.seedArray[t]=this.seedArray[t]+System.Random.MBIG|0);this.inext=0;this.inextp=21;n=1},sample:function(){return this.internalSample()*46566128752457969e-26},internalSample:function(){var n,t=this.inext,i=this.inextp;return(t=t+1|0)>=56&&(t=1),(i=i+1|0)>=56&&(i=1),n=this.seedArray[t]-this.seedArray[i]|0,n===System.Random.MBIG&&(n=n-1|0),n<0&&(n=n+System.Random.MBIG|0),this.seedArray[t]=n,this.inext=t,this.inextp=i,n},next:function(){return this.internalSample()},next$2:function(n,t){if(n>t)throw new System.ArgumentOutOfRangeException("minValue","'minValue' cannot be greater than maxValue.");var i=System.Int64(t).sub(System.Int64(n));return i.lte(System.Int64(2147483647))?Bridge.Int.clip32(this.sample()*System.Int64.toNumber(i))+n|0:System.Int64.clip32(Bridge.Int.clip64(this.getSampleForLargeRange()*System.Int64.toNumber(i)).add(System.Int64(n)))},next$1:function(n){if(n<0)throw new System.ArgumentOutOfRangeException("maxValue","'maxValue' must be greater than zero.");return Bridge.Int.clip32(this.sample()*n)},getSampleForLargeRange:function(){var n=this.internalSample(),i=this.internalSample()%2==0?!0:!1,t;return i&&(n=-n|0),t=n,t+=2147483646,t/4294967293},nextDouble:function(){return this.sample()},nextBytes:function(n){if(n==null)throw new System.ArgumentNullException("buffer");for(var t=0;t<n.length;t=t+1|0)n[t]=this.internalSample()%256&255}});Bridge.define("System.Guid",{inherits:function(){return[System.IComparable$1(System.Guid),System.IEquatable$1(System.Guid),System.IFormattable]},statics:{$valid:/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/ig,$split:/^(.{8})(.{4})(.{4})(.{4})(.{12})$/,empty:"00000000-0000-0000-0000-000000000000",config:{init:function(){this.$rng=new System.Random}},instanceOf:function(n){return typeof n=="string"&&n.match(System.Guid.$valid)},getDefaultValue:function(){return System.Guid.empty},parse:function(n,t){var i={};if(System.Guid.tryParse(n,t,i))return i.v;throw new System.FormatException("Unable to parse UUID");},tryParse:function(n,t,i){var r,u;if(i.v=System.Guid.empty,!Bridge.hasValue(n))throw new System.ArgumentNullException("uuid");if(t){if(t=t.toUpperCase(),t==="N"){if(r=System.Guid.$split.exec(n),!r)return!1;n=r.slice(1).join("-")}else if(t==="B"||t==="P"){if(u=t==="B",n[0]!==(u?"{":"(")||n[n.length-1]!==(u?"}":")"))return!1;n=n.substr(1,n.length-2)}if(n.match(System.Guid.$valid))return i.v=n.toLowerCase(),!0}else if(r=/^[{(]?([0-9a-f]{8})-?([0-9a-f]{4})-?([0-9a-f]{4})-?([0-9a-f]{4})-?([0-9a-f]{12})[)}]?$/ig.exec(n),r)return i.v=r.slice(1).join("-").toLowerCase(),!0;return!1},format:function(n,t){switch(t){case"n":case"N":return n.replace(/-/g,"");case"b":case"B":return"{"+n+"}";case"p":case"P":return"("+n+")";default:return n}},fromBytes:function(n){if(!n||n.length!==16)throw new System.ArgumentException("b","Must be 16 bytes");var t=n.map(function(n){return Bridge.Int.format(n&255,"x2")}).join("");return System.Guid.$split.exec(t).slice(1).join("-")},newGuid:function(){var n=Array(16);return System.Guid.$rng.nextBytes(n),n[6]=n[6]&15|64,n[8]=n[8]&191|128,System.Guid.fromBytes(n)},getBytes:function(n){for(var i=Array(16),r=n.replace(/-/g,""),t=0;t<16;t++)i[t]=parseInt(r.substr(t*2,2),16);return i}}});Bridge.define("System.Text.RegularExpressions.Regex",{statics:{_cacheSize:15,_defaultMatchTimeout:System.TimeSpan.fromMilliseconds(-1),getCacheSize:function(){return System.Text.RegularExpressions.Regex._cacheSize},setCacheSize:function(n){if(n<0)throw new System.ArgumentOutOfRangeException("value");System.Text.RegularExpressions.Regex._cacheSize=n},escape:function(n){if(n==null)throw new System.ArgumentNullException("str");return System.Text.RegularExpressions.RegexParser.escape(n)},unescape:function(n){if(n==null)throw new System.ArgumentNullException("str");return System.Text.RegularExpressions.RegexParser.unescape(n)},isMatch:function(n,t){var i=System.Text.RegularExpressions;return i.Regex.isMatch$2(n,t,i.RegexOptions.None,i.Regex._defaultMatchTimeout)},isMatch$1:function(n,t,i){var r=System.Text.RegularExpressions;return r.Regex.isMatch$2(n,t,i,r.Regex._defaultMatchTimeout)},isMatch$2:function(n,t,i,r){var u=new System.Text.RegularExpressions.Regex("constructor$3",t,i,r,!0);return u.isMatch(n)},match:function(n,t){var i=System.Text.RegularExpressions;return i.Regex.match$2(n,t,i.RegexOptions.None,i.Regex._defaultMatchTimeout)},match$1:function(n,t,i){var r=System.Text.RegularExpressions;return r.Regex.match$2(n,t,i,r.Regex._defaultMatchTimeout)},match$2:function(n,t,i,r){var u=new System.Text.RegularExpressions.Regex("constructor$3",t,i,r,!0);return u.match(n)},matches:function(n,t){var i=System.Text.RegularExpressions;return i.Regex.matches$2(n,t,i.RegexOptions.None,i.Regex._defaultMatchTimeout)},matches$1:function(n,t,i){var r=System.Text.RegularExpressions;return r.Regex.matches$2(n,t,i,r.Regex._defaultMatchTimeout)},matches$2:function(n,t,i,r){var u=new System.Text.RegularExpressions.Regex("constructor$3",t,i,r,!0);return u.matches(n)},replace:function(n,t,i){var r=System.Text.RegularExpressions;return r.Regex.replace$2(n,t,i,r.RegexOptions.None,r.Regex._defaultMatchTimeout)},replace$1:function(n,t,i,r){var u=System.Text.RegularExpressions;return u.Regex.replace$2(n,t,i,r,u.Regex._defaultMatchTimeout)},replace$2:function(n,t,i,r,u){var f=new System.Text.RegularExpressions.Regex("constructor$3",t,r,u,!0);return f.replace(n,i)},replace$3:function(n,t,i){var r=System.Text.RegularExpressions;return r.Regex.replace$5(n,t,i,r.RegexOptions.None,r.Regex._defaultMatchTimeout)},replace$4:function(n,t,i,r){var u=System.Text.RegularExpressions;return u.Regex.replace$5(n,t,i,r,u.Regex._defaultMatchTimeout)},replace$5:function(n,t,i,r,u){var f=new System.Text.RegularExpressions.Regex("constructor$3",t,r,u,!0);return f.replace$3(n,i)},split:function(n,t){var i=System.Text.RegularExpressions;return i.Regex.split$2(n,t,i.RegexOptions.None,i.Regex._defaultMatchTimeout)},split$1:function(n,t,i){var r=System.Text.RegularExpressions;return r.Regex.split$2(n,t,i,r.Regex._defaultMatchTimeout)},split$2:function(n,t,i,r){var u=new System.Text.RegularExpressions.Regex("constructor$3",t,i,r,!0);return u.split(n)}},_pattern:"",_matchTimeout:System.TimeSpan.fromMilliseconds(-1),_runner:null,_caps:null,_capsize:0,_capnames:null,_capslist:null,config:{init:function(){this._options=System.Text.RegularExpressions.RegexOptions.None}},constructor:function(n){this.constructor$1(n,System.Text.RegularExpressions.RegexOptions.None)},constructor$1:function(n,t){this.constructor$2(n,t,System.TimeSpan.fromMilliseconds(-1))},constructor$2:function(n,t,i){this.constructor$3(n,t,i,!1)},constructor$3:function(n,t,i){var r=System.Text.RegularExpressions,e,s,f,u,o;if(n==null)throw new System.ArgumentNullException("pattern");if(t<r.RegexOptions.None||t>>10!=0)throw new System.ArgumentOutOfRangeException("options");if((t&r.RegexOptions.ECMAScript)!=0&&(t&~(r.RegexOptions.ECMAScript|r.RegexOptions.IgnoreCase|r.RegexOptions.Multiline|r.RegexOptions.CultureInvariant))!=0)throw new System.ArgumentOutOfRangeException("options");if(e=System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Multiline|System.Text.RegularExpressions.RegexOptions.Singleline|System.Text.RegularExpressions.RegexOptions.IgnorePatternWhitespace,(t|e)!==e)throw new System.NotSupportedException("Specified Regex options are not supported.");for(this._validateMatchTimeout(i),this._pattern=n,this._options=t,this._matchTimeout=i,this._runner=new r.RegexRunner(this),s=this._runner.parsePattern(),f=s.sparseSettings.sparseSlotNames,this._capsize=f.length,this._capslist=[],this._capnames={},u=0;u<f.length;u++)o=f[u],this._capslist.push(o),this._capnames[o]=u},getMatchTimeout:function(){return this._matchTimeout},getOptions:function(){return this._options},getRightToLeft:function(){return(this._options&System.Text.RegularExpressions.RegexOptions.RightToLeft)!=0},isMatch:function(n){if(n==null)throw new System.ArgumentNullException("input");var t=this.getRightToLeft()?n.length:0;return this.isMatch$1(n,t)},isMatch$1:function(n,t){if(n==null)throw new System.ArgumentNullException("input");var i=this._runner.run(!0,-1,n,0,n.length,t);return i==null},match:function(n){if(n==null)throw new System.ArgumentNullException("input");var t=this.getRightToLeft()?n.length:0;return this.match$1(n,t)},match$1:function(n,t){if(n==null)throw new System.ArgumentNullException("input");return this._runner.run(!1,-1,n,0,n.length,t)},match$2:function(n,t,i){if(n==null)throw new System.ArgumentNullException("input");var r=this.getRightToLeft()?t+i:t;return this._runner.run(!1,-1,n,t,i,r)},matches:function(n){if(n==null)throw new System.ArgumentNullException("input");var t=this.getRightToLeft()?n.length:0;return this.matches$1(n,t)},matches$1:function(n,t){if(n==null)throw new System.ArgumentNullException("input");return new System.Text.RegularExpressions.MatchCollection(this,n,0,n.length,t)},getGroupNames:function(){if(this._capslist==null){for(var i=System.Globalization.CultureInfo.invariantCulture,t=[],r=this._capsize,n=0;n<r;n++)t[n]=System.Convert.toString(n,i,System.Convert.typeCodes.Int32);return t}return this._capslist.slice()},getGroupNumbers:function(){var t=this._caps,n,i,u,r;if(t==null)for(n=[],u=this._capsize,r=0;r<u;r++)n.push(r);else{n=[];for(i in t)t.hasOwnProperty(i)&&(n[t[i]]=i)}return n},groupNameFromNumber:function(n){var i,t;return this._capslist==null?n>=0&&n<this._capsize?(i=System.Globalization.CultureInfo.invariantCulture,System.Convert.toString(n,i,System.Convert.typeCodes.Int32)):"":this._caps!=null?(t=this._caps[n],t==null)?"":parseInt(t):n>=0&&n<this._capslist.length?this._capslist[n]:""},groupNumberFromName:function(n){var u,t,i,r;if(n==null)throw new System.ArgumentNullException("name");if(this._capnames!=null)return(u=this._capnames[n],u==null)?-1:parseInt(u);for(t=0,r=0;r<n.Length;r++){if(i=n[r],i>"9"||i<"0")return-1;t*=10;t+=i-"0"}return t>=0&&t<this._capsize?t:-1},replace:function(n,t){if(n==null)throw new System.ArgumentNullException("input");var i=this.getRightToLeft()?n.length:0;return this.replace$2(n,t,-1,i)},replace$1:function(n,t,i){if(n==null)throw new System.ArgumentNullException("input");var r=this.getRightToLeft()?n.length:0;return this.replace$2(n,t,i,r)},replace$2:function(n,t,i,r){if(n==null)throw new System.ArgumentNullException("input");if(t==null)throw new System.ArgumentNullException("replacement");var u=System.Text.RegularExpressions.RegexParser.parseReplacement(t,this._caps,this._capsize,this._capnames,this._options);return u.replace(this,n,i,r)},replace$3:function(n,t){if(n==null)throw new System.ArgumentNullException("input");var i=this.getRightToLeft()?n.length:0;return this.replace$5(n,t,-1,i)},replace$4:function(n,t,i){if(n==null)throw new System.ArgumentNullException("input");var r=this.getRightToLeft()?n.length:0;return this.replace$5(n,t,i,r)},replace$5:function(n,t,i,r){if(n==null)throw new System.ArgumentNullException("input");return System.Text.RegularExpressions.RegexReplacement.replace(t,this,n,i,r)},split:function(n){if(n==null)throw new System.ArgumentNullException("input");var t=this.getRightToLeft()?n.length:0;return this.split$2(n,0,t)},split$1:function(n,t){if(n==null)throw new System.ArgumentNullException("input");var i=this.getRightToLeft()?n.length:0;return this.split$2(n,t,i)},split$2:function(n,t,i){if(n==null)throw new System.ArgumentNullException("input");return System.Text.RegularExpressions.RegexReplacement.split(this,n,t,i)},_validateMatchTimeout:function(n){var t=n.getTotalMilliseconds();if(-1!==t&&(!(t>0)||!(t<=2147483646)))throw new System.ArgumentOutOfRangeException("matchTimeout");}});Bridge.define("System.Text.RegularExpressions.Capture",{_text:"",_index:0,_length:0,constructor:function(n,t,i){this._text=n;this._index=t;this._length=i},getIndex:function(){return this._index},getLength:function(){return this._length},getValue:function(){return this._text.substr(this._index,this._length)},toString:function(){return this.getValue()},_getOriginalString:function(){return this._text},_getLeftSubstring:function(){return this._text.slice(0,_index)},_getRightSubstring:function(){return this._text.slice(this._index+this._length,this._text.length)}});Bridge.define("System.Text.RegularExpressions.CaptureCollection",{inherits:function(){return[System.Collections.ICollection]},_group:null,_capcount:0,_captures:null,constructor:function(n){this._group=n;this._capcount=n._capcount},getSyncRoot:function(){return this._group},getIsSynchronized:function(){return!1},getIsReadOnly:function(){return!0},getCount:function(){return this._capcount},get:function(n){if(n===this._capcount-1&&n>=0)return this._group;if(n>=this._capcount||n<0)throw new System.ArgumentOutOfRangeException("i");return this._ensureCapturesInited(),this._captures[n]},copyTo:function(n,t){if(n==null)throw new System.ArgumentNullException("array");if(n.length<t+this._capcount)throw new System.IndexOutOfRangeException;for(var u,r=t,i=0;i<this._capcount;r++,i++)u=this.get(i),System.Array.set(n,u,[r])},getEnumerator:function(){return new System.Text.RegularExpressions.CaptureEnumerator(this)},_ensureCapturesInited:function(){var t,n,i,r;if(this._captures==null){for(t=[],t.length=this._capcount,n=0;n<this._capcount-1;n++)i=this._group._caps[n*2],r=this._group._caps[n*2+1],t[n]=new System.Text.RegularExpressions.Capture(this._group._text,i,r);this._capcount>0&&(t[this._capcount-1]=this._group);this._captures=t}}});Bridge.define("System.Text.RegularExpressions.CaptureEnumerator",{inherits:function(){return[System.Collections.IEnumerator]},_captureColl:null,_curindex:0,constructor:function(n){this._curindex=-1;this._captureColl=n},moveNext:function(){var n=this._captureColl.getCount();return this._curindex>=n?!1:(this._curindex++,this._curindex<n)},getCurrent:function(){return this.getCapture()},getCapture:function(){if(this._curindex<0||this._curindex>=this._captureColl.getCount())throw new System.InvalidOperationException("Enumeration has either not started or has already finished.");return this._captureColl.get(this._curindex)},reset:function(){this._curindex=-1}});Bridge.define("System.Text.RegularExpressions.Group",{inherits:function(){return[System.Text.RegularExpressions.Capture]},statics:{config:{init:function(){var n=new System.Text.RegularExpressions.Group("",[],0);this.getEmpty=function(){return n}}},synchronized:function(n){if(n==null)throw new System.ArgumentNullException("group");var t=n.getCaptures();return t.getCount()>0&&t.get(0),n}},_caps:null,_capcount:0,_capColl:null,constructor:function(n,t,i){var r=System.Text.RegularExpressions,u=i===0?0:t[(i-1)*2],f=i===0?0:t[i*2-1];r.Capture.prototype.$constructor.call(this,n,u,f);this._caps=t;this._capcount=i},getSuccess:function(){return this._capcount!==0},getCaptures:function(){return this._capColl==null&&(this._capColl=new System.Text.RegularExpressions.CaptureCollection(this)),this._capColl}});Bridge.define("System.Text.RegularExpressions.GroupCollection",{inherits:function(){return[System.Collections.ICollection]},_match:null,_captureMap:null,_groups:null,constructor:function(n,t){this._match=n;this._captureMap=t},getSyncRoot:function(){return this._match},getIsSynchronized:function(){return!1},getIsReadOnly:function(){return!0},getCount:function(){return this._match._matchcount.length},get:function(n){return this._getGroup(n)},getByName:function(n){if(this._match._regex==null)return System.Text.RegularExpressions.Group.getEmpty();var t=this._match._regex.groupNumberFromName(n);return this._getGroup(t)},copyTo:function(n,t){var r,f,u,i;if(n==null)throw new System.ArgumentNullException("array");if(r=this.getCount(),n.length<t+r)throw new System.IndexOutOfRangeException;for(u=t,i=0;i<r;u++,i++)f=this._getGroup(i),System.Array.set(n,f,[u])},getEnumerator:function(){return new System.Text.RegularExpressions.GroupEnumerator(this)},_getGroup:function(n){var t,i;return this._captureMap!=null?(i=this._captureMap[n],t=i==null?System.Text.RegularExpressions.Group.getEmpty():this._getGroupImpl(i)):t=n>=this._match._matchcount.length||n<0?System.Text.RegularExpressions.Group.getEmpty():this._getGroupImpl(n),t},_getGroupImpl:function(n){return n===0?this._match:(this._ensureGroupsInited(),this._groups[n])},_ensureGroupsInited:function(){var n,i,r,u,t;if(this._groups==null){for(n=[],n.length=this._match._matchcount.length,n.length>0&&(n[0]=this._match),t=0;t<n.length-1;t++)i=this._match._text,r=this._match._matches[t+1],u=this._match._matchcount[t+1],n[t+1]=new System.Text.RegularExpressions.Group(i,r,u);this._groups=n}}});Bridge.define("System.Text.RegularExpressions.GroupEnumerator",{inherits:function(){return[System.Collections.IEnumerator]},_groupColl:null,_curindex:0,constructor:function(n){this._curindex=-1;this._groupColl=n},moveNext:function(){var n=this._groupColl.getCount();return this._curindex>=n?!1:(this._curindex++,this._curindex<n)},getCurrent:function(){return this.getCapture()},getCapture:function(){if(this._curindex<0||this._curindex>=this._groupColl.getCount())throw new System.InvalidOperationException("Enumeration has either not started or has already finished.");return this._groupColl.get(this._curindex)},reset:function(){this._curindex=-1}});Bridge.define("System.Text.RegularExpressions.Match",{inherits:function(){return[System.Text.RegularExpressions.Group]},statics:{config:{init:function(){var n=new System.Text.RegularExpressions.Match(null,1,"",0,0,0);this.getEmpty=function(){return n}}},synchronized:function(n){if(n==null)throw new System.ArgumentNullException("match");for(var i=n.getGroups(),u=i.getCount(),r,t=0;t<u;t++)r=i.get(t),System.Text.RegularExpressions.Group.synchronized(r);return n}},_regex:null,_matchcount:null,_matches:null,_textbeg:0,_textend:0,_textstart:0,_balancing:!1,_groupColl:null,_textpos:0,constructor:function(n,t,i,r,u,f){var s=System.Text.RegularExpressions,o=[0,0],e;for(s.Group.prototype.$constructor.call(this,i,o,0),this._regex=n,this._matchcount=[],this._matchcount.length=t,e=0;e<t;e++)this._matchcount[e]=0;this._matches=[];this._matches.length=t;this._matches[0]=o;this._textbeg=r;this._textend=r+u;this._textstart=f;this._balancing=!1},getGroups:function(){return this._groupColl==null&&(this._groupColl=new System.Text.RegularExpressions.GroupCollection(this,null)),this._groupColl},nextMatch:function(){return this._regex==null?this:this._regex._runner.run(!1,this._length,this._text,this._textbeg,this._textend-this._textbeg,this._textpos)},result:function(n){if(n==null)throw new System.ArgumentNullException("replacement");if(this._regex==null)throw new System.NotSupportedException("Result cannot be called on a failed Match.");var t=System.Text.RegularExpressions.RegexParser.parseReplacement(n,this._regex._caps,this._regex._capsize,this._regex._capnames,this._regex._options);return t.replacement(this)},_isMatched:function(n){return n<this._matchcount.length&&this._matchcount[n]>0&&this._matches[n][this._matchcount[n]*2-1]!==-2},_addMatch:function(n,t,i){var r,e,f,u;if(this._matches[n]==null&&(this._matches[n]=new Array(2)),r=this._matchcount[n],r*2+2>this._matches[n].length){for(e=this._matches[n],f=new Array(r*8),u=0;u<r*2;u++)f[u]=e[u];this._matches[n]=f}this._matches[n][r*2]=t;this._matches[n][r*2+1]=i;this._matchcount[n]=r+1},_tidy:function(n){var e=this._matches[0],i,t,r,f,u;if(this._index=e[0],this._length=e[1],this._textpos=n,this._capcount=this._matchcount[0],this._balancing){for(i=0;i<this._matchcount.length;i++){for(f=this._matchcount[i]*2,u=this._matches[i],t=0;t<f;t++)if(u[t]<0)break;for(r=t;t<f;t++)u[t]<0?r--:(t!==r&&(u[r]=u[t]),r++);this._matchcount[i]=r/2}this._balancing=!1}},_groupToStringImpl:function(n){var t=this._matchcount[n];if(t===0)return"";var i=this._matches[n],r=i[(t-1)*2],u=i[t*2-1];return this._text.slice(r,r+u)},_lastGroupToStringImpl:function(){return this._groupToStringImpl(this._matchcount.length-1)}});Bridge.define("System.Text.RegularExpressions.MatchSparse",{inherits:function(){return[System.Text.RegularExpressions.Match]},_caps:null,constructor:function(n,t,i,r,u,f,e){var o=System.Text.RegularExpressions;o.Match.prototype.$constructor.call(this,n,i,r,u,f,e);this._caps=t},getGroups:function(){return this._groupColl==null&&(this._groupColl=new System.Text.RegularExpressions.GroupCollection(this,this._caps)),this._groupColl}});Bridge.define("System.Text.RegularExpressions.MatchCollection",{inherits:function(){return[System.Collections.ICollection]},_regex:null,_input:null,_beginning:0,_length:0,_startat:0,_prevlen:0,_matches:null,_done:!1,constructor:function(n,t,i,r,u){if(u<0||u>t.Length)throw new System.ArgumentOutOfRangeException("startat");this._regex=n;this._input=t;this._beginning=i;this._length=r;this._startat=u;this._prevlen=-1;this._matches=[]},getCount:function(){return this._done||this._getMatch(2147483647),this._matches.length},getSyncRoot:function(){return this},getIsSynchronized:function(){return!1},getIsReadOnly:function(){return!0},get:function(n){var t=this._getMatch(n);if(t==null)throw new System.ArgumentOutOfRangeException("i");return t},copyTo:function(n,t){var r,f,u,i;if(n==null)throw new System.ArgumentNullException("array");if(r=this.getCount(),n.length<t+r)throw new System.IndexOutOfRangeException;for(u=t,i=0;i<r;u++,i++)f=this._getMatch(i),System.Array.set(n,f,[u])},getEnumerator:function(){return new System.Text.RegularExpressions.MatchEnumerator(this)},_getMatch:function(n){if(n<0)return null;if(this._matches.length>n)return this._matches[n];if(this._done)return null;var t;do{if(t=this._regex._runner.run(!1,this._prevLen,this._input,this._beginning,this._length,this._startat),!t.getSuccess())return this._done=!0,null;this._matches.push(t);this._prevLen=t._length;this._startat=t._textpos}while(this._matches.length<=n);return t}});Bridge.define("System.Text.RegularExpressions.MatchEnumerator",{inherits:function(){return[System.Collections.IEnumerator]},_matchcoll:null,_match:null,_curindex:0,_done:!1,constructor:function(n){this._matchcoll=n},moveNext:function(){return this._done?!1:(this._match=this._matchcoll._getMatch(this._curindex),this._curindex++,this._match==null)?(this._done=!0,!1):!0},getCurrent:function(){if(this._match==null)throw new System.InvalidOperationException("Enumeration has either not started or has already finished.");return this._match},reset:function(){this._curindex=0;this._done=!1;this._match=null}});Bridge.define("System.Text.RegularExpressions.RegexOptions",{statics:{None:0,IgnoreCase:1,Multiline:2,ExplicitCapture:4,Compiled:8,Singleline:16,IgnorePatternWhitespace:32,RightToLeft:64,ECMAScript:256,CultureInvariant:512},$enum:!0,$flags:!0});Bridge.define("System.Text.RegularExpressions.RegexRunner",{statics:{},_runregex:null,_netEngine:null,_runtext:"",_runtextpos:0,_runtextbeg:0,_runtextend:0,_runtextstart:0,_quick:!1,_prevlen:0,constructor:function(n){if(n==null)throw new System.ArgumentNullException("regex");this._runregex=n;var i=n.getOptions(),t=System.Text.RegularExpressions.RegexOptions,r=(i&t.IgnoreCase)===t.IgnoreCase,u=(i&t.Multiline)===t.Multiline,f=(i&t.Singleline)===t.Singleline,e=(i&t.IgnorePatternWhitespace)===t.IgnorePatternWhitespace,o=n._matchTimeout.getTotalMilliseconds();this._netEngine=new System.Text.RegularExpressions.RegexNetEngine(n._pattern,r,u,f,e,o)},run:function(n,t,i,r,u,f){var e,o,s,h;if(f<0||f>i.Length)throw new System.ArgumentOutOfRangeException("start","Start index cannot be less than 0 or greater than input length.");if(u<0||u>i.Length)throw new ArgumentOutOfRangeException("length","Length cannot be less than 0 or exceed input length.");if(this._runtext=i,this._runtextbeg=r,this._runtextend=r+u,this._runtextstart=f,this._quick=n,this._prevlen=t,this._runregex.getRightToLeft()?(e=this._runtextbeg,o=-1):(e=this._runtextend,o=1),this._prevlen===0){if(this._runtextstart===e)return System.Text.RegularExpressions.Match.getEmpty();this._runtextstart+=o}return s=this._netEngine.match(this._runtext,this._runtextstart,this._prevlen),h=this._convertNetEngineResults(s),h},parsePattern:function(){return this._netEngine.parsePattern()},_convertNetEngineResults:function(n){var f,i,t,e,o,r,u,s;if(n.success&&this._quick)return null;if(!n.success)return System.Text.RegularExpressions.Match.getEmpty();for(f=this.parsePattern(),i=f.sparseSettings.isSparse?new System.Text.RegularExpressions.MatchSparse(this._runregex,f.sparseSettings.sparseSlotNumberMap,n.groups.length,this._runtext,0,this._runtext.length,this._runtextstart):new System.Text.RegularExpressions.Match(this._runregex,n.groups.length,this._runtext,0,this._runtext.length,this._runtextstart),r=0;r<n.groups.length;r++)for(t=n.groups[r],o=0,t.descriptor!=null&&(o=this._runregex.groupNumberFromName(t.descriptor.name)),u=0;u<t.captures.length;u++)e=t.captures[u],i._addMatch(o,e.capIndex,e.capLength);return s=n.capIndex+n.capLength,i._tidy(s),i}});Bridge.define("System.Text.RegularExpressions.RegexParser",{statics:{_Q:5,_S:4,_Z:3,_X:2,_E:1,_category:[0,0,0,0,0,0,0,0,0,2,2,0,2,2,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2,0,0,3,4,0,0,0,4,4,5,5,0,0,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,5,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,4,4,0,4,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,5,4,0,0,0],escape:function(n){for(var r,i,u,t=0;t<n.length;t++)if(System.Text.RegularExpressions.RegexParser._isMetachar(n[t])){r="";i=n[t];r+=n.slice(0,t);do{r+="\\";switch(i){case"\n":i="n";break;case"\r":i="r";break;case"\t":i="t";break;case"\f":i="f"}for(r+=i,t++,u=t;t<n.length;){if(i=n[t],System.Text.RegularExpressions.RegexParser._isMetachar(i))break;t++}r+=n.slice(u,t)}while(t<n.length);return r}return n},unescape:function(n){for(var f=System.Globalization.CultureInfo.invariantCulture,i,u,r,t=0;t<n.length;t++)if(n[t]==="\\"){i="";r=new System.Text.RegularExpressions.RegexParser(f);r._setPattern(n);i+=n.slice(0,t);do{for(t++,r._textto(t),t<n.length&&(i+=r._scanCharEscape()),t=r._textpos(),u=t;t<n.length&&n[t]!=="\\";)t++;i+=n.slice(u,t)}while(t<n.length);return i}return n},parseReplacement:function(n,t,i,r,u){var o=System.Globalization.CultureInfo.getCurrentCulture(),f=new System.Text.RegularExpressions.RegexParser(o),e;return f._options=u,f._noteCaptures(t,i,r),f._setPattern(n),e=f._scanReplacement(),new System.Text.RegularExpressions.RegexReplacement(n,e,t)},_isMetachar:function(n){var t=n.charCodeAt(0);return t<="|".charCodeAt(0)&&System.Text.RegularExpressions.RegexParser._category[t]>=System.Text.RegularExpressions.RegexParser._E}},_caps:null,_capsize:0,_capnames:null,_pattern:"",_currentPos:0,_concatenation:null,_culture:null,config:{init:function(){this._options=System.Text.RegularExpressions.RegexOptions.None}},constructor:function(n){this._culture=n;this._caps={}},_noteCaptures:function(n,t,i){this._caps=n;this._capsize=t;this._capnames=i},_setPattern:function(n){n==null&&(n="");this._pattern=n||"";this._currentPos=0},_scanReplacement:function(){this._concatenation=new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.Concatenate,this._options);for(var n,t,i;;){if(n=this._charsRight(),n===0)break;for(t=this._textpos();n>0&&this._rightChar()!=="$";)this._moveRight(),n--;this._addConcatenate(t,this._textpos()-t);n>0&&this._moveRightGetChar()==="$"&&(i=this._scanDollar(),this._concatenation.addChild(i))}return this._concatenation},_addConcatenate:function(n,t){var i,r,u;t!==0&&(t>1?(r=this._pattern.slice(n,n+t),i=new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.Multi,this._options,r)):(u=this._pattern[n],i=new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.One,this._options,u)),this._concatenation.addChild(i))},_useOptionE:function(){return(this._options&System.Text.RegularExpressions.RegexOptions.ECMAScript)!=0},_makeException:function(n){return new System.ArgumentException("Incorrect pattern. "+n)},_scanDollar:function(){var o=214748364,n,f,i,e,h;if(this._charsRight()===0)return new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.One,this._options,"$");var t=this._rightChar(),r,s=this._textpos(),u=s;if(t==="{"&&this._charsRight()>1?(r=!0,this._moveRight(),t=this._rightChar()):r=!1,t>="0"&&t<="9"){if(!r&&this._useOptionE()){for(n=-1,i=t-"0",this._moveRight(),this._isCaptureSlot(i)&&(n=i,u=this._textpos());this._charsRight()>0&&(t=this._rightChar())>="0"&&t<="9";){if(f=t-"0",i>o||i===o&&f>7)throw this._makeException("Capture group is out of range.");i=i*10+f;this._moveRight();this._isCaptureSlot(i)&&(n=i,u=this._textpos())}if(this._textto(u),n>=0)return new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.Ref,this._options,n)}else if(n=this._scanDecimal(),(!r||this._charsRight()>0&&this._moveRightGetChar()==="}")&&this._isCaptureSlot(n))return new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.Ref,this._options,n)}else if(r&&this._isWordChar(t)){if(e=this._scanCapname(),this._charsRight()>0&&this._moveRightGetChar()==="}"&&this._isCaptureName(e))return h=this._captureSlotFromName(e),new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.Ref,this._options,h)}else if(!r){n=1;switch(t){case"$":return this._moveRight(),new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.One,this._options,"$");case"&":n=0;break;case"`":n=System.Text.RegularExpressions.RegexReplacement.LeftPortion;break;case"'":n=System.Text.RegularExpressions.RegexReplacement.RightPortion;break;case"+":n=System.Text.RegularExpressions.RegexReplacement.LastGroup;break;case"_":n=System.Text.RegularExpressions.RegexReplacement.WholeString}if(n!==1)return this._moveRight(),new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.Ref,this._options,n)}return this._textto(s),new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.One,this._options,"$")},_scanDecimal:function(){for(var r=214748364,n=0,t,i;this._charsRight()>0;){if(t=this._rightChar(),t<"0"||t>"9")break;if(i=t-"0",this._moveRight(),n>r||n===r&&i>7)throw this._makeException("Capture group is out of range.");n*=10;n+=i}return n},_scanOctal:function(){var i,n,t;for(t=3,t>this._charsRight()&&(t=this._charsRight()),n=0;t>0&&(i=this._rightChar()-"0")<=7;t-=1)if(this._moveRight(),n*=8,n+=i,this._useOptionE()&&n>=32)break;return n&255},_scanHex:function(n){var t,i;if(t=0,this._charsRight()>=n)for(;n>0&&(i=this._hexDigit(this._moveRightGetChar()))>=0;n-=1)t*=16,t+=i;if(n>0)throw this._makeException("Insufficient hexadecimal digits.");return t},_hexDigit:function(n){var t,i=n.charCodeAt(0);return(t=i-"0".charCodeAt(0))<=9?t:(t=i-"a".charCodeAt(0))<=5?t+10:(t=i-"A".charCodeAt(0))<=5?t+10:-1},_scanControl:function(){if(this._charsRight()<=0)throw this._makeException("Missing control character.");var t=this._moveRightGetChar(),n=t.charCodeAt(0);if(n>="a".charCodeAt(0)&&n<="z".charCodeAt(0)&&(n=n-("a".charCodeAt(0)-"A".charCodeAt(0))),(n=n-"@".charCodeAt(0))<" ".charCodeAt(0))return String.fromCharCode(n);throw this._makeException("Unrecognized control character.");},_scanCapname:function(){for(var n=this._textpos();this._charsRight()>0;)if(!this._isWordChar(this._moveRightGetChar())){this._moveLeft();break}return _pattern.slice(n,this._textpos())},_scanCharEscape:function(){var n=this._moveRightGetChar();if(n>="0"&&n<="7")return this._moveLeft(),this._scanOctal();switch(n){case"x":return this._scanHex(2);case"u":return this._scanHex(4);case"a":return"\x07";case"b":return"\b";case"e":return"\x1b";case"f":return"\f";case"n":return"\n";case"r":return"\r";case"t":return"\t";case"v":return"\x0b";case"c":return this._scanControl();default:if(!this._useOptionE()&&this._isWordChar(n))throw this._makeException("Unrecognized escape sequence.");return n}},_captureSlotFromName:function(n){return this._capnames[n]},_isCaptureSlot:function(n){return this._caps!=null?this._caps[n]!=null:n>=0&&n<this._capsize},_isCaptureName:function(n){return this._capnames==null?!1:_capnames[n]!=null},_isWordChar:function(n){return System.Char.isLetter(n)},_charsRight:function(){return this._pattern.length-this._currentPos},_rightChar:function(){return this._pattern[this._currentPos]},_moveRightGetChar:function(){return this._pattern[this._currentPos++]},_moveRight:function(){this._currentPos++},_textpos:function(){return this._currentPos},_textto:function(n){this._currentPos=n},_moveLeft:function(){this._currentPos--}});Bridge.define("System.Text.RegularExpressions.RegexNode",{statics:{One:9,Multi:12,Ref:13,Empty:23,Concatenate:25},_type:0,_str:null,_children:null,_next:null,_m:0,config:{init:function(){this._options=System.Text.RegularExpressions.RegexOptions.None}},constructor:function(n,t,i){this._type=n;this._options=t;n===System.Text.RegularExpressions.RegexNode.Ref?this._m=i:this._str=i||null},addChild:function(n){this._children==null&&(this._children=[]);var t=n._reduce();this._children.push(t);t._next=this},childCount:function(){return this._children==null?0:this._children.length},child:function(n){return this._children[n]},_reduce:function(){var n;switch(this._type){case System.Text.RegularExpressions.RegexNode.Concatenate:n=this._reduceConcatenation();break;default:n=this}return n},_reduceConcatenation:function(){var e=!1,o=0,u,n,i,r,t,f;if(this._children==null)return new System.Text.RegularExpressions.RegexNode(System.Text.RegularExpressions.RegexNode.Empty,this._options);for(r=0,t=0;r<this._children.length;r++,t++)if(n=this._children[r],t<r&&(this._children[t]=n),n._type===System.Text.RegularExpressions.RegexNode.Concatenate&&n._isRightToLeft()){for(f=0;f<n._children.length;f++)n._children[f]._next=this;this._children.splice.apply(this._children,[r+1,0].concat(n._children));t--}else if(n._type===System.Text.RegularExpressions.RegexNode.Multi||n._type===System.Text.RegularExpressions.RegexNode.One){if(u=n._options&(System.Text.RegularExpressions.RegexOptions.RightToLeft|System.Text.RegularExpressions.RegexOptions.IgnoreCase),!e||o!==u){e=!0;o=u;continue}i=this._children[--t];i._type===System.Text.RegularExpressions.RegexNode.One&&(i._type=System.Text.RegularExpressions.RegexNode.Multi,i._str=i._str);(u&System.Text.RegularExpressions.RegexOptions.RightToLeft)==0?i._str+=n._str:i._str=n._str+i._str}else n._type===System.Text.RegularExpressions.RegexNode.Empty?t--:e=!1;return t<r&&this._children.splice(t,r-t),this._stripEnation(System.Text.RegularExpressions.RegexNode.Empty)},_stripEnation:function(n){switch(this.childCount()){case 0:return new t.RegexNode(n,this._options);case 1:return this.child(0);default:return this}},_isRightToLeft:function(){return(this._options&System.Text.RegularExpressions.RegexOptions.RightToLeft)>0?!0:!1}});Bridge.define("System.Text.RegularExpressions.RegexReplacement",{statics:{replace:function(n,t,i,r,u){var f,o,e,s,h,c,l;if(n==null)throw new System.ArgumentNullException("evaluator");if(r<-1)throw new System.ArgumentOutOfRangeException("count","Count cannot be less than -1.");if(u<0||u>i.length)throw new System.ArgumentOutOfRangeException("startat","Start index cannot be less than 0 or greater than input length.");if(r===0)return i;if(f=t.match$1(i,u),f.getSuccess()){if(o="",t.getRightToLeft()){c=[];e=i.length;do{if(s=f.getIndex(),h=f.getLength(),s+h!==e&&c.push(i.slice(s+h,e)),e=s,c.push(n(f)),--r==0)break;f=f.nextMatch()}while(f.getSuccess());for(o=new StringBuilder,e>0&&(o+=o.slice(0,e)),l=c.length-1;l>=0;l--)o+=c[l]}else{e=0;do{if(s=f.getIndex(),h=f.getLength(),s!==e&&(o+=i.slice(e,s)),e=s+h,o+=n(f),--r==0)break;f=f.nextMatch()}while(f.getSuccess());e<i.length&&(o+=i.slice(e,i.length))}return o}return i},split:function(n,t,i,r){var f,u,e,o,s,c,h,l;if(i<0)throw new System.ArgumentOutOfRangeException("count","Count can't be less than 0.");if(r<0||r>t.length)throw new System.ArgumentOutOfRangeException("startat","Start index cannot be less than 0 or greater than input length.");if(f=[],i===1)return f.push(t),f;if(--i,u=n.match$1(t,r),u.getSuccess())if(n.getRightToLeft()){for(o=t.length;;){for(s=u.getIndex(),c=u.getLength(),h=u.getGroups(),l=h.getCount(),f.push(t.slice(s+c,o)),o=s,e=1;e<l;e++)u._isMatched(e)&&f.push(h.get(e).toString());if(--i,i===0)break;if(u=u.nextMatch(),!u.getSuccess())break}f.push(t.slice(0,o));f.reverse()}else{for(o=0;;){for(s=u.getIndex(),c=u.getLength(),h=u.getGroups(),l=h.getCount(),f.push(t.slice(o,s)),o=s+c,e=1;e<l;e++)u._isMatched(e)&&f.push(h.get(e).toString());if(--i,i===0)break;if(u=u.nextMatch(),!u.getSuccess())break}f.push(t.slice(o,t.length))}else f.push(t);return f},Specials:4,LeftPortion:-1,RightPortion:-2,LastGroup:-3,WholeString:-4},_rep:"",_strings:[],_rules:[],constructor:function(n,t,i){if(this._rep=n,t._type!==System.Text.RegularExpressions.RegexNode.Concatenate)throw new System.ArgumentException("Replacement error.");for(var r="",u=[],e=[],f,o,s=0;s<t.childCount();s++){o=t.child(s);switch(o._type){case System.Text.RegularExpressions.RegexNode.Multi:case System.Text.RegularExpressions.RegexNode.One:r+=o._str;break;case System.Text.RegularExpressions.RegexNode.Ref:r.length>0&&(e.push(u.length),u.push(r),r="");f=o._m;i!=null&&f>=0&&(f=i[f]);e.push(-System.Text.RegularExpressions.RegexReplacement.Specials-1-f);break;default:throw new System.ArgumentException("Replacement error.");}}r.length>0&&(e.push(u.length),u.push(r));this._strings=u;this._rules=e},getPattern:function(){return _rep},replacement:function(n){return this._replacementImpl("",n)},replace:function(n,t,i,r){var u,e,f,o,s,h,c;if(i<-1)throw new System.ArgumentOutOfRangeException("count","Count cannot be less than -1.");if(r<0||r>t.length)throw new System.ArgumentOutOfRangeException("startat","Start index cannot be less than 0 or greater than input length.");if(i===0)return t;if(u=n.match$1(t,r),u.getSuccess()){if(e="",n.getRightToLeft()){h=[];f=t.length;do{if(o=u.getIndex(),s=u.getLength(),o+s!==f&&h.push(t.slice(o+s,f)),f=o,this._replacementImplRTL(h,u),--i==0)break;u=u.nextMatch()}while(u.getSuccess());for(f>0&&(e+=e.slice(0,f)),c=h.length-1;c>=0;c--)e+=h[c]}else{f=0;do{if(o=u.getIndex(),s=u.getLength(),o!==f&&(e+=t.slice(f,o)),f=o+s,e=this._replacementImpl(e,u),--i==0)break;u=u.nextMatch()}while(u.getSuccess());f<t.length&&(e+=t.slice(f,t.length))}return e}return t},_replacementImpl:function(n,t){for(var u=System.Text.RegularExpressions.RegexReplacement.Specials,i,r=0;r<this._rules.length;r++)if(i=this._rules[r],i>=0)n+=this._strings[i];else if(i<-u)n+=t._groupToStringImpl(-u-1-i);else switch(-u-1-i){case System.Text.RegularExpressions.RegexReplacement.LeftPortion:n+=t._getLeftSubstring();break;case System.Text.RegularExpressions.RegexReplacement.RightPortion:n+=t._getRightSubstring();break;case System.Text.RegularExpressions.RegexReplacement.LastGroup:n+=t._lastGroupToStringImpl();break;case System.Text.RegularExpressions.RegexReplacement.WholeString:n+=t._getOriginalString()}return n},_replacementImplRTL:function(n,t){for(var u=System.Text.RegularExpressions.RegexReplacement.Specials,i,r=_rules.length-1;r>=0;r--)if(i=this._rules[r],i>=0)n.push(this._strings[i]);else if(i<-u)n.push(t._groupToStringImpl(-u-1-i));else switch(-u-1-i){case System.Text.RegularExpressions.RegexReplacement.LeftPortion:n.push(t._getLeftSubstring());break;case System.Text.RegularExpressions.RegexReplacement.RightPortion:n.push(t._getRightSubstring());break;case System.Text.RegularExpressions.RegexReplacement.LastGroup:n.push(t._lastGroupToStringImpl());break;case System.Text.RegularExpressions.RegexReplacement.WholeString:n.push(t._getOriginalString())}}});Bridge.define("System.Text.RegularExpressions.RegexNetEngine",{statics:{jsRegex:function(n,t,i,r,u,f,e){var s,h,o,c;if(n==null)throw new System.ArgumentNullException("text");if(t!=null&&(t<0||t>n.length))throw new System.ArgumentOutOfRangeException("textStart","Start index cannot be less than 0 or greater than input length.");if(i==null)throw new System.ArgumentNullException("pattern");if(s="g",s+="m",u&&(s+="i"),h=new RegExp(i,s),t!=null&&(h.lastIndex=t),o=h.exec(n),o==null||o.length===0)return null;if(f){c=[];do c.push(o),o=h.exec(n),o!=null&&e&&e._checkTimeout();while(o!=null);return c}return o}},_pattern:"",_originalPattern:"",_patternInfo:null,_isCaseInsensitive:!1,_isMultiLine:!1,_isSingleline:!1,_isIgnoreWhitespace:!1,_text:"",_textStart:0,_timeoutMs:-1,_timeoutTime:-1,constructor:function(n,t,i,r,u,f){if(n==null)throw new System.ArgumentNullException("pattern");this._pattern=n;this._originalPattern=n;this._isCaseInsensitive=t;this._isMultiLine=i;this._isSingleline=r;this._isIgnoreWhitespace=u;this._timeoutMs=f},match:function(n,t,i){var f,s,k,u,a,v,y,p;if(n==null)throw new System.ArgumentNullException("text");if(t!=null&&(t<0||t>n.length))throw new System.ArgumentOutOfRangeException("textStart","Start index cannot be less than 0 or greater than input length.");if((this._text=n,this._textStart=t,this._timeoutTime=this._timeoutMs>0?(new Date).getTime()+System.Convert.toInt32(this._timeoutMs+.5):-1,f={capIndex:0,capLength:0,success:!1,value:"",groups:[],captures:[]},s=this.parsePattern(),s.shouldFail)||(k=s.groups,this._checkTimeout(),i>=0&&this._textStart>0&&this._text[this._textStart-1]==="\r"&&this._text[this._textStart]==="\n"&&s.hasEndOfMultiline&&this._textStart++,u=System.Text.RegularExpressions.RegexNetEngine.jsRegex(this._text,this._textStart,this._pattern,this._isMultiLine,this._isCaseInsensitive,!1,this),u==null)||i>=0&&s.isContiguous&&u.index!==this._textStart||u.index!==0&&s.mustCaptureFirstCh)return f;if(f.capIndex=u.index,f.capLength=u[0].length,f.success=!0,f.captures.push({capIndex:u.index,capLength:u[0].length,value:u[0]}),f.groups.push({capIndex:u.index,capLength:u[0].length,value:u[0],success:!0,captures:[f.captures[0]]}),u.length>1){for(var d={},tt={text:this._text,textOffset:this._textStart,pattern:this._pattern,patternStart:0,patternEnd:this._pattern.length},g=0,nt={},r,e,w,o,b,h,l,c=1;c<u.length+g;c++){if(this._checkTimeout(),e=k[c-1],e.constructs.isNonCapturing&&g++,r={descriptor:e,capIndex:0,capLength:0,value:"",valueFull:"",success:!1,captures:[],ctx:null},d[e.exprIndex]=r,w=e.parentGroup,w==null)this._matchGroup(tt,r,0),r.success&&this._matchCaptures(r);else if(a=d[w.exprIndex],a.success===!0)for(l=0;l<a.captures.length;l++)this._checkTimeout(),b=a.captures[l],this._matchGroup(b.ctx,r,b.capIndex),r.success&&this._matchCaptures(r);r.captures.length>0&&(h=r.captures[r.captures.length-1],r.capIndex=h.capIndex,r.capLength=h.capLength,r.value=h.value);e.constructs.isNonCapturing||(o=nt[e.name],o?(o.capIndex=r.capIndex,o.capLength=r.capLength,o.value=r.value,o.success=r.success,o.captures=o.captures.concat(r.captures)):(f.groups.push(r),nt[e.name]=r))}for(v=0;v<f.groups.length;v++)for(y=f.groups[v],delete y.ctx,p=0;p<y.captures.length;p++)delete y.captures[p].ctx}return f},parsePattern:function(){if(this._patternInfo==null){var t=System.Text.RegularExpressions.RegexNetEngineParser,n=t.parsePattern(this._pattern,this._isCaseInsensitive,this._isMultiLine,this._isSingleline,this._isIgnoreWhitespace);this._patternInfo=n;this._pattern=n.jsPattern}return this._patternInfo},_matchGroup:function(n,t,i){var u=t.descriptor,e=u.exprIndex,h=u.exprIndex+u.exprLength,o=u.exprIndex+u.exprFull.length,f,r,s;u.exprIndex>n.patternStart&&(f=this._matchSubExpr(n.text,n.textOffset,n.pattern,n.patternStart,n.patternEnd,n.patternStart,e),f!=null&&(n.textOffset=f.capIndex+f.capLength));r=this._matchSubExpr(n.text,n.textOffset,n.pattern,n.patternStart,n.patternEnd,e,o);r!=null&&r.captureGroup!=null&&(n.textOffset=r.capIndex+r.capLength,t.value=r.captureGroup,t.valueFull=r.capture,t.capIndex=r.capIndex+i,t.capLength=r.capLength,t.success=!0,s=u.constructs.isNonCapturing?3:1,u.innerGroups.length>0&&(t.ctx={text:t.valueFull,textOffset:0,pattern:n.pattern,patternStart:e+s,patternEnd:h-1}));n.patternStart=o},_matchCaptures:function(n){var t=n.descriptor,r,u,f,e,i;if(t.quantifier==null||t.quantifier.length===0||t.quantifier==="?"||n.valueFull==null||n.valueFull.length===0)n.captures.push({capIndex:n.capIndex,capLength:n.capLength,value:n.valueFull});else if(r=t.quantifier[0],r==="*"||r==="+"||r==="{"){if(u=System.Text.RegularExpressions.RegexNetEngine.jsRegex(n.valueFull,0,t.expr,this._isMultiLine,this._isCaseInsensitive,!0,this),u==null)throw new System.InvalidOperationException("Can't identify captures for the already matched group.");for(e=0;e<u.length;e++)f=u[e],n.captures.push({capIndex:f.index+n.capIndex,capLength:f[0].length,value:f[0]})}if(n.ctx!=null)for(i=0;i<n.captures.length;i++)n.captures[i].ctx={text:n.captures[i].value,textOffset:0,pattern:n.ctx.pattern,patternStart:n.ctx.patternStart,patternEnd:n.ctx.patternEnd}},_matchSubExpr:function(n,t,i,r,u,f,e){if(t<0||t>n.length)throw new System.ArgumentOutOfRangeException("textOffset");if(r<0||r>=i.length)throw new System.ArgumentOutOfRangeException("patternStartIndex");if(u<r||u>i.length)throw new System.ArgumentOutOfRangeException("patternEndIndex");if(f<r||f>=u)throw new System.ArgumentOutOfRangeException("subExprStartIndex");if(e<f||e>u)throw new System.ArgumentOutOfRangeException("subExprEndIndex");if(t===n.length)return null;var s=i.slice(f,e),h=i.slice(e,u),c=s+"(?="+h+")",o=System.Text.RegularExpressions.RegexNetEngine.jsRegex(n,t,c,this._isMultiLine,this._isCaseInsensitive,!1,this);return o!=null?{capture:o[0],captureGroup:o.length>1?o[1]:null,capIndex:o.index,capLength:o[0].length}:null},_checkTimeout:function(){if(!(this._timeoutTime<0)){var n=(new Date).getTime();if(n>=this._timeoutTime)throw new System.RegexMatchTimeoutException(this._text,this._pattern,System.TimeSpan.fromMilliseconds(this._timeoutMs));}}});Bridge.define("System.Text.RegularExpressions.RegexNetEngineParser",{statics:{_hexSymbols:"0123456789abcdefABCDEF",_octSymbols:"01234567",_decSymbols:"0123456789",_escapedChars:"abtrvfnexcu",_escapedCharClasses:"pPwWsSdD",_escapedAnchors:"AZzGbB",_escapedSpecialSymbols:" .,$^{}[]()|*+-=?\\|/\"':;~!@#%&",_whiteSpaceChars:" \r\n\t\v\f\u00A0\uFEFF",_unicodeCategories:["Lu","Ll","Lt","Lm","Lo","L","Mn","Mc","Me","M","Nd","Nl","No","N","Pc","Pd","Ps","Pe","Pi","Pf","Po","P","Sm","Sc","Sk","So","S","Zs","Zl","Zp","Z","Cc","Cf","Cs","Co","Cn","C"],_namedCharBlocks:["IsBasicLatin","IsLatin-1Supplement","IsLatinExtended-A","IsLatinExtended-B","IsIPAExtensions","IsSpacingModifierLetters","IsCombiningDiacriticalMarks","IsGreek","IsGreekandCoptic","IsCyrillic","IsCyrillicSupplement","IsArmenian","IsHebrew","IsArabic","IsSyriac","IsThaana","IsDevanagari","IsBengali","IsGurmukhi","IsGujarati","IsOriya","IsTamil","IsTelugu","IsKannada","IsMalayalam","IsSinhala","IsThai","IsLao","IsTibetan","IsMyanmar","IsGeorgian","IsHangulJamo","IsEthiopic","IsCherokee","IsUnifiedCanadianAboriginalSyllabics","IsOgham","IsRunic","IsTagalog","IsHanunoo","IsBuhid","IsTagbanwa","IsKhmer","IsMongolian","IsLimbu","IsTaiLe","IsKhmerSymbols","IsPhoneticExtensions","IsLatinExtendedAdditional","IsGreekExtended","IsGeneralPunctuation","IsSuperscriptsandSubscripts","IsCurrencySymbols","IsCombiningDiacriticalMarksforSymbols","IsCombiningMarksforSymbols","IsLetterlikeSymbols","IsNumberForms","IsArrows","IsMathematicalOperators","IsMiscellaneousTechnical","IsControlPictures","IsOpticalCharacterRecognition","IsEnclosedAlphanumerics","IsBoxDrawing","IsBlockElements","IsGeometricShapes","IsMiscellaneousSymbols","IsDingbats","IsMiscellaneousMathematicalSymbols-A","IsSupplementalArrows-A","IsBraillePatterns","IsSupplementalArrows-B","IsMiscellaneousMathematicalSymbols-B","IsSupplementalMathematicalOperators","IsMiscellaneousSymbolsandArrows","IsCJKRadicalsSupplement","IsKangxiRadicals","IsIdeographicDescriptionCharacters","IsCJKSymbolsandPunctuation","IsHiragana","IsKatakana","IsBopomofo","IsHangulCompatibilityJamo","IsKanbun","IsBopomofoExtended","IsKatakanaPhoneticExtensions","IsEnclosedCJKLettersandMonths","IsCJKCompatibility","IsCJKUnifiedIdeographsExtensionA","IsYijingHexagramSymbols","IsCJKUnifiedIdeographs","IsYiSyllables","IsYiRadicals","IsHangulSyllables","IsHighSurrogates","IsHighPrivateUseSurrogates","IsLowSurrogates","IsPrivateUse or IsPrivateUseArea","IsCJKCompatibilityIdeographs","IsAlphabeticPresentationForms","IsArabicPresentationForms-A","IsVariationSelectors","IsCombiningHalfMarks","IsCJKCompatibilityForms","IsSmallFormVariants","IsArabicPresentationForms-B","IsHalfwidthandFullwidthForms","IsSpecials"],tokenTypes:{literal:0,escChar:110,escCharOctal:111,escCharHex:112,escCharCtrl:113,escCharUnicode:114,escCharOther:115,escCharClass:120,escCharClassCategory:121,escCharClassBlock:122,escCharClassDot:123,escAnchor:130,escBackrefNumber:140,escBackrefName:141,charGroup:200,anchor:300,group:400,groupConstruct:401,groupConstructName:402,groupConstructImnsx:403,groupConstructImnsxMisc:404,quantifier:500,quantifierN:501,quantifierNM:502,alternation:600,alternationGroup:601,alternationGroupExpr:602,commentInline:700,commentXMode:701,toBeSkipped:900,tmpGroup:901},parsePattern:function(n,t,i,r,u){var f=System.Text.RegularExpressions.RegexNetEngineParser,e={ignoreCase:t,multiline:i,singleline:r,ignoreWhitespace:u},o=f._parsePatternImpl(n,e,0,n.length),h=[],s,c,l;return f._fillGroupDescriptors(o,h),s=f._getGroupSparseInfo(h),f._preTransformBackrefTokens(n,o,s),f._transformTokensForJsPattern(e,o,s,[],[],0),f._updateGroupDescriptors(o),c=f._constructPattern(o),l={originalPattern:n,jsPattern:c,groups:h,sparseSettings:s,isContiguous:e.isContiguous||!1,mustCaptureFirstCh:e.mustCaptureFirstCh||!1,shouldFail:e.shouldFail||!1,hasEndOfMultiline:e.hasEndOfMultiline||!1},l},_fillGroupDescriptors:function(n,t){var f=System.Text.RegularExpressions.RegexNetEngineParser,i,r,u;for(f._fillGroupStructure(t,n,null),u=1,r=0;r<t.length;r++)i=t[r],i.constructs.name1!=null?(i.name=i.constructs.name1,i.hasName=!0):(i.hasName=!1,i.name=u.toString(),++u)},_fillGroupStructure:function(n,t,i){for(var o=System.Text.RegularExpressions.RegexNetEngineParser,s=o.tokenTypes,u,r,h,f,c,e=0;e<t.length;e++)r=t[e],f=r.children&&r.children.length,c=f&&r.children[0].type===s.groupConstructImnsx,r.type!==s.group||c||(u={rawIndex:n.length+1,number:-1,parentGroup:null,innerGroups:[],name:null,hasName:!1,constructs:null,quantifier:null,exprIndex:-1,exprLength:0,expr:null,exprFull:null},r.group=u,n.push(u),i!=null&&(r.group.parentGroup=i,i.innerGroups.push(u)),h=f?r.children[0]:null,u.constructs=o._fillGroupConstructs(h)),f&&o._fillGroupStructure(n,r.children,r.group)},_getGroupSparseInfo:function(n){for(var s=System.Text.RegularExpressions.RegexNetEngineParser,o=["0"],u=[0],a={},v={},h={},e={},c=[],f,l,r,i,y,t=0;t<n.length;t++)(i=n[t],i.constructs.isNonCapturing)||i.constructs.isNumberName1&&(r=parseInt(i.constructs.name1),c.push(r),e[r]?e[r].push(i):e[r]=[i]);for(y=function(n,t){return n-t},c.sort(y),t=0;t<n.length;t++)(i=n[t],i.constructs.isNonCapturing)||(r=u.length,i.hasName||(l=[i],f=e[r],f!=null&&(l=l.concat(f),e[r]=null),s._addSparseSlotForSameNamedGroups(l,r,o,u)));for(t=0;t<n.length;t++)if((i=n[t],!i.constructs.isNonCapturing)&&i.hasName&&!i.constructs.isNumberName1){for(r=u.length,f=e[r];f!=null;)s._addSparseSlotForSameNamedGroups(f,r,o,u),e[r]=null,r=u.length,f=e[r];s._addSparseSlot(i,r,o,u)}for(t=0;t<c.length;t++)r=c[t],f=e[r],f!=null&&s._addSparseSlotForSameNamedGroups(f,r,o,u);for(t=0;t<u.length;t++)a[o[t]]=t,v[u[t]]=t;for(t=0;t<n.length;t++)(i=n[t],i.constructs.isNonCapturing)||(h[i.sparseSlotId]?h[i.sparseSlotId].push(i):h[i.sparseSlotId]=[i]);return{isSparse:u.length!==1+u[u.length-1],sparseSlotNames:o,sparseSlotNumbers:u,sparseSlotNameMap:a,sparseSlotNumberMap:v,sparseSlotGroupsMap:h,getSingleGroupByNumber:function(n){var t=this.sparseSlotNumberMap[n];return t==null?null:this.getSingleGroupBySlotId(t)},getSingleGroupByName:function(n){var t=this.sparseSlotNameMap[n];return t==null?null:this.getSingleGroupBySlotId(t)},getSingleGroupBySlotId:function(n){var t=this.sparseSlotGroupsMap[n];if(t.length!==1)throw new System.NotSupportedException("Redefined groups are not supported.");return t[0]}}},_addSparseSlot:function(n,t,i,r){n.sparseSlotId=i.length;i.push(n.name);r.push(t)},_addSparseSlotForSameNamedGroups:function(n,t,i,r){var e=System.Text.RegularExpressions.RegexNetEngineParser,u,f;if(e._addSparseSlot(n[0],t,i,r),f=n[0].sparseSlotId,n.length>1)for(u=1;u<n.length;u++)n[u].sparseSlotId=f},_fillGroupConstructs:function(n){var o=System.Text.RegularExpressions.RegexNetEngineParser,f=o.tokenTypes,t={name1:null,name2:null,isNumberName1:!1,isNumberName2:!1,isNonCapturing:!1,isIgnoreCase:null,isMultiline:null,isExplicitCapture:null,isSingleLine:null,isIgnoreWhitespace:null,isPositiveLookahead:!1,isNegativeLookahead:!1,isPositiveLookbehind:!1,isNegativeLookbehind:!1,isNonbacktracking:!1},s,i,h,c;if(n==null)return t;if(n.type===f.groupConstruct)switch(n.value){case"?:":t.isNonCapturing=!0;break;case"?=":t.isPositiveLookahead=!0;break;case"?!":t.isNegativeLookahead=!0;break;case"?>":t.isNonbacktracking=!0;break;case"?<=":t.isPositiveLookbehind=!0;break;case"?<!":t.isNegativeLookbehind=!0;break;default:throw new System.ArgumentException("Unrecognized grouping construct.");}else if(n.type===f.groupConstructName){if(s=n.value.slice(2,n.length-1),i=s.split("-"),i.length===0||i.length>2)throw new System.ArgumentException("Invalid group name.");t.name1=i[0];h=o._validateGroupName(i[0]);t.isNumberName1=h.isNumberName;i.length===2&&(t.name2=i[1],c=o._validateGroupName(i[1]),t.isNumberName2=c.isNumberName)}else if(n.type===f.groupConstructImnsx||n.type===f.groupConstructImnsxMisc)for(var l=n.type===f.groupConstructImnsx?1:0,a=n.length-1-l,u=!0,r,e=1;e<=a;e++)r=n.value[e],r==="-"?u=!1:r==="i"?t.isIgnoreCase=u:r==="m"?t.isMultiline=u:r==="n"?t.isExplicitCapture=u:r==="s"?t.isSingleLine=u:r==="x"&&(t.isIgnoreWhitespace=u);return t},_validateGroupName:function(n){var t,i,r;if(!n||!n.length)throw new System.ArgumentException("Invalid group name: Group names must begin with a word character.");if(t=n[0]>="0"&&n[0]<="9",t&&(i=System.Text.RegularExpressions.RegexNetEngineParser,r=i._matchChars(n,0,n.length,i._decSymbols),r.matchLength!==n.length))throw new System.ArgumentException("Invalid group name: Group names must begin with a word character.");return{isNumberName:t}},_preTransformBackrefTokens:function(n,t,i){for(var u=System.Text.RegularExpressions.RegexNetEngineParser,s=u.tokenTypes,e,h,o,c,l,r,f=0;f<t.length;f++){if(r=t[f],r.type===s.escBackrefNumber){if(e=r.value.slice(1),h=parseInt(e,10),h>=1&&i.getSingleGroupByNumber(h)!=null)continue;if(e.length===1)throw new System.ArgumentException("Reference to undefined group number "+e+".");if(o=u._parseOctalCharToken(r.value,0,r.length),o==null)throw new System.ArgumentException("Unrecognized escape sequence "+r.value.slice(0,2)+".");c=r.length-o.length;u._modifyPatternToken(r,n,s.escCharOctal,null,o.length);c>0&&(l=u._createPatternToken(n,s.literal,r.index+r.length,c),t.splice(f+1,0,l))}r.children&&r.children.length&&u._preTransformBackrefTokens(n,r.children,i)}},_transformTokensForJsPattern:function(n,t,i,r,u,f){for(var a=System.Text.RegularExpressions.RegexNetEngineParser,c=a.tokenTypes,s,l,e,h,v,w,b,p,y,k,o=0;o<t.length;o++){if(e=t[o],e.type===c.group){if(e.children&&e.children.length>0&&e.children[0].type===c.groupConstructImnsx){e.children.splice(0,1);l=a._createPatternToken("",c.tmpGroup,0,0,e.children,"","");l.localSettings=e.localSettings;t.splice(o,1,l);--o;continue}}else if(e.type===c.groupConstructName){t.splice(o,1);--o;continue}else if(e.type===c.escBackrefNumber){if(f>0)throw new System.NotSupportedException("Backreferences inside groups are not supported.");if(h=e.value.slice(1),w=parseInt(h,10),v=i.getSingleGroupByNumber(w),v==null)throw new System.ArgumentException("Reference to undefined group number "+h+".");if(r.indexOf(v.rawIndex)<0)throw new System.NotSupportedException("Reference to unreachable group number "+h+".");if(u.indexOf(v.rawIndex)>=0)throw new System.NotSupportedException("References to self/parent group number "+h+" are not supported.");v.rawIndex!==w&&(h="\\"+v.rawIndex.toString(),a._updatePatternToken(e,e.type,e.index,h.length,h))}else if(e.type===c.escBackrefName){if(f>0)throw new System.NotSupportedException("Backreferences inside groups are not supported.");if(h=e.value.slice(3,e.length-1),v=i.getSingleGroupByName(h),v==null){if(b=a._matchChars(h,0,h.length,a._decSymbols),b.matchLength===h.length){h="\\"+h;a._updatePatternToken(e,c.escBackrefNumber,e.index,h.length,h);--o;continue}throw new System.ArgumentException("Reference to undefined group name '"+h+"'.");}if(r.indexOf(v.rawIndex)<0)throw new System.NotSupportedException("Reference to unreachable group name '"+h+"'.");if(u.indexOf(v.rawIndex)>=0)throw new System.NotSupportedException("References to self/parent group name '"+h+"' are not supported.");h="\\"+v.rawIndex.toString();a._updatePatternToken(e,c.escBackrefNumber,e.index,h.length,h)}else if(e.type===c.anchor||e.type===c.escAnchor){if(e.value==="$")n.multiline?(n.hasEndOfMultiline=!0,s="(?=\\n|(?![\\d\\D]))",l=a._parseGroupToken(s,n,0,s.length),t.splice(o,1,l)):(s="(?![\\d\\D])",l=a._parseGroupToken(s,n,0,s.length),t.splice(o,1,l)),s="(?!\\r)",l=a._parseGroupToken(s,n,0,s.length),t.splice(o,0,l),++o;else if(e.value==="^")f===0&&o===0?n.multiline||(n.mustCaptureFirstCh=!0):n.shouldFail=!0;else if(e.value==="\\A")s="^",l=a._parseAnchorToken(s,0),t.splice(o,1,l),f===0&&o===0?n.mustCaptureFirstCh=!0:n.shouldFail=!0;else if(e.value==="\\Z")s="(?=\\n?(?![\\d\\D]))",l=a._parseGroupToken(s,n,0,s.length),t.splice(o,1,l);else if(e.value==="\\z")s="(?![\\d\\D])",l=a._parseGroupToken(s,n,0,s.length),t.splice(o,1,l);else if(e.value==="\\G"){f===0&&o===0?n.isContiguous=!0:n.shouldFail=!0;t.splice(o,1);--o;continue}}else if(e.type===c.escCharClassDot)s=n.singleline?"(?:.|\\r|\\n)":"(?:.|\\r)",l=a._parseGroupToken(s,n,0,s.length),t.splice(o,1,l);else if(e.type===c.groupConstructImnsx)s="?:",l=a._parseGroupConstructToken(s,n,0,s.length),t.splice(o,1,l);else if(e.type===c.groupConstructImnsxMisc){t.splice(o,1);--o;continue}else if(e.type===c.commentInline||e.type===c.commentXMode){t.splice(o,1);--o;continue}else if(e.type===c.toBeSkipped){t.splice(o,1);--o;continue}e.children&&e.children.length&&(p=e.type===c.group?[e.group.rawIndex]:[],p=p.concat(u),y=e.localSettings||n,k=e.type===c.tmpGroup?0:1,a._transformTokensForJsPattern(y,e.children,i,r,p,f+k),n.shouldFail=n.shouldFail||y.shouldFail,n.isContiguous=n.isContiguous||y.isContiguous,n.mustCaptureFirstCh=n.mustCaptureFirstCh||y.mustCaptureFirstCh,n.hasEndOfMultiline=n.hasEndOfMultiline||y.hasEndOfMultiline);e.type===c.group&&r.push(e.group.rawIndex)}},_updateGroupDescriptors:function(n,t){for(var o=System.Text.RegularExpressions.RegexNetEngineParser,e=o.tokenTypes,r,i,f,h,c,s=t||0,u=0;u<n.length;u++)i=n[u],i.index=s,i.children&&(c=i.childrenPostfix.length,o._updateGroupDescriptors(i.children,s+c),h=o._constructPattern(i.children),i.value=i.childrenPrefix+h+i.childrenPostfix,i.length=i.value.length),i.type===e.group&&i.group&&(r=i.group,r.exprIndex=i.index,r.exprLength=i.length,u+1<n.length&&(f=n[u+1],(f.type===e.quantifier||f.type===e.quantifierN||f.type===e.quantifierNM)&&(r.quantifier=f.value)),r.expr=i.value,r.exprFull=r.expr+(r.quantifier!=null?r.quantifier:""),delete i.group),s+=i.length},_constructPattern:function(n){for(var i="",r,t=0;t<n.length;t++)r=n[t],i+=r.value;return i},_parsePatternImpl:function(n,t,i,r){if(n==null)throw new System.ArgumentNullException("pattern");if(i<0||i>n.length)throw new System.ArgumentOutOfRangeException("startIndex");if(r<i||r>n.length)throw new System.ArgumentOutOfRangeException("endIndex");for(var e=System.Text.RegularExpressions.RegexNetEngineParser,h=e.tokenTypes,c=[],s=null,u,o,f=i;f<r;){if(o=n[f],t.ignoreWhitespace&&e._whiteSpaceChars.indexOf(o)>=0){++f;continue}if(o===".")u=e._parseDotToken(n,f,r);else if(o==="\\")u=e._parseEscapeToken(n,f,r);else if(o==="[")u=e._parseCharRangeToken(n,f,r);else if(o==="^"||o==="$")u=e._parseAnchorToken(n,f);else if(o==="("){if(u=e._parseGroupToken(n,t,f,r),u&&u.children&&u.children.length===1&&u.children[0].type===h.groupConstructImnsxMisc){f+=u.length;continue}}else u=o==="|"?e._parseAlternationToken(n,f):o==="#"&&t.ignoreWhitespace?e._parseXModeCommentToken(n,f,r):e._parseQuantifierToken(n,f,r);if(u==null){if(s!=null&&s.type===h.literal){s.value+=o;s.length++;f++;continue}u=e._createPatternToken(n,h.literal,f,1)}u!=null&&(c.push(u),s=u,f+=u.length)}return c},_parseEscapeToken:function(n,t,i){var r=System.Text.RegularExpressions.RegexNetEngineParser,o=r.tokenTypes,u=n[t],h,s,f,c,e;if(u!=="\\")return null;if(t+1>=i)throw new System.ArgumentException("Illegal \\ at end of pattern.");if(u=n[t+1],u>="1"&&u<="9")return h=r._matchChars(n,t+1,i,r._decSymbols,3),r._createPatternToken(n,o.escBackrefNumber,t,1+h.matchLength);if(s=r._parseEscapedChar(n,t,i),s!=null)return s;if(r._escapedAnchors.indexOf(u)>=0)return r._createPatternToken(n,o.escAnchor,t,2);if(u==="k"){if(t+2<i&&(f=n[t+2],(f==="'"||f==="<")&&(c=f==="<"?">":"'",e=r._matchUntil(n,t+3,i,c),e.unmatchLength===1&&e.matchLength>0)))return r._createPatternToken(n,o.escBackrefName,t,3+e.matchLength+1);throw new System.ArgumentException("Malformed \\k<...> named back reference.");}throw new System.ArgumentException("Unrecognized escape sequence \\"+u+".");},_parseOctalCharToken:function(n,t,i){var r=System.Text.RegularExpressions.RegexNetEngineParser,e=r.tokenTypes,u=n[t],f;return u==="\\"&&t+1<i&&(u=n[t+1],u>="0"&&u<="7")?(f=r._matchChars(n,t+1,i,r._octSymbols,3),r._createPatternToken(n,e.escCharOctal,t,1+f.matchLength)):null},_parseEscapedChar:function(n,t,i){var r=System.Text.RegularExpressions.RegexNetEngineParser,f=r.tokenTypes,u=n[t],h,o,c,e,s;if(u!=="\\"||t+1>=i)return null;if(u=n[t+1],r._escapedChars.indexOf(u)>=0){if(u==="x"){if(h=r._matchChars(n,t+2,i,r._hexSymbols,2),h.matchLength!==2)throw new System.ArgumentException("Insufficient hexadecimal digits.");return r._createPatternToken(n,f.escCharHex,t,4)}if(u==="c"){if(t+2>=i)throw new System.ArgumentException("Missing control character.");if(o=n[t+2],o>="a"&&o<="z"||o>="A"&&o<="Z")return r._createPatternToken(n,f.escCharCtrl,t,3);throw new System.ArgumentException("Unrecognized control character.");}else if(u==="u"){if(c=r._matchChars(n,t+2,i,r._hexSymbols,4),c.matchLength!==4)throw new System.ArgumentException("Insufficient hexadecimal digits.");return r._createPatternToken(n,f.escCharUnicode,t,6)}return r._createPatternToken(n,f.escChar,t,2)}if(u>="0"&&u<="7")return r._parseOctalCharToken(n,t,i);if(r._escapedCharClasses.indexOf(u)>=0){if(u==="p"||u==="P"){if(e=r._matchUntil(n,t+2,i,"}"),e.matchLength<3||e.match[0]!=="{"||e.unmatchLength!==1)throw new System.ArgumentException("Incomplete p{X} character escape.");if(s=e.slice(1),r._unicodeCategories.indexOf(s)>=0)return r._createPatternToken(n,f.escCharClassCategory,t,2+e.matchLength+1);if(r._namedCharBlocks.indexOf(s)>=0)return r._createPatternToken(n,f.escCharClassBlock,t,2+e.matchLength+1);throw new System.ArgumentException("Unknown property '"+s+"'.");}return r._createPatternToken(n,f.escCharClass,t,2)}return r._escapedSpecialSymbols.indexOf(u)>=0?r._createPatternToken(n,f.escCharOther,t,2):null},_parseCharRangeToken:function(n,t,i){var f=System.Text.RegularExpressions.RegexNetEngineParser,h=f.tokenTypes,e=[],u=n[t],r,o,c,s;if(u!=="[")return null;for(r=t+1,s=-1;r<i;){if(u=n[r],u==="\\"){if(o=f._parseEscapedChar(n,r,i),o==null)throw new System.ArgumentException("Unrecognized escape sequence \\"+u+".");e.push(o);r+=o.length;continue}if(u==="]"){s=r;break}c=f._createPatternToken(n,h.literal,r,1);e.push(c);++r}if(s<0||e.length<1)throw new System.ArgumentException("Unterminated [] set.");return f._createPatternToken(n,h.charGroup,t,1+s-t,e,"[","]")},_parseDotToken:function(n,t){var i=System.Text.RegularExpressions.RegexNetEngineParser,r=i.tokenTypes,u=n[t];return u!=="."?null:i._createPatternToken(n,r.escCharClassDot,t,1)},_parseAnchorToken:function(n,t){var i=System.Text.RegularExpressions.RegexNetEngineParser,u=i.tokenTypes,r=n[t];return r!=="^"&&r!=="$"?null:i._createPatternToken(n,u.anchor,t,1)},_updateSettingsFromConstructs:function(n,t){t.isIgnoreCase!=null&&(n.ignoreCase=t.isIgnoreCase);t.isMultiline!=null&&(n.multiline=t.isMultiline);t.isSingleLine!=null&&(n.singleline=t.isSingleLine);t.isIgnoreWhitespace!=null&&(n.ignoreWhitespace=t.isIgnoreWhitespace)},_parseGroupToken:function(n,t,i,r){var o=System.Text.RegularExpressions.RegexNetEngineParser,u=o.tokenTypes,h={ignoreCase:t.ignoreCase,multiline:t.multiline,singleline:t.singleline,ignoreWhitespace:t.ignoreWhitespace},a=n[i],l,s,tt,it,p,k,rt,d,g;if(a!=="(")return null;var w=1,c=i+1,e=-1,b=!1,nt=!1,v=!1,y=null,f=o._parseGroupConstructToken(n,h,i+1,r);if(f!=null&&(c+=f.length,f.type===u.commentInline?b=!0:f.type===u.alternationGroupExpr?nt=!0:f.type===u.groupConstructImnsx?(y=this._fillGroupConstructs(f),this._updateSettingsFromConstructs(h,y)):f.type===u.groupConstructImnsxMisc&&(y=this._fillGroupConstructs(f),this._updateSettingsFromConstructs(h,y),v=!0,c+=1)),v)e=r;else for(l=c;l<r;){if(a=n[l],a!=="("||b?a===")"&&--w:++w,w===0){e=l;break}++l}if(b){if(e<0)throw new System.ArgumentException("Unterminated (?#...) comment.");return o._createPatternToken(n,u.commentInline,i,1+e-i)}if(e<0)throw new System.ArgumentException("Not enough )'s.");if(s=o._parsePatternImpl(n,h,c,e),f==null||v||s.splice(0,0,f),nt){for(tt=s.length,k=0,p=0;p<tt;p++)if(it=s[p],it.type===u.alternation&&(++k,k>1))throw new System.ArgumentException("Too many | in (?()|).");return o._createPatternToken(n,u.alternationGroup,i,1+e-i,s,"(",")")}return v?(rt=o._createPatternToken(n,u.toBeSkipped,i,c-i,null),s.splice(0,0,rt),d=o._createPatternToken(n,u.tmpGroup,i,1+e-i,s,"",""),d.localSettings=h,d):(g=o._createPatternToken(n,u.group,i,1+e-i,s,"(",")"),g.localSettings=h,g)},_parseGroupConstructToken:function(n,t,i,r){var f=System.Text.RegularExpressions.RegexNetEngineParser,o=f.tokenTypes,u=n[i],h,c,s,l,e,a,v;if(u!=="?"||i+1>=r)return null;if(u=n[i+1],u===":"||u==="="||u==="!"||u===">")return f._createPatternToken(n,o.groupConstruct,i,2);if(u==="#")return f._createPatternToken(n,o.commentInline,i,2);if(u==="(")return f._parseAlternationGroupExprToken(n,t,i,r);if(u==="<"&&i+2<r&&(h=n[i+2],h==="="||h==="!"))return f._createPatternToken(n,o.groupConstruct,i,3);if(u==="<"||u==="'"){if(c=u==="<"?">":u,s=f._matchUntil(n,i+2,r,c),s.unmatchLength!==1||s.matchLength===0)throw new System.ArgumentException("Unrecognized grouping construct.");if(l=s.match.slice(0,1),"`~@#$%^&*()-+{}[]|\\/|'\";:,.?".indexOf(l)>=0)throw new System.ArgumentException("Invalid group name: Group names must begin with a word character.");return f._createPatternToken(n,o.groupConstructName,i,2+s.matchLength+1)}if(e=f._matchChars(n,i+1,r,"imnsx-"),e.matchLength>0&&(e.unmatchCh===":"||e.unmatchCh===")"))return a=e.unmatchCh===":"?o.groupConstructImnsx:o.groupConstructImnsxMisc,v=e.unmatchCh===":"?1:0,f._createPatternToken(n,a,i,1+e.matchLength+v);throw new System.ArgumentException("Unrecognized grouping construct.");},_parseQuantifierToken:function(n,t,i){var r=System.Text.RegularExpressions.RegexNetEngineParser,o=r.tokenTypes,f=n[t],u,e;return f==="*"||f==="+"||f==="?"?r._createPatternToken(n,o.quantifier,t,1):f!=="{"?null:(u=r._matchChars(n,t+1,i,r._decSymbols),u.matchLength===0)?null:u.unmatchCh==="}"?r._createPatternToken(n,o.quantifierN,t,1+u.matchLength+1):u.unmatchCh!==","?null:(e=r._matchChars(n,u.unmatchIndex+1,i,r._decSymbols),e.matchLength===0&&e.unmatchCh!=="}")?null:r._createPatternToken(n,o.quantifierNM,t,1+u.matchLength+1+e.matchLength+1)},_parseAlternationToken:function(n,t){var i=System.Text.RegularExpressions.RegexNetEngineParser,r=i.tokenTypes,u=n[t];return u!=="|"?null:i._createPatternToken(n,r.alternation,t,1)},_parseAlternationGroupExprToken:function(n,t,i,r){var e=System.Text.RegularExpressions.RegexNetEngineParser,f=e.tokenTypes,o=n[i],u;if(o!=="?"||i+1>=r||n[i+1]!=="("||(u=e._parseGroupToken(n,t,i+1,r),u==null))return null;if(u.children&&u.children.length)switch(u.children[0].type){case f.groupConstruct:case f.groupConstructName:case f.groupConstructImnsx:case f.groupConstructImnsxMisc:throw new System.NotSupportedException("Group constructs are not supported for Alternation expressions.");}return e._createPatternToken(n,f.alternationGroupExpr,u.index-1,1+u.length,u.children,"?(",")")},_parseXModeCommentToken:function(n,t,i){var f=System.Text.RegularExpressions.RegexNetEngineParser,e=f.tokenTypes,u=n[t],r;if(u!=="#")return null;for(r=t+1;r<i;)if(u=n[r],++r,u==="\n")break;return f._createPatternToken(n,e.commentXMode,t,r-t)},_createLiteralToken:function(n){var t=System.Text.RegularExpressions.RegexNetEngineParser;return t._createPatternToken(n,t.tokenTypes.literal,0,n.length)},_createPositiveLookaheadToken:function(n,t){var r=System.Text.RegularExpressions.RegexNetEngineParser,i="(?="+n+")";return r._parseGroupToken(i,t,0,i.length)},_createPatternToken:function(n,t,i,r,u,f,e){var o={type:t,index:i,length:r,value:n.slice(i,i+r)};return u!=null&&u.length>0&&(o.children=u,o.childrenPrefix=f,o.childrenPostfix=e),o},_modifyPatternToken:function(n,t,i,r,u){i!=null&&(n.type=i);(r!=null||u!=null)&&(r!=null&&(n.index=r),u!=null&&(n.length=u),n.value=t.slice(n.index,n.index+n.length))},_updatePatternToken:function(n,t,i,r,u){n.type=t;n.index=i;n.length=r;n.value=u},_matchChars:function(n,t,i,r,u){var f={match:"",matchIndex:-1,matchLength:0,unmatchCh:"",unmatchIndex:-1,unmatchLength:0},e=t,o;for(u!=null&&u>=0&&(i=t+u);e<i;){if(o=n[e],r.indexOf(o)<0){f.unmatchCh=o;f.unmatchIndex=e;f.unmatchLength=1;break}e++}return e>t&&(f.match=n.slice(t,e),f.matchIndex=t,f.matchLength=e-t),f},_matchUntil:function(n,t,i,r,u){var f={match:"",matchIndex:-1,matchLength:0,unmatchCh:"",unmatchIndex:-1,unmatchLength:0},e=t,o;for(u!=null&&u>=0&&(i=t+u);e<i;){if(o=n[e],r.indexOf(o)>=0){f.unmatchCh=o;f.unmatchIndex=e;f.unmatchLength=1;break}e++}return e>t&&(f.match=n.slice(t,e),f.matchIndex=t,f.matchLength=e-t),f}}});System.Console={output:null,log:function(n){if(System.Console.output!=null){System.Console.output+=n;return}console.log(n)}};Bridge.define("System.Threading.Timer",{inherits:[System.IDisposable],statics:{MAX_SUPPORTED_TIMEOUT:4294967294,EXC_LESS:"Number must be either non-negative and less than or equal to Int32.MaxValue or -1.",EXC_MORE:"Time-out interval must be less than 2^32-2.",EXC_DISPOSED:"The timer has been already disposed."},dueTime:System.Int64(0),period:System.Int64(0),timerCallback:null,state:null,id:null,disposed:!1,constructor$1:function(n,t,i,r){this.timerSetup(n,t,System.Int64(i),System.Int64(r))},constructor$3:function(n,t,i,r){var u=Bridge.Int.clip64(i.getTotalMilliseconds()),f=Bridge.Int.clip64(r.getTotalMilliseconds());this.timerSetup(n,t,u,f)},constructor$4:function(n,t,i,r){this.timerSetup(n,t,System.Int64(i),System.Int64(r))},constructor$2:function(n,t,i,r){this.timerSetup(n,t,i,r)},constructor:function(n){this.timerSetup(n,this,System.Int64(-1),System.Int64(-1))},timerSetup:function(n,t,i,r){if(this.disposed)throw new System.InvalidOperationException(System.Threading.Timer.EXC_DISPOSED);if(n==null)throw new System.ArgumentNullException("TimerCallback");if(i.lt(System.Int64(-1)))throw new System.ArgumentOutOfRangeException("dueTime",System.Threading.Timer.EXC_LESS);if(r.lt(System.Int64(-1)))throw new System.ArgumentOutOfRangeException("period",System.Threading.Timer.EXC_LESS);if(i.gt(System.Int64(System.Threading.Timer.MAX_SUPPORTED_TIMEOUT)))throw new System.ArgumentOutOfRangeException("dueTime",System.Threading.Timer.EXC_MORE);if(r.gt(System.Int64(System.Threading.Timer.MAX_SUPPORTED_TIMEOUT)))throw new System.ArgumentOutOfRangeException("period",System.Threading.Timer.EXC_MORE);return this.dueTime=i,this.period=r,this.state=t,this.timerCallback=n,this.runTimer(this.dueTime)},handleCallback:function(){if(!this.disposed&&this.timerCallback!=null){var n=this.id;this.timerCallback(this.state);System.Nullable.eq(this.id,n)&&this.runTimer(this.period,!1)}},runTimer:function(n,t){if(t===void 0&&(t=!0),t&&this.disposed)throw new System.InvalidOperationException(System.Threading.Timer.EXC_DISPOSED);if(n.ne(System.Int64(-1))&&!this.disposed){var i=n.toNumber();return this.id=Bridge.global.setTimeout(Bridge.fn.bind(this,this.handleCallback),i),!0}return!1},change:function(n,t){return this.changeTimer(System.Int64(n),System.Int64(t))},change$2:function(n,t){return this.changeTimer(Bridge.Int.clip64(n.getTotalMilliseconds()),Bridge.Int.clip64(t.getTotalMilliseconds()))},change$3:function(n,t){return this.changeTimer(System.Int64(n),System.Int64(t))},change$1:function(n,t){return this.changeTimer(n,t)},changeTimer:function(n,t){return this.clearTimeout(),this.timerSetup(this.timerCallback,this.state,n,t)},clearTimeout:function(){System.Nullable.hasValue(this.id)&&(window.clearTimeout(System.Nullable.getValue(this.id)),this.id=null)},dispose:function(){this.clearTimeout();this.disposed=!0}});typeof define=="function"&&define.amd?define("bridge",[],function(){return Bridge}):typeof module!="undefined"&&module.exports&&(module.exports=Bridge)})(this);

(function (globals) {
    "use strict";


    /** @namespace CM.Cryptography */

    /**
     * A highly portable Rijndael/Advanced Encryption Standard (AES) - CBC implementation 
     based on work by Brad Conte (bradconte.com)
     *
     * @public
     * @class CM.Cryptography.AES
     */
    Bridge.define('CM.Cryptography.AES', {
        statics: {
            AES_128_ROUNDS: 10,
            AES_192_ROUNDS: 12,
            AES_256_ROUNDS: 14,
            AES_BLOCK_SIZE: 16,
            aes_invsbox: null,
            aes_sbox: null,
            gf_mul: null,
            config: {
                init: function () {
                    this.aes_invsbox = [[82, 9, 106, 213, 48, 54, 165, 56, 191, 64, 163, 158, 129, 243, 215, 251], [124, 227, 57, 130, 155, 47, 255, 135, 52, 142, 67, 68, 196, 222, 233, 203], [84, 123, 148, 50, 166, 194, 35, 61, 238, 76, 149, 11, 66, 250, 195, 78], [8, 46, 161, 102, 40, 217, 36, 178, 118, 91, 162, 73, 109, 139, 209, 37], [114, 248, 246, 100, 134, 104, 152, 22, 212, 164, 92, 204, 93, 101, 182, 146], [108, 112, 72, 80, 253, 237, 185, 218, 94, 21, 70, 87, 167, 141, 157, 132], [144, 216, 171, 0, 140, 188, 211, 10, 247, 228, 88, 5, 184, 179, 69, 6], [208, 44, 30, 143, 202, 63, 15, 2, 193, 175, 189, 3, 1, 19, 138, 107], [58, 145, 17, 65, 79, 103, 220, 234, 151, 242, 207, 206, 240, 180, 230, 115], [150, 172, 116, 34, 231, 173, 53, 133, 226, 249, 55, 232, 28, 117, 223, 110], [71, 241, 26, 113, 29, 41, 197, 137, 111, 183, 98, 14, 170, 24, 190, 27], [252, 86, 62, 75, 198, 210, 121, 32, 154, 219, 192, 254, 120, 205, 90, 244], [31, 221, 168, 51, 136, 7, 199, 49, 177, 18, 16, 89, 39, 128, 236, 95], [96, 81, 127, 169, 25, 181, 74, 13, 45, 229, 122, 159, 147, 201, 156, 239], [160, 224, 59, 77, 174, 42, 245, 176, 200, 235, 187, 60, 131, 83, 153, 97], [23, 43, 4, 126, 186, 119, 214, 38, 225, 105, 20, 99, 85, 33, 12, 125]];
                    this.aes_sbox = [[99, 124, 119, 123, 242, 107, 111, 197, 48, 1, 103, 43, 254, 215, 171, 118], [202, 130, 201, 125, 250, 89, 71, 240, 173, 212, 162, 175, 156, 164, 114, 192], [183, 253, 147, 38, 54, 63, 247, 204, 52, 165, 229, 241, 113, 216, 49, 21], [4, 199, 35, 195, 24, 150, 5, 154, 7, 18, 128, 226, 235, 39, 178, 117], [9, 131, 44, 26, 27, 110, 90, 160, 82, 59, 214, 179, 41, 227, 47, 132], [83, 209, 0, 237, 32, 252, 177, 91, 106, 203, 190, 57, 74, 76, 88, 207], [208, 239, 170, 251, 67, 77, 51, 133, 69, 249, 2, 127, 80, 60, 159, 168], [81, 163, 64, 143, 146, 157, 56, 245, 188, 182, 218, 33, 16, 255, 243, 210], [205, 12, 19, 236, 95, 151, 68, 23, 196, 167, 126, 61, 100, 93, 25, 115], [96, 129, 79, 220, 34, 42, 144, 136, 70, 238, 184, 20, 222, 94, 11, 219], [224, 50, 58, 10, 73, 6, 36, 92, 194, 211, 172, 98, 145, 149, 228, 121], [231, 200, 55, 109, 141, 213, 78, 169, 108, 86, 244, 234, 101, 122, 174, 8], [186, 120, 37, 46, 28, 166, 180, 198, 232, 221, 116, 31, 75, 189, 139, 138], [112, 62, 181, 102, 72, 3, 246, 14, 97, 53, 87, 185, 134, 193, 29, 158], [225, 248, 152, 17, 105, 217, 142, 148, 155, 30, 135, 233, 206, 85, 40, 223], [140, 161, 137, 13, 191, 230, 66, 104, 65, 153, 45, 15, 176, 84, 187, 22]];
                    this.gf_mul = [[0, 0, 0, 0, 0, 0], [2, 3, 9, 11, 13, 14], [4, 6, 18, 22, 26, 28], [6, 5, 27, 29, 23, 18], [8, 12, 36, 44, 52, 56], [10, 15, 45, 39, 57, 54], [12, 10, 54, 58, 46, 36], [14, 9, 63, 49, 35, 42], [16, 24, 72, 88, 104, 112], [18, 27, 65, 83, 101, 126], [20, 30, 90, 78, 114, 108], [22, 29, 83, 69, 127, 98], [24, 20, 108, 116, 92, 72], [26, 23, 101, 127, 81, 70], [28, 18, 126, 98, 70, 84], [30, 17, 119, 105, 75, 90], [32, 48, 144, 176, 208, 224], [34, 51, 153, 187, 221, 238], [36, 54, 130, 166, 202, 252], [38, 53, 139, 173, 199, 242], [40, 60, 180, 156, 228, 216], [42, 63, 189, 151, 233, 214], [44, 58, 166, 138, 254, 196], [46, 57, 175, 129, 243, 202], [48, 40, 216, 232, 184, 144], [50, 43, 209, 227, 181, 158], [52, 46, 202, 254, 162, 140], [54, 45, 195, 245, 175, 130], [56, 36, 252, 196, 140, 168], [58, 39, 245, 207, 129, 166], [60, 34, 238, 210, 150, 180], [62, 33, 231, 217, 155, 186], [64, 96, 59, 123, 187, 219], [66, 99, 50, 112, 182, 213], [68, 102, 41, 109, 161, 199], [70, 101, 32, 102, 172, 201], [72, 108, 31, 87, 143, 227], [74, 111, 22, 92, 130, 237], [76, 106, 13, 65, 149, 255], [78, 105, 4, 74, 152, 241], [80, 120, 115, 35, 211, 171], [82, 123, 122, 40, 222, 165], [84, 126, 97, 53, 201, 183], [86, 125, 104, 62, 196, 185], [88, 116, 87, 15, 231, 147], [90, 119, 94, 4, 234, 157], [92, 114, 69, 25, 253, 143], [94, 113, 76, 18, 240, 129], [96, 80, 171, 203, 107, 59], [98, 83, 162, 192, 102, 53], [100, 86, 185, 221, 113, 39], [102, 85, 176, 214, 124, 41], [104, 92, 143, 231, 95, 3], [106, 95, 134, 236, 82, 13], [108, 90, 157, 241, 69, 31], [110, 89, 148, 250, 72, 17], [112, 72, 227, 147, 3, 75], [114, 75, 234, 152, 14, 69], [116, 78, 241, 133, 25, 87], [118, 77, 248, 142, 20, 89], [120, 68, 199, 191, 55, 115], [122, 71, 206, 180, 58, 125], [124, 66, 213, 169, 45, 111], [126, 65, 220, 162, 32, 97], [128, 192, 118, 246, 109, 173], [130, 195, 127, 253, 96, 163], [132, 198, 100, 224, 119, 177], [134, 197, 109, 235, 122, 191], [136, 204, 82, 218, 89, 149], [138, 207, 91, 209, 84, 155], [140, 202, 64, 204, 67, 137], [142, 201, 73, 199, 78, 135], [144, 216, 62, 174, 5, 221], [146, 219, 55, 165, 8, 211], [148, 222, 44, 184, 31, 193], [150, 221, 37, 179, 18, 207], [152, 212, 26, 130, 49, 229], [154, 215, 19, 137, 60, 235], [156, 210, 8, 148, 43, 249], [158, 209, 1, 159, 38, 247], [160, 240, 230, 70, 189, 77], [162, 243, 239, 77, 176, 67], [164, 246, 244, 80, 167, 81], [166, 245, 253, 91, 170, 95], [168, 252, 194, 106, 137, 117], [170, 255, 203, 97, 132, 123], [172, 250, 208, 124, 147, 105], [174, 249, 217, 119, 158, 103], [176, 232, 174, 30, 213, 61], [178, 235, 167, 21, 216, 51], [180, 238, 188, 8, 207, 33], [182, 237, 181, 3, 194, 47], [184, 228, 138, 50, 225, 5], [186, 231, 131, 57, 236, 11], [188, 226, 152, 36, 251, 25], [190, 225, 145, 47, 246, 23], [192, 160, 77, 141, 214, 118], [194, 163, 68, 134, 219, 120], [196, 166, 95, 155, 204, 106], [198, 165, 86, 144, 193, 100], [200, 172, 105, 161, 226, 78], [202, 175, 96, 170, 239, 64], [204, 170, 123, 183, 248, 82], [206, 169, 114, 188, 245, 92], [208, 184, 5, 213, 190, 6], [210, 187, 12, 222, 179, 8], [212, 190, 23, 195, 164, 26], [214, 189, 30, 200, 169, 20], [216, 180, 33, 249, 138, 62], [218, 183, 40, 242, 135, 48], [220, 178, 51, 239, 144, 34], [222, 177, 58, 228, 157, 44], [224, 144, 221, 61, 6, 150], [226, 147, 212, 54, 11, 152], [228, 150, 207, 43, 28, 138], [230, 149, 198, 32, 17, 132], [232, 156, 249, 17, 50, 174], [234, 159, 240, 26, 63, 160], [236, 154, 235, 7, 40, 178], [238, 153, 226, 12, 37, 188], [240, 136, 149, 101, 110, 230], [242, 139, 156, 110, 99, 232], [244, 142, 135, 115, 116, 250], [246, 141, 142, 120, 121, 244], [248, 132, 177, 73, 90, 222], [250, 135, 184, 66, 87, 208], [252, 130, 163, 95, 64, 194], [254, 129, 170, 84, 77, 204], [27, 155, 236, 247, 218, 65], [25, 152, 229, 252, 215, 79], [31, 157, 254, 225, 192, 93], [29, 158, 247, 234, 205, 83], [19, 151, 200, 219, 238, 121], [17, 148, 193, 208, 227, 119], [23, 145, 218, 205, 244, 101], [21, 146, 211, 198, 249, 107], [11, 131, 164, 175, 178, 49], [9, 128, 173, 164, 191, 63], [15, 133, 182, 185, 168, 45], [13, 134, 191, 178, 165, 35], [3, 143, 128, 131, 134, 9], [1, 140, 137, 136, 139, 7], [7, 137, 146, 149, 156, 21], [5, 138, 155, 158, 145, 27], [59, 171, 124, 71, 10, 161], [57, 168, 117, 76, 7, 175], [63, 173, 110, 81, 16, 189], [61, 174, 103, 90, 29, 179], [51, 167, 88, 107, 62, 153], [49, 164, 81, 96, 51, 151], [55, 161, 74, 125, 36, 133], [53, 162, 67, 118, 41, 139], [43, 179, 52, 31, 98, 209], [41, 176, 61, 20, 111, 223], [47, 181, 38, 9, 120, 205], [45, 182, 47, 2, 117, 195], [35, 191, 16, 51, 86, 233], [33, 188, 25, 56, 91, 231], [39, 185, 2, 37, 76, 245], [37, 186, 11, 46, 65, 251], [91, 251, 215, 140, 97, 154], [89, 248, 222, 135, 108, 148], [95, 253, 197, 154, 123, 134], [93, 254, 204, 145, 118, 136], [83, 247, 243, 160, 85, 162], [81, 244, 250, 171, 88, 172], [87, 241, 225, 182, 79, 190], [85, 242, 232, 189, 66, 176], [75, 227, 159, 212, 9, 234], [73, 224, 150, 223, 4, 228], [79, 229, 141, 194, 19, 246], [77, 230, 132, 201, 30, 248], [67, 239, 187, 248, 61, 210], [65, 236, 178, 243, 48, 220], [71, 233, 169, 238, 39, 206], [69, 234, 160, 229, 42, 192], [123, 203, 71, 60, 177, 122], [121, 200, 78, 55, 188, 116], [127, 205, 85, 42, 171, 102], [125, 206, 92, 33, 166, 104], [115, 199, 99, 16, 133, 66], [113, 196, 106, 27, 136, 76], [119, 193, 113, 6, 159, 94], [117, 194, 120, 13, 146, 80], [107, 211, 15, 100, 217, 10], [105, 208, 6, 111, 212, 4], [111, 213, 29, 114, 195, 22], [109, 214, 20, 121, 206, 24], [99, 223, 43, 72, 237, 50], [97, 220, 34, 67, 224, 60], [103, 217, 57, 94, 247, 46], [101, 218, 48, 85, 250, 32], [155, 91, 154, 1, 183, 236], [153, 88, 147, 10, 186, 226], [159, 93, 136, 23, 173, 240], [157, 94, 129, 28, 160, 254], [147, 87, 190, 45, 131, 212], [145, 84, 183, 38, 142, 218], [151, 81, 172, 59, 153, 200], [149, 82, 165, 48, 148, 198], [139, 67, 210, 89, 223, 156], [137, 64, 219, 82, 210, 146], [143, 69, 192, 79, 197, 128], [141, 70, 201, 68, 200, 142], [131, 79, 246, 117, 235, 164], [129, 76, 255, 126, 230, 170], [135, 73, 228, 99, 241, 184], [133, 74, 237, 104, 252, 182], [187, 107, 10, 177, 103, 12], [185, 104, 3, 186, 106, 2], [191, 109, 24, 167, 125, 16], [189, 110, 17, 172, 112, 30], [179, 103, 46, 157, 83, 52], [177, 100, 39, 150, 94, 58], [183, 97, 60, 139, 73, 40], [181, 98, 53, 128, 68, 38], [171, 115, 66, 233, 15, 124], [169, 112, 75, 226, 2, 114], [175, 117, 80, 255, 21, 96], [173, 118, 89, 244, 24, 110], [163, 127, 102, 197, 59, 68], [161, 124, 111, 206, 54, 74], [167, 121, 116, 211, 33, 88], [165, 122, 125, 216, 44, 86], [219, 59, 161, 122, 12, 55], [217, 56, 168, 113, 1, 57], [223, 61, 179, 108, 22, 43], [221, 62, 186, 103, 27, 37], [211, 55, 133, 86, 56, 15], [209, 52, 140, 93, 53, 1], [215, 49, 151, 64, 34, 19], [213, 50, 158, 75, 47, 29], [203, 35, 233, 34, 100, 71], [201, 32, 224, 41, 105, 73], [207, 37, 251, 52, 126, 91], [205, 38, 242, 63, 115, 85], [195, 47, 205, 14, 80, 127], [193, 44, 196, 5, 93, 113], [199, 41, 223, 24, 74, 99], [197, 42, 214, 19, 71, 109], [251, 11, 49, 202, 220, 215], [249, 8, 56, 193, 209, 217], [255, 13, 35, 220, 198, 203], [253, 14, 42, 215, 203, 197], [243, 7, 21, 230, 232, 239], [241, 4, 28, 237, 229, 225], [247, 1, 7, 240, 242, 243], [245, 2, 14, 251, 255, 253], [235, 19, 121, 146, 180, 167], [233, 16, 112, 153, 185, 169], [239, 21, 107, 132, 174, 187], [237, 22, 98, 143, 163, 181], [227, 31, 93, 190, 128, 159], [225, 28, 84, 181, 141, 145], [231, 25, 79, 168, 154, 131], [229, 26, 70, 163, 151, 141]];
                }
            },
            decrypt: function (encrypted, key, iv) {
                var key_schedule = System.Array.init(60, 0);
                var keySize = (key.length * 8) | 0;
                if (keySize !== 256) {
                    throw new System.ArgumentException("Key size must be 256 bits");
                }
                CM.Cryptography.AES.aes_key_setup(key, key_schedule, keySize);

                var output = System.Array.init(encrypted.length, 0);
                if (!CM.Cryptography.AES.aes_decrypt_cbc(encrypted, encrypted.length, output, key_schedule, keySize, iv)) {
                    throw new System.Exception("Invalid padding.");
                }

                return CM.Cryptography.AES.pKCS7Remove(output);
            },
            encrypt: function (clear, key, iv) {
                var key_schedule = System.Array.init(60, 0);
                var keySize = (key.length * 8) | 0;
                if (keySize !== 256) {
                    throw new System.ArgumentException("Key size must be 256 bits");
                }
                CM.Cryptography.AES.aes_key_setup(key, key_schedule, keySize);

                var padded = CM.Cryptography.AES.pKCS7Pad(clear);

                var output = System.Array.init(padded.length, 0);
                if (!CM.Cryptography.AES.aes_encrypt_cbc(padded, padded.length, output, key_schedule, keySize, iv)) {
                    throw new System.Exception("Invalid padding for input length " + clear.length + ".");
                }
                return output;
            },
            /**
             * Performs the AddRoundKey step. Each round has its own pre-generated 16-byte key in the
             form of 4 integers (the "w" array). Each integer is XOR'd by one column of the state.
             Also performs the job of InvAddRoundKey(); since the function is a simple XOR process,
             it is its own inverse.
             *
             * @static
             * @private
             * @this CM.Cryptography.AES
             * @memberof CM.Cryptography.AES
             * @param   {Array.<Array.<number>>}    state    
             * @param   {Array.<number>}            w        
             * @param   {number}                    idx
             * @return  {void}
             */
            addRoundKey: function (state, w, idx) {

                var subkey = System.Array.init(4, 0);

                // Subkey 1
                subkey[0] = (((((w[((idx + 0))] >>> 24) & 255))));
                subkey[1] = (((((w[((idx + 0))] >>> 16) & 255))));
                subkey[2] = (((((w[((idx + 0))] >>> 8) & 255))));
                subkey[3] = (((((w[((idx + 0))]) & 255))));
                state[0][0] = (state[0][0] ^ subkey[0]);
                state[1][0] = (state[1][0] ^ subkey[1]);
                state[2][0] = (state[2][0] ^ subkey[2]);
                state[3][0] = (state[3][0] ^ subkey[3]);
                // Subkey 2
                subkey[0] = (((((w[((idx + 1))] >>> 24) & 255))));
                subkey[1] = (((((w[((idx + 1))] >>> 16) & 255))));
                subkey[2] = (((((w[((idx + 1))] >>> 8) & 255))));
                subkey[3] = (((((w[((idx + 1))]) & 255))));
                state[0][1] = (state[0][1] ^ subkey[0]);
                state[1][1] = (state[1][1] ^ subkey[1]);
                state[2][1] = (state[2][1] ^ subkey[2]);
                state[3][1] = (state[3][1] ^ subkey[3]);
                // Subkey 3
                subkey[0] = (((((w[((idx + 2))] >>> 24) & 255))));
                subkey[1] = (((((w[((idx + 2))] >>> 16) & 255))));
                subkey[2] = (((((w[((idx + 2))] >>> 8) & 255))));
                subkey[3] = (((((w[((idx + 2))]) & 255))));
                state[0][2] = (state[0][2] ^ subkey[0]);
                state[1][2] = (state[1][2] ^ subkey[1]);
                state[2][2] = (state[2][2] ^ subkey[2]);
                state[3][2] = (state[3][2] ^ subkey[3]);
                // Subkey 4
                subkey[0] = (((((w[((idx + 3))] >>> 24) & 255))));
                subkey[1] = (((((w[((idx + 3))] >>> 16) & 255))));
                subkey[2] = (((((w[((idx + 3))] >>> 8) & 255))));
                subkey[3] = (((((w[((idx + 3))]) & 255))));
                state[0][3] = (state[0][3] ^ subkey[0]);
                state[1][3] = (state[1][3] ^ subkey[1]);
                state[2][3] = (state[2][3] ^ subkey[2]);
                state[3][3] = (state[3][3] ^ subkey[3]);
            },
            aes_decrypt: function (input, output, key, keysize) {
                var state = [System.Array.init(4, 0), System.Array.init(4, 0), System.Array.init(4, 0), System.Array.init(4, 0)];


                // Copy the input to the state.
                state[0][0] = input[0];
                state[1][0] = input[1];
                state[2][0] = input[2];
                state[3][0] = input[3];
                state[0][1] = input[4];
                state[1][1] = input[5];
                state[2][1] = input[6];
                state[3][1] = input[7];
                state[0][2] = input[8];
                state[1][2] = input[9];
                state[2][2] = input[10];
                state[3][2] = input[11];
                state[0][3] = input[12];
                state[1][3] = input[13];
                state[2][3] = input[14];
                state[3][3] = input[15];

                // Perform the necessary number of rounds. The round key is added first.
                // The last round does not perform the MixColumns step.
                if (keysize > 128) {
                    if (keysize > 192) {
                        CM.Cryptography.AES.addRoundKey(state, key, 56);
                        CM.Cryptography.AES.invShiftRows(state);
                        CM.Cryptography.AES.invSubBytes(state);
                        CM.Cryptography.AES.addRoundKey(state, key, 52);
                        CM.Cryptography.AES.invMixColumns(state);
                        CM.Cryptography.AES.invShiftRows(state);
                        CM.Cryptography.AES.invSubBytes(state);
                        CM.Cryptography.AES.addRoundKey(state, key, 48);
                        CM.Cryptography.AES.invMixColumns(state);
                    }
                    else {
                        CM.Cryptography.AES.addRoundKey(state, key, 48);
                    }
                    CM.Cryptography.AES.invShiftRows(state);
                    CM.Cryptography.AES.invSubBytes(state);
                    CM.Cryptography.AES.addRoundKey(state, key, 44);
                    CM.Cryptography.AES.invMixColumns(state);
                    CM.Cryptography.AES.invShiftRows(state);
                    CM.Cryptography.AES.invSubBytes(state);
                    CM.Cryptography.AES.addRoundKey(state, key, 40);
                    CM.Cryptography.AES.invMixColumns(state);
                }
                else {
                    CM.Cryptography.AES.addRoundKey(state, key, 40);
                }
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 36);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 32);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 28);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 24);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 20);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 16);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 12);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 8);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 4);
                CM.Cryptography.AES.invMixColumns(state);
                CM.Cryptography.AES.invShiftRows(state);
                CM.Cryptography.AES.invSubBytes(state);
                CM.Cryptography.AES.addRoundKey(state, key, 0);

                // Copy the state to the output array.
                output[0] = state[0][0];
                output[1] = state[1][0];
                output[2] = state[2][0];
                output[3] = state[3][0];
                output[4] = state[0][1];
                output[5] = state[1][1];
                output[6] = state[2][1];
                output[7] = state[3][1];
                output[8] = state[0][2];
                output[9] = state[1][2];
                output[10] = state[2][2];
                output[11] = state[3][2];
                output[12] = state[0][3];
                output[13] = state[1][3];
                output[14] = state[2][3];
                output[15] = state[3][3];
            },
            aes_decrypt_cbc: function (input, in_len, output, key, keysize, iv) {
                var buf_in = System.Array.init(CM.Cryptography.AES.AES_BLOCK_SIZE, 0);
                var buf_out = System.Array.init(CM.Cryptography.AES.AES_BLOCK_SIZE, 0);
                var iv_buf = System.Array.init(CM.Cryptography.AES.AES_BLOCK_SIZE, 0);
                var blocks, idx;

                if (in_len % CM.Cryptography.AES.AES_BLOCK_SIZE !== 0) {
                    return false;
                }

                blocks = (Bridge.Int.div(in_len, CM.Cryptography.AES.AES_BLOCK_SIZE)) | 0;

                System.Array.copy(iv, 0, iv_buf, 0, CM.Cryptography.AES.AES_BLOCK_SIZE);

                for (idx = 0; idx < blocks; idx = (idx + 1) | 0) {
                    System.Array.copy(input, ((idx * CM.Cryptography.AES.AES_BLOCK_SIZE) | 0), buf_in, 0, CM.Cryptography.AES.AES_BLOCK_SIZE);
                    CM.Cryptography.AES.aes_decrypt(buf_in, buf_out, key, keysize);
                    CM.Cryptography.AES.xor_buf(iv_buf, buf_out, CM.Cryptography.AES.AES_BLOCK_SIZE);
                    System.Array.copy(buf_out, 0, output, ((idx * CM.Cryptography.AES.AES_BLOCK_SIZE) | 0), CM.Cryptography.AES.AES_BLOCK_SIZE);
                    System.Array.copy(buf_in, 0, iv_buf, 0, CM.Cryptography.AES.AES_BLOCK_SIZE);
                }

                return true;
            },
            aes_encrypt: function (input, output, key, keysize) {
                var state = [System.Array.init(4, 0), System.Array.init(4, 0), System.Array.init(4, 0), System.Array.init(4, 0)];

                // Copy input array (should be 16 bytes long) to a matrix (sequential bytes are ordered
                // by row, not col) called "state" for processing.
                // *** Implementation note: The official AES documentation references the state by
                // column, then row. Accessing an element in C requires row then column. Thus, all state
                // references in AES must have the column and row indexes reversed for C implementation.
                state[0][0] = input[0];
                state[1][0] = input[1];
                state[2][0] = input[2];
                state[3][0] = input[3];
                state[0][1] = input[4];
                state[1][1] = input[5];
                state[2][1] = input[6];
                state[3][1] = input[7];
                state[0][2] = input[8];
                state[1][2] = input[9];
                state[2][2] = input[10];
                state[3][2] = input[11];
                state[0][3] = input[12];
                state[1][3] = input[13];
                state[2][3] = input[14];
                state[3][3] = input[15];

                // Perform the necessary number of rounds. The round key is added first.
                // The last round does not perform the MixColumns step.
                CM.Cryptography.AES.addRoundKey(state, key, 0);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 4);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 8);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 12);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 16);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 20);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 24);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 28);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 32);
                CM.Cryptography.AES.subBytes(state);
                CM.Cryptography.AES.shiftRows(state);
                CM.Cryptography.AES.mixColumns(state);
                CM.Cryptography.AES.addRoundKey(state, key, 36);
                if (keysize !== 128) {
                    CM.Cryptography.AES.subBytes(state);
                    CM.Cryptography.AES.shiftRows(state);
                    CM.Cryptography.AES.mixColumns(state);
                    CM.Cryptography.AES.addRoundKey(state, key, 40);
                    CM.Cryptography.AES.subBytes(state);
                    CM.Cryptography.AES.shiftRows(state);
                    CM.Cryptography.AES.mixColumns(state);
                    CM.Cryptography.AES.addRoundKey(state, key, 44);
                    if (keysize !== 192) {
                        CM.Cryptography.AES.subBytes(state);
                        CM.Cryptography.AES.shiftRows(state);
                        CM.Cryptography.AES.mixColumns(state);
                        CM.Cryptography.AES.addRoundKey(state, key, 48);
                        CM.Cryptography.AES.subBytes(state);
                        CM.Cryptography.AES.shiftRows(state);
                        CM.Cryptography.AES.mixColumns(state);
                        CM.Cryptography.AES.addRoundKey(state, key, 52);
                        CM.Cryptography.AES.subBytes(state);
                        CM.Cryptography.AES.shiftRows(state);
                        CM.Cryptography.AES.addRoundKey(state, key, 56);
                    }
                    else {
                        CM.Cryptography.AES.subBytes(state);
                        CM.Cryptography.AES.shiftRows(state);
                        CM.Cryptography.AES.addRoundKey(state, key, 48);
                    }
                }
                else {
                    CM.Cryptography.AES.subBytes(state);
                    CM.Cryptography.AES.shiftRows(state);
                    CM.Cryptography.AES.addRoundKey(state, key, 40);
                }

                // Copy the state to the output array.
                output[0] = state[0][0];
                output[1] = state[1][0];
                output[2] = state[2][0];
                output[3] = state[3][0];
                output[4] = state[0][1];
                output[5] = state[1][1];
                output[6] = state[2][1];
                output[7] = state[3][1];
                output[8] = state[0][2];
                output[9] = state[1][2];
                output[10] = state[2][2];
                output[11] = state[3][2];
                output[12] = state[0][3];
                output[13] = state[1][3];
                output[14] = state[2][3];
                output[15] = state[3][3];
            },
            aes_encrypt_cbc: function (input, in_len, output, key, keysize, iv) {
                var buf_in = System.Array.init(CM.Cryptography.AES.AES_BLOCK_SIZE, 0);
                var buf_out = System.Array.init(CM.Cryptography.AES.AES_BLOCK_SIZE, 0);
                var iv_buf = System.Array.init(CM.Cryptography.AES.AES_BLOCK_SIZE, 0);
                var blocks, idx;

                if (in_len % CM.Cryptography.AES.AES_BLOCK_SIZE !== 0) {
                    return false;
                }

                blocks = (Bridge.Int.div(in_len, CM.Cryptography.AES.AES_BLOCK_SIZE)) | 0;

                System.Array.copy(iv, 0, iv_buf, 0, CM.Cryptography.AES.AES_BLOCK_SIZE);

                for (idx = 0; idx < blocks; idx = (idx + 1) | 0) {
                    System.Array.copy(input, ((idx * CM.Cryptography.AES.AES_BLOCK_SIZE) | 0), buf_in, 0, CM.Cryptography.AES.AES_BLOCK_SIZE);
                    CM.Cryptography.AES.xor_buf(iv_buf, buf_in, CM.Cryptography.AES.AES_BLOCK_SIZE);
                    CM.Cryptography.AES.aes_encrypt(buf_in, buf_out, key, keysize);
                    System.Array.copy(buf_out, 0, output, ((idx * CM.Cryptography.AES.AES_BLOCK_SIZE) | 0), CM.Cryptography.AES.AES_BLOCK_SIZE);
                    System.Array.copy(buf_out, 0, iv_buf, 0, CM.Cryptography.AES.AES_BLOCK_SIZE);
                }

                return true;
            },
            /**
             * Performs the action of generating the keys that will be used in every round of
             encryption. "key" is the user-supplied input key, "w" is the output key schedule,
             "keysize" is the length in bits of "key", must be 128, 192, or 256.
             *
             * @static
             * @private
             * @this CM.Cryptography.AES
             * @memberof CM.Cryptography.AES
             * @param   {Array.<number>}    key        
             * @param   {Array.<number>}    w          
             * @param   {number}            keysize
             * @return  {void}
             */
            aes_key_setup: function (key, w, keysize) {
                var Nb = 4, Nr, Nk, idx;
                var temp;
                var Rcon = [16777216, 33554432, 67108864, 134217728, 268435456, 536870912, 1073741824, 2147483648, 452984832, 905969664, 1811939328, 3623878656, 2868903936, 1291845632, 2583691264];

                switch (keysize) {
                    case 128:
                        Nr = 10;
                        Nk = 4;
                        break;
                    case 192:
                        Nr = 12;
                        Nk = 6;
                        break;
                    case 256:
                        Nr = 14;
                        Nk = 8;
                        break;
                    default:
                        return;
                }

                for (idx = 0; idx < Nk; idx = (idx + 1) | 0) {
                    w[idx] = ((((key[((4 * idx) | 0)]) << 24) | ((key[((((4 * idx) | 0) + 1) | 0)]) << 16) | ((key[((((4 * idx) | 0) + 2) | 0)]) << 8) | ((key[((((4 * idx) | 0) + 3) | 0)])))) >>> 0;
                }

                for (idx = Nk; idx < ((Nb * (((Nr + 1) | 0))) | 0) ; idx = (idx + 1) | 0) {
                    temp = w[((idx - 1) | 0)];
                    if ((idx % Nk) === 0) {
                        temp = (CM.Cryptography.AES.subWord(CM.Cryptography.AES.kE_ROTWORD(temp)) ^ Rcon[((Bridge.Int.div((((idx - 1) | 0)), Nk)) | 0)]) >>> 0;
                    }
                    else {
                        if (Nk > 6 && (idx % Nk) === 4) {
                            temp = CM.Cryptography.AES.subWord(temp);
                        }
                    }
                    w[idx] = (w[((idx - Nk) | 0)] ^ temp) >>> 0;
                }
            },
            invMixColumns: function (state) {
                var col = System.Array.init(4, 0);

                // Column 1
                col[0] = state[0][0];
                col[1] = state[1][0];
                col[2] = state[2][0];
                col[3] = state[3][0];
                state[0][0] = CM.Cryptography.AES.gf_mul[col[0]][5];
                state[0][0] = (state[0][0] ^ CM.Cryptography.AES.gf_mul[col[1]][3]) & 255;
                state[0][0] = (state[0][0] ^ CM.Cryptography.AES.gf_mul[col[2]][4]) & 255;
                state[0][0] = (state[0][0] ^ CM.Cryptography.AES.gf_mul[col[3]][2]) & 255;
                state[1][0] = CM.Cryptography.AES.gf_mul[col[0]][2];
                state[1][0] = (state[1][0] ^ CM.Cryptography.AES.gf_mul[col[1]][5]) & 255;
                state[1][0] = (state[1][0] ^ CM.Cryptography.AES.gf_mul[col[2]][3]) & 255;
                state[1][0] = (state[1][0] ^ CM.Cryptography.AES.gf_mul[col[3]][4]) & 255;
                state[2][0] = CM.Cryptography.AES.gf_mul[col[0]][4];
                state[2][0] = (state[2][0] ^ CM.Cryptography.AES.gf_mul[col[1]][2]) & 255;
                state[2][0] = (state[2][0] ^ CM.Cryptography.AES.gf_mul[col[2]][5]) & 255;
                state[2][0] = (state[2][0] ^ CM.Cryptography.AES.gf_mul[col[3]][3]) & 255;
                state[3][0] = CM.Cryptography.AES.gf_mul[col[0]][3];
                state[3][0] = (state[3][0] ^ CM.Cryptography.AES.gf_mul[col[1]][4]) & 255;
                state[3][0] = (state[3][0] ^ CM.Cryptography.AES.gf_mul[col[2]][2]) & 255;
                state[3][0] = (state[3][0] ^ CM.Cryptography.AES.gf_mul[col[3]][5]) & 255;
                // Column 2
                col[0] = state[0][1];
                col[1] = state[1][1];
                col[2] = state[2][1];
                col[3] = state[3][1];
                state[0][1] = CM.Cryptography.AES.gf_mul[col[0]][5];
                state[0][1] = (state[0][1] ^ CM.Cryptography.AES.gf_mul[col[1]][3]) & 255;
                state[0][1] = (state[0][1] ^ CM.Cryptography.AES.gf_mul[col[2]][4]) & 255;
                state[0][1] = (state[0][1] ^ CM.Cryptography.AES.gf_mul[col[3]][2]) & 255;
                state[1][1] = CM.Cryptography.AES.gf_mul[col[0]][2];
                state[1][1] = (state[1][1] ^ CM.Cryptography.AES.gf_mul[col[1]][5]) & 255;
                state[1][1] = (state[1][1] ^ CM.Cryptography.AES.gf_mul[col[2]][3]) & 255;
                state[1][1] = (state[1][1] ^ CM.Cryptography.AES.gf_mul[col[3]][4]) & 255;
                state[2][1] = CM.Cryptography.AES.gf_mul[col[0]][4];
                state[2][1] = (state[2][1] ^ CM.Cryptography.AES.gf_mul[col[1]][2]) & 255;
                state[2][1] = (state[2][1] ^ CM.Cryptography.AES.gf_mul[col[2]][5]) & 255;
                state[2][1] = (state[2][1] ^ CM.Cryptography.AES.gf_mul[col[3]][3]) & 255;
                state[3][1] = CM.Cryptography.AES.gf_mul[col[0]][3];
                state[3][1] = (state[3][1] ^ CM.Cryptography.AES.gf_mul[col[1]][4]) & 255;
                state[3][1] = (state[3][1] ^ CM.Cryptography.AES.gf_mul[col[2]][2]) & 255;
                state[3][1] = (state[3][1] ^ CM.Cryptography.AES.gf_mul[col[3]][5]) & 255;
                // Column 3
                col[0] = state[0][2];
                col[1] = state[1][2];
                col[2] = state[2][2];
                col[3] = state[3][2];
                state[0][2] = CM.Cryptography.AES.gf_mul[col[0]][5];
                state[0][2] = (state[0][2] ^ CM.Cryptography.AES.gf_mul[col[1]][3]) & 255;
                state[0][2] = (state[0][2] ^ CM.Cryptography.AES.gf_mul[col[2]][4]) & 255;
                state[0][2] = (state[0][2] ^ CM.Cryptography.AES.gf_mul[col[3]][2]) & 255;
                state[1][2] = CM.Cryptography.AES.gf_mul[col[0]][2];
                state[1][2] = (state[1][2] ^ CM.Cryptography.AES.gf_mul[col[1]][5]) & 255;
                state[1][2] = (state[1][2] ^ CM.Cryptography.AES.gf_mul[col[2]][3]) & 255;
                state[1][2] = (state[1][2] ^ CM.Cryptography.AES.gf_mul[col[3]][4]) & 255;
                state[2][2] = CM.Cryptography.AES.gf_mul[col[0]][4];
                state[2][2] = (state[2][2] ^ CM.Cryptography.AES.gf_mul[col[1]][2]) & 255;
                state[2][2] = (state[2][2] ^ CM.Cryptography.AES.gf_mul[col[2]][5]) & 255;
                state[2][2] = (state[2][2] ^ CM.Cryptography.AES.gf_mul[col[3]][3]) & 255;
                state[3][2] = CM.Cryptography.AES.gf_mul[col[0]][3];
                state[3][2] = (state[3][2] ^ CM.Cryptography.AES.gf_mul[col[1]][4]) & 255;
                state[3][2] = (state[3][2] ^ CM.Cryptography.AES.gf_mul[col[2]][2]) & 255;
                state[3][2] = (state[3][2] ^ CM.Cryptography.AES.gf_mul[col[3]][5]) & 255;
                // Column 4
                col[0] = state[0][3];
                col[1] = state[1][3];
                col[2] = state[2][3];
                col[3] = state[3][3];
                state[0][3] = CM.Cryptography.AES.gf_mul[col[0]][5];
                state[0][3] = (state[0][3] ^ CM.Cryptography.AES.gf_mul[col[1]][3]) & 255;
                state[0][3] = (state[0][3] ^ CM.Cryptography.AES.gf_mul[col[2]][4]) & 255;
                state[0][3] = (state[0][3] ^ CM.Cryptography.AES.gf_mul[col[3]][2]) & 255;
                state[1][3] = CM.Cryptography.AES.gf_mul[col[0]][2];
                state[1][3] = (state[1][3] ^ CM.Cryptography.AES.gf_mul[col[1]][5]) & 255;
                state[1][3] = (state[1][3] ^ CM.Cryptography.AES.gf_mul[col[2]][3]) & 255;
                state[1][3] = (state[1][3] ^ CM.Cryptography.AES.gf_mul[col[3]][4]) & 255;
                state[2][3] = CM.Cryptography.AES.gf_mul[col[0]][4];
                state[2][3] = (state[2][3] ^ CM.Cryptography.AES.gf_mul[col[1]][2]) & 255;
                state[2][3] = (state[2][3] ^ CM.Cryptography.AES.gf_mul[col[2]][5]) & 255;
                state[2][3] = (state[2][3] ^ CM.Cryptography.AES.gf_mul[col[3]][3]) & 255;
                state[3][3] = CM.Cryptography.AES.gf_mul[col[0]][3];
                state[3][3] = (state[3][3] ^ CM.Cryptography.AES.gf_mul[col[1]][4]) & 255;
                state[3][3] = (state[3][3] ^ CM.Cryptography.AES.gf_mul[col[2]][2]) & 255;
                state[3][3] = (state[3][3] ^ CM.Cryptography.AES.gf_mul[col[3]][5]) & 255;
            },
            invShiftRows: function (state) {
                var t;

                // Shift right by 1
                t = state[1][3];
                state[1][3] = state[1][2];
                state[1][2] = state[1][1];
                state[1][1] = state[1][0];
                state[1][0] = t;
                // Shift right by 2
                t = state[2][3];
                state[2][3] = state[2][1];
                state[2][1] = t;
                t = state[2][2];
                state[2][2] = state[2][0];
                state[2][0] = t;
                // Shift right by 3
                t = state[3][3];
                state[3][3] = state[3][0];
                state[3][0] = state[3][1];
                state[3][1] = state[3][2];
                state[3][2] = t;
            },
            invSubBytes: function (state) {
                state[0][0] = CM.Cryptography.AES.aes_invsbox[state[0][0] >> 4][state[0][0] & 15];
                state[0][1] = CM.Cryptography.AES.aes_invsbox[state[0][1] >> 4][state[0][1] & 15];
                state[0][2] = CM.Cryptography.AES.aes_invsbox[state[0][2] >> 4][state[0][2] & 15];
                state[0][3] = CM.Cryptography.AES.aes_invsbox[state[0][3] >> 4][state[0][3] & 15];
                state[1][0] = CM.Cryptography.AES.aes_invsbox[state[1][0] >> 4][state[1][0] & 15];
                state[1][1] = CM.Cryptography.AES.aes_invsbox[state[1][1] >> 4][state[1][1] & 15];
                state[1][2] = CM.Cryptography.AES.aes_invsbox[state[1][2] >> 4][state[1][2] & 15];
                state[1][3] = CM.Cryptography.AES.aes_invsbox[state[1][3] >> 4][state[1][3] & 15];
                state[2][0] = CM.Cryptography.AES.aes_invsbox[state[2][0] >> 4][state[2][0] & 15];
                state[2][1] = CM.Cryptography.AES.aes_invsbox[state[2][1] >> 4][state[2][1] & 15];
                state[2][2] = CM.Cryptography.AES.aes_invsbox[state[2][2] >> 4][state[2][2] & 15];
                state[2][3] = CM.Cryptography.AES.aes_invsbox[state[2][3] >> 4][state[2][3] & 15];
                state[3][0] = CM.Cryptography.AES.aes_invsbox[state[3][0] >> 4][state[3][0] & 15];
                state[3][1] = CM.Cryptography.AES.aes_invsbox[state[3][1] >> 4][state[3][1] & 15];
                state[3][2] = CM.Cryptography.AES.aes_invsbox[state[3][2] >> 4][state[3][2] & 15];
                state[3][3] = CM.Cryptography.AES.aes_invsbox[state[3][3] >> 4][state[3][3] & 15];
            },
            kE_ROTWORD: function (x) {
                return (((((((x) << 8) >>> 0)) | ((x) >>> 24)) >>> 0));
            },
            /**
             * Performs the MixColums step. The state is multiplied by itself using matrix
             multiplication in a Galios Field 2^8. All multiplication is pre-computed in a table.
             Addition is equivilent to XOR. (Must always make a copy of the column as the original
             values will be destoyed.)
             *
             * @static
             * @private
             * @this CM.Cryptography.AES
             * @memberof CM.Cryptography.AES
             * @param   {Array.<Array.<number>>}    state
             * @return  {void}
             */
            mixColumns: function (state) {
                var col = System.Array.init(4, 0);

                // Column 1
                col[0] = state[0][0];
                col[1] = state[1][0];
                col[2] = state[2][0];
                col[3] = state[3][0];
                state[0][0] = CM.Cryptography.AES.gf_mul[col[0]][0];
                state[0][0] = (state[0][0] ^ CM.Cryptography.AES.gf_mul[col[1]][1]) & 255;
                state[0][0] = (state[0][0] ^ col[2]) & 255;
                state[0][0] = (state[0][0] ^ col[3]) & 255;
                state[1][0] = col[0];
                state[1][0] = (state[1][0] ^ CM.Cryptography.AES.gf_mul[col[1]][0]) & 255;
                state[1][0] = (state[1][0] ^ CM.Cryptography.AES.gf_mul[col[2]][1]) & 255;
                state[1][0] = (state[1][0] ^ col[3]) & 255;
                state[2][0] = col[0];
                state[2][0] = (state[2][0] ^ col[1]) & 255;
                state[2][0] = (state[2][0] ^ CM.Cryptography.AES.gf_mul[col[2]][0]) & 255;
                state[2][0] = (state[2][0] ^ CM.Cryptography.AES.gf_mul[col[3]][1]) & 255;
                state[3][0] = CM.Cryptography.AES.gf_mul[col[0]][1];
                state[3][0] = (state[3][0] ^ col[1]) & 255;
                state[3][0] = (state[3][0] ^ col[2]) & 255;
                state[3][0] = (state[3][0] ^ CM.Cryptography.AES.gf_mul[col[3]][0]) & 255;
                // Column 2
                col[0] = state[0][1];
                col[1] = state[1][1];
                col[2] = state[2][1];
                col[3] = state[3][1];
                state[0][1] = CM.Cryptography.AES.gf_mul[col[0]][0];
                state[0][1] = (state[0][1] ^ CM.Cryptography.AES.gf_mul[col[1]][1]) & 255;
                state[0][1] = (state[0][1] ^ col[2]) & 255;
                state[0][1] = (state[0][1] ^ col[3]) & 255;
                state[1][1] = col[0];
                state[1][1] = (state[1][1] ^ CM.Cryptography.AES.gf_mul[col[1]][0]) & 255;
                state[1][1] = (state[1][1] ^ CM.Cryptography.AES.gf_mul[col[2]][1]) & 255;
                state[1][1] = (state[1][1] ^ col[3]) & 255;
                state[2][1] = col[0];
                state[2][1] = (state[2][1] ^ col[1]) & 255;
                state[2][1] = (state[2][1] ^ CM.Cryptography.AES.gf_mul[col[2]][0]) & 255;
                state[2][1] = (state[2][1] ^ CM.Cryptography.AES.gf_mul[col[3]][1]) & 255;
                state[3][1] = CM.Cryptography.AES.gf_mul[col[0]][1];
                state[3][1] = (state[3][1] ^ col[1]) & 255;
                state[3][1] = (state[3][1] ^ col[2]) & 255;
                state[3][1] = (state[3][1] ^ CM.Cryptography.AES.gf_mul[col[3]][0]) & 255;
                // Column 3
                col[0] = state[0][2];
                col[1] = state[1][2];
                col[2] = state[2][2];
                col[3] = state[3][2];
                state[0][2] = CM.Cryptography.AES.gf_mul[col[0]][0];
                state[0][2] = (state[0][2] ^ CM.Cryptography.AES.gf_mul[col[1]][1]) & 255;
                state[0][2] = (state[0][2] ^ col[2]) & 255;
                state[0][2] = (state[0][2] ^ col[3]) & 255;
                state[1][2] = col[0];
                state[1][2] = (state[1][2] ^ CM.Cryptography.AES.gf_mul[col[1]][0]) & 255;
                state[1][2] = (state[1][2] ^ CM.Cryptography.AES.gf_mul[col[2]][1]) & 255;
                state[1][2] = (state[1][2] ^ col[3]) & 255;
                state[2][2] = col[0];
                state[2][2] = (state[2][2] ^ col[1]) & 255;
                state[2][2] = (state[2][2] ^ CM.Cryptography.AES.gf_mul[col[2]][0]) & 255;
                state[2][2] = (state[2][2] ^ CM.Cryptography.AES.gf_mul[col[3]][1]) & 255;
                state[3][2] = CM.Cryptography.AES.gf_mul[col[0]][1];
                state[3][2] = (state[3][2] ^ col[1]) & 255;
                state[3][2] = (state[3][2] ^ col[2]) & 255;
                state[3][2] = (state[3][2] ^ CM.Cryptography.AES.gf_mul[col[3]][0]) & 255;
                // Column 4
                col[0] = state[0][3];
                col[1] = state[1][3];
                col[2] = state[2][3];
                col[3] = state[3][3];
                state[0][3] = CM.Cryptography.AES.gf_mul[col[0]][0];
                state[0][3] = (state[0][3] ^ CM.Cryptography.AES.gf_mul[col[1]][1]) & 255;
                state[0][3] = (state[0][3] ^ col[2]) & 255;
                state[0][3] = (state[0][3] ^ col[3]) & 255;
                state[1][3] = col[0];
                state[1][3] = (state[1][3] ^ CM.Cryptography.AES.gf_mul[col[1]][0]) & 255;
                state[1][3] = (state[1][3] ^ CM.Cryptography.AES.gf_mul[col[2]][1]) & 255;
                state[1][3] = (state[1][3] ^ col[3]) & 255;
                state[2][3] = col[0];
                state[2][3] = (state[2][3] ^ col[1]) & 255;
                state[2][3] = (state[2][3] ^ CM.Cryptography.AES.gf_mul[col[2]][0]) & 255;
                state[2][3] = (state[2][3] ^ CM.Cryptography.AES.gf_mul[col[3]][1]) & 255;
                state[3][3] = CM.Cryptography.AES.gf_mul[col[0]][1];
                state[3][3] = (state[3][3] ^ col[1]) & 255;
                state[3][3] = (state[3][3] ^ col[2]) & 255;
                state[3][3] = (state[3][3] ^ CM.Cryptography.AES.gf_mul[col[3]][0]) & 255;
            },
            pKCS7Pad: function (input) {
                var pad = (CM.Cryptography.AES.AES_BLOCK_SIZE - (input.length % CM.Cryptography.AES.AES_BLOCK_SIZE)) | 0;
                if (pad === 0) {
                    // If the original data is a multiple of N bytes, then an extra block of bytes with value N is added.
                    pad = CM.Cryptography.AES.AES_BLOCK_SIZE;
                }
                var output = System.Array.init(((input.length + pad) | 0), 0);
                System.Array.copy(input, 0, output, 0, input.length);
                for (var i = input.length; i < output.length; i = (i + 1) | 0) {
                    output[i] = pad & 255;
                }
                return output;
            },
            pKCS7Remove: function (input) {
                var len = input[((input.length - 1) | 0)];
                if (len > input.length || len > CM.Cryptography.AES.AES_BLOCK_SIZE || len <= 0) {
                    throw new System.Exception("Invalid padding");
                }
                for (var o = 1; o <= len; o = (o + 1) | 0) {
                    if (input[((input.length - o) | 0)] !== len) {
                        return input;
                    }
                }
                var output = System.Array.init(((input.length - len) | 0), 0);
                System.Array.copy(input, 0, output, 0, ((input.length - len) | 0));
                return output;
            },
            shiftRows: function (state) {
                var t;

                // Shift left by 1
                t = state[1][0];
                state[1][0] = state[1][1];
                state[1][1] = state[1][2];
                state[1][2] = state[1][3];
                state[1][3] = t;
                // Shift left by 2
                t = state[2][0];
                state[2][0] = state[2][2];
                state[2][2] = t;
                t = state[2][1];
                state[2][1] = state[2][3];
                state[2][3] = t;
                // Shift left by 3
                t = state[3][0];
                state[3][0] = state[3][3];
                state[3][3] = state[3][2];
                state[3][2] = state[3][1];
                state[3][1] = t;
            },
            subBytes: function (state) {
                state[0][0] = CM.Cryptography.AES.aes_sbox[state[0][0] >> 4][state[0][0] & 15];
                state[0][1] = CM.Cryptography.AES.aes_sbox[state[0][1] >> 4][state[0][1] & 15];
                state[0][2] = CM.Cryptography.AES.aes_sbox[state[0][2] >> 4][state[0][2] & 15];
                state[0][3] = CM.Cryptography.AES.aes_sbox[state[0][3] >> 4][state[0][3] & 15];
                state[1][0] = CM.Cryptography.AES.aes_sbox[state[1][0] >> 4][state[1][0] & 15];
                state[1][1] = CM.Cryptography.AES.aes_sbox[state[1][1] >> 4][state[1][1] & 15];
                state[1][2] = CM.Cryptography.AES.aes_sbox[state[1][2] >> 4][state[1][2] & 15];
                state[1][3] = CM.Cryptography.AES.aes_sbox[state[1][3] >> 4][state[1][3] & 15];
                state[2][0] = CM.Cryptography.AES.aes_sbox[state[2][0] >> 4][state[2][0] & 15];
                state[2][1] = CM.Cryptography.AES.aes_sbox[state[2][1] >> 4][state[2][1] & 15];
                state[2][2] = CM.Cryptography.AES.aes_sbox[state[2][2] >> 4][state[2][2] & 15];
                state[2][3] = CM.Cryptography.AES.aes_sbox[state[2][3] >> 4][state[2][3] & 15];
                state[3][0] = CM.Cryptography.AES.aes_sbox[state[3][0] >> 4][state[3][0] & 15];
                state[3][1] = CM.Cryptography.AES.aes_sbox[state[3][1] >> 4][state[3][1] & 15];
                state[3][2] = CM.Cryptography.AES.aes_sbox[state[3][2] >> 4][state[3][2] & 15];
                state[3][3] = CM.Cryptography.AES.aes_sbox[state[3][3] >> 4][state[3][3] & 15];
            },
            /**
             * Substitutes a word using the AES S-Box.
             *
             * @static
             * @private
             * @this CM.Cryptography.AES
             * @memberof CM.Cryptography.AES
             * @param   {number}    word
             * @return  {number}
             */
            subWord: function (word) {
                var result;
                result = CM.Cryptography.AES.aes_sbox[(((word >>> 4) & 15) >>> 0)][((word & 15) >>> 0)];
                result = (result + (((CM.Cryptography.AES.aes_sbox[(((word >>> 12) & 15) >>> 0)][(((word >>> 8) & 15) >>> 0)] << 8) >>> 0))) >>> 0;
                result = (result + (((CM.Cryptography.AES.aes_sbox[(((word >>> 20) & 15) >>> 0)][(((word >>> 16) & 15) >>> 0)] << 16) >>> 0))) >>> 0;
                result = (result + (((CM.Cryptography.AES.aes_sbox[(((word >>> 28) & 15) >>> 0)][(((word >>> 24) & 15) >>> 0)] << 24) >>> 0))) >>> 0;
                return (result);
            },
            xor_buf: function (input, output, len) {
                for (var idx = 0; idx < len; idx = (idx + 1) | 0) {
                    output[idx] = (output[idx] ^ input[idx]) & 255;
                }
            }
        }
    });

    /**
     * A minimal hash-based message authentication code (HMAC) implementation
     *
     * @class CM.Cryptography.HMAC
     */
    Bridge.define('CM.Cryptography.HMAC', {
        _Key: null,
        _InnerPadding: null,
        _OuterPadding: null,
        _Hash: null,
        constructor: function (key, hash, blockSize) {
            this._Hash = hash;
            this._Key = key;
            if (key.length < blockSize) {
                // keys shorter than blocksize are zero-padded 
                this._Key = Bridge.cast(System.Array.clone(key), Array);
            }
            else {
                // keys longer than blocksize are shortened
                this._Key = this._Hash(key);
            }
            this._InnerPadding = System.Array.init(blockSize, 0);
            this._OuterPadding = System.Array.init(blockSize, 0);
            this.updateIOPadBuffers();
        },
        updateIOPadBuffers: function () {

            var inner = this._InnerPadding;
            var outer = this._OuterPadding;
            var key = this._Key;
            for (var i = 0; i < inner.length; i++) {
                if (i < key.length) {
                    inner[i] = ((54 ^ key[i]));
                    outer[i] = ((92 ^ key[i]));
                }
                else {
                    inner[i] = 54;
                    outer[i] = 92;
                }
            }
            ;
        },
        computeHash: function (b) {
            var inner = new System.Collections.Generic.List$1(System.Byte)();
            inner.addRange(this._InnerPadding);
            inner.addRange(b);
            var outer = new System.Collections.Generic.List$1(System.Byte)();
            outer.addRange(this._OuterPadding);
            outer.addRange(this._Hash(inner.toArray()));
            return this._Hash(outer.toArray());
        }
    });

    /**
     * An Rfc2898 (PBKDF2) implementation for derived key generation.
     *
     * @public
     * @class CM.Cryptography.Rfc2898
     */
    Bridge.define('CM.Cryptography.Rfc2898', {
        statics: {
            createHMACSHA1: function (pass, salt, iterations) {
                var c = new CM.Cryptography.SHA1.SHA1_CTX();

                var hmac = new CM.Cryptography.HMAC(pass, function (b) {
                    return CM.Cryptography.SHA1.computeHash(c, b);
                }, 64);

                return new CM.Cryptography.Rfc2898(Bridge.fn.bind(hmac, hmac.computeHash), salt, iterations);

            }
        },
        _Buffer: null,
        _Salt: null,
        _Interations: 0,
        _Block: 0,
        _Cursor: 0,
        _Hash: null,
        constructor: function (hash, salt, iterations) {
            this._Hash = hash;
            this._Salt = salt;
            this._Interations = iterations;
            this._Block = 1;
            this._Buffer = null;
        },
        int: function (i) {
            return [(((i >> 24)) & 255), (((i >> 16)) & 255), (((i >> 8)) & 255), (i & 255)];
        },
        getNextBlock: function () {
            var ar = new System.Collections.Generic.List$1(System.Byte)();
            ar.addRange(this._Salt);
            ar.addRange(this.int(this._Block));
            var hashValue = this._Hash(ar.toArray());
            var res = hashValue;
            for (var i = 2; i <= this._Interations; i = (i + 1)) {
                hashValue = this._Hash(hashValue);
                for (var j = 0; j < res.length; j = (j + 1)) {
                    res[j] = ((res[j] ^ hashValue[j]));
                }
            }
            this._Block = (this._Block + 1) | 0;
            return res;
        },
        getBytes: function (count) {
            if (count <= 0) {
                throw new System.ArgumentOutOfRangeException();
            }
            var ar = System.Array.init(count, 0);
            var idx = 0;
            while (idx < count) {
                if (this._Buffer == null || this._Cursor === this._Buffer.length) {
                    this._Buffer = this.getNextBlock();
                    this._Cursor = 0;
                }
                var toCopy = Math.min(((count - idx)), ((this._Buffer.length - this._Cursor)));
                System.Array.copy(this._Buffer, this._Cursor, ar, idx, toCopy);
                idx = (idx + toCopy) | 0;
                this._Cursor = (this._Cursor + toCopy) | 0;
            }
            return ar;
        }
    });

    Bridge.define('CM.Cryptography.RNG', {
        statics: {
            randomBytes: function (b) {
                // Only use a proper browser RNG

                if (typeof (window.Uint8Array) == 'function') {
                    var tmp = new Uint8Array(b.length);
                    (window.crypto || window.msCrypto).getRandomValues(tmp);
                    for (var i = 0; i < tmp.length; i++)
                        b[i] = tmp[i];
                } else {
                    for (var i = 0; i < b.length; i++)
                        b[i] = parseInt(Math.random() * 255);
                }
                ;
            }
        }
    });


    /**
     * A portable C# SHA-1, based on work by Brad Conte (bradconte.com)
     *
     * @static
     * @abstract
     * @public
     * @class CM.Cryptography.SHA1
     */
    Bridge.define('CM.Cryptography.SHA1', {
        statics: {
            k: null,
            config: {
                init: function () {
                    this.k = [1518500249, 1859775393, 2400959708, 3395469782];
                }
            },
            computeHash$1: function (data) {
                var ctx = new CM.Cryptography.SHA1.SHA1_CTX();
                CM.Cryptography.SHA1.update(ctx, data, data.length);
                var hash = System.Array.init(20, 0);
                CM.Cryptography.SHA1.final(ctx, hash);
                return hash;
            },
            computeHash: function (ctx, data) {
                ctx.reset();
                CM.Cryptography.SHA1.update(ctx, data, data.length);
                var hash = System.Array.init(20, 0);
                CM.Cryptography.SHA1.final(ctx, hash);
                return hash;
            },
            iNT64_ADD: function (a, b, c) {
                if (a.v > ((4294967295 - (c)) >>> 0)) {
                    b.v = (b.v + 1) >>> 0;
                }
                a.v = (a.v + c) >>> 0;
            },
            rOTLEFT: function (a, b) {
                return (((((((a) << (b)) >>> 0)) | ((a) >>> (((32 - (b)) | 0)))) >>> 0));
            },
            transform: function (ctx, data) {
                // optimised Bridge.NET code

                var a, b, c, d, e, t;
                var i, j;
                var m = ctx.m;
                var k = CM.Cryptography.SHA1.k;

                for (i = 0, j = 0; i < 16; i++, j += 4) {
                    m[i] = ((((((((((data[j] << 24))) + (((data[((j + 1))] << 16))))) + (((data[((j + 2))] << 8))))) + data[((j + 3))])));
                }

                for (; i < 80; i++) {
                    m[i] = (((((((m[((i - 3))] ^ m[((i - 8))])) ^ m[((i - 14))])) ^ m[((i - 16))])));
                    m[i] = ((((m[i] << 1))) | (m[i] >>> 31));
                }

                a = ctx.state[0];
                b = ctx.state[1];
                c = ctx.state[2];
                d = ctx.state[3];
                e = ctx.state[4];
                function rotleft(a, b) {
                    return (((((((a) << (b)))) | ((a) >>> (((32 - (b))))))));
                }
                for (i = 0; i < 20; i++) {
                    t = (((((((rotleft(a, 5) + ((((((b & c))) ^ (((~b & d)))))))) + e)) + k[0])) + m[i]);
                    e = d;
                    d = c;
                    c = rotleft(b, 30);
                    b = a;
                    a = t;
                }
                for (; i < 40; i++) {
                    t = (((((((rotleft(a, 5) + (((((b ^ c)) ^ d))))) + e)) + k[1])) + m[i]);
                    e = d;
                    d = c;
                    c = rotleft(b, 30);
                    b = a;
                    a = t;
                }
                for (; i < 60; i++) {
                    t = (((((((rotleft(a, 5) + ((((((((b & c))) ^ (((b & d))))) ^ (((c & d)))))))) + e)) + k[2])) + m[i]);
                    e = d;
                    d = c;
                    c = rotleft(b, 30);
                    b = a;
                    a = t;
                }
                for (; i < 80; i++) {
                    t = (((((((rotleft(a, 5) + (((((b ^ c)) ^ d))))) + e)) + k[3])) + m[i]);
                    e = d;
                    d = c;
                    c = rotleft(b, 30);
                    b = a;
                    a = t;
                }

                // masking is for javascript conversion
                ctx.state[0] = ((((ctx.state[0] + a))) & 4294967295);
                ctx.state[1] = ((((ctx.state[1] + b))) & 4294967295);
                ctx.state[2] = ((((ctx.state[2] + c))) & 4294967295);
                ctx.state[3] = ((((ctx.state[3] + d))) & 4294967295);
                ctx.state[4] = ((((ctx.state[4] + e))) & 4294967295);



            },
            update: function (ctx, data, len) {
                for (var i = 0; i < len; i = (i + 1) | 0) {
                    ctx.data[ctx.datalen] = data[i];
                    ctx.datalen = (ctx.datalen + 1) | 0;
                    if (ctx.datalen === 64) {
                        CM.Cryptography.SHA1.transform(ctx, ctx.data);
                        var hi = { v: ctx.bitlen[0] };
                        var lo = { v: ctx.bitlen[1] };
                        CM.Cryptography.SHA1.iNT64_ADD(hi, lo, 512);
                        ctx.bitlen[0] = hi.v;
                        ctx.bitlen[1] = lo.v;
                        ctx.datalen = 0;
                    }
                }
            },
            final: function (ctx, hash) {
                // optimised Bridge.NET code

                var i = ctx.datalen;

                // Pad whatever data is left in the buffer.
                if (ctx.datalen < 56) {
                    ctx.data[i++] = 128;
                    while (i < 56) {
                        ctx.data[i++] = 0;
                    }
                }
                else {
                    ctx.data[i++] = 128;
                    while (i < 64) {
                        ctx.data[i++] = 0;
                    }
                    CM.Cryptography.SHA1.transform(ctx, ctx.data);
                    for (i = 0; i < 56; i++) {
                        ctx.data[i] = 0;
                    }
                }

                // Append to the padding the total message's length in bits and transform.
                var hi = { v: ctx.bitlen[0] };
                var lo = { v: ctx.bitlen[1] };
                CM.Cryptography.SHA1.iNT64_ADD(hi, lo, (((ctx.datalen) * 8)));
                ctx.bitlen[0] = hi.v;
                ctx.bitlen[1] = lo.v;

                ctx.data[63] = ((((ctx.bitlen[0] & 255))));
                ctx.data[62] = (((((ctx.bitlen[0] >>> 8) & 255))));
                ctx.data[61] = (((((ctx.bitlen[0] >>> 16) & 255))));
                ctx.data[60] = (((((ctx.bitlen[0] >>> 24) & 255))));
                ctx.data[59] = ((((ctx.bitlen[1] & 255))));
                ctx.data[58] = (((((ctx.bitlen[1] >>> 8) & 255))));
                ctx.data[57] = (((((ctx.bitlen[1] >>> 16) & 255))));
                ctx.data[56] = (((((ctx.bitlen[1] >>> 24) & 255))));
                CM.Cryptography.SHA1.transform(ctx, ctx.data);

                // Since this implementation uses little endian byte ordering and MD uses big endian,
                // reverse all the bytes when copying the final state to the output hash.
                for (i = 0; i < 4; i++) {
                    var shift = (24 - ((i * 8)));
                    hash[i] = (((((ctx.state[0] >>> shift))))) & 255;
                    hash[((i + 4))] = (((((ctx.state[1] >>> shift))))) & 255;
                    hash[((i + 8))] = (((((ctx.state[2] >>> shift))))) & 255;
                    hash[((i + 12))] = (((((ctx.state[3] >>> shift))))) & 255;
                    hash[((i + 16))] = (((((ctx.state[4] >>> shift))))) & 255;
                }

                ;

            }
        }
    });

    Bridge.define('CM.Cryptography.SHA1.SHA1_CTX', {
        data: null,
        datalen: 0,
        bitlen: null,
        state: null,
        m: null,
        config: {
            init: function () {
                this.data = System.Array.init(64, 0);
                this.bitlen = System.Array.init(2, 0);
                this.state = System.Array.init(5, 0);
                this.m = System.Array.init(80, 0);
            }
        },
        constructor: function () {
            this.reset();
        },
        reset: function () {
            if (this.datalen !== 0) {

                if (typeof (Array.prototype.fill) == 'function') {
                    this.data.fill(0);
                } else {
                    var i = 0;
                    while (i < this.data.length)
                        this.data[i++] = 0;
                }
                ;
            }
            this.datalen = 0;
            this.state[0] = 1732584193;
            this.state[1] = 4023233417;
            this.state[2] = 2562383102;
            this.state[3] = 271733878;
            this.state[4] = 3285377520;
            this.bitlen[0] = 0;
            this.bitlen[1] = 0;
        }
    });



    Bridge.init();
})(this);

self.addEventListener('message', function (e) {
    var data = JSON.parse(e.data);

    switch (data.command) {
        case 'generate-rsa': {
          
            function FromJsonArray(untypedArray) {
                var ar = [];
                for (v in untypedArray) if (v != 'undefined') ar.push(untypedArray[v]);
                return Array.prototype.slice.call(ar);
            }
            var crunch = new Crunch();
            var p = crunch.nextPrime(FromJsonArray(data.p));
            var q = crunch.nextPrime(FromJsonArray(data.q));
            var exp = [1, 0, 1];
            var n = crunch.mul(p, q);
            var f = crunch.mul(crunch.decrement(p), crunch.decrement(q));
            var d = crunch.cut(crunch.inv(exp, f));
            // var inverseQ = crunch.cut(crunch.inv(p, q));
            self.postMessage(JSON.stringify({
                result: { d: d, modulus: n, exponent: exp }
            }));
            
        } break;
        case 'rfc2898': {
            var rfc2898 = CM.Cryptography.Rfc2898.createHMACSHA1(
               data.password, data.salt, data.iterations);
            self.postMessage(JSON.stringify({ 
                result: {
                    key: rfc2898.getBytes(32),
                    iv: rfc2898.getBytes(16)
                }}));
        } break;
        case 'aes-decrypt':
        case 'aes-encrypt': {
            var result = {
                error: null,
                result: null
            };

            try {
                if (data.command == 'aes-encrypt')
                    result.result = CM.Cryptography.AES.encrypt(data.input, data.key, data.iv);
                else
                    result.result = CM.Cryptography.AES.decrypt(data.input, data.key, data.iv);
            } catch (error) {
                result.error = 'Invalid derivation key.';
            }
            self.postMessage(JSON.stringify(result));
        } break;
        default:
            self.postMessage(JSON.stringify({ 'error': 'Unknown command' }));
            break;
    };
}, false);

