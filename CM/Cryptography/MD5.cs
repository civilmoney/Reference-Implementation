#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM.Cryptography {
    /// <summary>
    /// A portable C# MD5, based on work by Brad Conte (bradconte.com) MIT License
    /// </summary>
    public class MD5 {
        static uint ROTLEFT(uint a, int b) {
            return (uint)(((a) << (b)) | ((a) >> (32 - (b))));
        }
        static void INT64_ADD(ref uint a, ref uint b, uint c) {
            if (a > 0xffffffff - (c)) ++b; a += c;
        }
        public static byte[] ComputeHash(byte[] data) {
            var ctx = new MD5_CTX();
            Update(ctx, data, data.Length);
            var hash = new byte[16];
            Final(ctx, hash);
            return hash;
        }
        static uint F(uint x, uint y, uint z) { return ((x & y) | (~x & z)); }
        static uint G(uint x, uint y, uint z) { return ((x & z) | (y & ~z)); }
        static uint H(uint x, uint y, uint z) { return (x ^ y ^ z); }
        static uint I(uint x, uint y, uint z) { return (y ^ (x | ~z)); }

        static uint FF(uint a, uint b, uint c, uint d, uint m, int s, uint t) {
            a += F(b, c, d) + m + t;
            a = b + ROTLEFT(a, s);
            return a;
        }
        static uint GG(uint a, uint b, uint c, uint d, uint m, int s, uint t) {
            a += G(b, c, d) + m + t;
            a = b + ROTLEFT(a, s);
            return a;
        }
        static uint HH(uint a, uint b, uint c, uint d, uint m, int s, uint t) {
            a += H(b, c, d) + m + t;
            a = b + ROTLEFT(a, s);
            return a;
        }
        static uint II(uint a, uint b, uint c, uint d, uint m, int s, uint t) {
            a += I(b, c, d) + m + t;
            a = b + ROTLEFT(a, s);
            return a;
        }

        /*********************** FUNCTION DEFINITIONS ***********************/
        class MD5_CTX {
            public MD5_CTX() {
                Reset();
            }
            public uint[] state = new uint[5];
            public byte[] data = new byte[64];
            public int datalen;
            public uint[] bitlen = new uint[2];
            public void Reset() {
                datalen = 0;
                bitlen[0] = 0;
                bitlen[1] = 0;

                state[0] = 0x67452301;
                state[1] = 0xEFCDAB89;
                state[2] = 0x98BADCFE;
                state[3] = 0x10325476;
            }
        }
        static void Transform(MD5_CTX ctx, byte[] data) {
            uint a, b, c, d, i, j;
            uint[] m = new uint[16];
            // MD5 specifies big endian byte order, but this implementation assumes a little
            // endian byte order CPU. Reverse all the bytes upon input, and re-reverse them
            // on output (in md5_final()).
            for (i = 0, j = 0; i < 16; ++i, j += 4)
                m[i] = (data[j]) + ((uint)data[j + 1] << 8) + ((uint)data[j + 2] << 16) + ((uint)data[j + 3] << 24);

            a = ctx.state[0];
            b = ctx.state[1];
            c = ctx.state[2];
            d = ctx.state[3];

            a = FF(a, b, c, d, m[0], 7, 0xd76aa478);
            d = FF(d, a, b, c, m[1], 12, 0xe8c7b756);
            c = FF(c, d, a, b, m[2], 17, 0x242070db);
            b = FF(b, c, d, a, m[3], 22, 0xc1bdceee);
            a = FF(a, b, c, d, m[4], 7, 0xf57c0faf);
            d = FF(d, a, b, c, m[5], 12, 0x4787c62a);
            c = FF(c, d, a, b, m[6], 17, 0xa8304613);
            b = FF(b, c, d, a, m[7], 22, 0xfd469501);
            a = FF(a, b, c, d, m[8], 7, 0x698098d8);
            d = FF(d, a, b, c, m[9], 12, 0x8b44f7af);
            c = FF(c, d, a, b, m[10], 17, 0xffff5bb1);
            b = FF(b, c, d, a, m[11], 22, 0x895cd7be);
            a = FF(a, b, c, d, m[12], 7, 0x6b901122);
            d = FF(d, a, b, c, m[13], 12, 0xfd987193);
            c = FF(c, d, a, b, m[14], 17, 0xa679438e);
            b = FF(b, c, d, a, m[15], 22, 0x49b40821);

            a=GG(a, b, c, d, m[1], 5, 0xf61e2562);
            d=GG(d, a, b, c, m[6], 9, 0xc040b340);
            c=GG(c, d, a, b, m[11], 14, 0x265e5a51);
            b=GG(b, c, d, a, m[0], 20, 0xe9b6c7aa);
            a=GG(a, b, c, d, m[5], 5, 0xd62f105d);
            d=GG(d, a, b, c, m[10], 9, 0x02441453);
            c=GG(c, d, a, b, m[15], 14, 0xd8a1e681);
            b=GG(b, c, d, a, m[4], 20, 0xe7d3fbc8);
            a=GG(a, b, c, d, m[9], 5, 0x21e1cde6);
            d=GG(d, a, b, c, m[14], 9, 0xc33707d6);
            c=GG(c, d, a, b, m[3], 14, 0xf4d50d87);
            b=GG(b, c, d, a, m[8], 20, 0x455a14ed);
            a=GG(a, b, c, d, m[13], 5, 0xa9e3e905);
            d=GG(d, a, b, c, m[2], 9, 0xfcefa3f8);
            c=GG(c, d, a, b, m[7], 14, 0x676f02d9);
            b=GG(b, c, d, a, m[12], 20, 0x8d2a4c8a);

            a=HH(a, b, c, d, m[5], 4, 0xfffa3942);
            d=HH(d, a, b, c, m[8], 11, 0x8771f681);
            c=HH(c, d, a, b, m[11], 16, 0x6d9d6122);
            b=HH(b, c, d, a, m[14], 23, 0xfde5380c);
            a=HH(a, b, c, d, m[1], 4, 0xa4beea44);
            d=HH(d, a, b, c, m[4], 11, 0x4bdecfa9);
            c=HH(c, d, a, b, m[7], 16, 0xf6bb4b60);
            b=HH(b, c, d, a, m[10], 23, 0xbebfbc70);
            a=HH(a, b, c, d, m[13], 4, 0x289b7ec6);
            d=HH(d, a, b, c, m[0], 11, 0xeaa127fa);
            c=HH(c, d, a, b, m[3], 16, 0xd4ef3085);
            b=HH(b, c, d, a, m[6], 23, 0x04881d05);
            a=HH(a, b, c, d, m[9], 4, 0xd9d4d039);
            d=HH(d, a, b, c, m[12], 11, 0xe6db99e5);
            c=HH(c, d, a, b, m[15], 16, 0x1fa27cf8);
            b=HH(b, c, d, a, m[2], 23, 0xc4ac5665);

            a=II(a, b, c, d, m[0], 6, 0xf4292244);
            d=II(d, a, b, c, m[7], 10, 0x432aff97);
            c=II(c, d, a, b, m[14], 15, 0xab9423a7);
            b=II(b, c, d, a, m[5], 21, 0xfc93a039);
            a=II(a, b, c, d, m[12], 6, 0x655b59c3);
            d=II(d, a, b, c, m[3], 10, 0x8f0ccc92);
            c=II(c, d, a, b, m[10], 15, 0xffeff47d);
            b=II(b, c, d, a, m[1], 21, 0x85845dd1);
            a=II(a, b, c, d, m[8], 6, 0x6fa87e4f);
            d=II(d, a, b, c, m[15], 10, 0xfe2ce6e0);
            c=II(c, d, a, b, m[6], 15, 0xa3014314);
            b=II(b, c, d, a, m[13], 21, 0x4e0811a1);
            a=II(a, b, c, d, m[4], 6, 0xf7537e82);
            d=II(d, a, b, c, m[11], 10, 0xbd3af235);
            c=II(c, d, a, b, m[2], 15, 0x2ad7d2bb);
            b=II(b, c, d, a, m[9], 21, 0xeb86d391);

            ctx.state[0] += a;
            ctx.state[1] += b;
            ctx.state[2] += c;
            ctx.state[3] += d;
        }


        static void Update(MD5_CTX ctx, byte[] data, int len) {
            int i;

            for (i = 0; i < len; ++i) {
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

        static void Final(MD5_CTX ctx, byte[] hash) {
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
                for (i = 0; i < 56; i++)
                    ctx.data[i] = 0;
            }

            // Append to the padding the total message's length in bits and transform.

            uint hi = ctx.bitlen[0];
            uint lo = ctx.bitlen[1];
            INT64_ADD(ref hi, ref lo, (uint)ctx.datalen * 8);
            ctx.bitlen[0] = hi;
            ctx.bitlen[1] = lo;

            //ctx.data[56] = (byte)(ctx.bitlen & 0xff);
            //ctx.data[57] = (byte)((ctx.bitlen >> 8) & 0xff);
            //ctx.data[58] = (byte)((ctx.bitlen >> 16) & 0xff);
            //ctx.data[59] = (byte)((ctx.bitlen >> 24) & 0xff);
            //ctx.data[60] = (byte)((ctx.bitlen >> 32) & 0xff);
            //ctx.data[61] = (byte)((ctx.bitlen >> 40) & 0xff);
            //ctx.data[62] = (byte)((ctx.bitlen >> 48) & 0xff);
            //ctx.data[63] = (byte)((ctx.bitlen >> 56) & 0xff);

            ctx.data[56] = (byte)(ctx.bitlen[0] & 0xff);
            ctx.data[57] = (byte)((ctx.bitlen[0] >> 8) & 0xff);
            ctx.data[58] = (byte)((ctx.bitlen[0] >> 16) & 0xff);
            ctx.data[59] = (byte)((ctx.bitlen[0] >> 24) & 0xff);
            ctx.data[60] = (byte)(ctx.bitlen[1] & 0xff);
            ctx.data[61] = (byte)((ctx.bitlen[1] >> 8) & 0xff);
            ctx.data[62] = (byte)((ctx.bitlen[1] >> 16) & 0xff);
            ctx.data[63] = (byte)((ctx.bitlen[1] >> 24) & 0xff);

            Transform(ctx, ctx.data);

            // Since this implementation uses little endian byte ordering and MD uses big endian,
            // reverse all the bytes when copying the final state to the output hash.
            for (i = 0; i < 4; ++i) {
                hash[i] = (byte)((ctx.state[0] >> (i * 8)) & 0xff);
                hash[i + 4] = (byte)((ctx.state[1] >> (i * 8)) & 0xff);
                hash[i + 8] = (byte)((ctx.state[2] >> (i * 8)) & 0xff);
                hash[i + 12] = (byte)((ctx.state[3] >> (i * 8)) & 0xff);
            }
        }
    }
}
