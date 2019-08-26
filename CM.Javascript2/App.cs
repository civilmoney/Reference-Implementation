using Retyped;
using SichboUI;
using System;

namespace CM.JS {

    public class App : Element {

        public static App Instance;
        private Screen m_CurrentScreenType;
        private ScreenBase m_CurrentScreen;
        Controls.Button _Back;
        private Element m_Inner;
        internal Client Client;
        Action _OnBack;
        public void ShowBack(Action onBack=null) {
            _OnBack = onBack;
            _Back.Visible = true;
            if (_Back.Html.parentElement == null) {
                dom.document.body.appendChild(_Back.Html);
                _Back.Play();
                _Back.BringToFront();
            }
        }
        public void HideBack() {
            _Back.Remove();
            _OnBack = null; 
        }
        void ChromeBugWorkaround() {
          //  Html.style.overflow = "";
           //Html.style.overflow = "scroll";
        }
        public App() : base(style: "overflow-x:hidden;overflow-y:auto;") {
            Instance = this;

            Client = new Client();
            Client.PeerStateChanged += Client_PeerStateChanged;
            
            m_Inner = Div("innr");
            m_Inner.MaxWidth.Value = 1000;
            m_Inner.Margin.Value = new Thickness(30);
            m_Inner.SmallMargin.Value = new Thickness(15);
          

            _Back = new Controls.Button(Controls.ButtonStyle.NotSet, null, () => {
                if (_OnBack == null) {
                    Navigate("/");
                } else {
                    _OnBack.Invoke();
                    _OnBack = null;
                }
                if (dom.window.location.pathname == "/")
                    _Back.Remove();
            });

            _Back.Html.className = "bt-back";
            _Back.Width.Value = 48;
            _Back.Height.Value = 48;
            _Back.HorizontalAlignment = Alignment.Top;
            _Back.VerticalAlignment = Alignment.Bottom;
            _Back.Margin.Value = new Thickness(15);
            _Back.AnimFlyIn(new SichboUI.RelativeTransform(2, 2, 0, 0, 0, 0, 0),
                dur: Times.Long, ease: Easing.ElasticOut1);
            _Back.AnimFlyOut(new SichboUI.RelativeTransform(2, 2, 0, 0, 0, 0, 0), Times.Long,
                Easing.CubicBackIn);
            _Back.AnimFadeInOut(Times.Quick);


            MinHeight.Value = 1;
            IsMinHeightPercent = true;
            SmallWidth = 500;
            m_CurrentScreenType = Screen.Account;

            string lang = null;
            if (dom.window.localStorage != null)
                lang = dom.window.localStorage["language"].As<string>();

            if (lang == null
                && dom.navigator != null
                && dom.navigator.language != null
                && SR.Langauges.ContainsKey(dom.navigator.language.ToUpper())) {
                lang = dom.navigator.language.ToUpper();
            }

            SetLanguage(lang ?? "EN-GB");


            Retyped.dom.window.onpopstate = (e) => {
                if (_Modal != null)
                    CloseModal();
                Navigate(dom.window.location.pathname);
            };
          


        }
        Element _Modal;
        public void ShowModal(Element el) {
            m_Inner.Visible = false;
            _Modal = el;
            _Back.Visible = false;
            Add(_Modal);
            ChromeBugWorkaround();
        }
        public void CloseModal() {
            if (_Modal != null) {
                _Modal.Remove();
                _Modal = null;
            }
            _Back.Visible = true;
            m_Inner.Visible = true;
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
           // _NumPeers.InnerHTML = connected.ToString() +
           //     (connected > 1 ? Assets.SVG.CircleTick.ToString(10, 10, Assets.SVG.STATUS_GREEN_COLOR)
            //    : Assets.SVG.Warning.ToString(10, 10, "#cc0000"));
        }

        public void SetLanguage(string lang) {
            if (dom.window.localStorage != null)
                dom.window.localStorage["language"] = lang;
            SR.Load(lang, ReBuildUI);
        }

        void ReBuildUI() {
            Navigate(dom.window.location.pathname);
        }

        public string CurrentDOMUrl => Retyped.dom.window.location.pathname + Retyped.dom.window.location.search;

        [Bridge.ReadyAttribute]
        public static void Main() {
            Instance = new App();
            dom.document.body.querySelector("#loading").As<dom.HTMLElement>().RemoveEx();
            dom.document.body.appendChild(Instance.Html);
            Instance.Play();
        }

        public void Navigate(string rawUrl) {
            var url = rawUrl.Trim('/').Split('/');

            if (Enum.TryParse<Screen>(url[0].Replace("-",""), true, out var type)) {
                SetCurrentScreen(type, url);
            } else {
                // Does it look like a transaction?
                var id = url[0].Replace("+", " ").Replace("%20", " ");
                if (url.Length == 1
                    && Helpers.TryParseTransactionID(id,
                    out var utc, out var payee, out var payer)) {
                    SetCurrentScreen(Screen.TransactionScreen, url);
                } else 
                // Resolve/edit account
                if (url.Length == 2 && url[1] == "edit") {
                    SetCurrentScreen(Screen.NewAccount, url);
                } else {
                    SetCurrentScreen(Screen.Account, url);
                }
            }
        }

        public void UpdateHistory(string url, string title) {
            if (CurrentDOMUrl == url) {
                // no change, replace title
                Retyped.dom.window.history.replaceState(null, title);
            } else {
                Retyped.dom.window.history.pushState(null, title, url);
            }
            Retyped.dom.window.document.title = title;
        }
        public void AnchorNavigate(dom.HTMLAnchorElement sender, string url) {
            Navigate(url);
        }
        private void SetCurrentScreen(Screen s, string[] url) {
            if (m_CurrentScreen != null && m_CurrentScreenType == s) {
                if (m_CurrentScreen.TryHandleUrlChange(url)) {
                    return;
                }
            }
            m_Inner.Clear();
            m_Inner.Add(m_CurrentScreen = ScreenBase.Create(s, url));
            m_CurrentScreenType = s;
            ChromeBugWorkaround();
        }
    }
}