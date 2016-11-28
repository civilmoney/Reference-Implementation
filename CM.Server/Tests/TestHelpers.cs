#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

#if TESTS
using System;
using System.Text;
using CM;
using CM.Schema;
using Xunit;

namespace CM.Server.Tests
{
    static class TestHelpers {

        public static byte[] AESEncrypt(byte[] password, byte[] salt, byte[] clear) {
            var aesKey = new AsyncRequest<RFC2898CryptoRequest>() {
                Item = new RFC2898CryptoRequest() {
                    Salt = salt,
                    Iterations = 10000,
                    IVSizeBytes = 16,
                    KeySizeBytes = 32,
                    Password = password
                }
            };

            CM.Server.CryptoFunctions.Identity.BeginRFC2898(aesKey);

            var aes = new AsyncRequest<AESCryptoRequest>() {
                Item = new AESCryptoRequest() {
                    Key = aesKey.Item.OutputKey,
                    IV = aesKey.Item.OutputIV,
                    Input = clear
                }
            };

            CM.Server.CryptoFunctions.Identity.BeginAESEncrypt(aes);

            return aes.Item.Output;
        }
        const string VALID_ACCOUNT_CHARS = @"-0987654321abcdefghijklmnopqrstuvwxyzالمالالمدنيכסףהאזרחיनागरिकपैसेгражданскипари民间资金民間資金íüαστικέςχρήματα市民のお金시민돈āųųųųėėċċċċċپولمدنیąгражданскоеденьгиเงินทางแพ่งцивільнігрошіسولقمtiềndânsự";

        public static Account CreateAccount() {
            var rnd = new Random();
            int len = rnd.Next(3, 40);
            string id = "";
            for (int i = 0; i < len && Encoding.UTF8.GetByteCount(id) < 45; i++) {
                id += VALID_ACCOUNT_CHARS[rnd.Next(i == 0 ? 11 : 0, VALID_ACCOUNT_CHARS.Length)];
            }
            Assert.True(Helpers.IsIDValid(id), "Account ID '" + id + "' doesn't pass.");

            var a = new Account() {
                ID = id,
                APIVersion = Constants.APIVersion,
                Iso31662Region = ISO31662.Values[rnd.Next(0, ISO31662.Values.Length - 1)].ID,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            a.ChangePasswordAndSign(new AsyncRequest<PasswordRequest>() {
                Item = new PasswordRequest() {
                    NewPass = id
                }
            }, CM.Server.CryptoFunctions.Identity);

            var verify = new AsyncRequest<DataVerifyRequest>() {
                Item = new DataVerifyRequest() {
                    DataDateUtc = a.UpdatedUtc,
                    Input = a.GetSigningData(),
                    Signature = a.AccountSignature
                }
            };
            a.VerifySignature(verify, CM.Server.CryptoFunctions.Identity);
            Assert.Equal(verify.Result, CMResult.S_OK);
            return a;
        }
    }
}
#endif