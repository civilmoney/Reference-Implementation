#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;

namespace CM.Javascript {

    /// <summary>
    /// This is the main landing page for the web browser client.
    /// </summary>
    internal class HomePage : Page {

        public override string Title {
            get {
                return SR.TITLE_CIVIL_MONEY;
            }
        }

        public override string Url {
            get {
                return "/";
            }
        }

        public override void Build() {
            Element.ClassName = "homepage";

            // Alternative option for a coloured logo. Kind of prefer the monochrome here.
            // string svgHtml = Bridge.Browser.IsIE ? "<object type=\"image/svg+xml\" data=\"/cmlogo.svg\" height=\"50\" /></object>"
            //   : "<img type=\"image/svg+xml\" src=\"/cmlogo.svg\" height=\"50\" />";

            Element.Div("center", Assets.SVG.Logo.ToString(200, 50, "#000000"));// svgHtml);
            //Element.Div("center", SR.LABEL_CIVIL_MONEY_SUB_HEADING);

            var search = Element.Div("search");
            var accc = new AccountInputBox(search, null, true);
            accc.Element.FirstChild.InsertBefore(new HTMLDivElement() {
                InnerHTML = Assets.SVG.Search.ToString(20, 20, "#ccc"),
                ClassName = "icon"
            }, accc.Element.FirstChild.FirstChild);

            Element.Div(null, SR.HTML_CIVIL_MONEY_PROVIDES);
            var buttons = Element.Div("buttons");
            buttons.Button(SR.LABEL_CREATE_MY_ACCOUNT, "/register");
            buttons.Span(" " + SR.LABEL_OR + " ");
            buttons.Button(SR.LABEL_LEARN_MORE, "/about");
        }
    }
}