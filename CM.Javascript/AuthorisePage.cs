#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;
using System;
using System.Collections.Generic;

namespace CM.Javascript {

    /// <summary>
    /// Used to sign/authorise a Transaction status change.
    /// </summary>
    internal class AuthorisePage : Page {
        private Schema.Account _Account;
        private HTMLDivElement _ButtonsRow;
        private HTMLDivElement _Form;
        private string _ID;
        private Feedback _MainFeedback;
        private int _NewStatus;
        private Page _Previous;
        private HTMLDivElement _ReturnButtons;
        private List<AuthItem> _ToSign;

        public AuthorisePage(string id, int newStatus, string[] multipleSummaries)
            : this(id, newStatus) {
            for (int i = 0; i < multipleSummaries.Length; i++) {
                _ToSign.Add(new AuthItem(multipleSummaries[i]));
            }
        }

        public AuthorisePage(string id, int newStatus, Schema.Transaction t)
             : this(id, newStatus) {
            _ToSign.Add(new AuthItem(t));
        }

        private AuthorisePage(string id, int newStatus) {
            _ToSign = new List<AuthItem>();
            _ID = id;
            _NewStatus = newStatus;
            _Previous = App.Identity.CurrentPage;
        }

        public override string Title {
            get {
                return _ID;
            }
        }

        public override string Url {
            get {
                return "/" + _ID;
            }
        }

        public override void Build() {
            Element.ClassName = "authorisepage";
            _MainFeedback = new Feedback(Element, big: true);
            _ReturnButtons = Element.Div();
            var serverStatus = Element.Div();

            _Form = Element.Div();
            _Form.Style.Display = Display.None;

            string title1 = null;
            string title2 = null;

            bool isPayee = _ID == _ToSign[0].Payee;
            if (_ToSign.Count == 1) {
                title2 = isPayee ? _ToSign[0].Payer : _ToSign[0].Payee;
            } else {
                title2 = "multiple transactions";
            }
            decimal total = 0;

            for (int i = 0; i < _ToSign.Count; i++) {
                total += _ToSign[i].Amount;
            }

            if (isPayee) {
                var s = (Schema.PayeeStatus)_NewStatus;
                switch (s) {
                    case Schema.PayeeStatus.Accept: title1 = "Accept money from"; break;
                    case Schema.PayeeStatus.Decline: title1 = "Decline money from"; break;
                    case Schema.PayeeStatus.Refund: title1 = "Refund money to"; break;
                }
            } else {
                var s = (Schema.PayerStatus)_NewStatus;
                switch (s) {
                    case Schema.PayerStatus.Accept: title1 = "Accept money for"; break;
                    case Schema.PayerStatus.Dispute: title1 = "Dispute money with"; break;
                    case Schema.PayerStatus.Cancel: title1 = "Cancel money with"; break;
                }
            }
            _Form.H1(title1 + " " + title2);

            for (int i = 0; i < _ToSign.Count; i++) {
                var row = _Form.Div("row");
                var left = row.Div("cell-half");
                var right = row.Div("cell-half");

                left.H3(Page.HtmlEncode(_ToSign[i].ID));
                left.H2("").Amount(_ToSign[i].Amount, prefix: Constants.Symbol);
                right.H3(Assets.SVG.Tag.ToString(16, 16, "#000000") + " " + SR.LABEL_TAG_ORDER_NO);

                _ToSign[i].TagBox = right.TextBox(""); // this will populate after load.
                _ToSign[i].TagBox.Placeholder = "(" + SR.LABEL_OPTIONAL + ")";
                _ToSign[i].TagBox.MaxLength = 48;
                _ToSign[i].ServerStatus = serverStatus.Div("statusvisual");
            }

            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default,
                SR.LABEL_STATUS_CONTACTING_NETWORK);
            var fa = new AsyncRequest<FindAccountRequest>() {
                Item = new FindAccountRequest(_ID),
                OnComplete = (a) => {
                    if (a.Result.Success) {
                        _Account = a.Item.Output;
                        // Load all signing transactions
                        int loaded = 0;
                        for (int i = 0; i < _ToSign.Count; i++) {
                            _ToSign[i].OnLoaded = (item, hr) => {
                                if (item.Trans == null) {
                                    // Couldn't load one of the transactions. User should cancel out.
                                    _MainFeedback.Set(Assets.SVG.Warning,
                                        FeedbackType.Error,
                                        SR.LABEL_STATUS_A_PROBLEM_OCCURRED
                                        + " [" + item.ID + "]: "
                                        + hr.GetLocalisedDescription());
                                } else {
                                    loaded++;
                                    // persist the tag(s)
                                    item.TagBox.Value = item.Payee == _ID ? item.Trans.PayeeTag : item.Trans.PayerTag;
                                }
                                if (loaded == _ToSign.Count) {
                                    // Everything loaded correctly. Proceed.
                                    _Form.Style.Display = Display.Block;
                                    _MainFeedback.Hide();
                                }
                            };
                            _ToSign[i].BeginLoad();
                        }
                    } else {
                        _MainFeedback.Set(Assets.SVG.Wait,
                            FeedbackType.Error,
                            SR.LABEL_STATUS_A_PROBLEM_OCCURRED + ": " + a.Result.GetLocalisedDescription());
                    }
                }
            };
            App.Identity.Client.TryFindAccount(fa);

            _Form.H3(SR.LABEL_SECURITY);

            var ch = _Form.Div("confirm").CheckBox(SR.HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS);
            var reminder = _Form.Div("reminder", SR.LABEL_CIVIL_MONEY_SECURITY_REMINDER);

            var passRow = _Form.Div("row");
            passRow.Style.Display = Display.None;
            passRow.H3(SR.LABEL_SECRET_PASS_PHRASE);
            var pass = passRow.Password();
            _ButtonsRow = Element.Div("button-row");
            var submit = _ButtonsRow.Button(SR.LABEL_CONTINUE, (e) => {
                _Form.Style.Display = Display.None;
                _ButtonsRow.Style.Display = Display.None;

                var sign = new AsyncRequest<Schema.DataSignRequest>();
                sign.Item = new Schema.DataSignRequest();
                for (int i = 0; i < _ToSign.Count; i++) {
                    var item = _ToSign[i];
                    item.ServerStatus.Clear();
                    var t = item.Trans;
                    if (isPayee) {
                        // payee side
                        t.PayeeTag = item.TagBox.Value;
                        t.PayeeStatus = (Schema.PayeeStatus)_NewStatus;
                        t.PayeeUpdatedUtc = DateTime.UtcNow;
                        if (t.PayeeRegion == null)
                            t.PayeeRegion = _Account.Iso31662Region;
                        item.Transform = new Schema.DataSignRequest.Transform(t.GetPayeeSigningData());
                    } else {
                        t.PayerTag = item.TagBox.Value;
                        t.PayerStatus = (Schema.PayerStatus)_NewStatus;
                        t.PayerUpdatedUtc = DateTime.UtcNow;
                        if (t.PayerRegion == null)
                            t.PayerRegion = _Account.Iso31662Region;
                        item.Transform = new Schema.DataSignRequest.Transform(t.GetPayerSigningData());
                    }
                    sign.Item.Transforms.Add(item.Transform);
                }
                sign.Item.Password = System.Text.Encoding.UTF8.GetBytes(pass.Value);
                sign.OnComplete = (req) => {
                    if (req.Result == CMResult.S_OK) {
                        _MainFeedback.Set(Assets.SVG.Wait,
                            FeedbackType.Default,
                            SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
                        int completed = 0;
                        for (int i = 0; i < _ToSign.Count; i++) {
                            var item = _ToSign[i];
                            if (isPayee)
                                item.Trans.PayeeSignature = item.Transform.Output;
                            else
                                item.Trans.PayerSignature = item.Transform.Output;
                            item.BeginCommit((sender, hr) => {
                                completed++;
                                if (completed == _ToSign.Count) {
                                    OnAllTransactionsCommitted();
                                }
                            });
                        }
                    } else {
                        _Form.Style.Display = Display.Block;
                        _ButtonsRow.Style.Display = Display.Block;
                        _MainFeedback.Set(Assets.SVG.Warning,
                            FeedbackType.Error,
                            SR.LABEL_STATUS_SIGNING_FAILED);
                    }
                };

                _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default,
                    SR.LABEL_STATUS_SIGNING_INFORMATION + " ...");

                _Account.SignData(sign, JSCryptoFunctions.Identity);
            });
            submit.Style.Display = Display.None;

            _ButtonsRow.Button(SR.LABEL_CANCEL, (e) => {
                App.Identity.CurrentPage = _Previous;
            });
            ch.OnChange = (e) => {
                passRow.Style.Display = ch.Checked ? Display.Block : Display.None;
                submit.Style.Display = ch.Checked ? Display.Inline : Display.None;
                reminder.Style.Display = ch.Checked ? Display.None : Display.Block;
                if (ch.Checked)
                    pass.Focus();
            };
            for (int i = 0; i < _ToSign.Count - 1; i++)
                _ToSign[i].TagBox.OnEnterKeySetFocus(_ToSign[i + 1].TagBox);
            //_ToSign[_ToSign.Count - 1].TagBox.OnEnterKeySetFocus(ch);
            pass.OnEnterKey(submit.Click);
        }

        public override void OnRemoved() {
            base.OnRemoved();
        }

        private void OnAllTransactionsCommitted() {
            AccountPage.Prefetched = null; // Balance will probably have changed

            // So what happened?
            bool didAllTransactionWork = true;
            bool didAllFail = true;
            for (int i = 0; i < _ToSign.Count; i++) {
                if (_ToSign[i].CommitStatus != CMResult.S_OK) {
                    didAllTransactionWork = false;
                } else {
                    didAllFail = false;
                    // Update any successful transactions that are on display.
                    if (_Previous is AccountPage) {
                        ((AccountPage)_Previous).OnTransactionChanged(_ToSign[i].Trans);
                    } else if (_Previous is TransactionPage) {
                        ((TransactionPage)_Previous).OnTransactionChanged(_ToSign[i].Trans);
                    }
                }
            }
            if (_ToSign.Count == 1) {
                // Only 1 transaction, simple..
                if (didAllTransactionWork) {
                    _MainFeedback.Set(Assets.SVG.CircleTick,
                        FeedbackType.Success,
                        SR.LABEL_STATUS_TRANSACTION_UPDATED_SUCCESSFULLY);
                    var options = _ReturnButtons.Div("button-row center");
                    options.Button(SR.LABEL_STATUS_OK, (x) => {
                        if (_Previous is AccountPage) {
                            ((AccountPage)_Previous).RefreshBalance();
                        }
                        App.Identity.CurrentPage = _Previous;
                    });
                } else {
                    _MainFeedback.Set(Assets.SVG.Warning,
                        FeedbackType.Error,
                   SR.LABEL_STATUS_A_PROBLEM_OCCURRED
                   + ": " + _ToSign[0].CommitStatus.GetLocalisedDescription());
                    _Form.Style.Display = Display.Block;
                    _ButtonsRow.Style.Display = Display.Block;
                }
            } else {
                // For a multi-transaction situation try to summarise
                // the outcome, but basically if there's a mix of OK/failed
                // commits, they need to start over.
                if (didAllFail) {
                    // In this case the password was probably wrong, or they were
                    // offline, we can let them retry.
                    _MainFeedback.Set(Assets.SVG.Warning,
                        FeedbackType.Error,
                        SR.LABEL_STATUS_NO_TRANSACTIONS_UPDATED);
                    _Form.Style.Display = Display.Block;
                    _ButtonsRow.Style.Display = Display.Block;
                } else {
                    if (didAllTransactionWork)
                        _MainFeedback.Set(Assets.SVG.CircleTick,
                            FeedbackType.Success,
                            SR.LABEL_STATUS_ALL_TRANSACTIONS_UPDATED);
                    else
                        _MainFeedback.Set(Assets.SVG.Warning,
                            FeedbackType.Error,
                            SR.LABEL_STATUS_SOME_TRANSACTIONS_FAILED);
                    var options = _ReturnButtons.Div("button-row center");
                    options.Button(SR.LABEL_STATUS_OK, (x) => {
                        if (_Previous is AccountPage) {
                            ((AccountPage)_Previous).RefreshBalance();
                        }
                        App.Identity.CurrentPage = _Previous;
                    });
                }
            }
        }

        private class AuthItem {

            public decimal Amount;

            public CMResult CommitStatus = CMResult.S_False;

            public string ID;

            public Action<AuthItem, CMResult> OnLoaded;

            public string Payee;

            public string Payer;

            public HTMLDivElement ServerStatus;

            public string Status;

            public HTMLInputElement TagBox;

            public Schema.Transaction Trans;

            public Schema.DataSignRequest.Transform Transform;

            public AuthItem(string summaryData) {
                var args = summaryData.Split(' ');
                ID = args[0] + " " + args[1] + " " + args[2];
                Payee = args[1];
                Payer = args[2];
                Amount = decimal.Parse(args[3]);
            }

            public AuthItem(Schema.Transaction t) {
                ID = t.ID;
                Payee = t.PayeeID;
                Payer = t.PayerID;
                Amount = t.Amount;
            }
            public void BeginCommit(Action<AuthItem, CMResult> onComplete) {
                var put = new AsyncRequest<PutRequest>() {
                    Item = new PutRequest(Trans),
                    OnProgress = (sender) => {
                        (sender as AsyncRequest<PutRequest>).Item.UpdateUIProgress();
                    },
                    OnComplete = (putRes) => {
                        putRes.Item.UpdateUIProgress();
                        CommitStatus = putRes.Result;
                        onComplete(this, putRes.Result);
                        putRes.Item.UI.SetMainGlyph(putRes.Result.Success ? Assets.SVG.CircleTick : Assets.SVG.CircleError);
                    }
                };
                ServerStatus.Clear();
                ServerStatus.H3(ID + ":");
                put.Item.UI = new ServerProgressIndicator(ServerStatus);
                put.Item.UI.SetMainGlyph(Assets.SVG.Wait);
                put.Item.UI.Show();
                App.Identity.Client.TryPut(put);
            }

            public void BeginLoad() {
                if (Trans != null) {
                    OnLoaded(this, CMResult.S_OK);
                    return;
                }
                Status = SR.LABEL_STATUS_CONTACTING_NETWORK;
                App.Identity.Client.TryFindTransaction(new AsyncRequest<FindTransactionRequest>() {
                    Item = new FindTransactionRequest(ID),
                    OnComplete = (r) => {
                        if (r.Result.Success) {
                            Status = null;
                            Trans = r.Item.Output;
                        } else {
                            Status = r.Result.GetLocalisedDescription();
                        }
                        OnLoaded(this, r.Result);
                    }
                });
            }
        }
    }
}