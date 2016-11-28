#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Cryptography {

    /// <summary>
    /// Rivest-Shamir-Adleman (RSA)
    /// </summary>
    public class RSA {

        public static byte[] EMSA_PKCS1_v1_5Encode_256(byte[] b, int keyLength) {
            var res = Convert.FromBase64String("MDEwDQYJYIZIAWUDBAIBBQAEIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
            Array.Copy(b, 0, res, res.Length - 32, 32);

            int PSLength = System.Math.Max(8, keyLength - res.Length - 3);
            // PS = PSLength of 0xff

            // EM = 0x00 | 0x01 | PS | 0x00 | T
            byte[] EM = new byte[PSLength + res.Length + 3];
            EM[1] = 0x01;
            for (int i = 2; i < PSLength + 2; i++)
                EM[i] = 0xff;
            Array.Copy(res, 0, EM, PSLength + 3, res.Length);

            return EM;
        }
    }
}