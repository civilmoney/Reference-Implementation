#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using CM;
using System;

namespace CM.Server {

    /// <summary>
    /// A utility class to generate an account Governing Authority signature.
    /// The GA private key (held by the Civil Money steering group) is required
    /// in order to create a valid output.
    /// </summary>
    public class GenerateGoverningAuthoritySignature {

        public static string Generate(string privateKeyBase64, string accountCreationUtc, string regionCode) {
            regionCode = regionCode.ToUpper();
            if (ISO31662.GetName(regionCode) == null) {
                return "Invalid ISO 3166-2 region code";
            }
            byte[] key;
            try {
                key = Convert.FromBase64String(privateKeyBase64);
            } catch {
                return "Invalid private key";
            }
            var a = new CM.Schema.Account();
            a.CreatedUtc = Helpers.DateFromISO8601(accountCreationUtc);
            a.Iso31662Region = regionCode;
            var req = new AsyncRequest<RSASignRequest>() {
                Item = new RSASignRequest() {
                    Input = a.GetGoverningAuthoritySigningData(),
                    PrivateKey = key,
                    PublicKey = CM.Constants.GoverningAuthorityRSAPublicKey
                }
            };
            CM.Server.CryptoFunctions.Identity.BeginRSASign(req);
            return Convert.ToBase64String(req.Item.OutputSignature);
        }
    }
}