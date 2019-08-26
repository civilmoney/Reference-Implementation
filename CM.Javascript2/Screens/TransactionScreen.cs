using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SichboUI;
using CM.Schema;
using Retyped;

namespace CM.JS.Screens {
    class TransactionScreen : ScreenBase {
        string _ID;
        StackPanel _Stack;
        Transaction _Trans;
        Account _Payer;
        Account _Payee;
        string _PayeeID;
        string _PayerID;
        public TransactionScreen(ScreenArgs args)
            : base(args) {
            _ID = args.Url[0].Replace("+", " ").Replace("%20", " ");
            _Stack = new StackPanel();
            _Stack.HorizontalAlignment = Alignment.Stretch;
            _Stack.Margin.Value = new Thickness(0, 0, 60, 0);
          
            Add(_Stack);

            if (!Helpers.TryParseTransactionID(_ID, out var utc, out _PayeeID, out _PayerID)) {
                _Stack.Div("instructions", text: SR.LABEL_LINK_APPEARS_TO_BE_INVALID);
                App.Instance.UpdateHistory("/" + String.Join("/", args.Url), SR.TITLE_NOT_FOUND);
                _Stack.Add(new Controls.Button(Controls.ButtonStyle.BlackOutline, SR.TITLE_HOMEPAGE, (b) => {
                    App.Instance.Navigate("/");
                }, margin: new Thickness(15, 0, 0, 0)));
                return;
            }
            App.Instance.UpdateHistory("/" + _ID.Replace(" ", "+"), _ID);
            App.Instance.ShowBack(() => dom.window.history.back());
            Build();
        }
        void Build() {
            _Stack.Clear();
            OSD.Show(SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
            App.Instance.Client.TryFindTransaction(new AsyncRequest<FindTransactionRequest>() {
                Item = new FindTransactionRequest(_ID),
                OnComplete = (r) => {
                    if (r.Result.Success) {
                        _Trans = r.Item.Output;
                        UpdateDetails();
                    } else {
                        _Stack.Div("instructions", text: r.Result.GetLocalisedDescription());
                        _Stack.Add(new Controls.Button(Controls.ButtonStyle.BlackOutline, String.Format(SR.LABEL_GO_TO_ACCOUNT_BLANK, _PayerID), (b) => {
                            App.Instance.Navigate("/" + _PayerID);
                        }, margin: new Thickness(15, 0, 0, 0)));
                        _Stack.Add(new Controls.Button(Controls.ButtonStyle.BlackOutline, String.Format(SR.LABEL_GO_TO_ACCOUNT_BLANK, _PayeeID), (b) => {
                            App.Instance.Navigate("/" + _PayeeID);
                        }, margin: new Thickness(15, 0, 0, 0)));
                        OSD.Clear();
                    }
                }
            });
            App.Instance.Client.TryFindAccount(new AsyncRequest<FindAccountRequest>() {
                Item = new FindAccountRequest(_PayerID),
                OnComplete = (req) => {
                    if (req.Result == CMResult.S_OK) {
                        _Payer = req.Item.Output.Cast<Schema.Account>();
                        _Payer.AccountCalculations = new AccountCalculations(_Payer);
                        UpdateDetails();
                    } else {
                        _Payer = null;
                    }
                }
            });
            App.Instance.Client.TryFindAccount(new AsyncRequest<FindAccountRequest>() {
                Item = new FindAccountRequest(_PayeeID),
                OnComplete = (req) => {
                    if (req.Result == CMResult.S_OK) {
                        _Payee = req.Item.Output.Cast<Schema.Account>();
                        _Payee.AccountCalculations = new AccountCalculations(_Payee);
                        UpdateDetails();
                    } else {
                        _Payee = null;
                    }
                }
            });
        }
        void UpdateDetails() {
            var t = _Trans;

            if (t == null)
                return;

            OSD.Clear();

            _Stack.Clear();
            string status = "";
            string ico = "";
            switch (t.PayeeStatus) {
                case Schema.PayeeStatus.Accept:
                case Schema.PayeeStatus.NotSet:
                    switch (t.PayerStatus) {
                        case Schema.PayerStatus.NotSet: // Payer with NotSet is technically invalid
                        case Schema.PayerStatus.Accept: {
                                // use amount
                                if (t.PayeeStatus == Schema.PayeeStatus.NotSet)
                                    status = SR.LABEL_PAYEE_STATUS_NOTSET;
                            }
                            break;

                        case Schema.PayerStatus.Cancel:
                            status = SR.LABEL_PAYER_STATUS_CANCEL;
                            ico = "cancel";
                            break;

                        case Schema.PayerStatus.Dispute:
                            status = SR.LABEL_PAYER_STATUS_DISPUTE;
                            ico = "dispute";
                            break;
                    }
                    break;

                case Schema.PayeeStatus.Decline: status = SR.LABEL_PAYEE_STATUS_DECLINE; ico = "cancel"; break;
                case Schema.PayeeStatus.Refund: status = SR.LABEL_PAYEE_STATUS_REFUND; ico = "cancel"; break;
            }
            var div = _Stack.Div("trans-info").Html;
            div.Div("code").innerHTML = QRCode.GenerateQRCode(Constants.TrustedSite + t.ID.Replace(" ", "+"),
                128, 128, "128");
            div.Div("head").Amount(t.Amount, prefix: Constants.Symbol, roundTo2DP: true, status: status);
            div.Div("memo", text: t.Memo);

            string payerStatus = "";
            switch (t.PayerStatus) {
                case Schema.PayerStatus.Accept:
                    payerStatus = SR.LABEL_PAYER_STATUS_ACCEPT;
                    break;
                case Schema.PayerStatus.Cancel:
                    payerStatus = SR.LABEL_PAYER_STATUS_CANCEL;
                    break;
                case Schema.PayerStatus.NotSet:
                    payerStatus = SR.LABEL_PAYER_STATUS_NOTSET;
                    break;
                case Schema.PayerStatus.Dispute:
                    payerStatus = SR.LABEL_PAYER_STATUS_DISPUTE;
                    break;
            }
            string payeeStatus = "";
            switch (t.PayeeStatus) {
                case Schema.PayeeStatus.Accept:
                    payeeStatus = SR.LABEL_PAYEE_STATUS_ACCEPT;
                    break;
                default:
                case Schema.PayeeStatus.NotSet:
                    payeeStatus = SR.LABEL_PAYEE_STATUS_NOTSET;
                    break;
                case Schema.PayeeStatus.Refund:
                    payeeStatus = SR.LABEL_PAYEE_STATUS_REFUND;
                    break;
                case Schema.PayeeStatus.Decline:
                    payeeStatus = SR.LABEL_PAYEE_STATUS_DECLINE;
                    break;
            }

            var cell = div.Div("people");
            var left = cell.Div("left account-info");
            var middle = cell.Div("arrow ");
            var right = cell.Div("right account-info");

            left.Div("status", text: payerStatus);
            left.Div("id").A(text: _PayerID,
                url: "/" + _PayerID,
                onClick: App.Instance.AnchorNavigate);

            if (_Payer != null)
                left.AppendAccountInfo(_Payer);

            switch (t.PayerStatus) {
                case Schema.PayerStatus.Accept:
                    if (t.PayeeStatus == Schema.PayeeStatus.Accept) {
                        left.Div().Button("bt-gray", text: SR.LABEL_PAYER_STATUS_DISPUTE_VERB, onClick: (e) => {
                            ShowActionDialog(t.PayerID, (int)Schema.PayerStatus.Dispute);
                        });
                    } else if (t.PayeeStatus == Schema.PayeeStatus.NotSet) {
                        left.Div().Button("bt-gray", text: SR.LABEL_PAYER_STATUS_CANCEL_VERB, onClick: (e) => {
                            ShowActionDialog(t.PayerID, (int)Schema.PayerStatus.Cancel);
                        });
                    }
                    break;
            }
            left.Div().Button("bt-orange", text: "Audit", onClick: AuditPayer);

            right.Div("status", text: payeeStatus);
            right.Div("id").A(text: _PayeeID,
                url: "/" + _PayeeID,
                onClick: App.Instance.AnchorNavigate);

            if (_Payee != null)
                right.AppendAccountInfo(_Payee);

            switch (t.PayeeStatus) {
                case Schema.PayeeStatus.NotSet:
                    if (t.PayerStatus != Schema.PayerStatus.Cancel) {
                        right.Div().Button("bt-green", text: SR.LABEL_PAYEE_STATUS_ACCEPT_VERB, onClick: (e) => {
                            ShowActionDialog(t.PayeeID, (int)Schema.PayeeStatus.Accept);
                        });
                        right.Div().Button("bt-orange", text: SR.LABEL_PAYEE_STATUS_DECLINE_VERB, onClick: (e) => {
                            ShowActionDialog(t.PayeeID, (int)Schema.PayeeStatus.Decline);
                        });
                    }
                    break;

                case Schema.PayeeStatus.Accept:
                    right.Div().Button("bt-gray", text: SR.LABEL_PAYEE_STATUS_REFUND_VERB, onClick: (e) => {
                        ShowActionDialog(t.PayeeID, (int)Schema.PayeeStatus.Refund);
                    });
                    break;

                case Schema.PayeeStatus.Refund:
                case Schema.PayeeStatus.Decline:
                    break;
            }


        }

        void AuditPayer(dom.Event e) {
            var audit = new Controls.AuditControl(Constants.PATH_ACCNT + "/" + _PayerID + "/" + Constants.PATH_TRANS);
            audit.Margin.Value = new Thickness(30);
            var el = new Element();
            el.Div(style: "background:#fff;overflow-x:hidden;overflow-y:auto;",
             margin: new Thickness(0, 0, 60, 0)).Add(audit);
            el.Add(new Controls.Button(Controls.ButtonStyle.BlackOutline,
                SR.LABEL_CANCEL, (b) => {
                    App.Instance.CloseModal();
                }, margin: new Thickness(0, 30, 15, 0)) {
                HorizontalAlignment = Alignment.Right,
                VerticalAlignment = Alignment.Bottom
            });
            audit.UpdateResults();
            App.Instance.ShowModal(el);
        }

        void ShowActionDialog(string id, int status) {
            var el = new Element();
            var auth = new Controls.AuthoriseControl(id, status, (changed) => {
                if (changed) {
                    Build();
                }
                App.Instance.CloseModal();
            }, new TransactionIndex(_Trans));
            auth.Margin.Value = new Thickness(30);
            el.Div(style: "background:#fff;overflow-x:hidden;overflow-y:auto;").Add(auth);
            App.Instance.ShowModal(el);

        }

    }
}
