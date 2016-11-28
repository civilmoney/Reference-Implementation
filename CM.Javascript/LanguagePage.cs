#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;

namespace CM.Javascript {

    /// <summary>
    /// Language selection page.
    /// </summary>
    internal class LanguagePage : Page {

        public override string Title {
            get {
                return SR.TITLE_CHOOSE_YOUR_LANGUAGE;
            }
        }

        public override string Url {
            get {
                return "/language";
            }
        }

        public override void Build() {
            Element.ClassName = "languagepage";
            Element.H1(SR.TITLE_CHOOSE_YOUR_LANGUAGE);

            foreach (var kp in SR.Langauges) {
                Element.Div().A(kp.Value, OnLanguage)["lan"] = kp.Key;
            }
            Element.H4(SR.LABEL_CHOOSE_YOUR_LANGUAGE);
        }

        private void OnLanguage(MouseEvent<HTMLAnchorElement> e) {
            App.Identity.SetLanguage(e.CurrentTarget["lan"] as string);
        }
    }
}