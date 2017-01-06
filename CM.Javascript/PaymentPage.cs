#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;
using System;

namespace CM.Javascript {

    /// <summary>
    /// A page for sending a new payment/transaction.
    /// </summary>
    internal class PaymentPage : Page {
        private HTMLInputElement _Amount;
        private Feedback _AmountFeedback;
        private string _Config;
        private HTMLInputElement _Description;
        private AccountInputBox _From;
        private PaymentLink _Link;
        private Feedback _MainFeedback;
        private string _Payee;
        private HTMLInputElement _Tag;
        private AccountInputBox _To;

        public PaymentPage(string to, string config) {
            _Payee = to;
            _Config = config;
            if (!String.IsNullOrEmpty(config)) {
                PaymentLink.TryDecodeUrl(
                    Constants.TrustedSite + "/" + to + "/" + config,
                    out _Link);
            }
            if (_Link == null)
                _Link = new PaymentLink();// just for less code
        }
        public override string Title {
            get {
                return _Payee + " " + SR.LABEL_MAKE_A_PAYMENT;
            }
        }

        public override string Url {
            get {
                return "/" + _Payee + "/" + (_Link != null ? _Config : "pay");
            }
        }

        public override void Build() {
            Element.ClassName = "paymentpage";
            _MainFeedback = new Feedback(Element, big: true);
            var returnButtons = Element.Div();
            var serverStatus = Element.Div("statusvisual");

            var form = Element.Div();
            //form.Div("logo", "<img src=\"/cmlogo.svg\" type=\"image/svg\">");
            form.H1(SR.LABEL_PAY_TO);
            _To = new AccountInputBox(form, _Payee);

            var row = form.Div("row");
            var left = row.Div("cell-half");
            var right = row.Div("cell-half");

            left.H3(SR.LABEL_AMOUNT);
            var box = left.Div("amountinputbox" + (_Link.IsAmountReadOnly ? "" : " focusable"));
            box.Span(Constants.Symbol);
            _Amount = box.TextBox(_Link.Amount);
            _Amount.Type = InputType.Number;
            if (_Link.IsAmountReadOnly)
                box.AddClass("readonly");
            _Amount.ReadOnly = _Link.IsAmountReadOnly;
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
            // _Amount.SetAttribute("speech", "");
            // _Amount.SetAttribute("required", "");
            // _Amount.SetAttribute("pattern", "[0-9]{4}"+SR.CHAR_DECIMAL+"[0-9]{0,6}");
            _Amount.OnFocus += (e) => {
                if (!_Amount.ReadOnly)
                    box.AddClass("focused-input");
            };
            _Amount.OnBlur += (e) => {
                box.RemoveClass("focused-input");
            };
            _AmountFeedback = new Feedback(left);

            if (_Link.IsMemoReadOnly && String.IsNullOrWhiteSpace(_Link.Memo)) {
                // readonly and empty = disabled..
                _Description = new HTMLInputElement() { Type = InputType.Text };
            } else {
                right.H3(SR.LABEL_MEMO);
                _Description = right.TextBox(_Link.Memo);
                _Description.Placeholder = "(" + SR.LABEL_OPTIONAL + ")";
                _Description.ReadOnly = _Link.IsMemoReadOnly;
                if (_Link.IsMemoReadOnly)
                    _Description.AddClass("readonly");
                _Description.MaxLength = 48;
            }

            row = form.Div("row");
            left = row.Div("cell-half");
            right = row.Div("cell-half");

            left.H3(SR.LABEL_PAY_FROM);
            _From = new AccountInputBox(left, watermark: SR.LABEL_YOUR_ACCOUNT_NAME);
            _From.OnAccountChanged = (a) => {
                OnShowAmountHint();
            };

            right = row.Div("cell-half");
            right.H3(SR.LABEL_DONT_HAVE_AN_ACCOUNT);
            right = right.Div("register");
            right.Button(SR.LABEL_CREATE_MY_ACCOUNT, (e) => {
                RegisterPage.ReturnPath = App.Identity.CurrentPath;
                App.Identity.Navigate("/register");
            });
            right.Span(" " + SR.LABEL_OR + " ");
            right.Button(SR.LABEL_LEARN_MORE, (e) => {
                RegisterPage.ReturnPath = App.Identity.CurrentPath;
                App.Identity.Navigate("/about");
            });

            row = form.Div("row");
            left = row.Div("cell-half");
            left.H3(Assets.SVG.Tag.ToString(16, 16, "#000000") + " " + SR.LABEL_TAG);
            _Tag = left.TextBox("");
            _Tag.Placeholder = "(" + SR.LABEL_OPTIONAL + ")";
            _Tag.MaxLength = 48;

            row = form.Div("row");
            row.H3(SR.LABEL_SECURITY);

            var ch = row.Div("confirm").CheckBox(SR.HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS);
            var reminder = row.Div("reminder", SR.LABEL_CIVIL_MONEY_SECURITY_REMINDER);
            if (!String.IsNullOrWhiteSpace(_Link.Amount)) {
                OnShowAmountHint();
            }

            var passAndSubmit = form.Div("row");
            passAndSubmit.Style.Display = Display.None;
            passAndSubmit.H3(SR.LABEL_SECRET_PASS_PHRASE);
            var pass = passAndSubmit.Password();
            var buttonsRow = form.Div("button-row");
            var submit = buttonsRow.Button(SR.LABEL_CONTINUE, (e) => {
                var amount = GetAmount();
                if (amount == null) {
                    _AmountFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_THE_AMOUNT_IS_INVALID);
                    _Amount.ScrollTo();
                    _Amount.Focus();
                    return;
                }
                if (_To.Account == null) {
                    _To.Element.ScrollTo();
                    _To.SetFeedbackIfNoneAlready(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_A_VALID_PAYEE_ACCOUNT_NAME_IS_REQUIRED);
                    return;
                }
                if (_From.Account == null) {
                    _From.SetFeedbackIfNoneAlready(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_YOUR_ACCOUNT_NAME_IS_REQUIRED);
                    _From.Element.ScrollTo();
                    _From.Input.Focus();
                    return;
                }

                var t = new Schema.Transaction();
                t.APIVersion = Constants.APIVersion;
                t.CreatedUtc = DateTime.UtcNow;
                t.PayerID = _From.Account.ID;
                t.PayerRegion = _From.Account.Iso31662Region;
                t.PayerStatus = Schema.PayerStatus.Accept;
                t.PayerTag = _Tag.Value;
                t.PayerUpdatedUtc = t.CreatedUtc;
                t.PayeeID = _To.Account.ID;
                t.PayeeTag = _Link.PayeeTag;
                t.Memo = _Description.Value;
                t.Amount = amount.Value;
                serverStatus.Clear();
                form.Style.Display = Display.None;
                buttonsRow.Style.Display = Display.None;

                _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default,
                    SR.LABEL_STATUS_SIGNING_INFORMATION + " ...");

                _From.Account.SignData(new AsyncRequest<Schema.DataSignRequest>() {
                    Item = new Schema.DataSignRequest(t.GetPayerSigningData()) {
                        Password = System.Text.Encoding.UTF8.GetBytes(pass.Value)
                    },
                    OnComplete = (req) => {
                        if (req.Result == CMResult.S_OK) {
                            t.PayerSignature = req.Item.Transforms[0].Output;

                            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default,
                                SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");

                            serverStatus.Clear();
                            var prog = new ServerProgressIndicator(serverStatus);
                            prog.SetMainGlyph(Assets.SVG.Wait);
                            prog.Show();
                            var put = new AsyncRequest<PutRequest>() {
                                Item = new PutRequest(t) {
                                    UI = prog
                                },
                                OnProgress = (sender) => {
                                    (sender as AsyncRequest<PutRequest>).Item.UpdateUIProgress();
                                },
                                OnComplete = (putRes) => {
                                    putRes.Item.UpdateUIProgress();
                                    if (putRes.Result == CMResult.S_OK) {
                                        AccountPage.Prefetched = null; // Balance will probably have changed

                                        _MainFeedback.Set(Assets.SVG.CircleTick, FeedbackType.Success,
                                            SR.LABEL_STATUS_TRANSACTION_CREATED_SUCCESSFULLY);

                                        var options = returnButtons.Div("button-row center");
                                        options.Button(SR.LABEL_GO_TO_YOUR_ACCOUNT, "/" + t.PayerID);
                                        options.Button(String.Format(SR.LABEL_GO_TO_ACCOUNT_BLANK, t.PayeeID), "/" + t.PayeeID);
                                        prog.SetMainGlyph(Assets.SVG.CircleTick);
                                    } else {
                                        _MainFeedback.Set(Assets.SVG.Warning,
                                            FeedbackType.Error,
                                            SR.LABEL_STATUS_A_PROBLEM_OCCURRED
                                            + ": " + putRes.Result.GetLocalisedDescription());

                                        form.Style.Display = Display.Block;
                                        buttonsRow.Style.Display = Display.Block;
                                        prog.SetMainGlyph(Assets.SVG.CircleError);
                                    }
                                }
                            };

                            App.Identity.Client.TryPut(put);
                        } else {
                            form.Style.Display = Display.Block;
                            buttonsRow.Style.Display = Display.Block;
                            _MainFeedback.Set(Assets.SVG.Warning, FeedbackType.Error,
                                SR.LABEL_STATUS_SIGNING_FAILED);
                        }
                    }
                }, JSCryptoFunctions.Identity);
            });
            submit.Style.Display = Display.None;

            buttonsRow.Button(SR.LABEL_CANCEL, "/" + _Payee);
            ch.OnChange = (e) => {
                passAndSubmit.Style.Display = ch.Checked ? Display.Block : Display.None;
                submit.Style.Display = ch.Checked ? Display.Inline : Display.None;
                reminder.Style.Display = ch.Checked ? Display.None : Display.Block;
                if (ch.Checked)
                    pass.Focus();
            };
            if (_Description.ReadOnly) {
                _Amount.OnEnterKeySetFocus(_From.Input);
            } else {
                _Amount.OnEnterKeySetFocus(_Description);
                _Description.OnEnterKeySetFocus(_From.Input);
            }
            _From.Input.OnEnterKeySetFocus(_Tag);
            // _Tag.OnEnterKeySetFocus(ch);
            pass.OnEnterKey(submit.Click);
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
                && amount >= Constants.MinimumTransactionAmount)
                return amount;
            return null;
        }

        private void OnShowAmountHint() {
            decimal amount = GetAmount().GetValueOrDefault();

            if (amount >= Constants.MinimumTransactionAmount) {
                var feedback = String.Format(SR.LABEL_AMOUNT_HINT,
                     System.Math.Round(amount * 50, 2).ToString("N2"),
                    System.Math.Round(amount / 1, 2).ToString("N2"));
                if (_From.Account != null
                    && _From.Account.AccountCalculations != null
                    && _From.Account.AccountCalculations.RecentCredits != null
                    && _From.Account.AccountCalculations.RecentDebits != null) {
                    //_AmountFeedback
                    decimal rep;
                    RecentReputation name;
                    var calcs = _From.Account.AccountCalculations;
                    Helpers.CalculateRecentReputation(calcs.RecentCredits.Value,
                        calcs.RecentDebits.Value + amount,
                        out rep, out name);
                    var balance = Helpers.CalculateAccountBalance(calcs.RecentCredits.Value, calcs.RecentDebits.Value + amount);

                    feedback += "\n" + String.Format(SR.LABEL_REMAINING_BALANCE_HINT, balance, name.ToLocalisedName() + " (" + rep.ToString() + ")");
                }
                _AmountFeedback.Set(Assets.SVG.Speech, FeedbackType.Default, feedback);
            } else {
                _AmountFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_THE_AMOUNT_IS_INVALID);
            }
        }
    }
}