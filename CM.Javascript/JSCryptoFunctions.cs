#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM.Javascript {

    /// <summary>
    /// Crypto functions which are offloaded to webworkers.js.
    /// </summary>
    internal class JSCryptoFunctions : CM.ICryptoFunctions {

        public static readonly JSCryptoFunctions Identity = new JSCryptoFunctions();

        public void BeginAESDecrypt(AsyncRequest<AESCryptoRequest> e) {
            var r = e.Item;
            Bridge.Script.Write(@"
if(window.Worker) {
                var worker = new Worker('/webworkers.js');
                worker.onmessage = function (msg) {
                    var status = JSON.parse(msg.data);
                    if(status.error) {
                        e.completed(CM.CMResult.e_Crypto_Invalid_Password.$clone());
                        console.log('BeginRFC2898: '+status.error);
                    } else if (status.result) {
                        r.output = status.result;
                        e.completed(CM.CMResult.s_OK.$clone());
                    }
                };
                var args =  {'command': 'aes-decrypt', key: r.key, iv: r.iV, input: r.input };
                worker.postMessage(JSON.stringify(args));
} else {");
            r.Output = CM.Cryptography.AES.Decrypt(r.Input, r.Key, r.IV);
            Bridge.Script.Write(@"}");
        }

        public void BeginAESEncrypt(AsyncRequest<AESCryptoRequest> e) {
            var r = e.Item;
            Bridge.Script.Write(@"
if(window.Worker) {
                var worker = new Worker('/webworkers.js');
                worker.onmessage = function (msg) {
                    var status = JSON.parse(msg.data);
                    if(status.error) {
                        e.completed(CM.CMResult.e_Crypto_Invalid_Password.$clone());
                        console.log('BeginRFC2898: '+status.error);
                    } else if (status.result) {
                        r.output = status.result;
                        e.completed(CM.CMResult.s_OK.$clone());
                    }
                };
                var args =  {'command': 'aes-encrypt', key: r.key, iv: r.iV, input: r.input };
                worker.postMessage(JSON.stringify(args));
} else {
");
            r.Output = CM.Cryptography.AES.Encrypt(r.Input, r.Key, r.IV);
            e.Completed(CMResult.S_OK);
            Bridge.Script.Write(@"}");
        }

        public void BeginRFC2898(AsyncRequest<RFC2898CryptoRequest> e) {
            var r = e.Item;
            Bridge.Script.Write(@"
if(window.Worker) {
                var worker = new Worker('/webworkers.js');
                worker.onmessage = function (msg) {
                    var status = JSON.parse(msg.data);
                    if(status.error) {
                        e.completed(CM.CMResult.e_Crypto_Rfc2898_General_Failure.$clone());
                        console.log('BeginRFC2898: '+status.error);
                    } else if (status.result) {
                        r.outputIV = status.result.iv;
                        r.outputKey = status.result.key;
                        e.completed(CM.CMResult.s_OK.$clone());
                    }
                };
                var args =  {'command': 'rfc2898', password: r.password, salt: r.salt, iterations: r.iterations };
                worker.postMessage(JSON.stringify(args));
} else {
");
            var rfc = CM.Cryptography.Rfc2898.CreateHMACSHA1(r.Password, r.Salt, r.Iterations);
            r.OutputKey = rfc.GetBytes(32);
            r.OutputIV = rfc.GetBytes(16);
            e.Completed(CMResult.S_OK);
            Bridge.Script.Write(@"}");
        }

        public void BeginRSAKeyGen(AsyncRequest<RSAKeyRequest> e) {
            var r = e.Item;
            Bridge.Script.Write(@"
if (window.Worker) {
                var worker = new Worker('/webworkers.js');
                worker.onmessage = function (msg) {
                    var status = JSON.parse(msg.data);
                    if(status.error) {
                        e.completed(CM.CMResult.e_Crypto_RSA_Key_Gen_Failure.$clone());
                    } else if (status.result) {
                        r.output = status.result;
                        e.completed(CM.CMResult.s_OK.$clone());
                    }
                };
                // No window.crypto in web workers, have to do it here..
                var p = new Uint8Array(64);
                var q = new Uint8Array(64);
                (window.crypto || window.msCrypto).getRandomValues(p);
                (window.crypto || window.msCrypto).getRandomValues(q);
                var args =  {'command': 'generate-rsa', p: Array.prototype.slice.call(p), q: Array.prototype.slice.call(q) };
                var str = JSON.stringify(args);
                worker.postMessage(str);
} else {
                // If there's no webworker, there's probably no
                // Uint8Array/window.crypto either.
");
            byte[] tmp = new byte[64 * 2];
            CM.Cryptography.RNG.RandomBytes(tmp);
            Bridge.Script.Write(@"
                var p = [];
                var q = [];
                for(var i=0;i<64;i++){
                    p.push(tmp[i]);
                    q.push(tmp[i+64]);
                }
                var crunch = new Crunch();
                p = crunch.nextPrime(Array.prototype.slice.call(p));
                q = crunch.nextPrime(Array.prototype.slice.call(q));
                var exp = [1, 0, 1];
                var n = crunch.mul(p, q);
                var f = crunch.mul(crunch.decrement(p), crunch.decrement(q));
                var d = crunch.cut(crunch.inv(exp, f));
                r.output = { d:d, modulus: n, exponent:exp };
                e.completed(CM.CMResult.s_OK.$clone());
}
            ");
        }

        public void BeginRSASign(AsyncRequest<RSASignRequest> e) {
            var hashed = CM.Cryptography.SHA256.ComputeHash(e.Item.Input);
            var priv = e.Item.PrivateKey;
            var pub = e.Item.PublicKey;
            var c = CM.Cryptography.RSA.EMSA_PKCS1_v1_5Encode_256(hashed, priv.Length);
            byte[] sig = null;
            Bridge.Script.Write(@"
            var crunch = new Crunch();
            sig = crunch.exp(c, priv, pub);
            while (sig.length < priv.length)
                sig.splice(0, 0, 0);
            ");
            e.Item.OutputSignature = sig;
            e.Completed(CMResult.S_OK);
        }

        public void BeginRSAVerify(AsyncRequest<RSAVerifyRequest> e) {
            var hashed = CM.Cryptography.SHA256.ComputeHash(e.Item.Input);
            var signature = e.Item.InputSignature;
            var exponent = Constants.StandardExponent65537;
            var publicKey = e.Item.PublicKey;
            byte[] dec = null;
            Bridge.Script.Write(@"
            var crunch = new Crunch();
            dec = crunch.exp(signature, exponent, publicKey);
            while (dec.length < publicKey.length)
                dec.splice(0, 0, 0);
            ");
            var expected = CM.Cryptography.RSA.EMSA_PKCS1_v1_5Encode_256(hashed, publicKey.Length);
            var res = Helpers.IsHashEqual(expected, dec) ? CMResult.S_OK : CMResult.S_False;
            e.Completed(res);
        }

        public byte[] MD5Hash(byte[] b) {
            return CM.Cryptography.MD5.ComputeHash(b);
        }

        public byte[] SHA256Hash(byte[] b) {
            return CM.Cryptography.SHA256.ComputeHash(b);
        }
    }
}