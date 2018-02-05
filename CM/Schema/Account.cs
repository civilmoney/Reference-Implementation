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
    /// Describes a user account
    /// </summary>
    public partial class Account : Message, IStorable {

        /// <summary>
        /// Optional server-side computed data for convenience, populated only when requested.
        /// </summary>
        public AccountCalculations AccountCalculations;

        public Account() : base() {
        }

        public Account(string payload) : base(payload) {
        }

        #region IStorable

        public int ConsensusCount { get; set; }

        public bool ConsensusOK { get { return ConsensusCount >= Constants.MinimumNumberOfCopies; } }


        /// <summary>
        /// Accounts are stored in path ACCNT/{Account ID}
        /// </summary>
        public string Path {
            get {
                return "ACCNT/" + ID;
            }
        }

        #endregion IStorable

        /// <summary>
        /// Ensures validity of the current account data.
        /// </summary>
        public byte[] AccountSignature { get { return Values.Get<byte[]>("SIG"); } set { Values.Set<byte[]>("SIG", value); } }

        /// <summary>
        /// Currently always 1
        /// </summary>
        public uint APIVersion { get { return Values.Get<uint>("VER"); } set { Values.Set<uint>("VER", value); } }

        /// <summary>
        /// Creation time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime CreatedUtc { get { return Values.Get<DateTime>("UTC"); } set { Values.Set<DateTime>("UTC", value); } }

        /// <summary>
        /// A user-chosen unique account ID or name. Naming rules:
        /// - Account IDs must match the regular expression ^[\p{L}|\p{Mn}|UnicodeRanges][\p{L}|\p{Mn}|UnicodeRanges|0-9|\-]{2,47}$
        /// - That is, starts with a letter. Period '.' and dash '-' are allowed. Between 3 and 48 characters in length.
        /// - The size of ID must be &lt;= 48 UTF-8 bytes. This means that the 'perceived' maximum length is less than 48 if non-ASCII character ranges are used.
        /// - Account IDs are case in-sensitive during lookup.
        /// - Account IDs must be rejected if equal to an ISO-31662 code and no GOVERNING-AUTHORITY attribute is present. These IDs are reserved for governing authorities.
        /// - The ID cannot be changed.
        /// </summary>
        public string ID { get { return this["ID"]; } set { this["ID"] = value; } }

        /// <summary>
        /// A default ISO 3166-2 subdivision code
        /// </summary>
        public string Iso31662Region { get { return this["REG"]; } set { this["REG"] = value; } }

        /// <summary>
        /// Stores the private key. The Private key component is encrypted
        /// using a specified key derivation routine, such as RFC2898.
        /// </summary>
        public PrivateKey PrivateKey { get { return Values.Get<PrivateKey>("PRIKEY"); } set { Values.Set<PrivateKey>("PRIKEY", value); } }

        /// <summary>
        /// Modification time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime UpdatedUtc { get { return Values.Get<DateTime>("UPD-UTC"); } set { Values.Set<DateTime>("UPD-UTC", value); } }

        /// <summary>
        /// Appends a new PublicKey to the account.
        /// </summary>
        /// <param name="key">The new public key to append.</param>
        public void AppendNewPublicKey(PublicKey key) {
            if (key == null) throw new ArgumentNullException("key");
            if (key.EffectiveDate < CreatedUtc
                || key.EffectiveDate != UpdatedUtc)
                throw new ArgumentException("Key effective date must be equal to UpdatedUtc.");
            Values.Append("PUBKEY", key.ToString());
        }

        /// <summary>
        /// Gets all values in the Account containing the ATTR- prefix.
        /// </summary>
        /// <returns></returns>
        public NamedValueList CollectAttributes() {
            var ar = new NamedValueList();
            for (int i = 0; i < Values.Count; i++) {
                var v = Values[i];
                if (v.Name.StartsWith("ATTR-", StringComparison.OrdinalIgnoreCase)) {
                    ar.Append(v.Name, v.Value);
                }
            }
            return ar;
        }

        /// <summary>
        /// Returns a sorted list of all public keys in the account (Oldest first.)
        /// </summary>
        /// <returns></returns>
        public List<PublicKey> GetAllPublicKeys() {
            var ar = new List<PublicKey>();
            for (int i = 0; i < Values.Count; i++) {
                if (String.Compare(Values[i].Name, "PUBKEY", StringComparison.OrdinalIgnoreCase) == 0) {
                    PublicKey tmp;
                    if (PublicKey.TryParse(Values[i].Value, out tmp))
                        ar.Add(tmp);
                }
            }
            ar.Sort();
            return ar;
        }

        /// <summary>
        /// Account signing data consists of UTF8 encoded bytes of all Account line 'values'
        /// in their originally specified order, with the exception of CALC-XX and SIG lines.
        /// </summary>
        public byte[] GetSigningData() {
            var data = new List<byte>();
            for (int i = 0; i < Values.Count; i++) {
                var v = Values[i];
                if (v.Name == null
                    || String.Compare(v.Name, "SIG", StringComparison.OrdinalIgnoreCase) == 0
                    || v.Name.StartsWith("CALC-", StringComparison.OrdinalIgnoreCase))
                    continue;
                data.AddRange(Encoding.UTF8.GetBytes(Values[i].Value));
            }
            return data.ToArray();
        }

        /// <summary>
        /// Deletes and replaces all attributes with the specified collection.
        /// </summary>
        /// <param name="newAttributes">The new attribute set to replace with.</param>
        public void ReplaceAttributes(NamedValueList newAttributes) {
            for (int i = 0; i < Values.Count; i++) {
                if (Values[i].Name.StartsWith("ATTR-", StringComparison.OrdinalIgnoreCase)) {
                    Values.RemoveAt(i--);
                }
            }
            Values.Append(newAttributes);
        }

        private void BeginSigningData(byte[] privateKey, PublicKey pubKey, AsyncRequest<DataSignRequest> e, ICryptoFunctions crypto) {
            int completed = 0;
            for (int i = 0; i < e.Item.Transforms.Count; i++) {
                crypto.BeginRSASign(new AsyncRequest<RSASignRequest>() {
                    Item = new RSASignRequest() {
                        Input = e.Item.Transforms[i].Input,
                        PrivateKey = privateKey,
                        PublicKey = pubKey.Key,
                        Tag = e.Item.Transforms[i]
                    },
                    OnComplete = (signature) => {
                        if (!signature.Result.Success) {
                            e.Completed(signature.Result);
                            return;
                        }
                        ((DataSignRequest.Transform)signature.Item.Tag).Output = signature.Item.OutputSignature;
                        completed++;
                        if (completed == e.Item.Transforms.Count)
                            e.Completed(CMResult.S_OK);
                    }
                });
            }
        }

        /// <summary>
        /// RSA Signs the specified data, which is first SHA256'd. If private keys are not 
        /// withheld then the PasswordOrRSAPrivateKey should contain the clear text secret
        /// for the account's encryption derivation scheme, which is then used to decode 
        /// the RSA private key.
        /// </summary>
        /// <param name="e">The password (withheld private key) and data to sign.</param>
        /// <param name="crypto">The crypto implementation.</param>
        public void SignData(AsyncRequest<DataSignRequest> e, ICryptoFunctions crypto) {
            PublicKey pubKey;
            if (!TryFindPublicKey(DateTime.UtcNow, out pubKey)) {
                e.Completed(CMResult.E_Account_Missing_Public_Key);
                return;
            }

            switch (this.PrivateKey.SchemeID) {
                case PrivateKeySchemeID.KeyWithheld:
                    if (e.Item.PasswordOrRSAPrivateKey == null) {
                        // We'll demand at least 1024 bit keys
                        e.Completed(CMResult.E_Account_Withheld_Private_Key_Required);
                        return;
                    }
                    if(e.Item.PasswordOrRSAPrivateKey.Length < Constants.MinimumRSAKeySizeInBytes) {
                        e.Completed(CMResult.E_Crypto_Invalid_RSA_PrivateKey_TooWeak);
                        return;
                    }
                    BeginSigningData(e.Item.PasswordOrRSAPrivateKey, pubKey, e, crypto);
                    break;
                case PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000:
                    crypto.BeginRFC2898(new AsyncRequest<RFC2898CryptoRequest>() {
                        Item = new RFC2898CryptoRequest() {
                            Salt = this.PrivateKey.Salt,
                            Iterations = 10000,
                            IVSizeBytes = 16,
                            KeySizeBytes = 32,
                            Password = e.Item.PasswordOrRSAPrivateKey
                        },
                        OnComplete = (aesKey) => {
                            if (!aesKey.Result.Success) {
                                e.Completed(aesKey.Result);
                                return;
                            }
                            crypto.BeginAESDecrypt(new AsyncRequest<AESCryptoRequest>() {
                                Item = new AESCryptoRequest() {
                                    Key = aesKey.Item.OutputKey,
                                    IV = aesKey.Item.OutputIV,
                                    Input = this.PrivateKey.Encrypted
                                },
                                OnComplete = (aesResult) => {
                                    if (!aesResult.Result.Success) {
                                        e.Completed(aesResult.Result);
                                        return;
                                    }
                                    var privateKey = aesResult.Item.Output;
                                    BeginSigningData(privateKey, pubKey, e, crypto);

                                }
                            });
                        }
                    });
                    break;

                default:
                    e.Completed(CMResult.E_Crypto_Unrecognized_SchemeID);
                    break;
            }
        }

        /// <summary>
        /// Gets the public/private key
        /// </summary>
        /// <param name="dateUtc">The date when the public key was/is effective.</param>
        /// <param name="key">Pointer to receive the located public key.</param>
        /// <returns>True if a key was found, otherwise false.</returns>
        public bool TryFindPublicKey(DateTime dateUtc, out PublicKey key) {
            PublicKey bestKey = null;
            for (int i = 0; i < Values.Count; i++) {
                if (String.Compare(Values[i].Name, "PUBKEY", StringComparison.OrdinalIgnoreCase) == 0) {
                    PublicKey tmp;
                    PublicKey.TryParse(Values[i].Value, out tmp);
                    if (tmp.EffectiveDate > dateUtc
                        || (bestKey != null && tmp.EffectiveDate < bestKey.EffectiveDate))
                        continue;
                    bestKey = tmp;
                }
            }
            key = bestKey;
            return key != null;
        }

        /// <summary>
        /// Verifies the specified RSA signature using the account's public key
        /// for the specified time.
        /// </summary>
        /// <param name="e">The time stamp, data and signature to validate.</param>
        /// <param name="crypto">The crypto implementation.</param>
        public void VerifySignature(AsyncRequest<DataVerifyRequest> e, ICryptoFunctions crypto) {
            PublicKey pubKey;
            if (!TryFindPublicKey(e.Item.DataDateUtc, out pubKey)) {
                e.Completed(CMResult.E_Account_Missing_Public_Key);
                return;
            }
            crypto.BeginRSAVerify(new AsyncRequest<RSAVerifyRequest>() {
                Item = new RSAVerifyRequest() {
                    Exponent = Constants.StandardExponent65537,
                    Input = e.Item.Input,
                    InputSignature = e.Item.Signature,
                    PublicKey = pubKey.Key
                },
                OnComplete = (r) => {
                    e.Completed(r.Result);
                }
            });
        }

        public void ChangePasswordAndSign(AsyncRequest<PasswordRequest> e, ICryptoFunctions crypto) {
            e.UpdateProgress(0);
            if (PrivateKey != null && (e.Item.OldPass == null && e.Item.OldRSAPrivateKey == null)) {
                // old pass is required.
                e.Completed(CMResult.E_Crypto_Invalid_Password);
                return;
            }
            if (PrivateKey != null) {

                PublicKey oldPubKey;
                if (!TryFindPublicKey(DateTime.UtcNow, out oldPubKey)) {
                    e.Completed(CMResult.E_Account_Missing_Public_Key);
                    return;
                }

                if (e.Item.OldSchemeID == PrivateKeySchemeID.KeyWithheld) {

                    if(!Helpers.IsHashEqual(oldPubKey.Key, oldPubKey.Key)) {
                        e.Completed(CMResult.E_Crypto_Invalid_RSA_PrivateKey_Mismatch);
                        return;
                    }

                    ChangePasswordAndSign(e, crypto, e.Item.OldRSAPrivateKey, oldPubKey.Key);

                } else {
                    // we're doing a key change, decrypt the old one
                    crypto.BeginRFC2898(new AsyncRequest<RFC2898CryptoRequest>() {
                        Item = new RFC2898CryptoRequest() {
                            Salt = this.PrivateKey.Salt,
                            Iterations = 10000,
                            IVSizeBytes = 16,
                            KeySizeBytes = 32,
                            Password = Encoding.UTF8.GetBytes(e.Item.OldPass)
                        },
                        OnComplete = (aesKey) => {
                            if (!aesKey.Result.Success) {
                                e.Completed(aesKey.Result);
                                return;
                            }
                            crypto.BeginAESDecrypt(new AsyncRequest<AESCryptoRequest>() {
                                Item = new AESCryptoRequest() {
                                    Key = aesKey.Item.OutputKey,
                                    IV = aesKey.Item.OutputIV,
                                    Input = this.PrivateKey.Encrypted
                                },
                                OnComplete = (aesResult) => {
                                    if (!aesResult.Result.Success) {
                                        e.Completed(aesResult.Result);
                                        return;
                                    }
                                    var oldPrivateKey = aesResult.Item.Output;
                                    ChangePasswordAndSign(e, crypto, oldPrivateKey, oldPubKey.Key);
                                }
                            });
                        }

                    });
                }
            } else {
                ChangePasswordAndSign(e, crypto, null, null);
            }
        }

        private void ChangePasswordAndSign(AsyncRequest<PasswordRequest> e, ICryptoFunctions crypto,
            byte[] oldPrivateKey, byte[] oldPubKey) {

            if (e.Item.NewSchemeID == PrivateKeySchemeID.KeyWithheld) {

                var newPKRecord = new PrivateKey() {
                    SchemeID = PrivateKeySchemeID.KeyWithheld
                };

                ApplyNewPrivateKey(e, crypto, newPKRecord, 
                    e.Item.NewRSAPrivateKey, e.Item.NewRSAPublicKey,
                    oldPrivateKey, oldPubKey);

            } else {

                crypto.BeginRSAKeyGen(new AsyncRequest<RSAKeyRequest>() {
                    Item = new RSAKeyRequest(),
                    OnComplete = (rsa) => {
                        if (!rsa.Result.Success) {
                            e.Completed(rsa.Result);
                            return;
                        }
                        e.UpdateProgress(25);
                        byte[] salt = new byte[32];
                        CM.Cryptography.RNG.RandomBytes(salt);
                        crypto.BeginRFC2898(new AsyncRequest<RFC2898CryptoRequest>() {
                            Item = new RFC2898CryptoRequest() {
                                Salt = salt,
                                Iterations = 10000,
                                IVSizeBytes = 16,
                                KeySizeBytes = 32,
                                Password = Encoding.UTF8.GetBytes(e.Item.NewPass)
                            },
                            OnComplete = (aesKey) => {
                                if (!aesKey.Result.Success) {
                                    e.Completed(aesKey.Result);
                                    return;
                                }
                                e.UpdateProgress(50);
                                crypto.BeginAESEncrypt(
                                    new AsyncRequest<AESCryptoRequest>() {
                                        Item = new AESCryptoRequest() {
                                            Key = aesKey.Item.OutputKey,
                                            IV = aesKey.Item.OutputIV,
                                            Input = rsa.Item.Output.D
                                        },
                                        OnComplete = (encrypted) => {
                                            if (!encrypted.Result.Success) {
                                                e.Completed(encrypted.Result);
                                                return;
                                            }
                                            e.UpdateProgress(75);
                                            // Commit the encrypted private + clear public key
                                            var newPKRecord = new PrivateKey() {
                                                SchemeID = PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000,
                                                Salt = salt,
                                                Encrypted = encrypted.Item.Output
                                            };

                                            ApplyNewPrivateKey(e, crypto, newPKRecord, 
                                                rsa.Item.Output.D, rsa.Item.Output.Modulus,
                                                oldPrivateKey, oldPubKey);
                                        }
                                    });
                            }
                        });
                    }
                });
            }
        }

        private void ApplyNewPrivateKey(AsyncRequest<PasswordRequest> e, ICryptoFunctions crypto, 
            PrivateKey encryptedPrivateKey, byte[] newPrivateKey, byte[] newPublicKey, 
            byte[] oldPrivateKey, byte[] oldPublicKey) {

            this.PrivateKey = encryptedPrivateKey;

            var newPubKeyRecord = new PublicKey() {
                EffectiveDate = this.UpdatedUtc,
                Key = newPublicKey 
            };

            if (oldPrivateKey != null) {
                crypto.BeginRSASign(new AsyncRequest<RSASignRequest>() {
                    Item = new RSASignRequest() {
                        Input = newPubKeyRecord.GetModificationSigningData(),
                        PrivateKey = oldPrivateKey,
                        PublicKey = oldPublicKey
                    },
                    OnComplete = (signature) => {
                        if (!signature.Result.Success) {
                            e.Completed(signature.Result);
                            return;
                        }
                        newPubKeyRecord.ModificationSignature = signature.Item.OutputSignature;
                        this.AppendNewPublicKey(newPubKeyRecord);
                        SignAccount(e, crypto, newPrivateKey, newPublicKey);
                    }
                });
            } else {
                this.AppendNewPublicKey(newPubKeyRecord);
                SignAccount(e, crypto, newPrivateKey, newPublicKey);
            }

        }

        private void SignAccount(AsyncRequest<PasswordRequest> e, ICryptoFunctions crypto,
           byte[] privateKey, byte[] publicKey) {
            crypto.BeginRSASign(new AsyncRequest<RSASignRequest>() {
                Item = new RSASignRequest() {
                    Input = this.GetSigningData(),
                    PrivateKey = privateKey,
                    PublicKey = publicKey
                },
                OnComplete = (signature) => {
                    if (!signature.Result.Success) {
                        e.Completed(signature.Result);
                        return;
                    }
                    this.AccountSignature = signature.Item.OutputSignature;
                    e.UpdateProgress(100);
                    e.Completed(CMResult.S_OK);
                }
            });
        }

        /// <summary>
        /// Account IDs that are equal to an ISO3166-2 subdivision code
        /// must have an ATTR-GOV attribute which contains a signature
        /// of the Creation UTC date and region code.
        /// </summary>
        /// <returns>The data to use for IsGoverningAuthority verification.</returns>
        public byte[] GetGoverningAuthoritySigningData() {
            var data = new List<byte>();
            data.AddRange(Encoding.UTF8.GetBytes(Helpers.DateToISO8601(CreatedUtc)));
            data.AddRange(Encoding.UTF8.GetBytes(Iso31662Region));
            return data.ToArray();
        }

        /// <summary>
        /// Determines whether or not the account's ATTR-GOV signature is valid.
        /// </summary>
        public void CheckIsValidGoverningAuthority(AsyncRequest<bool> req, ICryptoFunctions crypto) {
            var sig = Values[AccountAttributes.GoverningAuthority_Key] ?? null;

            if (sig == null) {
                req.Completed(CMResult.E_General_Failure);
                return;
            }
            try {
                var verif = new AsyncRequest<RSAVerifyRequest>() {
                    Item = new RSAVerifyRequest() {
                        Exponent = Constants.StandardExponent65537,
                        Input = GetGoverningAuthoritySigningData(),
                        InputSignature = Convert.FromBase64String(sig),
                        PublicKey = Constants.GoverningAuthorityRSAPublicKey
                    },
                    OnComplete = (res) => {
                        req.Item = res.Result == CMResult.S_OK;
                        req.Completed(res.Result);
                    }
                };
                crypto.BeginRSAVerify(verif);
            } catch {
                req.Completed(CMResult.E_General_Failure);
            }
        }
    }

    public class PasswordRequest {
        public string NewPass;
        public string OldPass;
        public byte[] OldRSAPrivateKey;
        public byte[] NewRSAPrivateKey;
        public byte[] NewRSAPublicKey;
        public PrivateKeySchemeID NewSchemeID = PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000;
        public PrivateKeySchemeID OldSchemeID = PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000;
    }

    public class DataSignRequest {

        public DataSignRequest() {
            Transforms = new List<Transform>();
        }

        public DataSignRequest(byte[] input)
            : this() {
            Transforms.Add(new Transform(input));
        }

        public List<Transform> Transforms;

        /// <summary>
        /// If the account uses PrivateKeySchemeID AES_CBC_PKCS7_RFC2898_HMACSHA1_10000 then this
        /// is a cleartext password used for the key derivation scheme.
        /// 
        /// If the account uses PrivateKeySchemeID KeyWithheld then this is the current private key.
        /// </summary>
        public byte[] PasswordOrRSAPrivateKey;


        public class Transform {

            public Transform(byte[] input) {
                Input = input;
            }

            public byte[] Input;
            public byte[] Output;
        }
    }

    public class DataVerifyRequest {
        public DateTime DataDateUtc;
        public byte[] Input;
        public byte[] Signature;
    }
}