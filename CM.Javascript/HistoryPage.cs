#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM.Javascript {

    /// <summary>
    /// A page for providing quick access to previously viewed accounts.
    /// </summary>
    internal class HistoryPage : Page {

        public override string Title {
            get {
                return SR.TITLE_HISTORY;
            }
        }

        public override string Url {
            get {
                return "/history";
            }
        }

        public override void Build() {
            Element.ClassName = "historypage";
            Element.H1(SR.TITLE_HISTORY);
            Element.Div(null, "");
            var ar = HistoryManager.Instance.History;
            if (ar.Length == 0) {
                Element.H4(SR.LABEL_HISTORY_NO_ITEMS);
            } else {
                for (int i = 0; i < ar.Length; i++) {
                    Element.Div("item").A(HtmlEncode(ar[i]), "/" + ar[i]);
                }
            }
        }
    }
}