#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace CM.Cryptography {
    public enum ASN1Tag {
        Reserved = 0,
        BOOLEAN = 1,
        INTEGER = 2,
        BIT_STRING = 3,
        OCTET_STRING = 4,
        NULL = 5,
        OBJECT_IDENTIFIER = 6,
        ObjectDescriptor = 7,
        EXTERNAL = 8,
        REAL = 9,
        ENUMERATED = 10,
        EMBEDDED_PDV = 11,
        UTF8String = 12,
        RELATIVE_OID = 13,
        SEQUENCE = 16,
        SET = 17,
        NumericString = 18,
        PrintableString = 19,
        TeletexString = 20, //T61String
        VideotexString = 21,
        IA5String = 22,
        UTCTime = 23,
        GeneralizedTime = 24,
        GraphicString = 25,
        VisibleString = 26, //ISO646String
        GeneralString = 27,
        UniversalString = 28,
        CHARACTER_STRING = 29,
        BMPString = 30,

        CONSTRUCTED_SEQUENCE = 48,
        CONSTRUCTED_SET = 49,
    }

    /// <summary>
    /// A bare-bones ASN reader/writer implementation.
    /// </summary>
    public class ASN {
        public List<ASN> Items = new List<ASN>();
        public ASN1Tag Tag;
        public byte[] Value;
        private string Name;
        public ASN() {
        }

        public ASN(byte[] asn1Bytes) {
            Tag = (ASN1Tag)asn1Bytes[0];
            int length = asn1Bytes[1];
            if (length == 0x80) {
                throw new FormatException("Bad value length");
            }
            int intSize = 0;
            if (length > 0x80) {
                intSize = length - 0x80;
                length = 0;
                for (int i = 0; i < intSize; i++) {
                    length = length * 256 + asn1Bytes[i + 2];
                }
            }
            Value = new byte[length];
            BlockCopy(asn1Bytes, (2 + intSize), Value, 0, length);
            if (((int)Tag & 0x20) == 0x20) {
                int offset = (2 + intSize);
                DecodeRecursive(asn1Bytes, ref offset, asn1Bytes.Length);
            }
        }

        public string StringValue {
            get {
                if (Value == null)
                    return null;
                switch (Tag) {
                    case ASN1Tag.VisibleString:
                    case ASN1Tag.PrintableString:
                    case ASN1Tag.UTF8String:
                        return Encoding.UTF8.GetString(Value, 0, Value.Length);

                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// Returns  
        /// ----BEGIN WHATEVER---- 
        /// .. base64 ..
        /// .. base64 ..
        /// ----END WHATEVER----
        /// </summary>
        public static string BytesToBase64Blob(string name, byte[] data) {
            var s = new StringBuilder();
            s.Append($"-----BEGIN {name?.ToUpper()}-----\r\n");
            var b = System.Convert.ToBase64String(data);
            int i = 0;
            while (i < b.Length) {
                var chars = Math.Min(b.Length - i, 64);
                s.Append(b.Substring(i, chars));
                s.Append("\r\n");
                i += chars;
            }
            s.Append($"-----END {name?.ToUpper()}-----\r\n");
            return s.ToString();
        }

        /// <summary>
        /// Strips ----WHATEVER---- and parses multi line base64.
        /// </summary>
        public static byte[] Base64BlobStringToBytes(string str) {
            var sanitised = System.Text.RegularExpressions.Regex.Replace(str,
                      @"(\s*---.+?$|[\r|\n|\s])", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            return Convert.FromBase64String(sanitised);
        }

        /// <summary>
        /// In constant time
        /// </summary>
        public static bool CompareArray(byte[] a, byte[] b) {
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < a.Length && i < b.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static ASN FromRSAParameters(RSAParameters rsa) {
            var a = new ASN();
            a.Tag = ASN1Tag.CONSTRUCTED_SEQUENCE;
            a.Add("Zero", ASN1Tag.INTEGER, new byte[] { 0 });
            a.Add("Modulus", ASN1Tag.INTEGER, PadZero(rsa.Modulus));
            a.Add("Exponent", ASN1Tag.INTEGER, PadZero(rsa.Exponent));
            a.Add("D", ASN1Tag.INTEGER, PadZero(rsa.D));
            a.Add("P", ASN1Tag.INTEGER, PadZero(rsa.P));
            a.Add("Q", ASN1Tag.INTEGER, PadZero(rsa.Q));
            a.Add("DP", ASN1Tag.INTEGER, PadZero(rsa.DP));
            a.Add("DQ", ASN1Tag.INTEGER, PadZero(rsa.DQ));
            a.Add("InverseQ", ASN1Tag.INTEGER, PadZero(rsa.InverseQ));
            return a;
        }

        static byte[] PadZero(byte[] b) {
            if (b[0] > 127) {
                var tmp = new byte[b.Length + 1];
                BlockCopy(b, 0, tmp, 1, b.Length);
                b = tmp;
            }
            return b;
        }

        public static ASN ToPKCS1(ASN rsaParams) {

            var a = new ASN();
            a.Tag = ASN1Tag.CONSTRUCTED_SEQUENCE;
            a.Add("Zero", ASN1Tag.INTEGER, new byte[] { 0 });

            var id = new ASN();
            id.Tag = ASN1Tag.CONSTRUCTED_SEQUENCE;
            id.OID(OID_RSA_ENCRYPTION);
            id.Null();
            a.Items.Add(id);

            var rsa = new ASN();
            rsa.Tag = ASN1Tag.OCTET_STRING;
            rsa.Value = rsaParams.GetBytes(true);
            a.Items.Add(rsa);
            return a;
        }

        public static ASN FromString(string str) {
            return new ASN(Base64BlobStringToBytes(str));
        }

        public static void TLVDecode(byte[] data, ref int pos, out ASN1Tag tag, out int length, out byte[] value) {
            tag = (ASN1Tag)data[pos++];
            length = data[pos++];
            if ((length & 0x80) == 0x80) {
                int intSize = length & 0x7F;
                length = 0;
                for (int i = 0; i < intSize; i++)
                    length = length * 256 + data[pos++];
            }
            value = new byte[length];
            BlockCopy(data, pos, value, 0, length);
        }

        public static byte[] TLVEncode(ASN1Tag tag, byte[] val) {
            // adapted from mono's ASN implementation.
            if (val == null || val.Length == 0)
                return new byte[] { (byte)tag, 0 };
            byte[] ar;
            int lengthPrefix = 0;
            int len = val.Length;
            if (len > 127) {
                if (len <= Byte.MaxValue) {
                    ar = new byte[3 + len];
                    BlockCopy(val, 0, ar, 3, len);
                    lengthPrefix = 0x81;
                    ar[2] = (byte)(len);
                } else if (len <= UInt16.MaxValue) {
                    ar = new byte[4 + len];
                    BlockCopy(val, 0, ar, 4, len);
                    lengthPrefix = 0x82;
                    ar[2] = (byte)(len >> 8);
                    ar[3] = (byte)(len);
                } else if (len <= 0xFFFFFF) {
                    ar = new byte[5 + len];
                    BlockCopy(val, 0, ar, 5, len);
                    lengthPrefix = 0x83;
                    ar[2] = (byte)(len >> 16);
                    ar[3] = (byte)(len >> 8);
                    ar[4] = (byte)(len);
                } else {
                    ar = new byte[6 + len];
                    BlockCopy(val, 0, ar, 6, len);
                    lengthPrefix = 0x84;
                    ar[2] = (byte)(len >> 24);
                    ar[3] = (byte)(len >> 16);
                    ar[4] = (byte)(len >> 8);
                    ar[5] = (byte)(len);
                }
            } else {
                ar = new byte[2 + len];
                BlockCopy(val, 0, ar, 2, len);
                lengthPrefix = len;
            }
            ar[0] = (byte)tag;
            ar[1] = (byte)lengthPrefix;
            return ar;
        }

        public ASN Add(string name, ASN1Tag tag, byte[] value = null) {
            var a = new ASN() { Name = name, Tag = tag, Value = value };
            Items.Add(a);

            return a;
        }

        public ASN ConstructedSequence(string name) {
            return Add(name, ASN1Tag.CONSTRUCTED_SEQUENCE);
        }

        public ASN ConstructedSet(string name) {
            return Add(name, ASN1Tag.CONSTRUCTED_SET);
        }

        public ASN DnsNames(string name, IEnumerable<string> domainNames) {
            var ar = new List<byte>();
            foreach (var domain in domainNames) {
                var ascii = Encoding.UTF8.GetBytes(domain);
                ar.AddRange(TLVEncode((ASN1Tag)130, ascii));
            }
            return Add(name, ASN1Tag.OCTET_STRING, TLVEncode(ASN1Tag.CONSTRUCTED_SEQUENCE, ar.ToArray()));
        }

        public ASN FindDirect(ASN1Tag t) {
            for (int i = 0; i < Items.Count; i++)
                if (Items[i].Tag == t)
                    return Items[i];
            return null;
        }

        public ASN FindNode(byte[] oid) {
            if (Items.Count > 0
                && Items[0].Tag == ASN1Tag.OBJECT_IDENTIFIER
                && CompareArray(Items[0].Value, oid)) {
                return this;
            }
            for (int i = 0; i < Items.Count; i++) {
                var s = Items[i].FindNode(oid);
                if (s != null)
                    return s;
            }
            return null;
        }

        public string FindString(byte[] oid) {
            var node = FindNode(oid);
            if (node != null
                && node.Items.Count > 1
                && node.Items[1].StringValue != null)
                return node.Items[1].StringValue;
            return null;
        }

        public byte[] GetBytes(bool tlv) {
            var ar = new List<byte>();
            if (Items != null && Items.Count != 0) {
                foreach (var item in Items) {
                    ar.AddRange(item.GetBytes(true));
                }
            } else if (Value != null) {
                ar.AddRange(Value);
            }
            return tlv ? TLVEncode(Tag, ar.ToArray()) : ar.ToArray();
        }

        public byte[] GetValueBytesTrimmed() {
            var ar = GetBytes(false);
            return ar.SkipWhile(x => x == 0).ToArray();
        }

        public ASN Integer(string name, byte value) {
            return Add(name, ASN1Tag.INTEGER, new byte[] { value });
        }

        public ASN Null() {
            return Add(null, ASN1Tag.NULL);
        }

        public ASN OID(byte[] oid) {
            return Add("OID", ASN1Tag.OBJECT_IDENTIFIER, oid);
        }

        public ASN StringPrintable(string name, string s) {
            return Add(name, ASN1Tag.PrintableString, Encoding.UTF8.GetBytes(s));
        }
        public ASN StringUTF8(string name, string s) {
            return Add(name, ASN1Tag.UTF8String, Encoding.UTF8.GetBytes(s));
        }


        private static readonly byte[] OID_RSA_ENCRYPTION = new byte[] { 42, 134, 72, 134, 247, 13, 1, 1, 1 };

        public RSAParameters ToRSAParameters() {
            var asn = this;
            if (asn.Items.Count < 9
                || asn.Items[0].Value?.Length != 1
                || asn.Items[0].Value[0] != 0) {
                // check for pkcs1
                var pkcs = FindNode(OID_RSA_ENCRYPTION);
                if (pkcs != null
                    && asn.Items.Count > 2
                    && asn.Items[2].Tag == ASN1Tag.OCTET_STRING) {
                    asn = new ASN(asn.Items[2].Value);
                }
            }

            if (asn.Items.Count < 9
               || asn.Items[0].Value?.Length != 1
               || asn.Items[0].Value[0] != 0) {
                throw new FormatException("The data is not a recognised ASN.1 format (pkcs#1 or pkcs#8.)");
            }

            var rsa = new RSAParameters() {
                Modulus = asn.Items[1].GetValueBytesTrimmed(),
                Exponent = asn.Items[2].GetValueBytesTrimmed(),
                D = asn.Items[3].GetValueBytesTrimmed(),
                P = asn.Items[4].GetValueBytesTrimmed(),
                Q = asn.Items[5].GetValueBytesTrimmed(),
                DP = asn.Items[6].GetValueBytesTrimmed(),
                DQ = asn.Items[7].GetValueBytesTrimmed(),
                InverseQ = asn.Items[8].GetValueBytesTrimmed()
            };
            int keySize = rsa.Modulus.Length;
            int pqSize = keySize / 2;
            EnsureLength(ref rsa.D, keySize);
            EnsureLength(ref rsa.Modulus, keySize);
            EnsureLength(ref rsa.P, pqSize);
            EnsureLength(ref rsa.Q, pqSize);
            EnsureLength(ref rsa.DP, pqSize);
            EnsureLength(ref rsa.DQ, pqSize);
            EnsureLength(ref rsa.InverseQ, pqSize);

            return rsa;
        }

        public override string ToString() {
            return Name ?? Tag.ToString();
        }
        private static void EnsureLength(ref byte[] b, int len) {
            if (b.Length != len)
                Array.Resize(ref b, len);
        }

        private void DecodeRecursive(byte[] b, ref int cur, int count) {
            while (cur < count - 1) {
                ASN1Tag tag;
                int length;
                byte[] value;
                TLVDecode(b, ref cur, out tag, out length, out value);
                if (tag == 0)
                    continue;

                var item = new ASN() { Tag = tag, Value = value };
                Items.Add(item);
                if (((int)tag & 0x20) == 0x20) {
                    int tmp = cur;
                    item.DecodeRecursive(b, ref tmp, tmp + length);
                }

                cur += length;
            }
        }

        private static void BlockCopy(Array src, int srcOffset, Array dst, int dstOffset, int count) {
#if JAVASCRIPT
            Array.Copy(src, srcOffset, dst, dstOffset, count);
#else
            Buffer.BlockCopy(src, srcOffset, dst, dstOffset, count);
#endif
        }
    }
}
