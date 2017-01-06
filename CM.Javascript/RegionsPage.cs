#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;
using System;
using System.Collections.Generic;

namespace CM.Javascript {

    /// <summary>
    /// Lists ISO31662 geographical regions and provides access to inverse-taxation reports.
    /// </summary>
    internal class RegionsPage : Page {
        private RegionalDataList _DataList;
        private string _Region;

        public RegionsPage(string region) {
            _Region = region;
        }

        public override string Title {
            get {
                return !String.IsNullOrEmpty(_Region) ? ISO31662.GetName(_Region) : SR.TITLE_REGIONS;
            }
        }

        public override string Url {
            get {
                return "/regions/" + _Region;
            }
        }

        public override void Build() {
            Element.ClassName = "regionspage";
            if (String.IsNullOrEmpty(_Region)) {
                // overview
                BuildOverview();
            } else {
                BuildRegionReport();
            }
        }

        public override void OnAdded() {
            Window.OnScroll = OnScroll;
        }

        public override void OnRemoved() {
            Window.OnScroll = null;
        }

        private void BuildOverview() {
            Element.H1(SR.TITLE_CIVIL_MONEY_REGIONS);
            Element.Div(null, SR.LABEL_REGIONS_INTRO);

            Element.H2(SR.TITLE_BROWSE_REGIONS);
            Element.Div(null, "");
            var nav = Element.Div("items");
            var r = new HTMLDivElement();

            int count = 0;
            HTMLDivElement country = null;
            string lastCountry = null;
            char lastLetter = '\0';
            List<HTMLElement> letters = new List<HTMLElement>();
            for (int i = 0; i < ISO31662.Values.Length; i++) {
                var reg = ISO31662.Values[i];
                var c = reg.Name;
                var region = c.Substring(c.LastIndexOf('/') + 1);
                c = c.Substring(0, c.LastIndexOf('/'));
                if (lastCountry != c) {
                    lastCountry = c;
                    if (lastLetter != c[0]) {
                        letters.Add(r.H1(c[0].ToString()));
                        lastLetter = c[0];
                    }

                    country = r.Div("country");
                    country.H3(HtmlEncode(c));
                    country = country.Div("items");

                    count++;
                    if (count == 4) {
                        // r = list.Div("row");
                        count = 0;
                    }
                }
                country.A(HtmlEncode(region), "/regions/" + reg.ID);
                country.Span(" ");
            }

            for (int i = 0; i < letters.Count; i++) {
                var let = letters[i];
                var a = nav.A(let.InnerHTML, JumpToLetter);
                nav.Span(" ");
                a["let"] = let;
            }

            Element.AppendChild(r);
        }

        private void BuildRegionReport() {
            var name = ISO31662.GetName(_Region);
            var div = Element.Div("report");
            if (name == null) {
                // bad link
                div.H1(SR.TITLE_NOT_FOUND);

                div.Div("", SR.LABEL_LINK_APPEARS_TO_BE_INVALID);
                return;
            }
            var parts = name.Split('/');
            div.H1(parts[1]);
            div.H2(parts[0]);

            var feedback = new Feedback(div);
            feedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);
            var revenueRow = div.Div("revenue");
            App.Identity.Client.QueryAuthoritiveServer(new AsyncRequest<Client.HttpRequest>() {
                Item = new Client.HttpRequest("/api/get-revenue/" + _Region),
                OnComplete = (e) => {
                    if (e.Result == CMResult.S_OK
                       && e.Item.Content != null) {
                        //{ "count":"0", "revenue":"0", "lastUpdatedUtc":"date" }
                        var data = JSON.Parse(e.Item.Content);
                        int count = int.Parse(data["count"].ToString());
                        decimal revenue = decimal.Parse(data["revenue"].ToString());
                        var date = Helpers.DateFromISO8601(data["lastUpdatedUtc"].ToString());
                        revenueRow.H4(SR.LABEL_TIME_LAST_UPDATED + " " + Helpers.DateToISO8601(date));
                        revenueRow.H2(SR.LABEL_RECENT_REVENUE + ":");
                        revenueRow.H1("").Amount(revenue, prefix: Constants.Symbol, roundTo2DP: true);

                        //if (revenue == 0)
                        //    div.Div("", String.Format("There is currently no revenue data for {0}.", parts[1]));
                        revenueRow.Div("", SR.LABEL_REVENUE_REPORT_HINT);
                        feedback.Hide();
                    } else {
                        feedback.Set(Assets.SVG.Warning, FeedbackType.Default,
                            SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER);
                    }
                }
            });
            var dataRow = div.Div("revenue-items list");
            _DataList = new RegionalDataList(dataRow, _Region);
            _DataList.UpdateResults();
        }

        private void JumpToLetter(MouseEvent<HTMLAnchorElement> e) {
            var el = e.Target["let"] as HTMLElement;
            el.ScrollTo();
        }
        private void OnScroll(Event e) {
            if (_DataList != null
                && _DataList.IsScrollAtBottom)
                _DataList.UpdateResults();
        }

        private class RegionalDataList {
            private Feedback _Feedback;
            private bool _IsQueryRunning;
            private int _PageSize;
            private string _Region;
            private HTMLDivElement _Results;
            private int _StartAt;
            private int _Total;
            public RegionalDataList(HTMLElement parent, string region) {
                _Results = parent.Div();
                _Feedback = new Feedback(parent);
                _Total = 0;
                _PageSize = 25;
                _Region = region;
            }

            public bool IsScrollAtBottom {
                get {
                    var bottom = _Results.Position().Y + _Results.ScrollHeight;
                    var windowBottom = Window.ScrollY + Window.InnerHeight;
                    return (bottom < windowBottom);
                }
            }

            public void UpdateResults(bool force = false) {
                if (_StartAt > _Total && !force)
                    return;
                if (_IsQueryRunning)
                    return;
                _IsQueryRunning = true;
                _Feedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);
                App.Identity.Client.QueryAuthoritiveServer(new AsyncRequest<Client.HttpRequest>() {
                    Item = new Client.HttpRequest("/api/get-revenue-data/"
                    + _Region + "?from=" + Helpers.DateToISO8601(DateTime.UtcNow.Date.AddMonths(-1))
                    + "&to=" + Helpers.DateToISO8601(DateTime.UtcNow.Date.AddDays(1))
                    + "&max=" + _PageSize
                    + "&startat=" + _StartAt),
                    OnComplete = (e) => {
                        _IsQueryRunning = false;

                        if (e.Result == CMResult.S_OK
                           && e.Item.Content != null) {
                            _Feedback.Hide();
                            //
                            var lines = e.Item.Content.Split('\n');
                            bool hadData = false;
                            for (int i = 0; i < lines.Length; i++) {
                                // {Updated Utc} (ID: {Created Utc} {Payee} {Payer}) {Revenue} {NT|OK}
                                var parts = lines[i].Trim().Split(' ');
                                if (parts.Length != 6
                                    || !Helpers.IsIDValid(parts[2])
                                    || !Helpers.IsIDValid(parts[3]))
                                    continue;
                                var d = new HTMLDivElement() {
                                    ClassName = "res"
                                };
                                var row = d.Div("summary").Div("hitregion");
                                row.Div();
                                row.Div(null, Helpers.DateFromISO8601(parts[0]).ToString("yyyy-MM-dd"));
                                var id = parts[1] + " " + parts[2] + " " + parts[3];
                                var url = "/" + id.Replace(" ", "+");
                                row.Div("link").A(
                                    (parts[5] == "OK"
                                    ? Assets.SVG.CircleTick.ToString(16, 16, "#288600")
                                    : Assets.SVG.CircleError.ToString(16, 16, "#cccccc"))
                                    + HtmlEncode(id), url);
                                row.Div().Amount(decimal.Parse(parts[4]), roundTo2DP: false);
                                _Results.AppendChild(d);
                                hadData = true;
                            }
                            _StartAt += _PageSize;
                            _Total = (hadData ? _StartAt + 1 : _Total);
                        } else {
                            _Feedback.Set(Assets.SVG.Warning, FeedbackType.Default,
                                SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER);
                        }
                    }
                });
            }
        }
    }
}