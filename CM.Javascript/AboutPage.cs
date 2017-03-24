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
            var boxes = new Box[] {
                new Box(
                    SR.HTML_ABOUT_1_1,
                    SR.HTML_ABOUT_1_2,
                    null,true),
                 new Box(
                    SR.HTML_ABOUT_2_1,
                    SR.HTML_ABOUT_2_2,
                    "work", true),
                 new Box(
                     SR.HTML_ABOUT_3_1,
                     SR.HTML_ABOUT_3_2,
                    "whatif", true),
                  new Box(
                    SR.HTML_ABOUT_4_1,
                    SR.HTML_ABOUT_4_2,
                    "ubi", true),
                 new Box(
                     SR.HTML_ABOUT_5_1,
                     SR.HTML_ABOUT_5_2,
                    "nobanks"),
                 new Box(
                     SR.HTML_ABOUT_6_1,
                     SR.HTML_ABOUT_6_2,
                    "nodebt"),
                    new Box(
                    SR.HTML_ABOUT_7_1,
                    SR.HTML_ABOUT_7_2,
                    "valuetime"),
                    new Box(
                    SR.HTML_ABOUT_8_1,
                    SR.HTML_ABOUT_8_2,
                    "demurrage"),
                    new Box(
                    SR.HTML_ABOUT_9_1,
                    SR.HTML_ABOUT_9_2,
                    "hardtimes"),
                  new Box(
                    SR.HTML_ABOUT_10_1,
                    SR.HTML_ABOUT_10_2,
                    "taxation"),
                   new Box(
                    SR.HTML_ABOUT_11_1,
                    SR.HTML_ABOUT_11_2,
                    "datadistribution", true),
                    new Box(
                    SR.HTML_ABOUT_12_1,
                    SR.HTML_ABOUT_12_2,
                    "voting"),
                  new Box(
                    SR.HTML_ABOUT_13_1,
                    SR.HTML_ABOUT_13_2,
                    "disputes", true),
                  new Box(
                    SR.TITLE_THE_CIVIL_MONEY_HONOUR_CODE+".",
                    SR.HTML_CIVIL_MONEY_HONOUR_CODE,
                    null),
                   new Box(
                    SR.HTML_ABOUT_14_1,
                    SR.HTML_ABOUT_14_2,
                    null),
                   new Box(
                    SR.HTML_ABOUT_15_1,
                    null,
                    null),
            };

            // IE isn't always scaling <img /> tags with svg correctly.
            string svgHtml = Bridge.Browser.IsIE ? "<object type=\"image/svg+xml\" data=\"/{0}.svg\" /></object>"
                : "<img type=\"image/svg+xml\" src=\"/{0}.svg\" />";

            HTMLDivElement finalParagraph = null;
            for (int i = 0; i < boxes.Length; i++) {
                var b = boxes[i];
                var block = Element.Div("block block-" + i);
                var row = block.Div("row");
                if (b.Icon != null && (i % 2) == 0 && !b.FullRowImage)
                    row.Div("cell-half").Div("pic", String.Format(svgHtml, b.Icon));
                var text = row.Div(b.Icon != null && !b.FullRowImage ? "cell-half" : "");
                text.H1(b.Title);
                if (b.Icon != null && b.FullRowImage)
                    text.Div("pic", String.Format(svgHtml, b.Icon));
                else if (b.Icon != null && (i % 2) != 0 && !b.FullRowImage)
                    row.Div("cell-half").Div("pic", String.Format(svgHtml, b.Icon));
                finalParagraph = block.Div("row");
                if (b.Paragraph != null)
                    finalParagraph.H2(b.Paragraph);
            }

            // finalParagraph register link, but with javascript navigation to
            // retain ReturnPath
            finalParagraph.H2("").A("https://civil.money/register", "/register");

        }

        private class Box {

            public bool FullRowImage;

            public string Icon;

            public string Paragraph;

            public string Title;

            public Box(string title, string para, string icon, bool fullImage = false) {
                Title = title;
                Paragraph = para;
                Icon = icon;
                FullRowImage = fullImage;
            }
        }
    }
}