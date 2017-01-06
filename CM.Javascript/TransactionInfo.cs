#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;
using CM.Schema;
using System;

namespace CM.Javascript {

    /// <summary>
    /// A UI component which display transaction information and buttons for performing actions.
    /// </summary>
    internal class TransactionInfo {
        public Action OnButtonClick;
        public Transaction Transaction;
        public TransactionInfo(HTMLElement parent, Transaction t,
            bool includeAmount, bool showPayerButtons = true) {
            Transaction = t;
            var container = parent.Div("transinfo");
            var idHolder = container.Div("id");// Page.HtmlEncode("ID: " + t.ID));
            var url = "/" + t.ID.Replace(" ", "+");
            idHolder.Span("ID: ");
            idHolder.A(t.ID, url).AddEventListener(EventType.Click, RaiseOnButtonClick);

            if (includeAmount) {
                container.H2("").Amount(t.Amount, prefix: Constants.Symbol, roundTo2DP: false);
            }
            if (!String.IsNullOrWhiteSpace(t.Memo))
                container.Div("memo", Page.HtmlEncode(t.Memo));
            var row = container.Div("row");

            var right = row.Div("cell-third qr-cell");
            right.Div(null, QRCode.GenerateQRCode(Constants.TrustedSite + url, 128, 128));

            var left = row.Div("cell-third");
            left.H4(SR.LABEL_PAY_FROM);

            left.Div().A(Page.HtmlEncode(t.PayerID), "/" + t.PayerID);
            string status = "";
            switch (t.PayerStatus) {
                case Schema.PayerStatus.Accept:
                    status = Assets.SVG.CircleTick.ToString(16, 16, "#288600") + " " + SR.LABEL_PAYER_STATUS_ACCEPT;
                    break;

                case Schema.PayerStatus.Cancel:
                    status = Assets.SVG.CircleRemove.ToString(16, 16, "#cccccc") + " " + SR.LABEL_PAYER_STATUS_CANCEL;
                    break;

                case Schema.PayerStatus.NotSet:
                    status = Assets.SVG.CircleUnknown.ToString(16, 16, "#cccccc") + " " + SR.LABEL_PAYER_STATUS_NOTSET;
                    break;

                case Schema.PayerStatus.Dispute:
                    status = Assets.SVG.Warning.ToString(16, 16, "#cc0000") + " " + SR.LABEL_PAYER_STATUS_DISPUTE;
                    break;
            }
            left.Div("status", status);
            left.Div("tag", !String.IsNullOrWhiteSpace(t.PayerTag) ? Assets.SVG.Tag.ToString(16, 16, "#cccccc") + " " + Page.HtmlEncode(t.PayerTag) : "");
            var butts = left.Div("button-row");
            if (showPayerButtons) {
                switch (t.PayerStatus) {
                    case Schema.PayerStatus.Accept:
                        if (t.PayeeStatus == Schema.PayeeStatus.Accept) {
                            butts.Button(SR.LABEL_PAYER_STATUS_DISPUTE_VERB, (e) => {
                                App.Identity.CurrentPage = new AuthorisePage(t.PayerID, (int)Schema.PayerStatus.Dispute, t);
                                RaiseOnButtonClick();
                            });
                        } else if (t.PayeeStatus == Schema.PayeeStatus.NotSet) {
                            butts.Button(SR.LABEL_PAYER_STATUS_CANCEL_VERB, (e) => {
                                App.Identity.CurrentPage = new AuthorisePage(t.PayerID, (int)Schema.PayerStatus.Cancel, t);
                                RaiseOnButtonClick();
                            });
                        }
                        break;
                }
            }
            var mid = row.Div("cell-third");
            mid.H4(SR.LABEL_PAY_TO);
            mid.Div().A(Page.HtmlEncode(t.PayeeID), "/" + t.PayeeID);
            status = "";
            switch (t.PayeeStatus) {
                case Schema.PayeeStatus.Accept:
                    status = Assets.SVG.CircleTick.ToString(16, 16, "#288600") + " " + SR.LABEL_PAYEE_STATUS_ACCEPT;
                    break;

                default:
                case Schema.PayeeStatus.NotSet:
                    status = Assets.SVG.CircleUnknown.ToString(16, 16, "#cccccc") + " " + SR.LABEL_PAYEE_STATUS_NOTSET;
                    break;

                case Schema.PayeeStatus.Refund:
                    status = Assets.SVG.Warning.ToString(16, 16, "#cccccc") + " " + SR.LABEL_PAYEE_STATUS_REFUND;
                    break;

                case Schema.PayeeStatus.Decline:
                    status = Assets.SVG.CircleError.ToString(16, 16, "#cc0000") + " " + SR.LABEL_PAYEE_STATUS_DECLINE;
                    break;
            }
            mid.Div("status", status);
            mid.Div("tag", !String.IsNullOrWhiteSpace(t.PayeeTag) ? Assets.SVG.Tag.ToString(16, 16, "#cccccc") + " " + Page.HtmlEncode(t.PayeeTag) : "");
            butts = mid.Div("button-row");
            switch (t.PayeeStatus) {
                case Schema.PayeeStatus.NotSet:
                    if (t.PayerStatus != Schema.PayerStatus.Cancel) {
                        butts.Button(SR.LABEL_PAYEE_STATUS_ACCEPT_VERB, (e) => {
                            App.Identity.CurrentPage = new AuthorisePage(t.PayeeID, (int)Schema.PayeeStatus.Accept, t);
                            RaiseOnButtonClick();
                        });
                        butts.Button(SR.LABEL_PAYEE_STATUS_DECLINE_VERB, (e) => {
                            App.Identity.CurrentPage = new AuthorisePage(t.PayeeID, (int)Schema.PayeeStatus.Decline, t);
                            RaiseOnButtonClick();
                        });
                    }
                    break;

                case Schema.PayeeStatus.Accept:
                    butts.Button(SR.LABEL_PAYEE_STATUS_REFUND_VERB, (e) => {
                        App.Identity.CurrentPage = new AuthorisePage(t.PayeeID, (int)Schema.PayeeStatus.Refund, t);
                        RaiseOnButtonClick();
                    });
                    break;

                case Schema.PayeeStatus.Refund:
                case Schema.PayeeStatus.Decline:
                    break;
            }
        }

        private void RaiseOnButtonClick() {
            if (OnButtonClick != null)
                OnButtonClick();
        }
    }
}