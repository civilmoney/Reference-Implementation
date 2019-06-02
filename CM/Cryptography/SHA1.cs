#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM.Cryptography {

    /// <summary>
    /// A portable C# SHA-1, based on work by Brad Conte (bradconte.com) MIT License
    /// </summary>
#if JAVASCRIPT
    [Bridge.External] // We compile once and move into webworkers.js
#if Retyped
    [Bridge.Rules(ArrayIndex = Bridge.ArrayIndexRule.Plain)]
#endif
#endif
    public static class SHA1 {

        public static byte[] ComputeHash(byte[] data) {
            var ctx = new SHA1_CTX();
            Update(ctx, data, data.Length);
            var hash = new byte[20];
            Final(ctx, hash);
            return hash;
        }

        public static byte[] ComputeHash(SHA1_CTX ctx, byte[] data) {
            ctx.Reset();
            Update(ctx, data, data.Length);
            var hash = new byte[20];
            Final(ctx, hash);
            return hash;
        }

        private static void INT64_ADD(ref uint a, ref uint b, uint c) {
            if (a > 0xffffffff - (c)) ++b; a += c;
        }

        private static uint ROTLEFT(uint a, int b) {
            return (uint)(((a) << (b)) | ((a) >> (32 - (b))));
        }

        public class SHA1_CTX {
            public byte[] data = new byte[64];
            public int datalen;
            public uint[] bitlen = new uint[2];
            public uint[] state = new uint[5];

            public SHA1_CTX() {
                Reset();
            }

            public void Reset() {

#if JAVASCRIPT
            Bridge.Script.Write(@"
                 if (this.datalen != 0) {
                    if(typeof(Array.prototype.fill)=='function'){
                        this.data.fill(0);
                    } else {
                        var i = 0;
                        while(i<this.data.length)
                            this.data[i++] = 0;
                    }
                }
                this.datalen = 0;
                this.state[0] = 0x67452301;
                this.state[1] = 0xEFCDAB89;
                this.state[2] = 0x98BADCFE;
                this.state[3] = 0x10325476;
                this.state[4] = 0xc3d2e1f0;
                this.bitlen[0] = 0;
                this.bitlen[1] = 0;
            ");
#else
                if (datalen != 0) {
                    for (int i = 0; i < data.Length; i++)
                        data[i] = 0;
                }
                datalen = 0;
                state[0] = 0x67452301;
                state[1] = 0xEFCDAB89;
                state[2] = 0x98BADCFE;
                state[3] = 0x10325476;
                state[4] = 0xc3d2e1f0;
                bitlen[0] = 0;
                bitlen[1] = 0;
#endif
            }

            public uint[] M = new uint[80];
        }

        private static readonly uint[] k = new uint[4] {
                0x5a827999,
                0x6ed9eba1,
                0x8f1bbcdc,
                0xca62c1d6
            };

        private static void Transform(SHA1_CTX ctx, byte[] data) {
            // optimised Bridge.NET code
#if JAVASCRIPT
            Bridge.Script.Write(@"
                var a, b, c, d, e, t;
                var i, j;
                var m = ctx.M;
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

            ");
#else
            uint a, b, c, d, e, t;
            int i, j;
            uint[] m = ctx.M;
            for (i = 0, j = 0; i < 16; ++i, j += 4)
                m[i] = (((uint)data[j] << 24) + ((uint)data[j + 1] << 16) + ((uint)data[j + 2] << 8) + ((uint)data[j + 3]));

            for (; i < 80; ++i) {
                m[i] = (m[i - 3] ^ m[i - 8] ^ m[i - 14] ^ m[i - 16]);
                m[i] = (m[i] << 1) | (m[i] >> 31);
            }

            a = ctx.state[0];
            b = ctx.state[1];
            c = ctx.state[2];
            d = ctx.state[3];
            e = ctx.state[4];

            for (i = 0; i < 20; ++i) {
                t = ROTLEFT(a, 5) + ((b & c) ^ (~b & d)) + e + k[0] + m[i];
                e = d;
                d = c;
                c = ROTLEFT(b, 30);
                b = a;
                a = t;
            }
            for (; i < 40; ++i) {
                t = ROTLEFT(a, 5) + (b ^ c ^ d) + e + k[1] + m[i];
                e = d;
                d = c;
                c = ROTLEFT(b, 30);
                b = a;
                a = t;
            }
            for (; i < 60; ++i) {
                t = ROTLEFT(a, 5) + ((b & c) ^ (b & d) ^ (c & d)) + e + k[2] + m[i];
                e = d;
                d = c;
                c = ROTLEFT(b, 30);
                b = a;
                a = t;
            }
            for (; i < 80; ++i) {
                t = ROTLEFT(a, 5) + (b ^ c ^ d) + e + k[3] + m[i];
                e = d;
                d = c;
                c = ROTLEFT(b, 30);
                b = a;
                a = t;
            }

            // masking is for javascript conversion
            ctx.state[0] = (uint)(ctx.state[0] + a) & 0xFFFFFFFF;
            ctx.state[1] = (uint)(ctx.state[1] + b) & 0xFFFFFFFF;
            ctx.state[2] = (uint)(ctx.state[2] + c) & 0xFFFFFFFF;
            ctx.state[3] = (uint)(ctx.state[3] + d) & 0xFFFFFFFF;
            ctx.state[4] = (uint)(ctx.state[4] + e) & 0xFFFFFFFF;
#endif
        }

        private static void Update(SHA1_CTX ctx, byte[] data, int len) {
            for (int i = 0; i < len; ++i) {
                ctx.data[ctx.datalen] = data[i];
                ctx.datalen++;
                if (ctx.datalen == 64) {
                    Transform(ctx, ctx.data);
                    uint hi = ctx.bitlen[0];
                    uint lo = ctx.bitlen[1];
                    INT64_ADD(ref hi, ref lo, 512);
                    ctx.bitlen[0] = hi;
                    ctx.bitlen[1] = lo;
                    ctx.datalen = 0;
                }
            }
        }

        private static void Final(SHA1_CTX ctx, byte[] hash) {
            // optimised Bridge.NET code
#if JAVASCRIPT
            Bridge.Script.Write(@"
                var i = ctx.datalen;

                // Pad whatever data is left in the buffer.
                if (ctx.datalen < 56) {
                    ctx.data[i++] = 128;
                    while (i < 56) {
                        ctx.data[i++] = 0;
                    }
                }
                else  {
                    ctx.data[i++] = 128;
                    while (i < 64) {
                        ctx.data[i++] = 0;
                    }
                    CM.Cryptography.SHA1.Transform(ctx, ctx.data);
                    for (i = 0; i < 56; i++) {
                        ctx.data[i] = 0;
                    }
                }

                // Append to the padding the total message's length in bits and transform.
                var hi = { v : ctx.bitlen[0] };
                var lo = { v : ctx.bitlen[1] };
                CM.Cryptography.SHA1.INT64_ADD(hi, lo, (((ctx.datalen) * 8)));
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
                CM.Cryptography.SHA1.Transform(ctx, ctx.data);

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

    ");

#else
            int i = ctx.datalen;

            // Pad whatever data is left in the buffer.
            if (ctx.datalen < 56) {
                ctx.data[i++] = 0x80;
                while (i < 56)
                    ctx.data[i++] = 0x00;
            } else {
                ctx.data[i++] = 0x80;
                while (i < 64)
                    ctx.data[i++] = 0x00;
                Transform(ctx, ctx.data);
                for (i = 0; i < 56; i++)
                    ctx.data[i] = 0;
            }

            // Append to the padding the total message's length in bits and transform.
            uint hi = ctx.bitlen[0];
            uint lo = ctx.bitlen[1];
            INT64_ADD(ref hi, ref lo, (uint)ctx.datalen * 8);
            ctx.bitlen[0] = hi;
            ctx.bitlen[1] = lo;

            ctx.data[63] = (byte)(ctx.bitlen[0] & 0xff);
            ctx.data[62] = (byte)((ctx.bitlen[0] >> 8) & 0xff);
            ctx.data[61] = (byte)((ctx.bitlen[0] >> 16) & 0xff);
            ctx.data[60] = (byte)((ctx.bitlen[0] >> 24) & 0xff);
            ctx.data[59] = (byte)(ctx.bitlen[1] & 0xff);
            ctx.data[58] = (byte)((ctx.bitlen[1] >> 8) & 0xff);
            ctx.data[57] = (byte)((ctx.bitlen[1] >> 16) & 0xff);
            ctx.data[56] = (byte)((ctx.bitlen[1] >> 24) & 0xff);
            Transform(ctx, ctx.data);

            // Since this implementation uses little endian byte ordering and MD uses big endian,
            // reverse all the bytes when copying the final state to the output hash.
            for (i = 0; i < 4; ++i) {
                var shift = 24 - i * 8;
                hash[i] = (byte)((ctx.state[0] >> shift) & 0xff);
                hash[i + 4] = (byte)((ctx.state[1] >> shift) & 0xff);
                hash[i + 8] = (byte)((ctx.state[2] >> shift) & 0xff);
                hash[i + 12] = (byte)((ctx.state[3] >> shift) & 0xff);
                hash[i + 16] = (byte)((ctx.state[4] >> shift) & 0xff);
            }
#endif
        }
    }
}