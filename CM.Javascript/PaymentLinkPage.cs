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
    /// A "request for payment" page.
    /// </summary>
    internal class PaymentLinkPage : Page {
        private HTMLInputElement _Amount;
        private Feedback _AmountFeedback;
        private HTMLInputElement _Description;
        private HTMLDivElement _Form;
        private HTMLDivElement _Href;
        private string _ID;

        private PaymentLink _Link;

        private HTMLDivElement _POS;

        private HTMLButtonElement _POSButton;

        private HTMLInputElement _Tag;

        private Schema.Transaction _Trans;

        private HTMLDivElement _TransHolder;

        public PaymentLinkPage(string id) {
            _ID = id;
            _Link = new PaymentLink();
            _Link.Payee = id;
        }
        public override string Title {
            get {
                return _ID + " " + SR.LABEL_REQUEST_A_PAYMENT;
            }
        }

        public override string Url {
            get {
                return "/" + _ID + "/link";
            }
        }

        private bool IsLinkValid {
            get {
                return !_Link.IsAmountReadOnly || GetAmount() != null;
            }
        }

        public override void Build() {
            Element.ClassName = "paymentlinkpage";
            _Form = Element.Div();
            _POS = Element.Div("pos");
            _POS.Style.Display = Display.None;
            _Form.H1(String.Format(SR.LABEL_LINK_FOR_PAYMENT_TO, HtmlEncode(_ID)));
            var descRO = _Form.H3(SR.LABEL_MEMO).CheckBox(SR.LABEL_READONLY);
            descRO.Checked = _Link.IsMemoReadOnly = true;
            descRO.OnChange = (e) => {
                _Link.IsMemoReadOnly = descRO.Checked;
                OnLinkChanged();
            };
            _Description = _Form.TextBox("");
            _Description.Placeholder = "(" + SR.LABEL_OPTIONAL + ")";
            _Description.AddEventListener(EventType.KeyUp, (e) => {
                _Link.Memo = _Description.Value;
                OnLinkChanged();
            });

            var row = _Form.Div("row");
            var left = row.Div("cell-half");
            var right = row.Div("cell-half");

            var amountRO = left.H3(SR.LABEL_AMOUNT).CheckBox(SR.LABEL_READONLY);
            amountRO.Checked = _Link.IsAmountReadOnly = true;
            amountRO.OnChange = (e) => {
                _Link.IsAmountReadOnly = amountRO.Checked;
                OnLinkChanged();
            };

            var box = left.Div("amountinputbox focusable");
            box.Span(Constants.Symbol);
            _Amount = box.TextBox("");
            _Amount.Placeholder = "0" + SR.CHAR_DECIMAL + "00";
            _Amount.AddEventListener(EventType.KeyPress, (Event e) => {
                var ev = (KeyboardEvent)e;
                if (!char.IsDigit((char)ev.CharCode)
                    && !char.IsControl((char)ev.CharCode)
                    && SR.CHAR_DECIMAL.IndexOf((char)ev.CharCode) == -1
                    && SR.CHAR_THOUSAND_SEPERATOR.IndexOf((char)ev.CharCode) == -1) {
                    e.PreventDefault();
                    e.StopPropagation();
                }
            });
            _Amount.AddEventListener(EventType.KeyUp, OnShowAmountHint);
            _Amount.AddEventListener(EventType.Change, OnShowAmountHint);

            _AmountFeedback = new Feedback(left);

            right.H3(Assets.SVG.Tag.ToString(16, 16, "#000000") + " " + SR.LABEL_TAG);
            _Tag = right.TextBox("");
            _Tag.Placeholder = "(" + SR.LABEL_OPTIONAL + ")";
            _Tag.AddEventListener(EventType.KeyUp, (e) => {
                _Link.PayeeTag = _Tag.Value;
                OnLinkChanged();
            });
            row = _Form.Div("button-row");
            _POSButton = row.Button(SR.LABEL_POINT_OF_SALE, (e) => {
                ShowPOS();
            });
            _POSButton.Disabled = true;

            row.Button(SR.LABEL_PREVIEW, (e) => {
                Window.Open(_Link.ToString().Replace(Constants.TrustedSite, ""));
            });
            row.Button(SR.LABEL_CLEAR, (e) => {
                ResetForm();
            });
            row.Button(SR.LABEL_CANCEL, "/" + _ID);

            _Href = _Form.Div("linkoutput");

            OnLinkChanged();

            App.Identity.Client.Subscribe(_ID);
        }

        public override void OnAdded() {
            App.Identity.Client.PeerNotifiesReceived += Client_PeerNotifiesReceived;
            // Out link page may be removed/re-added numerous times. When we have a transaction
            // showing we need to keep its status/information up to date.
            if (_Trans != null) {
                var recent = AlertUI.RecentNotifications.ToArray();
                for (int i = 0; i < recent.Length; i++) {
                    if (_Trans.Path == recent[i].Item.Path) {
                        Client_PeerNotifiesReceived(recent[i]);
                    }
                }
            }
        }

        public override void OnRemoved() {
            App.Identity.Client.PeerNotifiesReceived -= Client_PeerNotifiesReceived;
        }
        private void Client_PeerNotifiesReceived(PeerNotifyArgs arg) {
            if (_POS.Style.Display != Display.Block)
                return;

            // We're looking out for new transactions having a matching tag and amount, or if
            // the transaction ID is established, any newer copies that might arrive
            var desiredAmount = GetAmount();
            if (arg.Item is Schema.Transaction) {
                var t = arg.Item as Schema.Transaction;
                if (_Trans == null || (_Trans.ID == t.ID && _Trans.UpdatedUtc < t.UpdatedUtc)) {
                    if (String.Compare(t.PayeeTag ?? String.Empty, _Link.PayeeTag ?? String.Empty) == 0
                    && (!_Link.IsAmountReadOnly || desiredAmount == null || desiredAmount.Value == t.Amount)
                    && (!_Link.IsMemoReadOnly || _Link.Memo == t.Memo)) {
                        _Trans = t;
                        _TransHolder.Clear();
                        _TransHolder.H2(SR.LABEL_MATCHING_TRANSACTION_RECEIVED_FROM);
                        new AccountInputBox(_TransHolder, t.PayerID, goGlyph: true);
                        new TransactionInfo(_TransHolder, t, true, showPayerButtons: false);
                        if (t.PayeeStatus != Schema.PayeeStatus.NotSet) {
                            _TransHolder.Div("details").Div("button-row").Button("Close", (e) => {
                                ResetForm();
                            });
                        }
                    }
                }
            }
        }

        private decimal? GetAmount() {
            var str = _Amount.Value.Replace(SR.CHAR_THOUSAND_SEPERATOR, "");
            // Bridge.NET decimal.TryParse quirk...

            if (str.IndexOf(SR.CHAR_DECIMAL) == -1)
                str += SR.CHAR_DECIMAL;
            if (str.EndsWith(SR.CHAR_DECIMAL))
                str += "0";
            decimal amount;
            if (decimal.TryParse(str, System.Globalization.CultureInfo.CurrentCulture, out amount)
                && amount >= Constants.MinimumTransactionAmount) {
                _POSButton.Disabled = false;
                return amount;
            }
            _POSButton.Disabled = true;
            return null;
        }

        private void OnLinkChanged() {
            var url = _Link.ToString();
            _Href.InnerHTML = IsLinkValid ? "<a href=\"" + url + "\" target=\"_blank\">" + url + "</a>" : "";
        }

        private void OnShowAmountHint() {
            decimal amount = GetAmount().GetValueOrDefault();
            if (amount >= Constants.MinimumTransactionAmount) {
                var feedback = String.Format(SR.LABEL_AMOUNT_HINT,
                     System.Math.Round(amount * 50, 2).ToString("N2"),
                    System.Math.Round(amount / 1, 2).ToString("N2"));
                _AmountFeedback.Set(Assets.SVG.Speech, FeedbackType.Default, feedback);
                _Link.Amount = amount.ToString();
            } else {
                _Link.Amount = "";
                _AmountFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_THE_AMOUNT_IS_INVALID);
            }
            OnLinkChanged();
        }

        private void ResetForm() {
            _POS.Style.Display = Display.None;
            _Form.Style.Display = Display.Block;
            _Tag.Value = "";
            _Amount.Value = "";
            _Description.Value = "";
            _AmountFeedback.Hide();
            _POSButton.Disabled = true;
            _Href.InnerHTML = "";
        }

        private void ShowPOS() {
            _POS.Clear();
            var url = _Link.ToString();
            _TransHolder = _POS.Div();
            var amount = GetAmount();
            var div = _TransHolder.Div("details");
            div.H1(SR.TITLE_PLEASE_PAY);
            div.H2(Page.EncodeAmount(amount.Value, prefix: Constants.Symbol));
            if (!String.IsNullOrWhiteSpace(_Link.Memo))
                div.Div("memo", Page.HtmlEncode(_Link.Memo));
            if (!String.IsNullOrWhiteSpace(_Link.PayeeTag))
                div.Div("tag", Assets.SVG.Tag.ToString(16, 16, "#cccccc") + " " + Page.HtmlEncode(_Link.PayeeTag));

            div.Div("center", QRCode.GenerateQRCode(url, 256, 256));
            div.Div("center", "<a href=\"" + url + "\" target=\"_blank\">" + url + "</a>");
            div.Div("button-row").Button(SR.LABEL_CANCEL, (e) => {
                _POS.Style.Display = Display.None;
                _Form.Style.Display = Display.Block;
            });

            _Form.Style.Display = Display.None;
            _POS.Style.Display = Display.Block;
        }
    }
}