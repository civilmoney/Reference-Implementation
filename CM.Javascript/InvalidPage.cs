#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM.Javascript {

    /// <summary>
    /// For a 404 type of response.
    /// </summary>
    internal class InvalidPage : Page {
        private string _Path;

        public InvalidPage(string path) {
            _Path = path;
        }

        public override string Title {
            get {
                return SR.TITLE_NOT_FOUND;
            }
        }

        public override string Url {
            get {
                return _Path;
            }
        }

        public override void Build() {
            Element.ClassName = "notfoundpage";
            Element.H1(SR.TITLE_NOT_FOUND);
            Element.Div(null, SR.LABEL_LINK_APPEARS_TO_BE_INVALID);
        }
    }
}