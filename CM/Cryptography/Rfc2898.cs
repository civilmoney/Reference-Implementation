#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;

namespace CM.Cryptography {

    /// <summary>
    /// Delegate for hash operations.
    /// </summary>
    public delegate byte[] HashFunction(byte[] data);

    /// <summary>
    /// A minimal hash-based message authentication code (HMAC) implementation
    /// </summary>
#if JAVASCRIPT
    [Bridge.External] // We compile once and move into webworkers.js
#endif
    internal class HMAC {
        private byte[] _Key;
        private byte[] _InnerPadding;
        private byte[] _OuterPadding;
        private HashFunction _Hash;

        public HMAC(byte[] key, HashFunction hash, int blockSize) {
            _Hash = hash;
            _Key = key;
            if (key.Length < blockSize) {
                // keys shorter than block size are zero-padded
                _Key = (byte[])key.Clone();
            } else {
                // keys longer than block size are shortened
                _Key = _Hash(key);
            }
            _InnerPadding = new byte[blockSize];
            _OuterPadding = new byte[blockSize];
            UpdateIOPadBuffers();
        }

        private void UpdateIOPadBuffers() {
#if JAVASCRIPT
            Bridge.Script.Write(@"
    var inner = this._InnerPadding;
    var outer = this._OuterPadding;
    var key = this._Key;
    for (var i = 0; i < inner.length; i++) {
        if (i < key.length) {
            inner[i] = ((54 ^ key[i]));
            outer[i] = ((92 ^ key[i]));
        }
        else  {
            inner[i] = 54;
            outer[i] = 92;
        }
    }
            ");
#else
            for (int i = 0; i < _InnerPadding.Length; i++) {
                if (i < _Key.Length) {
                    _InnerPadding[i] = (byte)(0x36 ^ _Key[i]);
                    _OuterPadding[i] = (byte)(0x5c ^ _Key[i]);
                } else {
                    _InnerPadding[i] = 0x36;
                    _OuterPadding[i] = 0x5c;
                }
            }
#endif
        }

        public byte[] ComputeHash(byte[] b) {
            var inner = new List<byte>();
            inner.AddRange(_InnerPadding);
            inner.AddRange(b);
            var outer = new List<byte>();
            outer.AddRange(_OuterPadding);
            outer.AddRange(_Hash(inner.ToArray()));
            return _Hash(outer.ToArray());
        }
    }

    /// <summary>
    /// An Rfc2898 (PBKDF2) implementation for derived key generation.
    /// </summary>
#if JAVASCRIPT
    [Bridge.External] // We compile once and move into webworkers.js
#endif
    public class Rfc2898 {

        public static Rfc2898 CreateHMACSHA1(byte[] pass, byte[] salt, int iterations) {
            var c = new SHA1.SHA1_CTX();

            HMAC hmac = new HMAC(pass, (b) => {
                return SHA1.ComputeHash(c, b);
            }, 64);

            return new Rfc2898(hmac.ComputeHash, salt, iterations);
        }

        private byte[] _Buffer;
        private byte[] _Salt;
        private int _Interations;
        private int _Block;
        private int _Cursor;
        private HashFunction _Hash;

        public Rfc2898(HashFunction hash, byte[] salt, int iterations) {
            _Hash = hash;
            _Salt = salt;
            _Interations = iterations;
            _Block = 1;
            _Buffer = null;
        }

        private byte[] GetNextBlock() {
            byte[] res = null;
#if JAVASCRIPT
           Bridge.Script.Write(@"

            var ar = [];
            ar = ar.concat(this._Salt);
            ar = ar.concat([(this._Block >> 24)&0xFF, (this._Block >> 16)&0xFF, (this._Block >> 8)&0xFF, (this._Block)&0xFF]);
            var hashValue = this._Hash(ar);
            res = hashValue;
            for (var i = 2; i <= this._Interations; i++) {
                hashValue = this._Hash(hashValue);
                for (var j = 0; j < res.length; j++)
                    res[j] = (res[j] ^ hashValue[j]);
            }
            this._Block++;
");
#else
            var ar = new byte[_Salt.Length + 4];
            Buffer.BlockCopy(_Salt, 0, ar, 0, _Salt.Length);
            ar[ar.Length - 4] = (byte)(_Block >> 24);
            ar[ar.Length - 3] = (byte)(_Block >> 16);
            ar[ar.Length - 2] = (byte)(_Block >> 8);
            ar[ar.Length - 1] = (byte)(_Block);
            byte[] hashValue = _Hash(ar);
            res = hashValue;
            for (int i = 2; i <= _Interations; i++) {
                hashValue = _Hash(hashValue);
                for (int j = 0; j < res.Length; j++)
                    res[j] = (byte)(res[j] ^ hashValue[j]);
            }
            _Block++;
#endif
            return res;
        }

        public byte[] GetBytes(int count) {
            if (count <= 0)
                throw new ArgumentOutOfRangeException();
            var ar = new byte[count];
            var idx = 0;
            while (idx < count) {
                if (_Buffer == null || _Cursor == _Buffer.Length) {
                    _Buffer = GetNextBlock();
                    _Cursor = 0;
                }
                var toCopy = Math.Min(count - idx, _Buffer.Length - _Cursor);
                Array.Copy(_Buffer, _Cursor, ar, idx, toCopy);
                idx += toCopy;
                _Cursor += toCopy;
            }
            return ar;
        }
    }
}