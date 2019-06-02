#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using CM.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CM {

    /// <summary>
    /// Describes a CM WebSocket request or response payload
    /// </summary>
    public partial class Message {
        public RequestHeader Request;
        public ResponseHeader Response;
        public string RawContent;

        private NamedValueList _Values;

        public Message() {
            _Values = new NamedValueList();
        }

        public Message(string payload) {
            _Values = NamedValueList.FromContentString(payload);
        }

        public override string ToString() {
            return Request != null ? Request.Action + " (" + Request.NOnce + ")"
                : Response != null ? "(" + Response.NOnce + ") " + Response.Code.ToString()
                : "Message";
        }

        public NamedValueList Values {
            get { return _Values; }
        }

#if JAVASCRIPT
        new
#endif

        public string this[string key] {
            get {
                return _Values[key];
            }
            set {
                _Values[key] = value;
            }
        }

        public byte[] ToContent() {
            return Encoding.UTF8.GetBytes(_Values.ToContentString());
        }

        public string ToContentString() {
            return _Values.ToContentString();
        }

        public string ToRequestString() {
            return ToRequestString(Request);
        }
        public string ToRequestString(Message.RequestHeader request) {
            if (request == null) throw new InvalidOperationException("No request specified.");
            var s = new StringBuilder();
            s.Append(request + "\r\n");
            s.Append(_Values.ToContentString());
            s.Append("END " + request.NOnce + "\r\n");
            return s.ToString();
        }
        public string ToResponseString() {
            if (Response == null) throw new InvalidOperationException("No response specified.");
            var s = new StringBuilder();
            s.Append(Response + "\r\n");
            s.Append(_Values.ToContentString());
            s.Append("END " + Response.NOnce + "\r\n");
            return s.ToString();
        }

        public T Cast<T>() where T : Message {
            if (typeof(T) == this.GetType())
                return (T)this;
            var t = System.Activator.CreateInstance<T>();
            t._Values = this._Values;
            t.Request = this.Request;
            t.Response = this.Response;
            t.RawContent = this.RawContent;
            return t;
        }
        public Message Clone() {
            var t = (Message)System.Activator.CreateInstance(this.GetType());
            t._Values = this._Values;
            t.Request = this.Request;
            t.Response = this.Response;
            t.RawContent = this.RawContent;
            return t;
        }
        public class RequestHeader {
            public const string PREFIX = "CMD";

            public RequestHeader(string line) {
                // CMD [PUT-ACCOUNT | GET-ACCOUNT | PUT-TRANSACTION | GET-TRANSACTION | FIND-TRANSACTIONS ] [NOnce] [Command specific args]
                OriginalLine = line;
                var args = line.Split(' ');
                if (args.Length < 3 || args[0] != PREFIX) {
                    IsValid = false;
                    return;
                }
                Action = args[1];
                NOnce = args[2];
                IsValid = Action.Length > 0 && NOnce.Length > 0;
                Arguments = args.Skip(3).ToArray();
            }

            public RequestHeader(string action, string nonce, params string[] args) {
                Action = action;
                NOnce = nonce;
                Arguments = args;
                OriginalLine = "CMD " + action + " " + nonce;
                if (args != null && args.Length > 0)
                    OriginalLine += " " + String.Join(" ", args);
                IsValid = true;
            }

            public string OriginalLine;
            public string Action;
            public string NOnce;
            public string[] Arguments;
            public bool IsValid;

            /// <summary>
            /// All commands presently have at most 1x argument. This saves some code.
            /// </summary>
            public string FirstArgument {
                get { return Arguments != null && Arguments.Length > 0 ? Arguments[0] : null; }
            }
            public string AllArguments {
                get { return Arguments != null && Arguments.Length > 0 ? String.Join(" ", Arguments) : null; }
            }
            public override string ToString() {
                return OriginalLine;
            }
        }

        public class ResponseHeader {
            public const string PREFIX = "RES";

            public ResponseHeader(CMResult res, string nonce, params string[] args) {
                Code = res;
                NOnce = nonce;
                // uint cast for Bridge bug
                OriginalLine = "RES 0x" + ((uint)res.Code).ToString("x") + " " + nonce;
                for (int i = 0; i < args.Length; i++)
                    OriginalLine += " " + args[i];
                IsValid = true;
            }

            public ResponseHeader(string line) {
                //RES 0x[hexadecimal CMResult Code] [NOnce] [Command specific arguments]
                OriginalLine = line;
                var args = line.Split(' ');
                if (args.Length < 3 || args[0] != PREFIX) {
                    IsValid = false;
                    return;
                }
                uint code = 0xffffffff;
#if JAVASCRIPT 
                // Workaround for (old) Bridge int.parse bug.
                // parseInt handles 0x OK, but it comes out as a long..
                Bridge.Script.Write("code = parseInt(args[1]);");
                // ..convert back to signed int.
                code = (uint)long.Parse(code.ToString());
#else
                var hex = args[1];
                if (hex.StartsWith("0x"))
                    hex = hex.Substring(2);
                uint.TryParse(hex,
                    System.Globalization.NumberStyles.AllowHexSpecifier,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out code);
#endif
                Code.Code = (int)code;
                NOnce = args[2];
                Arguments = args.Skip(3).ToArray();
                IsValid = true;
            }

            public string OriginalLine;
            public CMResult Code;
            public string NOnce;
            public string[] Arguments;
            public bool IsValid;

            /// <summary>
            /// Returns true if the response is valid and the CMResult code is >= 0.
            /// </summary>
            public bool IsSuccessful {
                get { return IsValid 
                        && Code.Code >= 0
                        // For Bridge uint -> long
                        && Code.Code < int.MaxValue;
                }
            }

            public override string ToString() {
                return OriginalLine;
            }
        }
    }

    public class NamedValueList {

        public NamedValueList Clone() {
            var ar = new NamedValueList();
            for (int i = 0; i < _Values.Count; i++)
                ar._Values.Add(new NamedValue(_Values[i].Name, _Values[i].Value));
            return ar;
        }

        private List<NamedValue> _Values = new List<NamedValue>();

        public string Find(string name) {
            for (int i = 0; i < _Values.Count; i++) {
                if (String.Compare(_Values[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0) {
                    return _Values[i].Value;
                }
            }
            return null;
        }

        public void RemoveAll(string name) {
            for (int i = 0; i < _Values.Count; i++) {
                if (String.Compare(_Values[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0) {
                    _Values.RemoveAt(i);
                    i--;
                }
            }
        }

        public void Clear() {
            _Values.Clear();
        }

        public NamedValue this[int index] {
            get {
                return _Values[index];
            }
        }

#if JAVASCRIPT
        new
#endif

        public string this[string key] {
            get {
                return Find(key);
            }
            set {
                Set(key, value);
            }
        }

        public void Append(string name, string value) {
            _Values.Add(new NamedValue(name, value));
        }

        public void RemoveAt(int index) {
            _Values.RemoveAt(index);
        }

        public void ReplaceWithRange(NamedValueList src, int startIndex, int endIndex) {
            _Values.Clear();
            for (int i = startIndex; i < endIndex; i++) {
                _Values.Add(src[i]);
            }
        }

        public void Append(string line) {
            if (!String.IsNullOrWhiteSpace(line))
                _Values.Add(new NamedValue(line));
        }

        public void Append(NamedValueList src) {
            _Values.AddRange(src._Values);
        }

        public void Set(string name, string value) {
            // Because message content is used for signing, it's important to trim any white space
            // around values so that we don't get unintentional signature mismatches after they
            // go over the wire and message parsing trims the ends.
            if (value != null)
                value = value.Trim();

            int idx = -1;
            for (int i = 0; i < _Values.Count; i++)
                if (String.Compare(_Values[i].Name, name, StringComparison.OrdinalIgnoreCase) == 0) {
                    idx = i;
                    break;
                }
            if (idx != -1)
                _Values[idx].Value = value;
            else
                Append(name, value);
        }

#if JAVASCRIPT
        void TranslateTypeName(ref string name){
            name = name.Replace("Bridge.", "System.");
            switch(name){
                case "Date": name = "System.DateTime"; break;
                case "String": name = "System.String"; break;
                case "Decimal": name = "System.Decimal"; break;
                case "UInt32": name = "System.UInt32"; break;
                case "Boolean": name = "System.Boolean"; break;
                case "Array": name = "System.Byte[]"; break;
            }
        }
#endif

        public T Get<T>(string key) {
            var name = typeof(T).FullName;
#if JAVASCRIPT
            TranslateTypeName(ref name);
#endif
            var str = Find(key);
            if (String.IsNullOrWhiteSpace(str)) {
//#if JAVASCRIPT
//                // Bridge bug workaround (enumeration don't have safe defaults)
//                if(name=="CM.Schema.PayeeStatus"||name=="CM.Schema.PayerStatus")
//                 Bridge.Script.Write("return 0;");
//#endif
                return default(T);
            }
            object o = null;

            switch (name) {
                case "System.UInt32": {
                        uint v;
                        if (!uint.TryParse(str, out v))
                            throw new MessageValueException(key);
                        o = v;
                    }
                    break;

                case "System.String": {
                        o = str.Trim();
                    }
                    break;
                case "System.Boolean": {
                        o = str == "1";
                    }
                    break;
                case "System.Decimal": {
                        decimal v;
#if JAVASCRIPT
                        if (!decimal.TryParse(str, System.Globalization.CultureInfo.InvariantCulture, out v))
                            throw new MessageValueException(key);
#else
                        if (!decimal.TryParse(str, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v))
                            throw new MessageValueException(key);
#endif
                        o = v;
                    }
                    break;

                case "System.DateTime": {
                        DateTime v;
                        if (!Helpers.DateFromISO8601(str, out v))
                            throw new MessageValueException(key);
                        o = v;
                    }
                    break;

                case "System.Byte[]": {
                        try {
                            o = Convert.FromBase64String(str);
                        } catch {
                            throw new MessageValueException(key);
                        }
                    }
                    break;

                case "CM.Schema.PrivateKey": {
                        PrivateKey v;
                        if (!PrivateKey.TryParse(str, out v))
                            throw new MessageValueException(key);
                        o = v;
                    }
                    break;

                case "CM.Schema.PayerStatus": {
                        Schema.PayerStatus v;
                        if (!Enum.TryParse(str, true, out v))
                            throw new MessageValueException(key);
                       o = v;
//                        // Bridge enumeration cast bug workaround
//#if JAVASCRIPT
//                       Bridge.Script.Write("return o;");
//#endif
                    }
                    break;

                case "CM.Schema.PayeeStatus": {
                        Schema.PayeeStatus v;
                        if (!Enum.TryParse(str, true, out v))
                            throw new MessageValueException(key);
                        o = v;
//                        // Bridge enumeration cast bug workaround
//#if JAVASCRIPT
//                       Bridge.Script.Write("return o;");
//#endif
                    }
                    break;
                default:
                    throw new ArgumentException(name + " is not supported for serialisation.");
            }
            return (T)o;
        }

        public void Set<T>(string key, T value) {
            var name = typeof(T).FullName;
#if JAVASCRIPT
            TranslateTypeName(ref name);
#endif
            switch (name) {
                case "System.Decimal":
                    Set(key, ((decimal)(object)value).ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture));
                    break;

                case "System.DateTime":
                    Set(key, Helpers.DateToISO8601((DateTime)(object)value));
                    break;

                case "System.Byte[]":
                    if (value != null)
                        Set(key, Convert.ToBase64String((byte[])(object)value));
                    break;
                case "System.String":
                case "System.UInt32":
                case "CM.Schema.PrivateKey":
                    if (value != null)
                        Set(key, value.ToString());
                    break;
                case "System.Boolean": {
                        if (value != null)
                            Set(key, ((bool)(object)value) ? "1" : "0");
                    }
                    break;
                // Bridge.NET hack for correct enumeration ToString()
                case "CM.Schema.PayerStatus":
                case "CM.Schema.PayeeStatus":
                    if (value != null) {
                        Set(key, Enum.GetName(typeof(T), value));
                    }
                    break;

                default:
                    throw new ArgumentException(name + " is not supported for serialisation.");
            }
        }

        public int Count { get { return _Values.Count; } }

        public string ToContentString() {
            var s = new StringBuilder();
            for (int i = 0; i < _Values.Count; i++) {
                if (!String.IsNullOrWhiteSpace(_Values[i].Value))
                    s.Append(_Values[i].ToString() + "\r\n");
            }
            return s.ToString();
        }

        public static NamedValueList FromContentString(string payload) {
            var ar = new NamedValueList();
            var lines = payload.Split('\n');
            for (int i = 0; i < lines.Length; i++) {
                ar.Append(lines[i].TrimEnd(Constants.NewLineChars));
            }
            return ar;
        }

        public class NamedValue {

            public string Name;
            public string Value;

            public NamedValue(string line) {
                if (line != null && line.IndexOf(':') > -1) {
                    line = line.Trim();
                    Name = line.Substring(0, line.IndexOf(':'));
                    Value = line.Substring(line.IndexOf(':') + 1).Trim();
                } else {
                    Name = line;
                    Value = null;
                }
            }

            public NamedValue(string n, string v) {
                Name = n;
                Value = v;
            }

            public override string ToString() {
                return Name != null ? Name + ": " + Value : String.Empty;
            }
        }
    }
}