#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;
using System;

namespace CM.Javascript {

    /// <summary>
    /// The /about page is intended to provide a high-level overview of the Civil Money system for
    /// new visitors.
    /// </summary>
    internal class AboutPage : Page {

        public override string Title {
            get {
                return SR.TITLE_ABOUT;
            }
        }

        public override string Url {
            get {
                return "/about";
            }
        }

        public override void Build() {
            Element.ClassName = "presentationpage";
            Element.Div("", SR.HTML_ABOUT);
            var noAccount = Element.Div("noaccountfooter");
            noAccount.Div(null, SR.HTML_CIVIL_MONEY_PROVIDES);
            var buttons = noAccount.Div("buttons");
            buttons.Button(SR.LABEL_CREATE_MY_ACCOUNT, "/register", className: "blue-button");
            
        }
        
    }
}