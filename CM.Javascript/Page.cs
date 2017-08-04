#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;
using System;

namespace CM.Javascript {

    /// <summary>
    /// All pages inherit this. Provides a basic container for a root Element and
    /// any special CSS that the page might want.
    /// </summary>
    internal abstract class Page {

        /// <summary>
        /// The main root element for the page.
        /// </summary>
        public HTMLDivElement Element;

        public bool IsBuilt;

        private static HTMLDivElement _Encoder = new HTMLDivElement();

        public Page() {
            Element = new HTMLDivElement();
        }

        public abstract string Title { get; }

        public abstract string Url { get; }

        public static string EncodeAmount(decimal num, string prefix = "") {
            var amount = Math.Abs(num);
            var neg = num < 0 ? " - " : "";
            return "<span class=\"amount\"><span>"
                + prefix + "</span><span>"
                + neg + (amount - (amount % 1)).ToString("N0").Replace(",", SR.CHAR_THOUSAND_SEPERATOR)
                + "</span><span>" + (amount % 1).ToString(SR.CHAR_DECIMAL + "00")
                + "</span></span>";
        }

        /// <summary>
        /// Helper utility for HTML for safety. 100% of non-hard-coded text must use this.
        /// </summary>
        public static string HtmlEncode(string s) {
            if (s == null) return "";
            // Javascript is a single threaded environment, so this shared element is fine.
            _Encoder.TextContent = s;
            return _Encoder.InnerHTML.Replace("\"", "&quot;");
        }
        /// <summary>
        /// Called when the page is being opened.
        /// </summary>
        public abstract void Build();

        /// <summary>
        /// Called when the page has been added to the DOM
        /// </summary>
        public virtual void OnAdded() { }

        /// <summary>
        /// Called when the page is being removed (useful for cancelling tasks etc.)
        /// </summary>
        public virtual void OnRemoved() { }
    }
}