using CM.JS.Controls;
using CM.Schema;
using Retyped;
using SichboUI;
using System;
using System.Threading.Tasks;

namespace CM.JS.Screens {

    internal class AccountScreen : ScreenBase {

        private Account _Account;
        private AuditControl _Audit;
        private Element _Bottom;
        private int _DelayToken = 0;
        private PaymentLink _PayLink;
        private Element _Progress = null;
        private AsyncRequest<FindAccountRequest> _Search = null;
        private View _View;

        public AccountScreen(ScreenArgs args)
            : base(args) {
            var top = new StackPanel();
            top.HorizontalAlignment = Alignment.Stretch;

            var field = new Field(FieldType.Textbox, "",
                value: args.Url.Length > 0 ? args.Url[0] : "", placeholder: SR.LABEL_FIND_AN_ACCOUNT,
                border: 0);

            if (args.Url.Length > 1) {
                if (!Enum.TryParse<View>(args.Url[1], true, out _View)) {
                    if (PaymentLink.TryDecodeUrl(
                        Constants.TrustedSite + "/" + args.Url[0] + "/" + args.Url[1],
                        out _PayLink)) {
                        _View = View.Pay;
                    }
                }
            }

            field.VerticalAlignment = Alignment.Top;
            field.ValueElement.Html.style.fontSize = "3em";
            field.ValueElement.Html.style.fontWeight = "900";
            field.ValueElement.Html.spellcheck = false;
            field.ValueElement.Html.style.textAlign = "center";
            field.LabelElement.Html.style.fontWeight = "200";
            field.Html.style.borderRadius = "unset";
            field.Html.style.cursor = "text";

            field.ValueOrCheckedChanged += OnAccountChange;
            field.OKPressed += (e) => {
                // Enter key
                OnAccountChange(field);
            };
            _Progress = field.Div(className: "field-prog");
            _Progress.Height.Value = 1;
            _Progress.VerticalAlignment = Alignment.Bottom;
            _Progress.Width.Value = 0;
            _Progress.Width.OnState(ElementState.Adding, 1, 0, Times.Normal, Easing.CubicOut);
            _Progress.MinWidth.Value = 400;
            _Progress.IsWidthPercent = true;
            top.Add(field);

            Add(top);

            _Bottom = new Element();

            _Bottom.Margin.Value = new Thickness(160, 0, 0, 0);
            Add(_Bottom);

            OnAccountChange(field);
        }

        private enum View {
            Summary,
            Pay,
            Audit
        }
        private async void OnAccountChange(Field field) {
            var toke = ++_DelayToken;
            field.ClearError();
            await Task.Delay(500);
            if (_DelayToken != toke)
                return;
            if (_Search != null)
                _Search.IsCancelled = true;
            _Account = null;
            _Progress.Width.Animate(0, Times.Normal, Easing.CubicOut);
            _Progress.Class = "field-prog";
            if (String.IsNullOrEmpty(field.Value)) {
                ShowIntro();
                return;
            }

            if (!Helpers.IsIDValid(field.Value)) {
                field.SetError(SR.LABEL_ACCOUNT_NAME_INSTRUCTIONS);
                _Progress.Class = "field-prog not-found";
            } else {
                _Search = new AsyncRequest<FindAccountRequest>() {
                    Item = new FindAccountRequest(field.Value),
                    OnProgress = (sender) => {
                        _Progress.Width.Animate(sender.ProgressPercent / 100.0,
                            Times.Long, Easing.CubicOut);

                        OSD.Show(sender.ProgressPercent.ToString("N0") + "% " + SR.LABEL_STATUS_CONTACTING_NETWORK);
                    },
                    OnComplete = (sender) => {
                        var req = sender as AsyncRequest<FindAccountRequest>;
                        if (req != _Search || req.IsCancelled)
                            return; // stale search
                        _Progress.Width.Animate(1,
                            Times.Long, Easing.CubicOut);
                        if (req.Result == CMResult.S_OK) {
                            _Account = req.Item.Output.Cast<Schema.Account>();
                            _Account.AccountCalculations = new AccountCalculations(_Account);
                            OnAccountLoaded();
                            _Progress.Class = "field-prog ok";
                        } else {
                            _Progress.Width.Animate(0, Times.Long, Easing.ElasticOut1);
                            _Progress.Class = "field-prog not-found";
                            ShowAccountNotFound(field.Value);
                        }
                    }
                };
                OSD.Show(SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
                App.Instance.Client.TryFindAccount(_Search);
            }
        }

        private void OnAccountLoaded() {
            OSD.Clear();
            App.Instance.ShowBack();
            switch (_View) {
                case View.Audit:
                    ShowAudit();
                    break;

                case View.Summary:
                    ShowSummary();
                    break;

                case View.Pay:
                    ShowPay();
                    break;
            }
        }

        private void ShowAccountNotFound(string search) {
            _Bottom.Clear();
            OSD.Clear();
            _Bottom.VerticalAlignment = Alignment.Stretch;
            var st = new StackPanel();
            st.HorizontalAlignment = Alignment.Stretch;
            st.VerticalAlignment = Alignment.Center;
            st.Div(text: String.Format(SR.LABEL_STATUS_ACCOUNT_NOT_FOUND, search),
                style: "text-align:center;");
            _Bottom.Add(st);
        }

        private void ShowAudit() {
            App.Instance.UpdateHistory("/" + _Account.ID + "/audit", "Audit of " + _Account.ID);
            _Bottom.Clear();
            _Bottom.Add(new Button(ButtonStyle.BlackOutline, SR.LABEL_CANCEL, () => {
                ShowSummary();
            }) {
                HorizontalAlignment = Alignment.Center
            });
            _Audit = new AuditControl(
                Constants.PATH_ACCNT + "/" + _Account.ID + "/" + Constants.PATH_TRANS,
                showActions: true);
            _Bottom.Add(_Audit);
            _Audit.UpdateResults();
        }

        private void ShowIntro() {
            App.Instance.HideBack();
            _Bottom.Clear();
            _Bottom.VerticalAlignment = Alignment.Stretch;
            App.Instance.UpdateHistory("/", "Civil Money");
            var st = new StackPanel();
            st.HorizontalAlignment = Alignment.Center;
            st.VerticalAlignment = Alignment.Top;

            var logo = new Element(tagName: "h1", text: "//Civil Money", style: "font-weight: 900;letter-spacing:-2px;white-space:nowrap;");
            logo.AnimFlyIn(new SichboUI.RelativeTransform(0, 0, 0, 0, 0, 0, 0),
                dur: Times.Long, ease: Easing.ElasticOut1Cubic);
            logo.VerticalAlignment = Alignment.Center;
            logo.HorizontalAlignment = Alignment.Center;
            st.AnimFadeInOut(Times.Normal);
            logo.Margin.Value = new Thickness(15, 0, 5, 0);
            st.Add(logo);
            st.Div(
                text: SR.LABEL_CIVIL_MONEY_SUB_HEADING,
                style: "text-align:center;",
                margin: new Thickness(0, 0, 80, 0)).Width.Value = 200;

            var about = new Button(ButtonStyle.NotSet, SR.LABEL_LEARN_MORE, () => { });
            about.HorizontalAlignment = Alignment.Center;
            about.Html.As<dom.HTMLAnchorElement>().href = "/about";
            about.HrefMode = true;
            about.Html.style.color = Colors.DarkText;
            st.Add(about);
            var newAccount = new Button(ButtonStyle.NotSet, SR.LABEL_CREATE_MY_ACCOUNT, () => App.Instance.Navigate("/new-account"));
            newAccount.HorizontalAlignment = Alignment.Center;
            st.Add(newAccount);
            _Bottom.Add(st);
        }

        private void ShowPay() {
            _Bottom.Clear();
            _Bottom.VerticalAlignment = Alignment.Top;
            if (_PayLink == null) {
                App.Instance.UpdateHistory("/" + _Account.ID + "/pay", SR.LABEL_PAY_TO + " " + _Account.ID);
            } else {
                App.Instance.UpdateHistory(_PayLink.UrlPath, SR.LABEL_PAY_TO + " " + _Account.ID);
            }
            App.Instance.ShowBack(() => ShowSummary());
            var link = _PayLink ?? new PaymentLink();
            bool isReadonly = _PayLink != null;
            _PayLink = null;
            link.Payee = _Account.ID;

            var st = new StackPanel();
            st.HorizontalAlignment = Alignment.Stretch;

            Field memo = null;

            if (String.IsNullOrEmpty(link.Memo)) {
                memo = new Field(FieldType.Textbox, SR.LABEL_MEMO, link.Memo);
                memo.ValueElement.Html.As<dom.HTMLInputElement>().maxLength = 48;
                st.Add(memo);
            } else {
                st.Div(style: "text-align:center;padding:0 0 30px 0;", text: link.Memo);
            }

            Field amount = null;
            Element amountFeedback = null;
            if (String.IsNullOrEmpty(link.Amount)) {
                amount = new Field(FieldType.Amount, SR.LABEL_AMOUNT);
                amount.ValueElement.Html.style.fontSize = "3em";
                amount.ValueElement.Html.style.lineHeight = "1em";
                st.Add(amount);
            } else {
                var price = decimal.Parse(link.Amount);
                st.Div(style: "font-size:3em;text-align:center;").Html.Amount(price, Constants.Symbol);
                amountFeedback = st.Div(style: "padding:15px 0 30px 0;text-align:center;white-space: pre-wrap;",
                    text: Field.GetAmountFeedback(price));
            }

            var tag = new Field(FieldType.Textbox, SR.LABEL_TAG);
            tag.ValueElement.Html.As<dom.HTMLInputElement>().maxLength = 48;

            var from = new Field(FieldType.Account, SR.LABEL_PAY_FROM);
            var sig = new SignatureBox();

            Element qr = null;
            if (!isReadonly) {
                qr = st.Div(style: "text-align:center;");
                qr.MinHeight.Value = 260;
                qr.Width.Value = 0.5;
                qr.IsWidthPercent = true;
                qr.MinWidth.Value = 200;
            }
            st.Add(from);
            st.Add(tag);
            st.Add(sig);

            if (!isReadonly) {
                var toke = 0;
                var refreshQR = new Action(async () => {
                    qr.Html.style.opacity = "0.1";
                    var t = ++toke;
                    await Task.Delay(1000);
                    if (t != toke)
                        return;
                    qr.Html.Clear();
                    var url = link.ToString();
                    qr.Html.Div().innerHTML = QRCode.GenerateQRCode(url, 200, 200, "100%");
                    qr.Html.Div(style: "word-break: break-all;padding-bottom:15px;").A("bt-gray", text: "Copy link", onClick: (sender, v) => {
                        var el = new dom.HTMLTextAreaElement();
                        el.style.position = "fixed";
                        el.value = url;
                        el.readOnly = true;
                        dom.document.body.appendChild(el);
                        el.select();
                        if (dom.document.execCommand("copy")) {
                            OSD.Show("Link copied to clipboard.");
                            dom.setTimeout((o) => { OSD.Clear(); }, 2000);
                        } else {
                            dom.window.prompt("Press Ctrl + C to copy:", url);
                        }
                        el.RemoveEx();
                    }).target = "_blank";
                    qr.Html.style.opacity = "1";
                });
                refreshQR();
                memo.ValueOrCheckedChanged += (e) => {
                    link.Memo = e.Value;
                    link.IsMemoReadOnly = !String.IsNullOrEmpty(e.Value);
                    refreshQR();
                };
                amount.ValueOrCheckedChanged += (e) => {
                    link.Amount = e.AmountValue != null ? e.AmountValue.ToString() : null;
                    link.IsAmountReadOnly = link.Amount != null;
                    refreshQR();
                };
            }

            from.ValueOrCheckedChanged += (e) => {
                sig.Account = from.AccountValue;
                if (amount != null) {
                    amount.AmountAccount = from.AccountValue;
                } else {
                    amountFeedback.TextContent = Field.GetAmountFeedback(decimal.Parse(link.Amount), sig.Account);
                }
            };

            st.Add(new ButtonBar(SR.LABEL_CONTINUE, (b) => {
                var amountVal = amount == null ? decimal.Parse(link.Amount) : amount.AmountValue;
                if (amountVal == null) {
                    amount.SetError(SR.LABEL_THE_AMOUNT_IS_INVALID);
                    return;
                }

                var payer = from.AccountValue;
                if (payer == null) {
                    from.SetError(SR.LABEL_YOUR_ACCOUNT_NAME_IS_REQUIRED);
                    return;
                }

                var t = new Transaction {
                    APIVersion = Constants.APIVersion,
                    CreatedUtc = DateTime.UtcNow,
                    PayerID = payer.ID,
                    PayerRegion = payer.Iso31662Region,
                    PayerStatus = Schema.PayerStatus.Accept,
                    PayerTag = tag.Value,
                    PayeeID = _Account.ID,
                    Memo = memo == null ? link.Memo : memo.Value,
                    Amount = amountVal.Value
                };
                t.PayerUpdatedUtc = t.CreatedUtc;

                var prog = new ServerProgressIndicator();
                prog.Update(ServerProgressIndicatorStatus.Waiting, 0, SR.LABEL_STATUS_SIGNING_INFORMATION + " ...");
                prog.Show();

                payer.SignData(new AsyncRequest<Schema.DataSignRequest>() {
                    Item = new Schema.DataSignRequest(t.GetPayerSigningData()) {
                        PasswordOrRSAPrivateKey = sig.PasswordOrPrivateKey
                    },
                    OnComplete = (req) => {
                        if (req.Result == CMResult.S_OK) {
                            t.PayerSignature = req.Item.Transforms[0].Output;

                            prog.Update(ServerProgressIndicatorStatus.Waiting, 0,
                                SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");

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
                                        prog.Finished(ServerProgressIndicatorStatus.Success,
                                            SR.LABEL_STATUS_TRANSACTION_CREATED_SUCCESSFULLY,
                                            null,
                                            () => {
                                                App.Instance.Navigate("/" + payer.ID);
                                            });
                                    } else {
                                        prog.Finished(ServerProgressIndicatorStatus.Error,
                                           SR.LABEL_STATUS_A_PROBLEM_OCCURRED, putRes.Result.GetLocalisedDescription(),
                                           () => {
                                           });
                                    }
                                }
                            };

                            App.Instance.Client.TryPut(put);
                        } else {
                            prog.Finished(ServerProgressIndicatorStatus.Error,
                                 SR.LABEL_STATUS_A_PROBLEM_OCCURRED,
                                          SR.LABEL_STATUS_SIGNING_FAILED,
                                           () => {
                                           });
                        }
                    }
                }, JSCryptoFunctions.Identity);
            }, SR.LABEL_CANCEL, () => {
                ShowSummary();
            }));

            _Bottom.Add(st);
        }
        private void ShowSummary() {
            var a = _Account;
            _Bottom.Clear();
            _Bottom.VerticalAlignment = Alignment.Top;

            var st = new StackPanel();
            st.HorizontalAlignment = Alignment.Stretch;
            st.VerticalAlignment = Alignment.Top;
            App.Instance.UpdateHistory("/" + a.ID, a.ID);
            st.Div("account-info",
                style: "text-align:center; font-size: 1.25em;")
                .Html.AppendAccountInfo(a);

            st.Div(margin: new Thickness(15, 0), style: "text-align:center;")
                .Html.innerHTML = QRCode.GenerateQRCode(Constants.TrustedSite + a.ID, 256, 256, "256");
           
            st.Add(new Button(ButtonStyle.BigBlack, "Pay " + a.ID + " / " + SR.LABEL_POINT_OF_SALE, () => {
                ShowPay();
            }, margin: new Thickness(30, 0, 0, 0)) {
                HorizontalAlignment = Alignment.Center
            });

            st.Add(new Button(ButtonStyle.BigRed, "Audit", (b) => {
                b.Remove();
                _Audit = new AuditControl(
                    Constants.PATH_ACCNT + "/" + _Account.ID + "/" + Constants.PATH_TRANS,
                    showActions: true);
                st.Add(_Audit);
                _Audit.UpdateResults();
            }, margin: new Thickness(15, 0, 0, 0)) {
                HorizontalAlignment = Alignment.Center
            });

            st.Div(style: "text-align:center;padding-top:60px", text: "Own this account?");

            st.Add(new Button(ButtonStyle.BlackOutline, SR.LABEL_EDIT_ACCOUNT, () => {
                App.Instance.Navigate("/" + a.ID + "/edit");
            }, margin: new Thickness(15, 0, 60, 0)) {
                HorizontalAlignment = Alignment.Center
            });

            _Bottom.Add(st);
        }
    }
}