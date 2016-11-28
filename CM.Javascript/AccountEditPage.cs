#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;
using CM.Schema;
using System;
using System.Collections.Generic;

namespace CM.Javascript {

    /// <summary>
    /// Page for editing an account. Provides basic functionality for editing all CM.Account fields
    /// and committing a newly signed account update to the network.
    /// </summary>
    internal class AccountEditPage : Page {
        private HTMLDivElement _Form;
        private string _ID;
        private Feedback _MainFeedback;
        private List<NotifyEditor> _Notifications;
        private HTMLDivElement _ReturnButtons;
        private HTMLDivElement _ServerStatus;
        private List<SkillEditor> _Skills;
        private Account Account;

        public AccountEditPage(string id) {
            _ID = id;
            _Skills = new List<SkillEditor>();
            _Notifications = new List<NotifyEditor>();
        }

        public override string Title {
            get {
                return _ID;
            }
        }

        public override string Url {
            get {
                return "/" + _ID + "/edit";
            }
        }

        public override void Build() {
            Element.ClassName = "accounteditpage";
            Element.H1(SR.TITLE_ACCOUNT_SETTINGS);

            _MainFeedback = new Feedback(Element, big: true);
            _ReturnButtons = Element.Div();
            _ServerStatus = Element.Div("statusvisual");
            _Form = Element.Div();
            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);

            var search = new AsyncRequest<FindAccountRequest>() {
                Item = new FindAccountRequest(_ID)
            };

            search.OnComplete = (sender) => {
                var req = sender as AsyncRequest<FindAccountRequest>;

                if (req.Result == CMResult.S_OK) {
                    var a = req.Item.Output.Cast<Schema.Account>();
                    Account = a;
                    BuildForm();
                    _MainFeedback.Hide();
                } else if (req.Result == CMResult.E_Item_Not_Found) {
                    _MainFeedback.Set(Assets.SVG.Warning, FeedbackType.Default,
                        String.Format(SR.LABEL_STATUS_ACCOUNT_NOT_FOUND, Page.HtmlEncode(_ID)),
                        SR.LABEL_RETRY, () => {
                            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);

                            App.Identity.Client.TryFindAccount(search);
                        });
                } else {
                    // Some other error, network probably
                    _MainFeedback.Set(Assets.SVG.Warning, FeedbackType.Default,
                        SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER + " " + req.Result.GetLocalisedDescription(),
                        SR.LABEL_RETRY, () => {
                            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);

                            App.Identity.Client.TryFindAccount(search);
                        });
                }
            };

            App.Identity.Client.TryFindAccount(search);
        }

        private void BuildForm() {
            _Form.Div("hint", SR.LABEL_ACCOUNT_SETTINGS_INTRO);
            _Form.H3(SR.LABEL_REGION);

            var country = _Form.Select();
            var region = _Form.Select();
            var _RegionFeedback = new Feedback(_Form);

            country.AppendChild(new HTMLOptionElement() { Value = "", InnerHTML = "(" + SR.LABEL_PLEASE_SELECT + ")" });
            string lastCountry = null;
            foreach (var kp in ISO31662.Values) {
                var s = kp.Name.Split('/')[0];
                if (lastCountry != s) {
                    lastCountry = s;
                    country.AppendChild(new HTMLOptionElement() { Value = s, InnerHTML = s });
                }
            }

            country.OnChange += (e) => {
                region.Clear();
                if (country.Value.Length > 0) {
                    region.Style.Display = Display.Block;
                    var s = country.Value + "/";
                    foreach (var kp in ISO31662.Values) {
                        if (kp.Name.StartsWith(s)) {
                            var reg = kp.Name.Split('/')[1];
                            region.AppendChild(new HTMLOptionElement() { Value = kp.ID, InnerHTML = reg });
                        }
                    }
                } else {
                    region.Style.Display = Display.None;
                }
            };

            region.OnChange += (e) => {
                if (ISO31662.GetName(region.Value) != null)
                    _RegionFeedback.Hide();
            };
            country.Value = ISO31662.GetName(Account.Iso31662Region).Split('/')[0];
            country.OnChange(null);
            region.Value = Account.Iso31662Region;

            var atts = Account.CollectAttributes();
            _Form.H3(SR.LABEL_INCOME_ELIGIBILITY);
            _Form.Div("hint", SR.LABEL_INCOME_ELIGIBILITY_INTRO);
            var rdoName = "income";
            var rdoWorking = _Form.Div("radio").RadioButton(rdoName, SR.LABEL_INCOME_ELIGIBILITY_WORKING);
            var rdoLooking = _Form.Div("radio").RadioButton(rdoName, SR.LABEL_INCOME_ELIGIBILITY_LOOKING_FOR_WORK);
            var rdoHealthProblem = _Form.Div("radio").RadioButton(rdoName, SR.LABEL_INCOME_ELIGIBILITY_HEALTH_PROBLEM);
            var rdoRetired = _Form.Div("radio").RadioButton(rdoName, SR.LABEL_INCOME_ELIGIBILITY_RETIRED);
            var rdoNotSet = _Form.Div("radio").RadioButton(rdoName, SR.LABEL_VALUE_NOT_SET);

            switch (atts[AccountAttributes.IncomeEligibility_Key]) {
                default: rdoNotSet.Checked = true; break;
                case AccountAttributes.IncomeEligibility_Working: rdoWorking.Checked = true; break;
                case AccountAttributes.IncomeEligibility_LookingForWork: rdoLooking.Checked = true; break;
                case AccountAttributes.IncomeEligibility_HealthProblem: rdoHealthProblem.Checked = true; break;
                case AccountAttributes.IncomeEligibility_Retired: rdoRetired.Checked = true; break;
            }

            _Form.H3(SR.LABEL_SKILLS_AND_SERVICES);
            _Form.Div("hint", SR.LABEL_SKILLS_AND_SERVICES_INTRO);
            var skillHolder = _Form.Div();
            for (int i = 0; i < atts.Count; i++) {
                var a = atts[i];
                if (a.Name == AccountAttributes.SkillOrService_Key
                    && !String.IsNullOrEmpty(a.Value)) {
                    _Skills.Add(new SkillEditor(skillHolder, new AccountAttributes.SkillCsv(a.Value)));
                }
            }
            _Form.Div("button-row").Button(SR.LABEL_ADD_ANOTHER_ITEM, (e) => {
                _Skills.Add(new SkillEditor(skillHolder, new AccountAttributes.SkillCsv()));
            });
            if (_Skills.Count == 0) {
                _Skills.Add(new SkillEditor(skillHolder, new AccountAttributes.SkillCsv()));
            }

            _Form.H3(SR.LABEL_PUSH_NOTIFICATIONS);
            _Form.Div("hint", SR.LABEL_PUSH_NOTIFICATIONS_INTRO);
            var notifyHolder = _Form.Div();
            for (int i = 0; i < atts.Count; i++) {
                var a = atts[i];
                if (a.Name == AccountAttributes.PushNotification_Key
                    && !String.IsNullOrEmpty(a.Value)) {
                    _Notifications.Add(new NotifyEditor(notifyHolder, new AccountAttributes.PushNotificationCsv(a.Value)));
                }
            }
            _Form.Div("button-row").Button(SR.LABEL_ADD_ANOTHER_ITEM, (e) => {
                _Notifications.Add(new NotifyEditor(notifyHolder, new AccountAttributes.PushNotificationCsv()));
            });
            if (_Notifications.Count == 0) {
                _Notifications.Add(new NotifyEditor(notifyHolder, new AccountAttributes.PushNotificationCsv()));
            }

            var changePass = _Form.Div("changepass").CheckBox(SR.LABEL_CHANGE_MY_PASS_PHRASE);
            var newPassFields = _Form.Div();
            newPassFields.Style.Display = Display.None;
            changePass.OnChange = (e) => {
                newPassFields.Style.Display = changePass.Checked ? Display.Block : Display.None;
            };
            var pass1 = newPassFields.Password();
            newPassFields.H3(SR.LABEL_REENTER_PASS_PHRASE);
            var pass2 = newPassFields.Password();
            var pass2Feedback = new Feedback(newPassFields);

            _Form.H3(SR.LABEL_SECURITY);
            _Form.Div("reminder", SR.LABEL_CIVIL_MONEY_SECURITY_REMINDER);
            var ch = _Form.Div("confirm").CheckBox(SR.HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS);

            var passAndSubmit = _Form.Div("row");
            passAndSubmit.Style.Display = Display.None;
            passAndSubmit.H3(SR.LABEL_SECRET_PASS_PHRASE);
            var pass = passAndSubmit.Password();
            var buttonsRow = _Form.Div("button-row");
            var submit = buttonsRow.Button(SR.LABEL_CONTINUE, (e) => {
                // validate

                if (ISO31662.GetName(region.Value) == null) {
                    _RegionFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_PLEASE_SELECT_YOUR_REGION);
                    return;
                }
                if (changePass.Checked) {
                    if (pass1.Value != pass2.Value) {
                        pass2Feedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_PASSWORD_REENTRY_MISMATCH);
                        return;
                    }
                }
                pass2Feedback.Hide();

                Account.Iso31662Region = region.Value;

                atts[AccountAttributes.IncomeEligibility_Key] =
                 rdoWorking.Checked ? AccountAttributes.IncomeEligibility_Working
                 : rdoLooking.Checked ? AccountAttributes.IncomeEligibility_LookingForWork
                 : rdoHealthProblem.Checked ? AccountAttributes.IncomeEligibility_HealthProblem
                 : rdoRetired.Checked ? AccountAttributes.IncomeEligibility_Retired
                 : "";

                for (int i = 0; i < atts.Count; i++)
                    if (atts[i].Name == AccountAttributes.SkillOrService_Key
                        || atts[i].Name == AccountAttributes.PushNotification_Key)
                        atts.RemoveAt(i--);

                for (int i = 0; i < _Skills.Count; i++) {
                    if (!String.IsNullOrWhiteSpace(_Skills[i].Value.Value))
                        atts.Append(AccountAttributes.SkillOrService_Key, _Skills[i].Value.ToString());
                }

                for (int i = 0; i < _Notifications.Count; i++) {
                    if (!String.IsNullOrWhiteSpace(_Notifications[i].Value.HttpUrl))
                        atts.Append(AccountAttributes.PushNotification_Key, _Notifications[i].Value.ToString());
                }

                Account.ReplaceAttributes(atts);
                Account.UpdatedUtc = DateTime.UtcNow;

                _Form.Style.Display = Display.None;
                buttonsRow.Style.Display = Display.None;
                _ServerStatus.Clear();
                _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_SIGNING_INFORMATION + " ...");

                if (changePass.Checked) {
                    // PASSWORD/PRIVATE KEY CHANGE
                    Account.ChangePasswordAndSign(new AsyncRequest<Schema.PasswordRequest>() {
                        Item = new Schema.PasswordRequest() {
                            NewPass = pass1.Value,
                            OldPass = pass.Value
                        },
                        OnProgress = (sender) => {
                            var res = sender as AsyncRequest<Schema.PasswordRequest>;
                            var msg = SR.LABEL_PLEASE_WAIT;
                            switch (res.ProgressPercent) {
                                case 0: msg = SR.LABEL_STATUS_GENERATING_NEW_SECRET_KEY; break;
                                case 25: msg = SR.LABEL_STATUS_PROCESSING_PASS_PHRASE; break;
                                case 50: msg = SR.LABEL_STATUS_ENCRYPTING_SECRET_KEY; break;
                                case 75: msg = SR.LABEL_STATUS_SIGNING_INFORMATION; break;
                                case 100: msg = SR.LABEL_STATUS_CONTACTING_NETWORK; break;
                            }
                            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, res.ProgressPercent + "% " + msg + "...");
                        },
                        OnComplete = (done) => {
                            if (done.Result.Success) {
                                _ServerStatus.Clear();
                                var prog = new ServerProgressIndicator(_ServerStatus);
                                prog.SetMainGlyph(Assets.SVG.Wait);
                                prog.Show();
                                var put = new AsyncRequest<PutRequest>() {
                                    Item = new PutRequest(Account) { UI = prog },
                                    OnComplete = (sender) => {
                                        var req = sender as AsyncRequest<PutRequest>;
                                        req.Item.UpdateUIProgress();
                                        if (req.Result == CMResult.S_OK) {
                                            _MainFeedback.Set(Assets.SVG.CircleTick, FeedbackType.Success, SR.LABEL_STATUS_ACCOUNT_UPDATED_SUCCESSFULLY);
                                            var options = _ReturnButtons.Div("button-row center");
                                            options.Button(SR.LABEL_GO_TO_YOUR_ACCOUNT, "/" + Account.ID);
                                            prog.SetMainGlyph(Assets.SVG.CircleTick);
                                        } else {
                                            _MainFeedback.Set(Assets.SVG.CircleError,
                                                 FeedbackType.Error,
                                                SR.LABEL_STATUS_A_PROBLEM_OCCURRED
                                                + ": " + req.Result.GetLocalisedDescription());
                                            _Form.Style.Display = Display.Block;
                                            buttonsRow.Style.Display = Display.Block;
                                            prog.SetMainGlyph(Assets.SVG.CircleError);
                                        }
                                    },
                                    OnProgress = (sender) => {
                                        var req = sender as AsyncRequest<PutRequest>;
                                        req.Item.UpdateUIProgress();
                                    }
                                };
                                App.Identity.Client.TryPut(put);
                            } else {
                                _Form.Style.Display = Display.Block;
                                buttonsRow.Style.Display = Display.Block;
                                _MainFeedback.Set(Assets.SVG.CircleError,
                                    FeedbackType.Error,
                                    SR.LABEL_STATUS_A_PROBLEM_OCCURRED + ": " + done.Result.GetLocalisedDescription());
                                System.Console.WriteLine(done.Result.ToString());
                            }
                        }
                    }, JSCryptoFunctions.Identity);
                } else {
                    // BASIC RE-SIGN
                    Account.SignData(new AsyncRequest<Schema.DataSignRequest>() {
                        Item = new Schema.DataSignRequest(Account.GetSigningData()) {
                            Password = System.Text.Encoding.UTF8.GetBytes(pass.Value)
                        },
                        OnComplete = (req) => {
                            if (req.Result == CMResult.S_OK) {
                                Account.AccountSignature = req.Item.Transforms[0].Output;

                                _MainFeedback.Set(Assets.SVG.Wait,
                                    FeedbackType.Default,
                                    SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");

                                _ServerStatus.Clear();
                                var prog = new ServerProgressIndicator(_ServerStatus);
                                prog.SetMainGlyph(Assets.SVG.Wait);
                                prog.Show();
                                var put = new AsyncRequest<PutRequest>() {
                                    Item = new PutRequest(Account) { UI = prog },
                                    OnProgress = (sender) => {
                                        (sender as AsyncRequest<PutRequest>).Item.UpdateUIProgress();
                                    },
                                    OnComplete = (putRes) => {
                                        putRes.Item.UpdateUIProgress();
                                        if (putRes.Result == CMResult.S_OK) {
                                            _MainFeedback.Set(Assets.SVG.CircleTick,
                                                FeedbackType.Success,
                                                SR.LABEL_STATUS_ACCOUNT_UPDATED_SUCCESSFULLY);
                                            var options = _ReturnButtons.Div("button-row center");
                                            options.Button(SR.LABEL_GO_TO_YOUR_ACCOUNT, "/" + Account.ID);
                                            prog.SetMainGlyph(Assets.SVG.CircleTick);
                                        } else {
                                            _MainFeedback.Set(Assets.SVG.Warning,
                                                FeedbackType.Error,
                                                SR.LABEL_STATUS_A_PROBLEM_OCCURRED
                                                + ": " + putRes.Result.GetLocalisedDescription());
                                            _Form.Style.Display = Display.Block;
                                            buttonsRow.Style.Display = Display.Block;
                                            prog.SetMainGlyph(Assets.SVG.CircleError);
                                        }
                                    }
                                };
                                App.Identity.Client.TryPut(put);
                            } else {
                                _Form.Style.Display = Display.Block;
                                buttonsRow.Style.Display = Display.Block;
                                _MainFeedback.Set(Assets.SVG.Warning, FeedbackType.Error,
                                    SR.LABEL_STATUS_SIGNING_FAILED);
                            }
                        }
                    }, JSCryptoFunctions.Identity);
                }
            });
            submit.Style.Display = Display.None;

            buttonsRow.Button(SR.LABEL_CANCEL, "/" + Account.ID);
            ch.OnChange = (e) => {
                passAndSubmit.Style.Display = ch.Checked ? Display.Block : Display.None;
                submit.Style.Display = ch.Checked ? Display.Inline : Display.None;
            };
        }

        private class NotifyEditor {
            public AccountAttributes.PushNotificationCsv Value;

            public NotifyEditor(HTMLElement parent, AccountAttributes.PushNotificationCsv value) {
                Value = value;
                var row = parent.Div("push row");
                var label = row.TextBox(value.Label);
                label.Placeholder = SR.LABEL_LABEL;
                label.OnChange = (e) => {
                    value.Label = label.Value;
                };
                var url = row.TextBox(value.HttpUrl);
                url.Placeholder = "http(s)://domain.com/sink";
                url.OnChange = (e) => {
                    value.HttpUrl = url.Value;
                };
            }
        }

        private class SkillEditor {
            public AccountAttributes.SkillCsv Value;

            public SkillEditor(HTMLElement parent, AccountAttributes.SkillCsv value) {
                Value = value;
                var row = parent.Div("skill row");
                var sel = row.Select();
                sel.AppendChild(new HTMLOptionElement() { Value = ((int)AccountAttributes.SkillLevel.Amateur).ToString(), InnerHTML = SR.LABEL_SKILL_LEVEL_AMATEUR });
                sel.AppendChild(new HTMLOptionElement() { Value = ((int)AccountAttributes.SkillLevel.Qualified).ToString(), InnerHTML = SR.LABEL_SKILL_LEVEL_QUALIFIED });
                sel.AppendChild(new HTMLOptionElement() { Value = ((int)AccountAttributes.SkillLevel.Experienced).ToString(), InnerHTML = SR.LABEL_SKILL_LEVEL_EXPERIENCED });
                sel.AppendChild(new HTMLOptionElement() { Value = ((int)AccountAttributes.SkillLevel.Certified).ToString(), InnerHTML = SR.LABEL_SKILL_LEVEL_CERTIFIED });
                sel.Value = ((int)value.Level).ToString();
                sel.OnChange = (e) => {
                    Value.Level = (AccountAttributes.SkillLevel)int.Parse(sel.Value);
                };
                var tb = row.TextBox(value.Value);
                tb.Placeholder = SR.LABEL_ENTER_SKILL_OR_SERVICE;
                tb.OnChange = (e) => {
                    value.Value = tb.Value;
                };
            }
        }
    }
}