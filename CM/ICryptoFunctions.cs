#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM {

    /// <summary>
    /// Represents all cryptography functions that are needed by CM. Native crypto library functions
    /// can be swapped in using this interface instead of using the slower portable reference
    /// implementations. Many of these operation can be very expensive and slow ARM hardware, so some
    /// ICryptoFunctions take an asynchronous worker pattern, even though some platforms will be able
    /// to run very fast and synchronously.
    /// </summary>
    public interface ICryptoFunctions {
        // Reasonably quick operations

        byte[] MD5Hash(byte[] b);

        byte[] SHA256Hash(byte[] b);

        // Potentially time-consuming operations

        void BeginRFC2898(AsyncRequest<RFC2898CryptoRequest> e);

        void BeginAESEncrypt(AsyncRequest<AESCryptoRequest> e);

        void BeginAESDecrypt(AsyncRequest<AESCryptoRequest> e);

        void BeginRSASign(AsyncRequest<RSASignRequest> e);

        void BeginRSAVerify(AsyncRequest<RSAVerifyRequest> e);

        void BeginRSAKeyGen(AsyncRequest<RSAKeyRequest> e);
    }

    public class RFC2898CryptoRequest {
        public byte[] Password;
        public byte[] Salt;
        public int Iterations;
        public int KeySizeBytes;
        public int IVSizeBytes;
        public byte[] OutputKey;
        public byte[] OutputIV;
    }

    public class AESCryptoRequest {
        public byte[] Input;
        public byte[] Key;
        public byte[] IV;
        public byte[] Output;
    }

    public class RSASignRequest {
        public byte[] Input;
        public byte[] PrivateKey;
        public byte[] PublicKey;
        public byte[] OutputSignature;
        public object Tag;
    }

    public class RSAVerifyRequest {
        public byte[] Input;
        public byte[] InputSignature;
        public byte[] Exponent;
        public byte[] PublicKey;
        // Just set CryptoRequest.Success accordingly.
    }

    public class RSAKeyRequest {
        public RSAParameters Output;
    }

    public class RSAParameters {

        /// <summary>
        /// Represents the D parameter for the RSA algorithm.
        /// </summary>
        public byte[] D;

        /// <summary>
        /// Represents the Exponent parameter for the RSA algorithm.
        /// </summary>
        public byte[] Exponent;

        /// <summary>
        /// Represents the Modulus parameter for the RSA algorithm.
        /// </summary>
        public byte[] Modulus;

    }
}