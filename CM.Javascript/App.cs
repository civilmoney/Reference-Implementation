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
    /// This is the main javascript entry point for the web client.
    /// </summary>
    internal class App {

        public static App Identity;

        public PopupBubble PopupBubble;

        internal Client Client;

        private AlertUI _Alerts;

        private string _CurrentHash;

        private Page _CurrentPage;

        private HTMLAnchorElement _Help;

        private HTMLAnchorElement _History;

        private HTMLDivElement _Holder;

        private HTMLAnchorElement _Home;

        private HTMLAnchorElement _Language;

        private HTMLDivElement _Menu;

        private HTMLElement _NumPeers;

        private HTMLAnchorElement _Regions;

        private HTMLDivElement _Root;

        private HTMLAnchorElement _Status;

        private HTMLAnchorElement _Voting;

        public App() {
            Client = new Client();
            Client.PeerStateChanged += Client_PeerStateChanged;
            //Window.OnHashChange = CheckLocation;
            Window.OnPopState = CheckLocation;

            string lang = null;
            if (Window.LocalStorage != null)
                lang = Window.LocalStorage.GetItem("language") as string;

            if (lang == null
                && Global.Navigator != null
                && Global.Navigator.Language != null
                && SR.Langauges.ContainsKey(Global.Navigator.Language.ToUpper())) {
                lang = Global.Navigator.Language.ToUpper();
            }

            SetLanguage(lang ?? "EN-GB");

            // var s = new System.Text.StringBuilder();
            // var dic = typeof(CMResult);
            // var keys = Type.GetOwnPropertyNames(dic);
            // foreach (string key in keys) {
            //     if (key.IndexOf("s_") == -1 && key.IndexOf("e_") == -1) continue;
            //     var v = ((CMResult)dic[key]);
            //     s.Append("|" + "0x" + ((uint)v.Code).ToString("X") + "|" + key + "|" + v.Description+"|\r\n");
            // }
            // Console.WriteLine(s.ToString());
        }

        public Page CurrentPage {
            get { return _CurrentPage; }
            set {
                ChangePage(value);
            }
        }

        public string CurrentPath {
            get { return _CurrentHash; }
        }

        [Ready]
        public static void Main() {
            Document.Head.AppendChild(new HTMLMetaElement() {
                Name = "viewport",
                Content = "width=device-width, initial-scale=1, maximum-scale=1.0, minimum-scale=1.0"
            });

            Identity = new App();
        }
        public void Navigate(string path) {
            var start = Window.Location.Protocol + "//" + Window.Location.Host;
            if (path.StartsWith(start)) {
                path = path.Substring(start.Length);
            }
            //if (!path.StartsWith(start)
            //    && start.IndexOf("://") > -1) {
            //    // Some off-site URL
            //    Window.Location.Assign(path);
            //    return;
            //}
            if (path.Length <= 1) {
                ChangePage(new HomePage());
            } else {
                var parts = path.Substring(1).Split('/');
                switch (parts[0]) {
                    case "register":
                        ChangePage(new RegisterPage());
                        break;

                    case "status":
                        ChangePage(new StatusPage());
                        break;

                    case "language":
                        ChangePage(new LanguagePage());
                        break;

                    case "history":
                        ChangePage(new HistoryPage());
                        break;

                    case "help":
                        ChangePage(new HelpPage());
                        break;

                    case "about":
                        ChangePage(new AboutPage());
                        break;

                    case "api":
                        ChangePage(new ApiPage());
                        break;

                    case "regions":
                        ChangePage(new RegionsPage(parts.Length == 2 ? parts[1] : String.Empty));
                        break;

                    case "vote":
                        ChangePage(new VotesPage(parts.Length == 2 ? parts[1] : String.Empty));
                        break;

                    default:

                        RegisterPage.ReturnPath = null;

                        // does it look like a transaction ID?
                        // yyyy-MM-ddTHH:mm:ss payer payee
                        var id = parts[0].Replace("+", " ").Replace("%20", " ");
                        DateTime utc;
                        string payee, payer;
                        if (parts.Length == 1
                            && Helpers.TryParseTransactionID(id, out utc, out payee, out payer)) {
                            ChangePage(new TransactionPage(id));
                        }
                        // assume it's an account, if valid
                        else if (Helpers.IsIDValid(parts[0])) {
                            if (parts.Length == 2) {
                                switch (parts[1]) {
                                    case "edit":
                                        ChangePage(new AccountEditPage(parts[0]));
                                        break;

                                    case "link":
                                        ChangePage(new PaymentLinkPage(parts[0]));
                                        break;

                                    default: // modified base64 payment links
                                    case "pay":
                                        ChangePage(new PaymentPage(parts[0], parts[1]));
                                        break;
                                }
                            } else {
                                ChangePage(new AccountPage(parts[0]));
                            }
                        } else {
                            // Page not found
                            ChangePage(new InvalidPage(path));
                        }
                        break;
                }
            }
        }

        public void SetLanguage(string lang) {
            if (Window.LocalStorage != null)
                Window.LocalStorage.SetItem("language", lang);
            SR.Load(lang, ReBuildUI);
        }

        private void ChangePage(Page newPage) {
            if (_CurrentPage != null) {
                _CurrentPage.Element.ParentElement.RemoveChild(_CurrentPage.Element);//.Remove();
                _CurrentPage.OnRemoved();
            }
            if (!newPage.IsBuilt) {
                newPage.Build();
                newPage.IsBuilt = true;
            }
            Window.History.PushState(null, newPage.Title, newPage.Url);
            Window.Document.Title = newPage.Title;
            _CurrentHash = Window.Location.PathName;
            _CurrentPage = newPage;
            _Holder.AppendChild(newPage.Element);
            newPage.OnAdded();
            CloseAllDialogs();
            HideNav();
            Window.ScrollTo(0, 0);

            for (int i = 0; i < _Menu.Children.Length; i++)
                _Menu.Children[i].RemoveClass("current");
            if (newPage is HomePage) {
                _Home.AddClass("current");
            } else if (newPage is StatusPage) {
                _Status.AddClass("current");
            } else if (newPage is RegionsPage) {
                _Regions.AddClass("current");
            } else if (newPage is LanguagePage) {
                _Language.AddClass("current");
            } else if (newPage is HistoryPage) {
                _History.AddClass("current");
            } else if (newPage is HelpPage) {
                _Help.AddClass("current");
            } else if (newPage is VotesPage) {
                _Voting.AddClass("current");
            }
        }

        private void CheckLocation(Event ev) {
            if (_CurrentHash == Window.Location.PathName)
                return;
            Navigate(Window.Location.PathName);
        }

        private void Client_PeerStateChanged(Peer arg) {
            int connected = 0;
            for (int i = 0; i < Client.Peers.Count; i++) {
                try {
                    if (Client.Peers[i].State == PeerState.Connected)
                        connected++;
                } catch {
                    // ignore potential IndexOfOutOfRange on informative feature
                }
            }
            _NumPeers.InnerHTML = connected.ToString() +
                (connected > 1 ? Assets.SVG.CircleTick.ToString(10, 10, "#288600")
                : Assets.SVG.Warning.ToString(10, 10, "#cc0000"));
        }
        private void CloseAllDialogs() {
        }

        private void HideNav() {
        }

        private void OnMenu(MouseEvent<HTMLAnchorElement> e) {
            if (_Menu.ContainsClass("shown")) {
                _Menu.RemoveClass("shown");
                _Holder.RemoveClass("menushown");
                _Alerts.Element.RemoveClass("menushown");
                if (Window.LocalStorage != null)
                    Window.LocalStorage.SetItem("showmenu", "0");
            } else {
                _Menu.AddClass("shown");
                _Holder.AddClass("menushown");
                _Alerts.Element.AddClass("menushown");
                if (Window.LocalStorage != null)
                    Window.LocalStorage.SetItem("showmenu", "1");
            }
        }

        private void ReBuildUI() {
            _CurrentHash = null; // reset navigation

            Document.Body.Clear();

            _Root = new HTMLDivElement() { ClassName = "cm" };
            PopupBubble = new PopupBubble(_Root);

            _Alerts = new AlertUI(Client, _Root);
            _Menu = _Root.Div("menu");
            _Holder = _Root.Div("main");

            var menuGlyphColour = "#222";
            _Menu.A(Assets.SVG.Hamburger.ToString(16, 16, menuGlyphColour), OnMenu, "expand");
            _Home = _Menu.A(Assets.SVG.Home.ToString(24, 24, menuGlyphColour), "/");
            _Home.Title = SR.TITLE_HOMEPAGE;
            _Home.Span(SR.TITLE_HOMEPAGE, "label");
            _History = _Menu.A(Assets.SVG.History.ToString(24, 24, menuGlyphColour), "/history");
            _History.Title = SR.TITLE_HISTORY;
            _History.Span(SR.TITLE_HISTORY, "label");
            _Regions = _Menu.A(Assets.SVG.Regions.ToString(24, 24, menuGlyphColour), "/regions");
            _Regions.Title = SR.TITLE_REGIONS;
            _Regions.Span(SR.TITLE_REGIONS, "label");
            _Voting = _Menu.A(Assets.SVG.Voting.ToString(24, 24, menuGlyphColour), "/vote");
            _Voting.Title = SR.TITLE_VOTING;
            _Voting.Span(SR.TITLE_VOTING, "label");

            _Help = _Menu.A(Assets.SVG.Support.ToString(24, 24, menuGlyphColour), "/help");
            _Help.Title = SR.TITLE_HELP;
            _Help.Span(SR.TITLE_HELP, "label");

            _Menu.Div("spacer");
            _Language = _Menu.A("<b>" + SR.CurrentLanguage.Replace("-", "<br/>") + "</b>", "/language", "lang");
            _Language.Title = SR.TITLE_CHOOSE_YOUR_LANGUAGE;
            _Language.Span(SR.Langauges[SR.CurrentLanguage], "label");
            _Status = _Menu.A(null, "/status", "status");
            _Status.Title = SR.TITLE_PEERS;
            _NumPeers = _Status.Span("0", "num-peers");
            _Status.Span(Assets.SVG.Peers.ToString(24, 24, menuGlyphColour));
            _Status.Span(SR.TITLE_PEERS, "label");

            Document.Title = SR.TITLE_CIVIL_MONEY;
            Document.Body.AppendChild(_Root);

            System.Threading.Tasks.Task.Run(() => { CheckLocation(null); });

            if (Window.LocalStorage != null) {
                var showmenu = Window.LocalStorage.GetItem("showmenu") as string;
                if ((showmenu == null && Window.InnerWidth > 1000) || showmenu == "1")
                    OnMenu(null);
            }
        }
    }
}