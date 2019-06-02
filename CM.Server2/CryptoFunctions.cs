#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;

namespace CM.Server {

    /// <summary>
    /// For .NET runtime, all crypto operations are fast enough that they run synchronously.
    /// </summary>
    public class CryptoFunctions : ICryptoFunctions, IDisposable {
        public static readonly CryptoFunctions Identity;

        private SHA256 _SHA256;
        private MD5 _MD5;

        static CryptoFunctions() {
            CM.Cryptography.RNG.RandomBytes = RNG;
            Identity = new CryptoFunctions();
        }

        private CryptoFunctions() {
            _SHA256 = SHA256.Create();
            _MD5 = MD5.Create();
        }

        public void BeginAESDecrypt(AsyncRequest<AESCryptoRequest> e) {
            try {
                using (var aes = Aes.Create()) {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = e.Item.Key;
                    aes.IV = e.Item.IV;
                    using (var ms = new MemoryStream()) {
                        var s = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
                        s.Write(e.Item.Input, 0, e.Item.Input.Length);
                        s.Flush();
                        s.Dispose();
                        e.Item.Output = ms.ToArray();
                    }
                }
                e.Completed(CMResult.S_OK);
            } catch {
                e.Completed(CMResult.E_Crypto_Invalid_Password);
            }
        }

        public void BeginAESEncrypt(AsyncRequest<AESCryptoRequest> e) {
            try {
                using (var aes = Aes.Create()) {
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = e.Item.Key;
                    aes.IV = e.Item.IV;
                    using (var ms = new MemoryStream()) {
                        var s = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
                        s.Write(e.Item.Input, 0, e.Item.Input.Length);
                        s.FlushFinalBlock();
                        s.Dispose();
                        e.Item.Output = ms.ToArray();
                    }
                }
                e.Completed(CMResult.S_OK);
            } catch {
                e.Completed(CMResult.E_Crypto_Invalid_Password);
            }
        }

        public void BeginRFC2898(AsyncRequest<RFC2898CryptoRequest> e) {
            try {
                using (var key = new Rfc2898DeriveBytes(e.Item.Password, e.Item.Salt, e.Item.Iterations)) {
                    e.Item.OutputKey = key.GetBytes(e.Item.KeySizeBytes);
                    e.Item.OutputIV = key.GetBytes(e.Item.IVSizeBytes);
                }
                e.Completed(CMResult.S_OK);
            } catch {
                e.Completed(CMResult.E_Crypto_Rfc2898_General_Failure);
            }
        }

        /// <summary>
        /// This is never called on production servers, but is needed for tests.
        /// It's also a useful reference implementation for other apps.
        /// </summary>
        public void BeginRSAKeyGen(AsyncRequest<RSAKeyRequest> e) {
            try {
                System.Security.Cryptography.RSAParameters key;

                using (var rsa = RSA.Create()) {
                    rsa.KeySize = 1024;
                    key = rsa.ExportParameters(true);
                    // To test recovery works...
                    // var recover = RSAParametersFromKey(key.Modulus, key.D);
                    // rsa.ImportParameters(recover);
                }
                e.Item.Output = new RSAParameters() {
                    D = key.D,
                    Exponent = Constants.StandardExponent65537,
                    Modulus = key.Modulus,
                };
                e.Completed(CMResult.S_OK);
            } catch {
                e.Completed(CMResult.E_Crypto_RSA_Key_Gen_Failure);
            }
        }

        public void BeginRSASign(AsyncRequest<RSASignRequest> e) {
            try {
                //
                //  PQ recovery is really expensive and unnecessary for RSA signing.
                //  I don't know why Microsoft decided to demand it. Only the
                //  Private key (D) and public key (Modulus) are actually used.
                //
                //  using (var rsa = new RSACryptoServiceProvider()) {
                //      var key = RSAParametersFromKey(e.Request.PublicKey, e.Request.PrivateKey);
                //      rsa.ImportParameters(key);
                //      e.Request.OutputSignature = rsa.SignData(e.Request.Input, CryptoConfig.MapNameToOID("SHA256"));
                //      e.Completed(CMResult.S_OK);
                //  }
                //

                var hashed = SHA256Hash(e.Item.Input);
                var packed = CM.Cryptography.RSA.EMSA_PKCS1_v1_5Encode_256(hashed, e.Item.PrivateKey.Length);
                
                BigInteger n = FromBigEndian(e.Item.PublicKey);
                BigInteger d = FromBigEndian(e.Item.PrivateKey);
                BigInteger exp = FromBigEndian(Constants.StandardExponent65537);
                BigInteger c = FromBigEndian(packed);
                BigInteger sig = BigInteger.ModPow(c, d, n);
                int keySize = e.Item.PublicKey.Length;
                var fast = sig.ToByteArray();
                EnsureLength(ref fast, keySize);
                Array.Reverse(fast);
                e.Item.OutputSignature = fast;
                // To test signing correctness...
                // byte[] reference;
                // using (var rsa = RSA.Create()) {
                //     var key = RSAParametersFromKey(e.Item.PublicKey, e.Item.PrivateKey);
                //     rsa.ImportParameters(key);
                //     reference = rsa.SignData(e.Item.Input,
                //     System.Security.Cryptography.HashAlgorithmName.SHA256,
                //     System.Security.Cryptography.RSASignaturePadding.Pkcs1);
                // }
                // if (!Helpers.IsHashEqual(fast, reference))
                //    throw new Exception();
                e.Completed(CMResult.S_OK);
            } catch {
                e.Completed(CMResult.E_Crypto_RSA_Signing_General_Failure);
            }
        }

        public void BeginRSAVerify(AsyncRequest<RSAVerifyRequest> e) {
            var res = CMResult.E_Crypto_RSA_Verify_General_Failure;

#if !DESKTOPCLR
            using (var rsa = RSA.Create()) {
                rsa.ImportParameters(new System.Security.Cryptography.RSAParameters() {
                    Modulus = e.Item.PublicKey,
                    Exponent = Constants.StandardExponent65537
                });
                res = rsa.VerifyData(e.Item.Input, e.Item.InputSignature,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                 System.Security.Cryptography.RSASignaturePadding.Pkcs1)
                ? CMResult.S_OK : CMResult.S_False;
            }
#else
            using (var rsa = new RSACryptoServiceProvider()) {
                rsa.ImportParameters(new System.Security.Cryptography.RSAParameters()
                {
                    Modulus = e.Item.PublicKey,
                    Exponent = Constants.StandardExponent65537
                });
                using (var sha = SHA256.Create())
                    res = rsa.VerifyData(e.Item.Input, sha, e.Item.InputSignature)
                    ? CMResult.S_OK : CMResult.S_False;
            }
#endif

            e.Completed(res);
        }

        public byte[] SHA256Hash(byte[] b) {
            lock (_SHA256)
                return _SHA256.ComputeHash(b);
        }

        public byte[] MD5Hash(byte[] b) {
            lock (_MD5)
                return _MD5.ComputeHash(b);
        }

        public static BigInteger FromBigEndian(byte[] p) {
            var b = new byte[p.Length];
            Array.Copy(p, b, b.Length);
            Array.Reverse(b);
            if (b[b.Length - 1] > 127
                // key starts with zero
                || b[b.Length - 1] == 0) {
                Array.Resize(ref b, b.Length + 1);
                b[b.Length - 1] = 0;
            }
            return new BigInteger(b);
        }

        private static BigInteger ModInverse(BigInteger e, BigInteger n) {
            return BigInteger.ModPow(e, n - 2, n);
        }

        /// <summary>
        /// PQ recovery example by alex137 on http://stackoverflow.com/a/32436331
        /// </summary>
        private static void RecoverPQ(BigInteger n, BigInteger e, BigInteger d, out BigInteger p, out BigInteger q) {
            int nBitCount = (int)(BigInteger.Log(n, 2) + 1);

            // Step 1: Let k = de – 1. If k is odd, then go to Step 4
            BigInteger k = d * e - 1;
            if (k.IsEven) {
                // Step 2 (express k as (2^t)r, where r is the largest odd integer
                // dividing k and t >= 1)
                BigInteger r = k;
                BigInteger t = 0;

                do {
                    r = r / 2;
                    t = t + 1;
                } while (r.IsEven);

                // Step 3

                bool success = false;
                BigInteger y = 0;

                using (var rng = RandomNumberGenerator.Create())
                    for (int i = 1; i <= 100; i++) {
                        // 3a
                        BigInteger g;
                        do {
                            byte[] randomBytes = new byte[nBitCount / 8 + 1]; // +1 to force a positive number
                            rng.GetBytes(randomBytes);
                            randomBytes[randomBytes.Length - 1] = 0;
                            g = new BigInteger(randomBytes);
                        } while (g >= n);

                        // 3b
                        y = BigInteger.ModPow(g, r, n);

                        // 3c
                        if (y == 1 || y == n - 1) {
                            // 3g
                            continue;
                        }

                        // 3d
                        BigInteger x;
                        for (BigInteger j = 1; j < t; j = j + 1) {
                            // 3d1
                            x = BigInteger.ModPow(y, 2, n);

                            // 3d2
                            if (x == 1) {
                                success = true;
                                break;
                            }

                            // 3d3
                            if (x == n - 1) {
                                // 3g
                                continue;
                            }

                            // 3d4
                            y = x;
                        }

                        // 3e
                        x = BigInteger.ModPow(y, 2, n);
                        if (x == 1) {
                            success = true;
                            break;
                        }

                        // 3g
                        // (loop again)
                    }

                if (success) {
                    // Step 5
                    p = BigInteger.GreatestCommonDivisor((y - 1), n);
                    q = n / p;
                    if (p > q) {
                        var tmp = q;
                        q = p;
                        p = tmp;
                    }
                    return;
                }
            }
            throw new Exception("Cannot compute P and Q");
        }

        public static void RNG(byte[] b) {
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(b);
        }

        /// <summary>
        /// Recovers P,Q etc from a public and private key.
        /// </summary>
        /// <param name="publicKey"></param>
        /// <param name="privateKey"></param>
        /// <returns></returns>
        public static System.Security.Cryptography.RSAParameters RSAParametersFromKey(byte[] publicKey, byte[] privateKey) {
            BigInteger n = FromBigEndian(publicKey);
            BigInteger d = FromBigEndian(privateKey);
            BigInteger e = FromBigEndian(Constants.StandardExponent65537);
            BigInteger p, q;
            RecoverPQ(n, e, d, out p, out q);
            int keySize = publicKey.Length;
            int pqSize = keySize / 2;
            var dp = BigInteger.Remainder(d, p - 1);
            var dq = BigInteger.Remainder(d, q - 1);
            var inverseQ = ModInverse(q, p);
            var rsa = new System.Security.Cryptography.RSAParameters() {
                D = d.ToByteArray().Take(keySize).ToArray(),
                Modulus = n.ToByteArray().Take(keySize).ToArray(),
                Exponent = e.ToByteArray(),
                P = p.ToByteArray().Take(pqSize).ToArray(),
                Q = q.ToByteArray().Take(pqSize).ToArray(),
                DP = dp.ToByteArray().Take(pqSize).ToArray(),
                DQ = dq.ToByteArray().Take(pqSize).ToArray(),
                InverseQ = inverseQ.ToByteArray().Take(pqSize).ToArray()
            };

            // Any leading zeros will have been dropped by BigInteger
            // but we need to keep them.

            EnsureLength(ref rsa.D, keySize);
            EnsureLength(ref rsa.Modulus, keySize);
            EnsureLength(ref rsa.P, pqSize);
            EnsureLength(ref rsa.Q, pqSize);
            EnsureLength(ref rsa.DP, pqSize);
            EnsureLength(ref rsa.DQ, pqSize);
            EnsureLength(ref rsa.InverseQ, pqSize);

            Array.Reverse(rsa.D);
            Array.Reverse(rsa.Modulus);
            Array.Reverse(rsa.Exponent);
            Array.Reverse(rsa.P);
            Array.Reverse(rsa.Q);
            Array.Reverse(rsa.DP);
            Array.Reverse(rsa.DQ);
            Array.Reverse(rsa.InverseQ);
            return rsa;
        }

        private static void EnsureLength(ref byte[] b, int len) {
            if (b.Length != len)
                Array.Resize(ref b, len);
        }

        public void Dispose() {
            _SHA256?.Dispose();
            _MD5?.Dispose();
        }
    }
}