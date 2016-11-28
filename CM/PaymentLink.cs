#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Text;

namespace CM {

    /// <summary>
    /// Describes a Civil Money Point of Sale payment link which
    /// can be used by customers to send money for a specific
    /// amount/description. A payment link is in the form of
    /// https://civil.money/{accountname}/{modified-base64-config}
    /// </summary>
    /// <remarks>
    /// For security only Payers can create new transactions,
    /// so this is to help facilitate a seamless POS.
    /// </remarks>
    public class PaymentLink {
        public string Payee;
        public string Amount;
        public string PayeeTag;
        public string Memo;
        public bool IsAmountReadOnly;
        public bool IsMemoReadOnly;

        private static string Escape(string v) {
            if (v == null) return v;
            return v.Replace("\n", "\\n");
        }

        private static string UnEscape(string v) {
            if (v == null) return v;
            return v.Replace("\\n", "\n");
        }

        /// <summary>
        /// Attempts to decode a payment URL.
        /// </summary>
        /// <param name="url">The full URL to decode, including host name</param>
        /// <param name="config">A pointer to receive the decoded configuration.</param>
        /// <returns>True if decoding was successful, otherwise false.</returns>
        public static bool TryDecodeUrl(string url, out PaymentLink config) {
            // https://civil.money/<accountname>/<modified-base64-config>
            config = null;
            if (!url.StartsWith(Constants.TrustedSite + "/"))
                return false;
            url = url.Substring(Constants.TrustedSite.Length + 1);
            var parts = url.Split('/');
            if (parts.Length != 2
                || parts[1].Length < 5
                || !Helpers.IsIDValid(parts[0]))
                return false;
            try {
                var b64 = parts[1].Replace("_", "/").Replace("-", "+").Replace("~", "=");
                var b = Convert.FromBase64String(b64);
                var payload = Encoding.UTF8.GetString(b, 0, b.Length);
                var ar = payload.Split('\n');
                if (ar.Length != 5)
                    return false;
                config = new PaymentLink();
                config.Payee = parts[0];
                config.Amount = UnEscape(ar[0]);
                config.PayeeTag = UnEscape(ar[1]);
                config.Memo = UnEscape(ar[2]);
                config.IsAmountReadOnly = ar[3] == "1";
                config.IsMemoReadOnly = ar[4] == "1";
                return true;
            } catch {
                return false;
            }
        }

        public override string ToString() {
            var b = Encoding.UTF8.GetBytes(String.Join("\n", new string[] {
                    Escape(Amount),
                    Escape(PayeeTag),
                    Escape(Memo),
                    IsAmountReadOnly?"1":"",
                    IsMemoReadOnly?"1":""
                }));
            var modifiedB64 = Convert.ToBase64String(b).Replace("/", "_").Replace("+", "-").Replace("=", "~");
            return Constants.TrustedSite + "/" + Payee + "/" + modifiedB64;
        }
    }
}