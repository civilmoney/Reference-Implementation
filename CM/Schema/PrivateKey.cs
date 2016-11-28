#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Schema {

    public enum PrivateKeySchemeID {
        AES_CBC_PKCS7_RFC2898_HMACSHA1_10000 = 0,
    }

    public class PrivateKey {
        public PrivateKeySchemeID SchemeID;
        public byte[] Salt;
        public byte[] Encrypted;

        /// <summary>
        /// Returns a CM Message formatted PRI-KEY representation - {scheme-id},{salt},{encrypted}
        /// </summary>
        public override string ToString() {
            return (int)SchemeID
                + "," + Convert.ToBase64String(Salt ?? new Byte[0])
                + "," + Convert.ToBase64String(Encrypted ?? new Byte[0]);
        }

        /// <summary>
        /// Attempts to parse a PublicPrivateKey out of the specified PRI-KEY line '{scheme-id},{salt},{encrypted}'
        /// </summary>
        /// <param name="delimitedData">{scheme-id},{salt},{encrypted}</param>
        /// <param name="key">Pointer to receive the parsed key.</param>
        /// <returns>True if parsing succeeds, otherwise false.</returns>
        public static bool TryParse(string delimitedData, out PrivateKey key) {
            key = null;
            if (String.IsNullOrWhiteSpace(delimitedData))
                return false;
            int cursor = 0;
            string scheme = delimitedData.NextCsvValue(ref cursor);
            string salt = delimitedData.NextCsvValue(ref cursor);
            string priv = delimitedData.NextCsvValue(ref cursor);
            byte[] privBytes = Convert.FromBase64String(priv);
            byte[] saltBytes = Convert.FromBase64String(salt);
            uint schemeID;
            if (!uint.TryParse(scheme, out schemeID))
                return false;
            key = new PrivateKey() {
                Encrypted = privBytes,
                Salt = saltBytes,
                SchemeID = (PrivateKeySchemeID)schemeID,
            };
            return true;
        }
    }
}