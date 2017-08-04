#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Cryptography {

    /// <summary>
    /// A portable C# SHA-256, based on work by Brad Conte (bradconte.com) MIT License
    /// </summary>
    public static partial class SHA256 {

        public static byte[] ComputeHash(byte[] data) {
            var ctx = new SHA256_CTX();
            Update(ctx, data, data.Length);
            var hash = new byte[32];
            Final(ctx, hash);
            return hash;
        }

        /// <summary>
        /// Not all platforms (such as javascript) handle INT64 very well
        /// </summary>
        private static void INT64_ADD(ref uint a, ref uint b, uint c) {
            if (a > 0xffffffff - (c)) ++b; a += c;
        }

        private class SHA256_CTX {
            public byte[] data = new byte[64];
            public int datalen;
            public uint[] bitlen = new uint[2];
            public uint[] m = new uint[64];
            public uint[] state = new uint[8] {
                    0x6a09e667,  0xbb67ae85, 0x3c6ef372, 0xa54ff53a,
                    0x510e527f,  0x9b05688c, 0x1f83d9ab, 0x5be0cd19
                    };

        }

        private static readonly uint[] _k = new uint[] {
   0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
   0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
   0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
   0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
   0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
   0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
   0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
   0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2
};

        private delegate uint f(uint v);

        private static void Transform(SHA256_CTX ctx, byte[] data) {
            var k = _k;

#if JAVASCRIPT
            Bridge.Script.Write(@"
            var a, b, c, d, e, f, g, h, t1, t2;
            var m = ctx.m;

            var i, j;
            for (i = 0, j = 0; i < 16; ++i, j += 4)
                m[i] =  ((data[j] << 24) | (data[j + 1] << 16) | (data[j + 2] << 8) | (data[j + 3])) >>> 0;

            for (; i < 64; ++i) {
                t1 = m[i - 2];
                t1 = ((t1 >>> 17) | (t1 << 15)) 
                    ^ ((t1 >>> 19) | (t1 << 13)) 
                    ^  (t1 >>> 10);
                t2 = m[i - 15];
                t2 = ((t2 >>> 7) | (t2 << 25)) 
                    ^ ((t2 >>> 18) | (t2 << 14))
                    ^ (t2 >>> 3);

                m[i] = t1 + m[i - 7] + t2 + m[i - 16];

            }

            a = ctx.state[0];
            b = ctx.state[1];
            c = ctx.state[2];
            d = ctx.state[3];
            e = ctx.state[4];
            f = ctx.state[5];
            g = ctx.state[6];
            h = ctx.state[7];

            var ep1, ep0, ch, maj;

            for (i = 0; i < 64; ++i) {
                ep1 = ((e >>> 6) | (e << 26)) 
                    ^ ((e >>> 11) | (e << 21)) 
                    ^ ((e >>> 25) | (e << 7));

                ch = g ^ (e & (f ^ g));
                t1 = h + ep1 + ch + k[i] + m[i];

                ep0 = ((a >>> 2) | (a << 30)) 
                    ^ ((a >>> 13) | (a << 19)) 
                    ^ ((a >>> 22) | (a << 10));
                maj = (a & b) | (c & (a ^ b));
                t2 = ep0 + maj;
                h = g;
                g = f;
                f = e;
                e = (d + t1)>>>0;
                d = c;
                c = b;
                b = a;
                a = (t1 + t2)>>>0;
            }
            ctx.state[0] = (((ctx.state[0] + a) >>> 0) & 4294967295) >>> 0;
            ctx.state[1] = (((ctx.state[1] + b) >>> 0) & 4294967295) >>> 0;
            ctx.state[2] = (((ctx.state[2] + c) >>> 0) & 4294967295) >>> 0;
            ctx.state[3] = (((ctx.state[3] + d) >>> 0) & 4294967295) >>> 0;
            ctx.state[4] = (((ctx.state[4] + e) >>> 0) & 4294967295) >>> 0;
            ctx.state[5] = (((ctx.state[5] + f) >>> 0) & 4294967295) >>> 0;
            ctx.state[6] = (((ctx.state[6] + g) >>> 0) & 4294967295) >>> 0;
            ctx.state[7] = (((ctx.state[7] + h) >>> 0) & 4294967295) >>> 0;
");
#else
            uint a, b, c, d, e, f, g, h, t1, t2;
            var m = ctx.m;

            int i, j;
            for (i = 0, j = 0; i < 16; ++i, j += 4)
                m[i] = (uint)((data[j] << 24)
                    | (data[j + 1] << 16)
                    | (data[j + 2] << 8)
                    | (data[j + 3]));
            for (; i < 64; ++i) {
                t1 = m[i - 2];
                t1 = ((t1 >> 17) | (t1 << 15))
                    ^ ((t1 >> 19) | (t1 << 13))
                    ^ (t1 >> 10);
                t2 = m[i - 15];
                t2 = ((t2 >> 7) | (t2 << 25))
                    ^ ((t2 >> 18) | (t2 << 14))
                    ^ (t2 >> 3);

                m[i] = t1 + m[i - 7] + t2 + m[i - 16];

            }
            a = ctx.state[0];
            b = ctx.state[1];
            c = ctx.state[2];
            d = ctx.state[3];
            e = ctx.state[4];
            f = ctx.state[5];
            g = ctx.state[6];
            h = ctx.state[7];

            uint ep1, ep0, ch, maj;

            for (i = 0; i < 64; ++i) {
                ep1 = ((e >> 6) | (e << 26))
                    ^ ((e >> 11) | (e << 21))
                    ^ ((e >> 25) | (e << 7));

                ch = g ^ (e & (f ^ g));
                t1 = h + ep1 + ch + k[i] + m[i];

                ep0 = ((a >> 2) | (a << 30))
                    ^ ((a >> 13) | (a << 19))
                    ^ ((a >> 22) | (a << 10));
                maj = (a & b) | (c & (a ^ b));
                t2 = ep0 + maj;
                h = g;
                g = f;
                f = e;
                e = d + t1;
                d = c;
                c = b;
                b = a;
                a = t1 + t2;
            }



            ctx.state[0] = (uint)(ctx.state[0] + a) & 0xFFFFFFFF;
            ctx.state[1] = (uint)(ctx.state[1] + b) & 0xFFFFFFFF;
            ctx.state[2] = (uint)(ctx.state[2] + c) & 0xFFFFFFFF;
            ctx.state[3] = (uint)(ctx.state[3] + d) & 0xFFFFFFFF;
            ctx.state[4] = (uint)(ctx.state[4] + e) & 0xFFFFFFFF;
            ctx.state[5] = (uint)(ctx.state[5] + f) & 0xFFFFFFFF;
            ctx.state[6] = (uint)(ctx.state[6] + g) & 0xFFFFFFFF;
            ctx.state[7] = (uint)(ctx.state[7] + h) & 0xFFFFFFFF;

#endif

        }

        private static void Update(SHA256_CTX ctx, byte[] data, int len) {
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

        private static void Final(SHA256_CTX ctx, byte[] hash) {
#if JAVASCRIPT
            Bridge.Script.Write(@"
                var i = ctx.datalen;

                // Pad whatever data is left in the buffer.
                if (ctx.datalen < 56) {
                    ctx.data[i++] = 128;
                    while (i < 56) {
                        ctx.data[i++] = 0;
                    }
                } else {
                    ctx.data[i++] = 128;
                    while (i < 64) {
                        ctx.data[i++] = 0;
                    }
                    CM.Cryptography.SHA256.Transform(ctx, ctx.data);
                    for(i=0;i<56;i++)
                        ctx.data[i] = 0;
                }

                // Append to the padding the total message's length in bits and transform.
                var hi = { v : ctx.bitlen[0] };
                var lo = { v : ctx.bitlen[1] };
                CM.Cryptography.SHA256.INT64_ADD(hi, lo, (((ctx.datalen >>> 0) * 8) >>> 0));
                ctx.bitlen[0] = hi.v;
                ctx.bitlen[1] = lo.v;

                ctx.data[63] = (((ctx.bitlen[0] & 255) >>> 0)) & 255;
                ctx.data[62] = ((((ctx.bitlen[0] >>> 8) & 255) >>> 0)) & 255;
                ctx.data[61] = ((((ctx.bitlen[0] >>> 16) & 255) >>> 0)) & 255;
                ctx.data[60] = ((((ctx.bitlen[0] >>> 24) & 255) >>> 0)) & 255;
                ctx.data[59] = (((ctx.bitlen[1] & 255) >>> 0)) & 255;
                ctx.data[58] = ((((ctx.bitlen[1] >>> 8) & 255) >>> 0)) & 255;
                ctx.data[57] = ((((ctx.bitlen[1] >>> 16) & 255) >>> 0)) & 255;
                ctx.data[56] = ((((ctx.bitlen[1] >>> 24) & 255) >>> 0)) & 255;
                CM.Cryptography.SHA256.Transform(ctx, ctx.data);

                // Since this implementation uses little endian byte ordering and SHA uses big endian,
                // reverse all the bytes when copying the final state to the output hash.
                for (i = 0; i < 4; i = (i + 1) | 0) {
                    hash[i] = ((((ctx.state[0] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                    hash[((i + 4) | 0)] = ((((ctx.state[1] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                    hash[((i + 8) | 0)] = ((((ctx.state[2] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                    hash[((i + 12) | 0)] = ((((ctx.state[3] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                    hash[((i + 16) | 0)] = ((((ctx.state[4] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                    hash[((i + 20) | 0)] = ((((ctx.state[5] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                    hash[((i + 24) | 0)] = ((((ctx.state[6] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                    hash[((i + 28) | 0)] = ((((ctx.state[7] >>> (((24 - ((i * 8) | 0)) | 0))) & 255) >>> 0)) & 255;
                }
            ");
#else


            int i;

            i = ctx.datalen;

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
                Array.Clear(ctx.data, 0, 56);
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

            // Since this implementation uses little endian byte ordering and SHA uses big endian,
            // reverse all the bytes when copying the final state to the output hash.
            for (i = 0; i < 4; ++i) {
                hash[i] = (byte)((ctx.state[0] >> (24 - i * 8)) & 0xff);
                hash[i + 4] = (byte)((ctx.state[1] >> (24 - i * 8)) & 0xff);
                hash[i + 8] = (byte)((ctx.state[2] >> (24 - i * 8)) & 0xff);
                hash[i + 12] = (byte)((ctx.state[3] >> (24 - i * 8)) & 0xff);
                hash[i + 16] = (byte)((ctx.state[4] >> (24 - i * 8)) & 0xff);
                hash[i + 20] = (byte)((ctx.state[5] >> (24 - i * 8)) & 0xff);
                hash[i + 24] = (byte)((ctx.state[6] >> (24 - i * 8)) & 0xff);
                hash[i + 28] = (byte)((ctx.state[7] >> (24 - i * 8)) & 0xff);
            }
#endif
        }
    }
}