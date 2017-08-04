#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;
using System;
using System.Collections.Generic;
using System.Text;

namespace CM.Javascript {

    /// <summary>
    /// Displays an alert at the top of the page when a push notification has been received.
    /// </summary>
    internal class AlertUI {
        public static List<PeerNotifyArgs> RecentNotifications = new List<PeerNotifyArgs>();
        public HTMLDivElement Element;
        private HTMLElement _Count;
        private Dictionary<string, Info> _Dic;
        private HTMLButtonElement _Dismiss;
        private HTMLElement _Glyph;
        private bool _IsMinimised;
        private HTMLDivElement _Items;
        private int _LastCount;
        private HTMLButtonElement _Toggle;
        public AlertUI(Client client, HTMLDivElement parent) {
            _Dic = new Dictionary<string, Info>();
            Element = parent.Div("alerts");
            client.PeerNotifiesReceived += Client_PeerNotifiesReceived;

            _Toggle = Element.Button("", OnToggle);
            _Toggle.ClassName = "toggle";
            _Count = _Toggle.Span("");
            _Toggle.Span(SR.LABEL_NOTIFICATIONS);
            _Glyph = _Toggle.Span("");

            _Items = Element.Div("items");
            _Dismiss = Element.Button(SR.LABEL_DISMISS_ALL, (e) => {
                _Dic.Clear();
                _Items.Clear();
                Element.Style.Display = Display.None;
            });
            _IsMinimised = true;
            UpdateToggleButton();
        }

        public void Minimise() {
            _IsMinimised = true;
            UpdateToggleButton();
        }

        private void Client_PeerNotifiesReceived(PeerNotifyArgs arg) {
            Info info;
            if (!_Dic.TryGetValue(arg.Item.Path, out info)) {
                info = new Info(arg.Item.Path, this) {
                    Element = _Items.Div("item")
                };
                _Dic[arg.Item.Path] = info;
            }
            if (info.Copies.Count > 0
                && info.Copies[0].UpdatedUtc < arg.Item.UpdatedUtc) {
                // Newer update incoming
                info.Copies.Clear();
                info.Peers.Clear();
            }
            info.Copies.Add(arg.Item);
            for (int i = 0; i < arg.Peers.Count; i++) {
                if (!info.Peers.Contains(arg.Peers[i])) {
                    info.Peers.Add(arg.Peers[i]);
                }
            }

            info.Render();
            UpdateToggleButton();
            Element.Style.Display = Display.Block;

            RecentNotifications.Add(arg);
            while (RecentNotifications.Count > 50) {
                RecentNotifications.RemoveAt(0);
            }
        }

        private void Dismiss(string path) {
            Info info;
            if (_Dic.TryGetValue(path, out info)) {
                _Dic.Remove(path);
                info.Element.RemoveEx();
            }
            UpdateToggleButton();
            if (_Dic.Count == 0) {
                Element.Style.Display = Display.None;
            }
        }

        private void OnToggle(MouseEvent<HTMLButtonElement> arg) {
            _IsMinimised = !_IsMinimised;
            UpdateToggleButton();
        }

        private void UpdateToggleButton() {
            var s = new StringBuilder();
            if (_LastCount != _Dic.Count) {
                _Count.Clear();
                _Count.Span(_Dic.Count.ToString(), "count popin");
                _LastCount = _Dic.Count;
            }
            _Glyph.InnerHTML = (!_IsMinimised ? Assets.SVG.ArrowUp : Assets.SVG.ArrowDown).ToString(16, 16, "#000000");

            if (_IsMinimised) {
                _Items.Style.Display = Display.None;
                _Dismiss.Style.Display = Display.None;
            } else {
                _Items.Style.Display = Display.Block;
                _Dismiss.Style.Display = Display.Inline;
            }
        }

        private class Info {
            public List<IStorable> Copies = new List<IStorable>();
            public HTMLDivElement Element;
            public List<Peer> Peers = new List<Peer>();
            private AlertUI _Owner;
            private string _Path;
            private string _Url;

            public Info(string path, AlertUI owner) {
                _Path = path;
                _Owner = owner;
            }

            public void Render() {
                Element.Clear();

                var div = new HTMLDivElement();
                var inner = div.Div();
                inner.Div("close").Button(Assets.SVG.Close.ToString(16, 16, "#000000"), (e) => {
                    Dismiss();
                });
                var details = inner.Div("details");
                var numbers = inner.Div("numbers");
                if (Copies[0] is Schema.Account) {
                    var typedAr = new List<Schema.Account>();
                    for (int i = 0; i < Copies.Count; i++)
                        typedAr.Add((Schema.Account)Copies[i]);
                    Schema.Account item;

                    Helpers.CheckConsensus<Schema.Account>(typedAr, out item);
                    if (item != null) {
                        details.H2(String.Format(SR.LABEL_ALERT_ACCOUNT_BLANK_MODIFIED, Page.HtmlEncode(item.ID)));
                        details.H3((item.ConsensusCount >= Constants.MinimumNumberOfCopies ?
                            Assets.SVG.CircleTick.ToString(16, 16, Assets.SVG.STATUS_GREEN_COLOR)
                            : Assets.SVG.Warning.ToString(16, 16, "#cccccc"))
                            + " " + item.ConsensusCount.ToString() + " " + SR.LABEL_CONFIRMATIONS);
                        _Url = "/" + item.ID;
                    }
                } else if (Copies[0] is Schema.Transaction) {
                    var typedAr = new List<Schema.Transaction>();
                    for (int i = 0; i < Copies.Count; i++)
                        typedAr.Add((Schema.Transaction)Copies[i]);
                    Schema.Transaction item;
                    Helpers.CheckConsensus<Schema.Transaction>(typedAr, out item);
                    if (item != null) {
                        string title = "";
                        if (item.PayeeUpdatedUtc > item.PayerUpdatedUtc) {
                            title = item.PayeeID + " ";
                            switch (item.PayeeStatus) {
                                case Schema.PayeeStatus.Accept: title += SR.LABEL_ALERT_ACCEPTED_TRANSACTION.ToString().ToLower(); break;
                                case Schema.PayeeStatus.NotSet: break;
                                case Schema.PayeeStatus.Decline: title += SR.LABEL_ALERT_DECLINED_TRANSACTION.ToLower(); break;
                                case Schema.PayeeStatus.Refund: title += SR.LABEL_ALERT_REFUNDED_TRANSACTION.ToLower(); break;
                            }
                        } else {
                            title = item.PayerID + " ";
                            switch (item.PayerStatus) {
                                case Schema.PayerStatus.Accept: title += SR.LABEL_ALERT_SENT_TRANSACTION.ToString().ToLower(); break;
                                case Schema.PayerStatus.NotSet: title += SR.LABEL_ALERT_SENT_TRANSACTION.ToString().ToLower(); break;
                                case Schema.PayerStatus.Cancel: title += SR.LABEL_ALERT_CANCELLED_TRANSACTION.ToLower(); break;
                                case Schema.PayerStatus.Dispute: title += SR.LABEL_ALERT_DISPUTED_TRANSACTION.ToLower(); break;
                            }
                        }
                        details.H2(Page.HtmlEncode(title));
                        details.H3((item.ConsensusCount >= Constants.MinimumNumberOfCopies ?
                           Assets.SVG.CircleTick.ToString(16, 16, Assets.SVG.STATUS_GREEN_COLOR)
                           : Assets.SVG.Warning.ToString(16, 16, "#cccccc"))
                           + " " + item.ConsensusCount.ToString() + " " + SR.LABEL_CONFIRMATIONS);
                        numbers.H2("").Amount(item.Amount, prefix: Constants.Symbol, roundTo2DP: false);
                        var ti = new TransactionInfo(div, item, false);
                        ti.OnButtonClick = Dismiss;
                    } else {
                    }
                } else {
                    throw new NotImplementedException();
                }

                if (_Url != null) {
                    details.OnClick = OnClick;
                    numbers.OnClick = OnClick;
                    details.Style.Cursor = Cursor.Pointer;
                    numbers.Style.Cursor = Cursor.Pointer;
                }

                Element.AppendChild(div);
            }

            private void Dismiss() {
                _Owner.Dismiss(_Path);
                Element.RemoveEx();
            }

            private void OnClick(MouseEvent<HTMLDivElement> arg) {
                App.Identity.Navigate(_Url);
                _Owner.Minimise();
            }
        }
    }
}