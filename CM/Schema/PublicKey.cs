#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Text;

namespace CM.Schema {

    /// <summary>
    /// Describes a public/private key and its corresponding effective date.
    /// </summary>
    public class PublicKey : IComparable<PublicKey> {

        /// <summary>
        /// The date when this key has taken effect. Must be less than or equal to the Account's UPDATED-UTC.
        /// </summary>
        public DateTime EffectiveDate;

        /// <summary>
        /// The public key for use with RSA signature validation.
        /// </summary>
        public byte[] Key;

        /// <summary>
        /// During account password change, this is the signature of NEW Public Key + EffectiveDate using the PREVIOUS Private Key.
        /// Basically proof of ownership during a password change. Peers are going to make sure that PREVIOUS PublicKey works
        /// against the NEW Key + EffectiveDate.
        /// </summary>
        public byte[] ModificationSignature;

        /// <summary>
        /// Compares based on EffectiveDate ascending.
        /// </summary>
        public int CompareTo(PublicKey other) {
            return EffectiveDate.CompareTo(other.EffectiveDate);
        }

        /// <summary>
        /// Gets the data to sign when appending a new public key. It must be signed using
        /// the PREVIOUS private key.
        /// </summary>
        /// <returns></returns>
        public byte[] GetModificationSigningData() {
            var ar = new List<byte>();
            ar.AddRange(Encoding.UTF8.GetBytes(Helpers.DateToISO8601(EffectiveDate)));
            ar.AddRange(Key);
            return ar.ToArray();
        }

        /// <summary>
        /// Returns a CM Message formatted PUBKEY representation - {date},{public key},{modification signature}
        /// </summary>
        public override string ToString() {
            return Helpers.DateToISO8601(EffectiveDate)
                + "," + Convert.ToBase64String(Key ?? new Byte[0])
                + "," + Convert.ToBase64String(ModificationSignature ?? new Byte[0]);
        }

        /// <summary>
        /// Attempts to parse a PublicPrivateKey out of the specified KEY line '- {date},{public key},{modification signature}'
        /// </summary>
        /// <param name="delimitedData">{date},{public key},{modification signature}</param>
        /// <param name="key">Pointer to receive the parsed key.</param>
        /// <returns>True if parsing succeeds, otherwise false.</returns>
        public static bool TryParse(string delimitedData, out PublicKey key) {
            key = null;
            if (String.IsNullOrWhiteSpace(delimitedData))
                return false;
            int cursor = 0;
            string date = delimitedData.NextCsvValue(ref cursor);
            string pub = delimitedData.NextCsvValue(ref cursor);
            string mod = delimitedData.NextCsvValue(ref cursor);
            DateTime d;
            if (!Helpers.DateFromISO8601(date, out d))
                return false;
            try {
                byte[] pubBytes = Convert.FromBase64String(pub);
                byte[] modBytes = Convert.FromBase64String(mod);
                key = new PublicKey() {
                    EffectiveDate = d,
                    Key = pubBytes,
                    ModificationSignature = modBytes
                };
                return true;
            } catch {
                // Bad data
                return false;
            }
        }
    }
}