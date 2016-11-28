#region License
// 
// All code is Copyleft unless otherwise denoted.
// Created by: Simon Bond <simon@sichbo.ca>
//
#endregion

#region Other Credits
//
// BigInt was very loosely derived from BigInteger Class Version 1.03 
// by Chew Keong TAN - Copyright (c) 2002. GPL license.
//
#endregion

#define NO64
#if !JAVASCIPT
//#define validate
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using INT = System.UInt32;
#if validate
using ULONG = System.UInt64;
using LONG = System.Int64;
#endif

namespace CM.Cryptography {

    public class BigInt : IComparable<BigInt> {

        const int BITS = sizeof(INT) * 8;
        const INT SIGN_MASK = 0x80000000;
        const INT INT_MASK = INT.MaxValue;
        const int MaxLengthInBytes = 1024;
        const int MaxLength = MaxLengthInBytes / sizeof(INT);
        const int BYTE_POS = 5; //3
        const int BIT_POS = 0x1F; //7

        INT[] _Data = new INT[MaxLength];
        int _Length = 0;

        void UpdateLength() {
            int len = MaxLength;
            while (len > 1 && _Data[len - 1] == 0)
                len--;
            _Length = len;
        }


        public BigInt(byte[] v) {
            Replace(v);
        }
#if NO64
        public BigInt(int value) {
            Replace(value);
        }
        void Replace(int value) {
            _Length = 0;
            _Data[_Data.Length - 1] = 0;
            if (value >= 0 && (INT)value <= INT.MaxValue) {
                _Data[0] = (INT)value;
                _Length = 1;
                return;
            }
            var v = value;
            _Data[0] = (INT)v;
            _Length++;
            if (v < 0) {
                v = -1;
                while (_Length < _Data.Length)
                    _Data[_Length++] = INT_MASK;
            }
            if (value > 0
                && (v != 0 || IsNegative))
                throw new Exception("Positive overflow");
            if (value < 0
                && (v != -1 || !IsNegative))
                throw new Exception("Negative underflow");
        }
        void Replace(uint hi, uint lo) {
            _Length = 0;
            _Data[_Data.Length - 1] = 0;
            _Data[0] = lo;
            _Data[1] = hi;
            _Length = hi == 0 ? 1 : 2;
            if (_Length == 0) _Length = 1;
        }
#else
        public BigInt(int value) {
            Replace((LONG)value);
        }
        public BigInt(uint value) {
            Replace((ULONG)value);
        }
        public BigInt(ULONG value) {
            Replace(value);
        }
        void Replace(int value) {
            Replace((LONG)value);
        }
       void Replace(LONG value) {
            _Length = 0;
            _Data[_Data.Length - 1] = 0;
            if (value >= 0 && (INT)value <= INT.MaxValue) {
                _Data[0] = (INT)value;
                _Length = 1;
                return;
            }
            var v = value;
            while (v != 0 && _Length < _Data.Length) {
                _Data[_Length] = (INT)(v & INT_MASK);
                v >>= BITS;
                _Length++;
            }
            if (_Length == 0) _Length = 1;
            if (value > 0
                && (v != 0 || IsNegative))
                throw new Exception("Positive overflow");
            if (value < 0
                && (v != -1 || !IsNegative))
                throw new Exception("Negative underflow");
        }
        
        
#endif
#if validate
        void Replace(ULONG value) {
            _Length = 0;
            _Data[_Data.Length - 1] = 0;
            if (value >= 0 && value <= INT.MaxValue) {
                _Data[0] = (INT)value;
                _Length = 1;
                return;
            }
            var v = value;
            while (v != 0 && _Length < _Data.Length) {
                _Data[_Length] = (INT)(v & INT_MASK);
                v >>= BITS;
                _Length++;
            }
            if (_Length == 0) _Length = 1;
            if (v > 0 || IsNegative)
                throw new Exception("Positive overflow");
        }
#endif
        public void Replace(byte[] v) {
            if (v.Length > MaxLengthInBytes)
                throw new Exception("Too many bytes");
            int i = v.Length - 1;
            int x = 0;
            int len = 0;
            while (i >= 3) {
                _Data[x] = (uint)(v[i - 3] << 24 | v[i - 2] << 16 | v[i - 1] << 8 | v[i]);
                i -= 4;
                if (_Data[x] != 0)
                    len = x + 1;
                x++;
            }
            switch (i) {
                case 2: _Data[x] = (uint)(v[i - 2] << 16 | v[i - 1] << 8 | v[i]); break;
                case 1: _Data[x] = (uint)(v[i - 1] << 8 | v[i]); break;
                case 0: _Data[x] = (uint)(v[i]); break;
            }
            if (_Data[x] != 0)
                len = x + 1;
            _Length = len;
        }
        public void Replace(uint[] v) {
            int len = 0;
            for (int i = 0; i < v.Length; i++) {
                _Data[i] = v[v.Length - i - 1];
                if (_Data[i] != 0)
                    len = i;
            }
            len++;
            _Length = len;//UpdateLength();
        }

        void Replace(BigInt copy) {
            _Length = copy._Length;
            Array.Copy(copy._Data, _Data, copy._Data.Length);
        }
        public BigInt(BigInt copy) {
            Replace(copy);
        }

        public byte[] GetBytes() {
            // round to nearest int32?
            // var b = new byte[(int)Math.Ceiling(_Length / 4.0) * 4];
            // Array.Copy(_Data, b, b.Length);
            // Array.Reverse(b);

            int i = _Length - 1;
            var b = new byte[_Length * 4];
            int x = 0;
            while (i >= 0) {
                b[x++] = (byte)((_Data[i] >> 24) & 0xFF);
                b[x++] = (byte)((_Data[i] >> 16) & 0xFF);
                b[x++] = (byte)((_Data[i] >> 8) & 0xFF);
                b[x++] = (byte)((_Data[i] >> 0) & 0xFF);
                i--;
            }
            return b;
        }

        public int Length {
            get { return _Length* BITS/8; }
        }

        public int ActualBitCount {
            get {
                uint value = _Data[_Length - 1];
                uint mask = SIGN_MASK;
                int bits = BITS;
                while (bits > 0 && (value & mask) == 0) {
                    bits--;
                    mask >>= 1;
                }
                bits += ((_Length - 1) * BITS);

                return bits;
            }
        }

        public bool IsEven {
            get {
                return (_Data[0] & 0x1) == 0;
            }
        }
        public bool IsOne {
            get {
                return _Length == 1 && _Data[0] == 1;
            }
        }
        public bool IsNegative {
            get {
                return (_Data[_Data.Length - 1] & SIGN_MASK) != 0;
            }
        }

        public uint this[int idx] {
            get {
                return _Data[idx];
            }
            set {
                _Data[idx] = value;
                _Length = idx + 1;
            }
        }



        #region Syntactic Sugar
        public static implicit operator BigInt(int value) { return new BigInt(value); }
#if !NO64
        public static implicit operator BigInt(ULONG value) { return new BigInt(value); }
#endif
        public static bool operator <(BigInt a, BigInt b) { return a.CompareTo(b) < 0; }
        public static bool operator ==(BigInt a, BigInt b) { if (object.ReferenceEquals(a, b)) return true; return a.CompareTo(b) == 0; }
        public static bool operator ==(BigInt a, int b) { return a == new BigInt(b); }
        public static bool operator !=(BigInt a, BigInt b) { if (object.ReferenceEquals(a, b)) return false; return a.CompareTo(b) != 0; }
        public static bool operator !=(BigInt a, int b) { return a != new BigInt(b); }
        public static bool operator <(BigInt a, int b) { return a < new BigInt(b); }
        public static bool operator >(BigInt a, BigInt b) { return a.CompareTo(b) > 0; }
        public static bool operator >=(BigInt a, BigInt b) { return a.CompareTo(b) >= 0; }
        public static bool operator <=(BigInt a, BigInt b) { return a.CompareTo(b) <= 0; }
        public static bool operator >(BigInt a, int b) { return a > new BigInt(b); }
        public static BigInt operator *(BigInt a, BigInt b) { return Multiply(a, b); }
        public static BigInt operator /(BigInt a, BigInt b) { return Divide(a, b); }
        public static BigInt operator <<(BigInt a, int shiftVal) { return ShiftLeft(a, shiftVal); }
        public static BigInt operator >>(BigInt a, int shiftVal) { return ShiftRight(a, shiftVal); }
        public static BigInt operator /(BigInt a, int b) { return Divide(a, new BigInt(b)); }
        public static BigInt operator -(BigInt a) { return Negate(a); }
        public static BigInt operator -(BigInt a, BigInt b) { return Subtract(a, b); }
        public static BigInt operator -(BigInt a, int b) { return a - new BigInt(b); }
        public static BigInt operator +(BigInt a, BigInt b) { return Add(a, b); }
        public static BigInt operator +(BigInt a, int b) { return Add(a, new BigInt(b)); }
        public static BigInt operator %(BigInt a, BigInt b) { return Mod(a, b); }
        public static BigInt operator %(BigInt a, int b) { return Mod(a, new BigInt(b)); }
        public static BigInt operator &(BigInt a, BigInt b) { return And(a, b); }
        #endregion

        public string ToBase64String() {
            return Convert.ToBase64String(GetBytes());
        }
        public static BigInt FromBase64String(string s) {
            return new BigInt(Convert.FromBase64String(s));
        }

        public override string ToString() {
            return ToString(10);
        }

        string ToString(int radix) {
            if (radix < 2 || radix > 36)
                throw (new ArgumentException("Radix must be >= 2 and <= 36"));

            string charSet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string result = "";

            BigInt a = this;

            bool negative = IsNegative;
            if (negative)
                a = -a;

            BigInt quotient = new BigInt(0);
            BigInt remainder = new BigInt(0);
            BigInt biRadix = new BigInt(radix);

            if (a._Length == 1 && a._Data[0] == 0)
                result = "0";
            else {
                while (a._Length > 1 || (a._Length == 1 && a._Data[0] != 0)) {
                    Divide(a, biRadix, ref quotient, ref remainder);

                    if (remainder._Data[0] < 10)
                        result = remainder._Data[0] + result;
                    else
                        result = charSet[(int)remainder._Data[0] - 10] + result;

                    a = quotient;
                }
                if (negative)
                    result = "-" + result;
            }

            return result;
        }

        public int CompareTo(BigInt other) {

            if (other == null)
                return 1;

            int pos = _Data.Length - 1;

            // bi1 is negative, bi2 is positive
            if (IsNegative && !other.IsNegative)
                return -1;
            // bi1 is positive, bi2 is negative
            else if (!IsNegative && other.IsNegative)
                return 1;

            // same sign
            int len = (this._Length > other._Length) ? this._Length : other._Length;
            for (pos = len - 1; pos > 0 && this._Data[pos] == other._Data[pos]; pos--) ;

            return ((pos == 0 && this._Data[pos] == other._Data[pos])) ? 0
                : this._Data[pos] > other._Data[pos] ? 1
                : -1;
        }

        public override bool Equals(object o) {
            if (o == null)
                return false;
            BigInt other = (BigInt)o;
            if (this._Length != other._Length)
                return false;
            return this.CompareTo(other) == 0;
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        // The following are derived from Chew Keong TAN's BigInt2eger

        #region Basic math 

        static BigInt Add(BigInt a, BigInt b) {
            var res = new BigInt(0);
            Add(res, a, b);
            return res;
        }
        static void Add(BigInt res, BigInt a, BigInt b) {
            bool aNegative = a.IsNegative;
            var len = Math.Max(a._Length, b._Length);
#if NO64
            uint carry = 0, hi, lo;
            for (int i = 0; i < len; i++) {
                hi = 0;
                lo = a._Data[i];
                INT64Math.AddUint32(ref hi, ref lo, b._Data[i]);
                INT64Math.AddUint32(ref hi, ref lo, carry);

#if validate
                LONG v = (LONG)a._Data[i] + (LONG)b._Data[i] + carry;
                if (hi != (v >> BITS) || (uint)v != lo)
                    throw new Exception();
#endif
                carry = hi;
                res._Data[i] = lo;
            }
#else
            LONG carry = 0;
            for (int i = 0; i < len; i++) {
                LONG v = (LONG)a._Data[i] + (LONG)b._Data[i] + carry;
                carry = (v >> BITS);
                res._Data[i] = (INT)(v & INT_MASK);
            }
#endif
            if (carry != 0 && len < res._Data.Length) {
                res._Data[len] = (INT)carry;
            }
            res.UpdateLength();
            if (a.IsNegative == b.IsNegative && res.IsNegative != a.IsNegative)
                throw new ArithmeticException();
        }
        static BigInt Subtract(BigInt a, BigInt b) {
            var res = new BigInt(0);
            Subtract(res, a, b);
            return res;
        }

        static void Subtract(BigInt res, BigInt a, BigInt b) {
            // Let res and a potentially be the same instance
            var len = Math.Max(a._Length, b._Length);
            bool aNegative = a.IsNegative;
            int len2 = 0;
#if NO64
            uint carry = 0, hi, lo;
            for (int i = 0; i < len; i++) {
                hi = 0;
                lo = a._Data[i];
                INT64Math.SubtractUInt32(ref hi, ref lo, b._Data[i]);
                INT64Math.SubtractUInt32(ref hi, ref lo, carry);
#if validate
                LONG v = ((LONG)a._Data[i] - (LONG)b._Data[i] - carry);
                if ((int)hi != (v >> BITS) || (uint)v != lo)
                    throw new Exception();
#endif
                carry = (hi != 0 ? (uint)1 : 0);
                res._Data[i] = lo;
                if (lo != 0)
                    len2 = i;
            }
#else
            LONG carry = 0;
            LONG one = 1; // bridge.net performance
            LONG zero = 0;// bridge.net performance
            for (int i = 0; i < len; i++) {
                LONG v = ((LONG)a._Data[i] - (LONG)b._Data[i] - carry);
                carry = (v < 0 ? one : zero);
                res._Data[i] = (INT)(v & INT_MASK);
                if (res._Data[i] != 0)
                    len2 = i;
            }
#endif
            if (carry != 0 && len < res._Data.Length) {
                for (int i = len; i < res._Data.Length; i++)
                    res._Data[i] = INT_MASK;
                len2 = res._Data.Length - 1;
            }
            len2++;
            res._Length = len2;// res.UpdateLength();

            if (aNegative != b.IsNegative && res.IsNegative != aNegative)
                throw new ArithmeticException();
        }
        static BigInt Negate(BigInt a) {
            if (a._Length == 1 && a._Data[0] == 0)
                return a;
            BigInt result = new BigInt(a);
            result.Negate();
            return result;
        }
        void Negate() {
            if (_Length == 1 && _Data[0] == 0)
                return;
            bool wasNegative = this.IsNegative;
            // 1's complement
            for (int i = 0; i < _Data.Length; i++)
                _Data[i] = (INT)(~(_Data[i]) & INT_MASK);

#if NO64
            uint carry = 1, lo, hi;
            int index = 0;
            while (carry != 0 && index < _Data.Length) {
                hi = 0;
                lo = _Data[index];
                INT64Math.AddUint32(ref hi, ref lo, 1);
                _Data[index] = lo;
                carry = hi;
                index++;
            }
#else

            LONG val, carry = 1;
            int index = 0;
            while (carry != 0 && index < _Data.Length) {
                val = ((LONG)_Data[index]);
                val++;
                _Data[index] = (INT)(val & INT_MASK);
                carry = (val >> BITS) & INT_MASK;
                index++;
            }
#endif
            if (wasNegative == this.IsNegative)
                throw new ArithmeticException("Overflow in negation");
            UpdateLength();
        }
        static BigInt ShiftRight(BigInt a, int shiftVal) {
            BigInt result = new BigInt(a);
            result._Length = ShiftRightArithmetic(result._Data, shiftVal);
            if (a.IsNegative) {
                for (int i = result._Data.Length - 1; i >= result._Length; i--)
                    result._Data[i] = INT_MASK;
                INT mask = SIGN_MASK;
                for (int i = 0; i < BITS; i++) {
                    if ((result._Data[result._Length - 1] & mask) != 0)
                        break;
                    result._Data[result._Length - 1] |= mask;
                    mask >>= 1;
                }
                result._Length = result._Data.Length;
            }
            return result;
        }
        static int ShiftRightArithmetic(INT[] buffer, int shiftVal) {
            int shiftAmount = BITS;
            int invShift = 0;
            int bufLen = buffer.Length;
            while (bufLen > 1 && buffer[bufLen - 1] == 0)
                bufLen--;
            for (int count = shiftVal; count > 0;) {
                if (count < shiftAmount) {
                    shiftAmount = count;
                    invShift = BITS - shiftAmount;
                }
                INT carry = 0;
                for (int i = bufLen - 1; i >= 0; i--) {
                    INT val = (INT)(buffer[i] >> shiftAmount);
                    val |= carry;
                    carry = (INT)(((buffer[i]) << invShift) & INT_MASK);
                    buffer[i] = val;
                }

                count -= shiftAmount;
            }
            while (bufLen > 1 && buffer[bufLen - 1] == 0)
                bufLen--;
            return bufLen;
        }
        static BigInt ShiftLeft(BigInt a, int shiftVal) {
            var result = new BigInt(a);
            result._Length = ShiftLeft(result._Data, shiftVal);
            return result;
        }
        static int ShiftLeft(INT[] buffer, int shiftVal) {
            int shiftAmount = BITS;
            int bufLen = buffer.Length;

            while (bufLen > 1 && buffer[bufLen - 1] == 0)
                bufLen--;

            for (int count = shiftVal; count > 0;) {
                if (count < shiftAmount)
                    shiftAmount = count;

#if NO64
                uint carry = 0;
                uint hi, lo;
                for (int i = 0; i < bufLen; i++) {
                    INT64Math.ShiftLeft(buffer[i], shiftAmount, out hi, out lo);
                    lo |= carry;
#if validate
                    ULONG val = ((ULONG)buffer[i] << shiftAmount) | carry;
                    if ((INT)(val & INT_MASK) != lo || (val >> BITS) != hi)
                        throw new Exception();
#endif

                    buffer[i] = lo;
                    carry = hi;
                }
#else
                ULONG carry = 0;
                for (int i = 0; i < bufLen; i++) {
                    ULONG val = ((ULONG)buffer[i] << shiftAmount);
                    val |= carry;
                    buffer[i] = (INT)(val & INT_MASK);
                    carry = (val >> BITS);
                }
#endif

                if (carry != 0) {
                    if (bufLen + 1 <= buffer.Length) {
                        buffer[bufLen] = (INT)carry;
                        bufLen++;
                    }
                }
                count -= shiftAmount;
            }
            return bufLen;
        }
        static BigInt Multiply(BigInt a, BigInt b) {
            var result = new BigInt(0);
            Multiply(result, a, b);
            return result;
        }
        static void Multiply(BigInt result, BigInt a, BigInt b) {

            bool bi1Neg = a.IsNegative;
            bool bi2Neg = b.IsNegative;

            // take the absolute value of the inputs
            if (bi1Neg)
                a = -a;
            if (bi2Neg)
                b = -b;

            var dest = result._Data;
            var bData = b._Data;

            // multiply the absolute values
            int len = 0;
            for (int i = 0; i < a._Length; i++) {
                if (a._Data[i] == 0) continue;
#if NO64
                uint mcarry = 0;
#else
                ULONG mcarry = 0;
#endif
                var aVal = a._Data[i];
                for (int j = 0, k = i; j < b._Length; j++, k++) {
                    // k = i + j
#if NO64
                    uint lo = 0, hi = 0;
                    if(bData[j]!=0)
                    INT64Math.Multiply(aVal, bData[j], out hi, out lo);
                    INT64Math.AddUint32(ref hi, ref lo, dest[k]);
                    INT64Math.AddUint32(ref hi, ref lo, mcarry);
#if validate
                    ULONG val = (((ULONG)aVal * (ULONG)bData[j]) + (ULONG)dest[k] + mcarry);
                    if ((uint)val != lo)
                        throw new Exception();
#endif
                    mcarry = hi;
                    dest[k] = lo;
                    if (lo != 0)
                        len = k;

#else
                    ULONG val = (((ULONG)aVal * (ULONG)bData[j]) + (ULONG)dest[k] + mcarry);
                    mcarry = ((val >> BITS) & INT_MASK);
                    dest[k] = (INT)(val & INT_MASK);
                    if (val != 0)
                        len = k;
#endif

                }
                if (mcarry != 0) {
                    dest[i + b._Length] = (INT)(mcarry & INT_MASK);
                    len = i + b._Length;
                }
            }
            len++;
            result._Length = len;//result.UpdateLength();


            // overflow check (result is -ve)
            if (result.IsNegative) {
                int lastPos = dest.Length - 1;
                if (bi1Neg != bi2Neg && dest[lastPos] == SIGN_MASK) {  // different sign

                    // handle the special case where multiplication produces
                    // a max negative number in 2's complement.

                    if (result._Length == 1)
                        return;
                    else {
                        bool isMaxNeg = true;
                        for (int i = 0; i < result._Length - 1 && isMaxNeg; i++) {
                            if (dest[i] != 0)
                                isMaxNeg = false;
                        }

                        if (isMaxNeg)
                            return;
                    }
                }
                throw new ArithmeticException("Multiplication overflow");
            }
            // if input has different signs, then result is -ve
            if (bi1Neg != bi2Neg) {
                result.Negate();
                return;
            }

        }
        static BigInt Divide(BigInt dividend, BigInt divisor) {
            bool dividendNeg = dividend.IsNegative;
            if (dividendNeg)
                dividend = -dividend;
            bool divisorNeg = divisor.IsNegative;
            if (divisorNeg)
                divisor = -divisor;
            if (dividend < divisor)
                return new BigInt(0);
            var quotient = new BigInt(0);
            var remainder = new BigInt(0);
            Divide(dividend, divisor, ref quotient, ref remainder);
            if (dividendNeg != divisorNeg)
                return -quotient;
            return quotient;
        }
        static void Divide(BigInt a, BigInt b, ref BigInt outQuotient, ref BigInt outRemainder) {
            INT[] result = new INT[a._Data.Length];
            int remainderLen = a._Length + 1;
            INT[] remainder = new INT[remainderLen];
            int shift = 0, resultPos = 0, y = 0;
            if (b._Length == 1) {
                // copy dividend to reminder
                int len = 1;
                for (int i = 0; i < a._Data.Length; i++) {
                    outRemainder._Data[i] = a._Data[i];
                    if (a._Data[i] != 0)
                        len = i + 1;
                }
                outRemainder._Length = len;//     outRemainder.UpdateLength();
#if NO64
                uint divisorHi = 0;
                uint divisorLo = b._Data[0];
                int pos = outRemainder._Length - 1;
                uint dividendHi = 0;
                uint dividendLo = outRemainder._Data[pos];
#if validate
                ULONG divisor = (ULONG)b._Data[0];
                ULONG dividend = (ULONG)outRemainder._Data[pos];
#endif
                if (INT64Math.CompareTo(dividendHi, dividendLo, divisorHi, divisorLo) >= 0) {
                    int qHi, remHi;
                    uint qLo, remLo;
                    INT64Math.ModDiv(dividendHi, dividendLo, divisorHi, divisorLo,
                        out qHi, out qLo, out remHi, out remLo);

#if validate
                    if ((uint)(dividend / divisor) != qLo || (uint)(dividend % divisor) != remLo)
                        throw new Exception();
#endif
                    result[resultPos++] = (INT)qLo;
                    outRemainder._Data[pos] = remLo;
                }
                pos--;
                while (pos >= 0) {
                    dividendHi = outRemainder._Data[pos + 1];
                    dividendLo = outRemainder._Data[pos];
                    int qHi, remHi;
                    uint qLo, remLo;
                    INT64Math.ModDiv(dividendHi, dividendLo, divisorHi, divisorLo,
                        out qHi, out qLo, out remHi, out remLo);

#if validate
                    dividend = (((ULONG)outRemainder._Data[pos + 1] << BITS) + (ULONG)outRemainder._Data[pos]);
                    if ((uint)(dividend / divisor) != qLo || (uint)(dividend % divisor) != remLo)
                        throw new Exception();
#endif

                    result[resultPos++] = (INT)qLo;
                    outRemainder._Data[pos + 1] = 0;
                    outRemainder._Data[pos--] = remLo;
                }
#else
                ULONG divisor = (ULONG)b._Data[0];
                int pos = outRemainder._Length - 1;
                ULONG dividend = (ULONG)outRemainder._Data[pos];
                if (dividend >= divisor) {
                    ULONG quotient = (dividend / divisor);
                    result[resultPos++] = (INT)(quotient & INT_MASK);
                    outRemainder._Data[pos] = (INT)((dividend % divisor) & INT_MASK);
                }
                pos--;
                while (pos >= 0) {
                    dividend = (((ULONG)outRemainder._Data[pos + 1] << BITS) + (ULONG)outRemainder._Data[pos]);
                    ULONG quotient = (dividend / divisor);
                    result[resultPos++] = (INT)(quotient & INT_MASK);
                    outRemainder._Data[pos + 1] = 0;
                    outRemainder._Data[pos--] = (INT)((dividend % divisor) & INT_MASK);
                }
#endif
                outRemainder.UpdateLength();

            } else {
                INT mask = SIGN_MASK;
                INT val = b._Data[b._Length - 1];
                while (mask != 0 && (val & mask) == 0) {
                    shift++; mask >>= 1;
                }

                for (int i = 0; i < a._Length; i++)
                    remainder[i] = a._Data[i];
                ShiftLeft(remainder, shift);
                b = b << shift;


                int j = remainderLen - b._Length;
                int pos = remainderLen - 1;

                int divisorLen = b._Length + 1;
                INT[] dividendPart = new INT[divisorLen];
                var kk = new BigInt(0);
                var yy = new BigInt(0);
                var ss = new BigInt(0);
                var q_hatBi = new BigInt(0);

#if NO64
                uint first = b._Data[b._Length - 1];
                uint second = b._Data[b._Length - 2];
#if validate
                ULONG firstDivisorByte = (ULONG)b._Data[b._Length - 1];
                ULONG secondDivisorByte = (ULONG)b._Data[b._Length - 2];
#endif

                while (j > 0) {

                    uint dividendHi = remainder[pos];
                    uint dividendLo = remainder[pos - 1];

                    int qh, rh;
                    uint ql, rl;
                    INT64Math.ModDiv(dividendHi, dividendLo, 0, first, out qh, out ql, out rh, out rl);
                    uint q_hat = (uint)ql, q_hatHi = (uint)qh, r_hatHi = (uint)rh, r_hat = rl;
#if validate
                    ULONG dividend = (((ULONG)remainder[pos] << BITS) + (ULONG)remainder[pos - 1]);
                    ULONG q_hatB = (dividend / firstDivisorByte);
                    ULONG r_hatB = (dividend % firstDivisorByte);
                    if (q_hatB != (q_hatHi << 32 | q_hat)
                        || r_hat != (r_hatHi << 32 | r_hat))
                        throw new Exception();
#endif
                    bool done = false;
                    while (!done) {
                        done = true;
                        uint mulH, mulL;
                        INT64Math.Multiply(q_hatHi, q_hat, 0, second, out mulH, out mulL);
                        bool condition = (qh == 1 && q_hat == 0) ||
                           //(q_hat * secondDivisorByte) > ((r_hat << BITS) + remainder[pos - 2])
                           INT64Math.CompareTo(mulH, mulL, r_hat, remainder[pos - 2]) > 0;
#if validate
                        if ((q_hatB * secondDivisorByte) != ((ulong)mulH << 32 | mulL))
                            throw new Exception();
                        bool testRes = q_hatB == 0x100000000 || (q_hatB * secondDivisorByte) > ((r_hatB << BITS) + remainder[pos - 2]);
                        if (testRes != condition)
                            throw new Exception();
#endif


                        if (condition) {
                            INT64Math.SubtractUInt32(ref q_hatHi, ref q_hat, 1);
                            INT64Math.AddUint32(ref r_hatHi, ref r_hat, first);
#if validate
                            q_hatB--;
                            r_hatB += firstDivisorByte;
#endif
                            if (r_hat == 0)//r_hat < 0x100000000)
                                done = false;
                        }
                    }
                    //Debug.WriteLine("q_hat: " + q_hat+ " r_hat: " + r_hat);
                    for (int h = 0; h < divisorLen; h++)
                        dividendPart[h] = remainder[pos - h];


                    kk.Replace(dividendPart);//var kk = new BigInt2(dividendPart);
                    q_hatBi.Replace(q_hatHi, q_hat);
#if validate
                    var test = new BigInt(0);
                    test.Replace(q_hatB);
                    if (test != q_hatBi)
                        throw new Exception();
#endif
                    ss.Reset();
                    Multiply(ss, b, q_hatBi);
                    //Debug.WriteLine("ss: " + ss);
                    //Debug.WriteLine("kk: " + kk);
                    while (ss > kk) {
                        q_hat--;
                        Subtract(ss, ss, b); //ss -= b;
                        //Debug.WriteLine("["+ q_hat + "] ss: " + ss);
                    }
                    Subtract(yy, kk, ss);
                    //Debug.WriteLine("yy: " + yy);
                    for (int h = 0; h < divisorLen; h++)
                        remainder[pos - h] = yy._Data[b._Length - h];

                    result[resultPos++] = (INT)q_hat;

                    pos--;
                    j--;
                }
#else
                ULONG firstDivisorByte = (ULONG)b._Data[b._Length - 1];
                ULONG secondDivisorByte = (ULONG)b._Data[b._Length - 2];
                while (j > 0) {
                    ULONG dividend = (((ULONG)remainder[pos] << BITS) + (ULONG)remainder[pos - 1]);
                    ULONG q_hat = (dividend / firstDivisorByte);
                    ULONG r_hat = (dividend % firstDivisorByte);
                    bool done = false;
                    while (!done) {
                        done = true;

                        if (q_hat == 0x100000000 ||
                           (q_hat * secondDivisorByte) > ((r_hat << BITS) + remainder[pos - 2])) {
                            q_hat--;
                            r_hat += firstDivisorByte;
                            if (r_hat < 0x100000000)
                                done = false;
                        }
                    }
                    //Debug.WriteLine("q_hat: " + q_hat+ " r_hat: " + r_hat);
                    for (int h = 0; h < divisorLen; h++)
                        dividendPart[h] = remainder[pos - h];


                    kk.Replace(dividendPart);//var kk = new BigInt2(dividendPart);
                    q_hatBi.Replace(q_hat);
                    ss.Reset();
                    Multiply(ss, b, q_hatBi);
                    //Debug.WriteLine("ss: " + ss);
                    //Debug.WriteLine("kk: " + kk);
                    while (ss > kk) {
                        q_hat--;
                        Subtract(ss, ss, b); //ss -= b;
                        //Debug.WriteLine("["+ q_hat + "] ss: " + ss);
                    }
                    Subtract(yy, kk, ss);
                    //Debug.WriteLine("yy: " + yy);
                    for (int h = 0; h < divisorLen; h++)
                        remainder[pos - h] = yy._Data[b._Length - h];

                    result[resultPos++] = (INT)q_hat;

                    pos--;
                    j--;
                }
#endif

                outRemainder._Length = ShiftRightArithmetic(remainder, shift);

                for (y = 0; y < outRemainder._Length; y++)
                    outRemainder._Data[y] = remainder[y];
                for (; y < outRemainder._Data.Length; y++)
                    outRemainder._Data[y] = 0;
            }

            y = 0;
            int len2 = 1;
            for (int x = resultPos - 1; x >= 0; x--, y++) {
                outQuotient._Data[y] = result[x];
                if (result[x] != 0)
                    len2 = y + 1;
            }
            for (; y < outQuotient._Data.Length; y++)
                outQuotient._Data[y] = 0;

            outQuotient._Length = len2;// outQuotient.UpdateLength();

        }
        void Reset() {
            for (int i = 0; i < _Data.Length; i++)
                _Data[i] = 0;
            _Length = 1;
        }
        static BigInt Mod(BigInt a, BigInt b) {
            var remainder = new BigInt(0);
            Mod(remainder, a, b);
            return remainder;
        }
        static void Mod(BigInt result, BigInt a, BigInt b) {
            result.Replace(a);
            bool dividendNeg = a.IsNegative;
            if (dividendNeg)
                a = -a;
            if (b.IsNegative)
                b = -b;
            if (a < b)
                return;
            var quotient = new BigInt(0);
            Divide(a, b, ref quotient, ref result);
            if (dividendNeg)
                result.Negate();
        }
        static BigInt And(BigInt a, BigInt b) {
            var result = new BigInt(0);
            int len = Math.Max(a._Length, b._Length);
            for (int i = 0; i < len; i++) {
                result._Data[i] = (INT)(a._Data[i] & b._Data[i]);
            }
            result.UpdateLength();
            return result;
        }

        #endregion

        public static BigInt GreatestCommonDivisor(BigInt left, BigInt right) {
            BigInt x = left.IsNegative ? -left : left;
            BigInt y = right.IsNegative ? -right : right;
            BigInt g = y;
            while (x._Length > 1 || (x._Length == 1 && x._Data[0] != 0)) {
                g = x;
                x = y % x;
                y = g;
            }

            return g;
        }

        public static BigInt ModInverse(BigInt value, BigInt modulus) {
            var p = new BigInt[] { 0, 1 };
            var q = new BigInt[2];    // quotients
            var r = new BigInt[] { 0, 0 };             // remainders
            var a = modulus;
            var b = value;
            int step = 0;
            while (b._Length > 1 || (b._Length == 1 && b._Data[0] != 0)) {
                var quotient = new BigInt(0);
                var remainder = new BigInt(0);

                if (step > 1) {
                    var pval = (p[0] - (p[1] * q[0])) % modulus;
                    p[0] = p[1];
                    p[1] = pval;
                }
                Divide(a, b, ref quotient, ref remainder);
                q[0] = q[1];
                r[0] = r[1];
                q[1] = quotient; r[1] = remainder;
                a = b;
                b = remainder;
                step++;
            }

            if (r[0]._Length > 1 || (r[0]._Length == 1 && r[0]._Data[0] != 1))
                throw (new ArithmeticException("No inverse!"));

            BigInt result = ((p[0] - (p[1] * q[0])) % modulus);

            if (result.IsNegative)
                result += modulus;  // get the least positive modulus

            return result;
        }

        public static BigInt Remainder(BigInt left, BigInt right) {
            BigInt q = new BigInt(0);
            BigInt r = new BigInt(0);
            Divide(left, right, ref q, ref r);
            return r;
        }
        class PrimeContext {
            public PrimeContext() {
                q1 = new BigInt(0);
                q2 = new BigInt(0);
                q3 = new BigInt(0);
                r1 = new BigInt(0);
                r2 = new BigInt(0);

            }
            public BigInt q1;
            public BigInt q2;
            public BigInt q3;
            public BigInt r1;
            public BigInt r2;
        }
        public static BigInt ModPow(BigInt value, BigInt exp, BigInt n) {

            if (exp.IsNegative)
                throw new ArithmeticException("Exponent must be positive");
            BigInt resultNum = 1;
            BigInt tempNum;
            bool thisNegative = value.IsNegative;

            if (thisNegative) {  // negative this
                tempNum = -value % n;
            } else
                tempNum = value % n;  // ensures (tempNum * tempNum) < b^(2k)

            if (n.IsNegative)
                n = -n;

            // calculate constant = b^(2k) / m
            var constant = new BigInt(0);

            int i = n._Length << 1;
            constant._Data[i] = 1;
            constant._Length = i + 1;

            constant = constant / n;

            int totalBits = exp.ActualBitCount;
            int count = 0;

            // perform squaring and multiply exponentiation
            PrimeContext e = new PrimeContext();
            for (int pos = 0; pos < exp._Length; pos++) {
                INT mask = 1;

                for (int index = 0; index < BITS; index++) {
                    if ((exp._Data[pos] & mask) != 0)
                        BarrettReduction(e, resultNum, resultNum * tempNum, n, constant);
                    mask <<= 1;
                    BarrettReduction(e, tempNum, tempNum * tempNum, n, constant);
                    if (tempNum._Length == 1 && tempNum._Data[0] == 1) {
                        if (thisNegative && (exp._Data[0] & 1) != 0)    //odd exp
                            resultNum.Negate();
                        return resultNum;
                    }
                    count++;
                    if (count == totalBits)
                        break;
                }
            }

            if (thisNegative && (exp._Data[0] & 1) != 0)    //odd exp
                resultNum.Negate();

            return resultNum;
        }

        public static bool IsProbablePrime(BigInt value) {
            return IsProbablePrime(new PrimeContext(), value);
        }

        static bool IsProbablePrime(PrimeContext e, BigInt value) {

            BigInt thisVal = value.IsNegative ? -value : value;

            if (thisVal._Length == 1) {
                // test small numbers
                if (thisVal._Data[0] == 0 || thisVal._Data[0] == 1)
                    return false;
                else if (thisVal._Data[0] == 2 || thisVal._Data[0] == 3)
                    return true;
            }

            if ((thisVal._Data[0] & 1) == 0)     // even numbers
                return false;


            // test for divisibility by primes < 2000
            BigInt divisor = new BigInt(0);
            BigInt resultNum = new BigInt(0);
            for (int p = 0; p < PrimesBelow2000.Length; p++) {
                divisor.Replace(PrimesBelow2000[p]);
                if (divisor >= thisVal)
                    break;
                Mod(resultNum, thisVal, divisor);
                if (resultNum._Data[0] == 0) {
                    return false;
                }
            }

            // Perform BASE 2 Rabin-Miller Test
            // calculate values of s and t
            BigInt p_sub1 = thisVal - 1;
            int s = 0;

            for (int index = 0; index < p_sub1._Length; index++) {
                INT mask = 1;

                for (int i = 0; i < BITS; i++) {
                    if ((p_sub1._Data[index] & mask) != 0) {
                        index = p_sub1._Length;      // to break the outer loop
                        break;
                    }
                    mask <<= 1;
                    s++;
                }
            }

            BigInt t = p_sub1 >> s;

            int bits = thisVal.ActualBitCount;
            BigInt a = 2;

            // b = a^t mod p
            BigInt b = ModPow(a, t, thisVal);
            bool result = false;

            if (b._Length == 1 && b._Data[0] == 1)         // a^t mod p = 1
                result = true;

            for (int j = 0; result == false && j < s; j++) {
                if (b == p_sub1) {        // a^((2^j)*t) mod p = p-1 for some 0 <= j <= s-1
                    result = true;
                    break;
                }
                Mod(b, Multiply(b, b), thisVal); //b = (b * b) % thisVal;
            }

            //if number is strong pseudoprime to base 2, then do a strong lucas test
            if (result)
                result = LucasStrongTestHelper(e, thisVal);

            return result;
        }

        public static BigInt GeneratePrime(int length) {
            var result = new BigInt(0);
            var resultMin1 = new BigInt(0);
            var one = new BigInt(1);
            var ee = new BigInt(65537);
            var e = new PrimeContext();
            int c = 0;
            do {
                result.FillRandom(length);
                result._Data[0] |= 0x01;     // make it odd
                resultMin1.Replace(result);
                Subtract(resultMin1, result, one);
                c++;
            } while (GreatestCommonDivisor(resultMin1, ee) == one && !IsProbablePrime(e, result));

            return result;
        }

        public delegate void RandomBytesFunction(byte[] ar, int length);
        byte[] _RandomBytes;
        void FillRandom(int bits) {
            int bytes = bits >> 3;
            int remBits = bits & 7;

            if (remBits != 0)
                bytes++;

            if (_RandomBytes == null || _RandomBytes.Length != bytes)
                _RandomBytes = new byte[bytes];
            RNG.RandomBytes(_RandomBytes);
           
            for (int i = bytes; i < _RandomBytes.Length; i++)
                _RandomBytes[i] = 0;

            if (remBits != 0) {
                _RandomBytes[bytes - 1] |= (byte)(1 << (remBits - 1));
                _RandomBytes[bytes - 1] &= (byte)(0xFF >> (8 - remBits));
            } else
                _RandomBytes[bytes - 1] |= 0x80;

            Replace(_RandomBytes);

        }

        public static BigInt Sqrt(BigInt value) {

            uint numBits = (uint)value.ActualBitCount;

            if ((numBits & 0x1) != 0)        // odd number of bits
                numBits = (numBits >> 1) + 1;
            else
                numBits = (numBits >> 1);

            int bytePos = (int)(numBits >> BYTE_POS);
            int bitPos = (int)(numBits & BIT_POS);

            INT mask;

            BigInt result = new BigInt(0);
            if (bitPos == 0)
                mask = INT_MASK;
            else {
                mask = (INT)(1 << bitPos);
                bytePos++;
            }
            result._Length = bytePos;

            for (int i = bytePos - 1; i >= 0; i--) {
                while (mask != 0) {
                    // guess
                    result._Data[i] ^= mask;

                    // undo the guess if its square is larger than this
                    if ((result * result) > value)
                        result._Data[i] ^= mask;

                    mask >>= 1;
                }
                mask = SIGN_MASK;
            }
            return result;
        }

        #region Esoteric math functions
        static BigInt BarrettReduction(BigInt x, BigInt n, BigInt constant) {
            int k = n._Length,
                kPlusOne = k + 1,
                kMinusOne = k - 1;

            var q1 = new BigInt(0);

            // q1 = x / b^(k-1)
            for (int i = kMinusOne, j = 0; i < x._Length; i++, j++)
                q1._Data[j] = x._Data[i];
            q1._Length = x._Length - kMinusOne;
            if (q1._Length <= 0)
                q1._Length = 1;


            var q2 = q1 * constant;
            var q3 = new BigInt(0);

            // q3 = q2 / b^(k+1)
            for (int i = kPlusOne, j = 0; i < q2._Length; i++, j++)
                q3._Data[j] = q2._Data[i];
            q3._Length = q2._Length - kPlusOne;
            if (q3._Length <= 0)
                q3._Length = 1;


            // r1 = x mod b^(k+1)
            // i.e. keep the lowest (k+1) words
            var r1 = new BigInt(0);
            int lengthToCopy = (x._Length > kPlusOne) ? kPlusOne : x._Length;
            for (int i = 0; i < lengthToCopy; i++)
                r1._Data[i] = x._Data[i];
            r1._Length = lengthToCopy;


            // r2 = (q3 * n) mod b^(k+1)
            // partial multiplication of q3 and n

            var r2 = new BigInt(0);
            for (int i = 0; i < q3._Length; i++) {
                if (q3._Data[i] == 0) continue;
#if NO64
                uint mcarry = 0;
                uint q3Val = q3._Data[i];
                int t = i;
                for (int j = 0; j < n._Length && t < kPlusOne; j++, t++) {
                    // t = i + j
                    uint hi, lo;
                    INT64Math.Multiply(q3Val, n._Data[j], out hi, out lo);
                    INT64Math.AddUint32(ref hi, ref lo, r2._Data[t]);
                    INT64Math.AddUint32(ref hi, ref lo, mcarry);
#if validate
                    ULONG val = ((q3Val * (ULONG)n._Data[j]) + (ULONG)r2._Data[t] + mcarry);
                    if ((uint)val != lo || val >> 32 != hi)
                        throw new Exception();
#endif
                    mcarry = hi;
                    r2._Data[t] = lo;
                }

                if (t < kPlusOne)
                    r2._Data[t] = (INT)mcarry;
#else
                ULONG mcarry = 0;
                ULONG q3Val = (ULONG)q3._Data[i];
                int t = i;
                for (int j = 0; j < n._Length && t < kPlusOne; j++, t++) {
                    // t = i + j
                    ULONG val = ((q3Val * (ULONG)n._Data[j]) + (ULONG)r2._Data[t] + mcarry);
                    mcarry = ((val >> BITS) & INT_MASK);
                    r2._Data[t] = (INT)(val & INT_MASK);
                }

                if (t < kPlusOne)
                    r2._Data[t] = (INT)mcarry;
#endif
            }
            r2._Length = kPlusOne;
            while (r2._Length > 1 && r2._Data[r2._Length - 1] == 0)
                r2._Length--;

            r1 -= r2;

            if (r1.IsNegative) {
                var val = new BigInt(0);
                val._Data[kPlusOne] = 1;
                val._Length = kPlusOne + 1;
                r1 += val;
            }

            while (r1 >= n)
                r1 -= n;

            return r1;
        }

        static void BarrettReduction(PrimeContext e, BigInt result, BigInt x, BigInt n, BigInt constant) {
            int k = n._Length,
                kPlusOne = k + 1,
                kMinusOne = k - 1;

            BigInt q1 = e.q1;
            BigInt q2 = e.q2;
            BigInt q3 = e.q3;
            BigInt r1 = e.r1;
            BigInt r2 = e.r2;
            q1.Reset();

            // q1 = x / b^(k-1)
            for (int i = kMinusOne, j = 0; i < x._Length; i++, j++)
                q1._Data[j] = x._Data[i];
            q1._Length = x._Length - kMinusOne;
            if (q1._Length <= 0)
                q1._Length = 1;

            q2.Reset();
            Multiply(q2, q1, constant); // var q2 = q1 * constant;

            q3.Reset();

            // q3 = q2 / b^(k+1)
            for (int i = kPlusOne, j = 0; i < q2._Length; i++, j++)
                q3._Data[j] = q2._Data[i];
            q3._Length = q2._Length - kPlusOne;
            if (q3._Length <= 0)
                q3._Length = 1;


            // r1 = x mod b^(k+1)
            // i.e. keep the lowest (k+1) words
            r1.Reset();
            int lengthToCopy = (x._Length > kPlusOne) ? kPlusOne : x._Length;
            for (int i = 0; i < lengthToCopy; i++)
                r1._Data[i] = x._Data[i];
            r1._Length = lengthToCopy;


            // r2 = (q3 * n) mod b^(k+1)
            // partial multiplication of q3 and n

            r2.Reset();
            for (int i = 0; i < q3._Length; i++) {
                if (q3._Data[i] == 0) continue;




#if NO64
                uint mcarry = 0;
                uint q3Val = q3._Data[i];
                int t = i;
                for (int j = 0; j < n._Length && t < kPlusOne; j++, t++) {
                    // t = i + j
                    uint hi, lo;
                    INT64Math.Multiply(q3Val, n._Data[j], out hi, out lo);
                    INT64Math.AddUint32(ref hi, ref lo, r2._Data[t]);
                    INT64Math.AddUint32(ref hi, ref lo, mcarry);
#if validate
                    ULONG val = ((q3Val * (ULONG)n._Data[j]) + (ULONG)r2._Data[t] + mcarry);
                    if ((uint)val != lo || val >> 32 != hi)
                        throw new Exception();
#endif
                    mcarry = hi;
                    r2._Data[t] = lo;
                }

                if (t < kPlusOne)
                    r2._Data[t] = (INT)mcarry;
#else
                 int t = i;
                ULONG q3Val = (ULONG)q3._Data[i];
                ULONG mcarry = 0;
                for (int j = 0; j < n._Length && t < kPlusOne; j++, t++) {
                    // t = i + j
                    ULONG val = ((q3Val * (ULONG)n._Data[j]) + (ULONG)r2._Data[t] + mcarry);
                    mcarry = ((val >> BITS) & INT_MASK);
                    r2._Data[t] = (INT)(val & INT_MASK);
                }

                if (t < kPlusOne)
                    r2._Data[t] = (INT)mcarry;
#endif
            }
            r2._Length = kPlusOne;
            while (r2._Length > 1 && r2._Data[r2._Length - 1] == 0)
                r2._Length--;
            r1 -= r2;

            if (r1.IsNegative) {
                r2.Reset();
                r2._Data[kPlusOne] = 1;
                r2._Length = kPlusOne + 1;
                Add(r1, r1, r2);
            }
            while (r1 >= n)
                Subtract(r1, r1, n);
            result.Replace(r1);
        }

        static bool LucasStrongTestHelper(PrimeContext e, BigInt thisVal) {
            // Do the test (selects D based on Selfridge)
            // Let D be the first element of the sequence
            // 5, -7, 9, -11, 13, ... for which J(D,n) = -1
            // Let P = 1, Q = (1-D) / 4

            int D = 5, sign = -1, dCount = 0;
            bool done = false;


            while (!done) {
                int Jresult = Jacobi(D, thisVal);
                // Debug.WriteLine("Jresult: " + Jresult);
                if (Jresult == -1)
                    done = true;    // J(D, this) =  1
                else {
                    if (Jresult == 0 && Math.Abs(D) < thisVal)       // divisor found
                        return false;

                    if (dCount == 20) {
                        // check for square
                        BigInt root = Sqrt(thisVal);
                        if (root * root == thisVal)
                            return false;
                    }

                    //Console.WriteLine(D);
                    D = (int)((Math.Abs(D) + 2) * sign);
                    sign = -sign;
                }
                dCount++;
            }

            int Q = (1 - D) >> 2;

            BigInt p_add1 = thisVal + 1;
            int s = 0;

            for (int index = 0; index < p_add1._Length; index++) {
                INT mask = 1;
                for (int i = 0; i < BITS; i++) {
                    if ((p_add1._Data[index] & mask) != 0) {
                        index = p_add1._Length;      // to break the outer loop
                        break;
                    }
                    mask <<= 1;
                    s++;
                }
            }

            BigInt t = p_add1 >> s;

            // calculate constant = b^(2k) / m
            // for Barrett Reduction
            var constant = new BigInt(0);

            int nLen = thisVal._Length << 1;
            constant._Data[nLen] = 1;
            constant._Length = nLen + 1;

            constant = constant / thisVal;

            BigInt[] lucas = LucasSequenceHelper(1, Q, t, thisVal, constant, 0);
            bool isPrime = false;

            if ((lucas[0]._Length == 1 && lucas[0]._Data[0] == 0) ||
               (lucas[1]._Length == 1 && lucas[1]._Data[0] == 0)) {
                // u(t) = 0 or V(t) = 0
                isPrime = true;
            }

            for (int i = 1; i < s; i++) {
                if (!isPrime) {
                    // doubling of index
                    BarrettReduction(e, lucas[1], lucas[1] * lucas[1], thisVal, constant);
                    lucas[1] = (lucas[1] - (lucas[2] << 1)) % thisVal;

                    if ((lucas[1]._Length == 1 && lucas[1]._Data[0] == 0))
                        isPrime = true;
                }

                BarrettReduction(e, lucas[2], lucas[2] * lucas[2], thisVal, constant);     //Q^k
            }


            if (isPrime)     // additional checks for composite numbers
            {
                // If n is prime and gcd(n, Q) == 1, then
                // Q^((n+1)/2) = Q * Q^((n-1)/2) is congruent to (Q * J(Q, n)) mod n

                BigInt g = GreatestCommonDivisor(thisVal, Q);
                if (g._Length == 1 && g._Data[0] == 1)         // gcd(this, Q) == 1
                {
                    if (lucas[2].IsNegative)
                        Add(lucas[2], lucas[2], thisVal);

                    BigInt temp = new BigInt(Q * Jacobi(Q, thisVal)) % thisVal;
                    if (temp.IsNegative)
                        Add(temp, temp, thisVal);
                    if (lucas[2] != temp)
                        isPrime = false;
                }
            }

            return isPrime;
        }

        static int Jacobi(BigInt a, BigInt b) {
            // Jacobi defined only for odd integers
            if ((b._Data[0] & 1) == 0)
                throw new ArgumentException("Jacobi defined only for odd integers.");

            if (a >= b) a %= b;
            if (a._Length == 1 && a._Data[0] == 0) return 0;  // a == 0
            if (a._Length == 1 && a._Data[0] == 1) return 1;  // a == 1

            if (a < 0) {
                if ((((b - 1)._Data[0]) & 2) == 0)       //if( (((b-1) >> 1).data[0] & 0x1) == 0)
                    return Jacobi(-a, b);
                else
                    return -Jacobi(-a, b);
            }

            int e = 0;
            for (int index = 0; index < a._Length; index++) {
                INT mask = 1;
                for (int i = 0; i < BITS; i++) {
                    if ((a._Data[index] & mask) != 0) {
                        index = a._Length;      // to break the outer loop
                        break;
                    }
                    mask <<= 1;
                    e++;
                }
            }

            BigInt a1 = a >> e;

            int s = 1;
            if ((e & 0x1) != 0 && ((b._Data[0] & 0x7) == 3 || (b._Data[0] & 0x7) == 5))
                s = -1;

            if ((b._Data[0] & 0x3) == 3 && (a1._Data[0] & 0x3) == 3)
                s = -s;

            if (a1._Length == 1 && a1._Data[0] == 1)
                return s;
            else
                return (s * Jacobi(b % a1, a1));
        }

        private static BigInt[] LucasSequenceHelper(BigInt P, BigInt Q,
                                                        BigInt k, BigInt n,
                                                        BigInt constant, int s) {
            BigInt[] result = new BigInt[3];

            if ((k._Data[0] & 1) == 0)
                throw (new ArgumentException("Argument k must be odd."));

            int numbits = k.ActualBitCount;
            INT mask = (INT)(1 << ((numbits & BIT_POS) - 1));

            // v = v0, v1 = v1, u1 = u1, Q_k = Q^0

            BigInt v = 2 % n, Q_k = 1 % n,
                       v1 = P % n, u1 = Q_k;
            bool flag = true;

            for (int i = k._Length - 1; i >= 0; i--)     // iterate on the binary expansion of k
            {
                //Console.WriteLine("round");
                while (mask != 0) {
                    if (i == 0 && mask == 1)        // last bit
                        break;

                    if ((k._Data[i] & mask) != 0)             // bit is set
                    {
                        // index doubling with addition

                        u1 = (u1 * v1) % n;

                        v = ((v * v1) - (P * Q_k)) % n;
                        v1 = BarrettReduction(v1 * v1, n, constant);
                        v1 = (v1 - ((Q_k * Q) << 1)) % n;

                        if (flag)
                            flag = false;
                        else
                            Q_k = BarrettReduction(Q_k * Q_k, n, constant);

                        Q_k = (Q_k * Q) % n;
                    } else {
                        // index doubling
                        u1 = ((u1 * v) - Q_k) % n;

                        v1 = ((v * v1) - (P * Q_k)) % n;
                        v = BarrettReduction(v * v, n, constant);
                        v = (v - (Q_k << 1)) % n;

                        if (flag) {
                            Q_k = Q % n;
                            flag = false;
                        } else
                            Q_k = BarrettReduction(Q_k * Q_k, n, constant);
                    }

                    mask >>= 1;
                }
                mask = SIGN_MASK;
            }

            // at this point u1 = u(n+1) and v = v(n)
            // since the last bit always 1, we need to transform u1 to u(2n+1) and v to v(2n+1)

            u1 = ((u1 * v) - Q_k) % n;
            v = ((v * v1) - (P * Q_k)) % n;
            if (flag)
                flag = false;
            else
                Q_k = BarrettReduction(Q_k * Q_k, n, constant);

            Q_k = (Q_k * Q) % n;


            for (int i = 0; i < s; i++) {
                // index doubling
                u1 = (u1 * v) % n;
                v = ((v * v) - (Q_k << 1)) % n;

                if (flag) {
                    Q_k = Q % n;
                    flag = false;
                } else
                    Q_k = BarrettReduction(Q_k * Q_k, n, constant);
            }

            result[0] = u1;
            result[1] = v;
            result[2] = Q_k;

            return result;
        }

        #endregion

        // primes smaller than 2000 to test the generated prime number
        static readonly int[] PrimesBelow2000 = {
                    2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97,
                    101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199,
                    211, 223, 227, 229, 233, 239, 241, 251, 257, 263, 269, 271, 277, 281, 283, 293,
                    307, 311, 313, 317, 331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389, 397,
                    401, 409, 419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499,
                    503, 509, 521, 523, 541, 547, 557, 563, 569, 571, 577, 587, 593, 599,
                    601, 607, 613, 617, 619, 631, 641, 643, 647, 653, 659, 661, 673, 677, 683, 691,
                    701, 709, 719, 727, 733, 739, 743, 751, 757, 761, 769, 773, 787, 797,
                    809, 811, 821, 823, 827, 829, 839, 853, 857, 859, 863, 877, 881, 883, 887,
                    907, 911, 919, 929, 937, 941, 947, 953, 967, 971, 977, 983, 991, 997,
                    1009, 1013, 1019, 1021, 1031, 1033, 1039, 1049, 1051, 1061, 1063, 1069, 1087, 1091, 1093, 1097,
                    1103, 1109, 1117, 1123, 1129, 1151, 1153, 1163, 1171, 1181, 1187, 1193,
                    1201, 1213, 1217, 1223, 1229, 1231, 1237, 1249, 1259, 1277, 1279, 1283, 1289, 1291, 1297,
                    1301, 1303, 1307, 1319, 1321, 1327, 1361, 1367, 1373, 1381, 1399,
                    1409, 1423, 1427, 1429, 1433, 1439, 1447, 1451, 1453, 1459, 1471, 1481, 1483, 1487, 1489, 1493, 1499,
                    1511, 1523, 1531, 1543, 1549, 1553, 1559, 1567, 1571, 1579, 1583, 1597,
                    1601, 1607, 1609, 1613, 1619, 1621, 1627, 1637, 1657, 1663, 1667, 1669, 1693, 1697, 1699,
                    1709, 1721, 1723, 1733, 1741, 1747, 1753, 1759, 1777, 1783, 1787, 1789,
                    1801, 1811, 1823, 1831, 1847, 1861, 1867, 1871, 1873, 1877, 1879, 1889,
                    1901, 1907, 1913, 1931, 1933, 1949, 1951, 1973, 1979, 1987, 1993, 1997, 1999 };


        static class INT64Math {
            public static int CompareTo(uint hiA, uint loA, uint hiB, uint loB) {
                if (hiA == hiB)
                    return loA == loB ? 0 : loA > loB ? 1 : -1; //loA.CompareTo(loB);
                return hiA == hiB ? 0 : hiA > hiB ? 1 : -1;// hiA.CompareTo(hiB);
            }
            public static int CompareTo(int hiA, uint loA, int hiB, uint loB) {
                if (hiA == hiB)
                    return loA == loB ? 0 : loA > loB ? 1 : -1; //loA.CompareTo(loB);
                return hiA == hiB ? 0 : hiA > hiB ? 1 : -1;//hiA.CompareTo(hiB);
            }
            public static int CompareTo(int hiA, uint loA, uint hiB, uint loB) {
                if ((uint)hiA == hiB)
                    return loA == loB ? 0 : loA > loB ? 1 : -1; //loA.CompareTo(loB);
                var ua = ((uint)hiA);
                return ua == hiB ? 0 : ua > hiB ? 1 : -1;//((uint)hiA).CompareTo(hiB);
            }
            public static void ShiftLeft(ref int hi, ref uint lo, int n) {
                if (n <= 0)
                    return;
                if (n > 63)
                    return;

                if (n > 31) {
                    n -= 32;
                    hi = (int)lo;
                    lo = 0;
                }
                if (n > 0) {
                    // Get the high N bits of the low part
                    // into the low part of a temporary value.
                    uint bits = lo >> (32 - n);
                    // Shift the low part
                    lo = (lo << n) >> 0; // workaround bridge bug
                    // Shift the high part and OR-in the lower bits.
                    hi = ((hi << n) | (int)bits);
                }
            }
            public static void ShiftLeft(ref uint hi, ref uint lo, int n) {
                if (n <= 0)
                    return;
                if (n > 63)
                    return;

                if (n > 31) {
                    n -= 32;
                    hi = lo;
                    lo = 0;
                }
                if (n > 0) {
                    // Get the high N bits of the low part
                    // into the low part of a temporary value.
                    uint bits = lo >> (32 - n);
                    // Shift the low part
                    lo = (lo << n) >> 0;// workaround bridge bug
                    // Shift the high part and OR-in the lower bits.
                    hi = ((hi << n) | bits);
                }
            }
            public static void ShiftLeft(uint value, int n, out uint hi, out uint lo) {
                hi = 0;
                lo = value;
                ShiftLeft(ref hi, ref lo, n);
            }

            public static ulong Join(ref uint hi, ref uint lo) {
                return (ulong)hi << 32 | lo;
            }
            public static void AddUint32(ref uint hi, ref uint lo, uint v) {
                lo = (uint)(lo + v);
                if (lo < v)
                    hi++;
            }
            public static void AddInt32(ref int hi, ref uint lo, int v) {
                var tmp = lo;
                lo = (lo + (uint)v);
                if (lo < tmp)
                    hi++;
            }
            public static void SubtractUInt32(ref uint hi, ref uint lo, uint v) {
                var tmp = lo;
                lo = (lo - v);
                if (lo > tmp)
                    hi--;
            }
            public static void Subtract(ref int aHi, ref uint aLo, int bHi, uint bLo) {
                aHi = aHi - bHi;
                var tmp = (uint)(aLo - bLo);
                if (tmp > aLo)
                    aHi--;
                aLo = tmp;
            }
            public static void Divide(ref uint aHi, ref uint aLo,
               ref uint bHi, ref uint bLo,
               out int hi, out uint lo) {
                int qHi, remHi;
                uint qLo, remLo;
                ModDiv(aHi, aLo, bHi, bLo, out qHi, out qLo, out remHi, out remLo);
                hi = qHi;
                lo = (uint)qLo;
            }
            public static void Mod(ref uint aHi, ref uint aLo,
               ref uint bHi, ref uint bLo,
               out int hi, out uint lo) {
                int qHi, remHi;
                uint qLo, remLo;
                ModDiv(aHi, aLo, bHi, bLo, out qHi, out qLo, out remHi, out remLo);
                hi = remHi;
                lo = remLo;
            }
            public static void ModDiv(uint aHi, uint aLo,
                uint bHi, uint bLo,
                out int quoHi, out uint quoLo,
                out int remHi, out uint remLo) {
                if (bHi == 0 && bLo == 0)
                    throw new DivideByZeroException();


                quoHi = (int)aHi;
                quoLo = aLo;
                remHi = 0;
                remLo = 0;
                int zero = 0;
                uint zeroU = 0;
                for (int i = 0; i < 64; i++) {
                    // Left shift Remainder:Quotient by 1
                    ShiftLeft(ref remHi, ref remLo, 1);
                    if (CompareTo(quoHi, quoLo, zero, zeroU) < 0)
                        remLo |= 1;
                    ShiftLeft(ref quoHi, ref quoLo, 1);
                    if (CompareTo(remHi, remLo, bHi, bLo) >= 0) {
                        Subtract(ref remHi, ref remLo, (int)bHi, bLo);
                        AddInt32(ref quoHi, ref quoLo, 1);
                    }
                }
            }
            public static void Multiply(uint a, uint b, out uint hi, out uint lo) {
                var ah = (uint)0;
                var al = a;

                var bh = (uint)0;
                var bl = b;

                var a5 = ah >> 20;
                var a4 = (ah >> 7) & 0x1fff;
                var a3 = ((ah << 6) | (al >> 26)) & 0x1fff;
                var a2 = (al >> 13) & 0x1fff;
                var a1 = al & 0x1fff;

                var b5 = bh >> 20;
                var b4 = (bh >> 7) & 0x1fff;
                var b3 = ((bh << 6) | (bl >> 26)) & 0x1fff;
                var b2 = (bl >> 13) & 0x1fff;
                var b1 = bl & 0x1fff;

                var c1 = a1 * b1;
                var c2 = a1 * b2 + a2 * b1;
                var c3 = a1 * b3 + a2 * b2 + a3 * b1;
                var c4 = a1 * b4 + a2 * b3 + a3 * b2 + a4 * b1;
                var c5 = a1 * b5 + a2 * b4 + a3 * b3 + a4 * b2 + a5 * b1;

                c2 = c2 + (c1 >> 13);
                c1 &= 0x1fff;
                c3 = c3 + (c2 >> 13);
                c2 &= 0x1fff;
                c4 = c4 + (c3 >> 13);
                c3 &= 0x1fff;
                c5 = c5 + (c4 >> 13);
                c4 &= 0x1fff;

                hi = ((c5 << 20) | (c4 << 7) | (c3 >> 6)) >> 0;
                lo = ((c3 << 26) | (c2 << 13) | c1) >> 0;
            }

            public static void Multiply(uint aHi, uint aLo, uint bHi, uint bLo, out uint hi, out uint lo) {
                var ah = aHi;
                var al = aLo;

                var bh = bHi;
                var bl = bLo;

                var a5 = ah >> 20;
                var a4 = (ah >> 7) & 0x1fff;
                var a3 = ((ah << 6) | (al >> 26)) & 0x1fff;
                var a2 = (al >> 13) & 0x1fff;
                var a1 = al & 0x1fff;

                var b5 = bh >> 20;
                var b4 = (bh >> 7) & 0x1fff;
                var b3 = ((bh << 6) | (bl >> 26)) & 0x1fff;
                var b2 = (bl >> 13) & 0x1fff;
                var b1 = bl & 0x1fff;

                var c1 = a1 * b1;
                var c2 = a1 * b2 + a2 * b1;
                var c3 = a1 * b3 + a2 * b2 + a3 * b1;
                var c4 = a1 * b4 + a2 * b3 + a3 * b2 + a4 * b1;
                var c5 = a1 * b5 + a2 * b4 + a3 * b3 + a4 * b2 + a5 * b1;

                c2 = c2 + (c1 >> 13);
                c1 &= 0x1fff;
                c3 = c3 + (c2 >> 13);
                c2 &= 0x1fff;
                c4 = c4 + (c3 >> 13);
                c3 &= 0x1fff;
                c5 = c5 + (c4 >> 13);
                c4 &= 0x1fff;

                hi = ((c5 << 20) | (c4 << 7) | (c3 >> 6)) >> 0;
                lo = ((c3 << 26) | (c2 << 13) | c1) >> 0;
            }
        }

    }

}
