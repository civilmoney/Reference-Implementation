using CM.JS.Controls;
using CM.Schema;
using Retyped;
using SichboUI;
using System;

namespace CM.JS.Screens {

    internal class AccountEditScreen : ScreenBase {
        private Account _Account;
        private PrivateKeySchemeID _CurrentPrivateKeySchemeID;
        private Element _MainFeedback;
        private PrivateKeySchemeID _NewPrivateKeySchemeID;
        private Button _Button;
        private AsyncRequest<FindAccountRequest> _DupeSearch;
        private string _LastCheckedID;
        private Field _NewPass;
        private RSAParameters _NewRSAKey = null;
        private ServerProgressIndicator _Prog;
        private SignatureBox _SigningBox;

        public AccountEditScreen(ScreenArgs url) : base(url) {
            App.Instance.ShowBack();
            if (url.Url[url.Url.Length - 1] == "edit") {
                El("div", text: SR.LABEL_STATUS_CONTACTING_NETWORK + " .. ", halign: Alignment.Center, valign: Alignment.Center);
                var search = new AsyncRequest<FindAccountRequest>() {
                    Item = new FindAccountRequest(url.Url[0]),
                    OnComplete = (sender) => {
                        Clear();
                        if (sender.Result == CMResult.S_OK) {
                            _Account = sender.Item.Output.Cast<Schema.Account>();
                            _CurrentPrivateKeySchemeID = _Account.PrivateKey != null ? _Account.PrivateKey.SchemeID : PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000;
                            _NewPrivateKeySchemeID = _CurrentPrivateKeySchemeID;
                            Build();
                        } else {
                            var st = new StackPanel();
                            st.HorizontalAlignment = Alignment.Center;
                            st.VerticalAlignment = Alignment.Center;
                            st.El("h1", text: SR.TITLE_NOT_FOUND);
                            st.Div("instructions", text: String.Format(SR.LABEL_STATUS_ACCOUNT_NOT_FOUND, url.Url[0]), margin: new Thickness(30, 0));
                            st.Add(new Button(ButtonStyle.BlackOutline, SR.LABEL_CONTINUE, () => {
                                App.Instance.Navigate("/");
                            }));
                            Add(st);
                        }
                    }
                };

                App.Instance.Client.TryFindAccount(search);

            } else {
                _Account = new Schema.Account();

                _CurrentPrivateKeySchemeID = PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000;
                _NewPrivateKeySchemeID = _CurrentPrivateKeySchemeID;
                Build();
            }
        }

        private void Build() {
            var st = new StackPanel();
            bool isNew = _Account.ID == null;
            st.MaxWidth.Value = 600;
            st.HorizontalAlignment = Alignment.Stretch;
            var marg = new Thickness(60, 0, 0, 0);
            Field name = null;

            if (isNew) {
                st.El("h1", text: SR.LABEL_CREATE_MY_ACCOUNT);
                st.Div("instructions").Html.innerHTML = @"The value of your Civilised Money account is determined by you and the people and businesses with whom you choose to interact. 
                <br/><br/>Traditional currencies are based on scarcity which is an anti-social contract promoting secrecy, deception and the destructive and toxic enterprise culture. 
                <br/><br/>Thank you for beginning our journey towards a mended society.";

                name = new Field(FieldType.Textbox, SR.LABEL_ACCOUNT_NAME);
                st.Add(name);

                name.ValueOrCheckedChanged += OnDupeCheck;

                App.Instance.UpdateHistory("/new-account", SR.LABEL_CREATE_MY_ACCOUNT);
            } else {
                st.El("h1", text: SR.LABEL_EDIT_ACCOUNT);
                st.Div("instructions", text: SR.LABEL_ACCOUNT_SETTINGS_INTRO);
                App.Instance.UpdateHistory($"/{_Account.ID}/edit", _Account.ID);
            }

            var country = new Field(FieldType.Select, "Country");
            country.AddOption("", "(" + SR.LABEL_PLEASE_SELECT + ")");
            string lastCountry = null;
            foreach (var kp in ISO31662.Values) {
                var s = kp.Name.Split('/')[0];
                if (lastCountry != s) {
                    lastCountry = s;
                    country.AddOption(s, s);
                }
            }

            var region = new Field(FieldType.Select, "Region");
            region.Visible = false;
            country.ValueOrCheckedChanged += (e) => {
                region.ValueElement.Clear();
                if (country.Value.Length > 0) {
                    var s = country.Value + "/";
                    foreach (var kp in ISO31662.Values) {
                        if (kp.Name.StartsWith(s)) {
                            var reg = kp.Name.Split('/')[1];
                            region.AddOption(kp.ID, reg);
                        }
                    }
                    region.Visible = true;
                } else {
                    region.Visible = false;
                }
            };
            if (_Account.Iso31662Region != null) {
                country.Value = ISO31662.GetName(_Account.Iso31662Region).Split('/')[0];
                region.Value = _Account.Iso31662Region;
            }

            st.Add(country);
            st.Add(region);
            st.El("h3", text: SR.LABEL_INCOME_ELIGIBILITY, margin: marg);
            st.Div("instructions", text: SR.LABEL_INCOME_ELIGIBILITY_INTRO);
            var atts = _Account.CollectAttributes();
            var incomeWorking = new Field(FieldType.Toggle, SR.LABEL_INCOME_ELIGIBILITY_WORKING) { ToggleGroup = "income" };
            var incomeLooking = new Field(FieldType.Toggle, SR.LABEL_INCOME_ELIGIBILITY_LOOKING_FOR_WORK) { ToggleGroup = "income" };
            var incomeHealth = new Field(FieldType.Toggle, SR.LABEL_INCOME_ELIGIBILITY_HEALTH_PROBLEM) { ToggleGroup = "income" };
            var incomeRetired = new Field(FieldType.Toggle, SR.LABEL_INCOME_ELIGIBILITY_RETIRED) { ToggleGroup = "income" };
            var incomeNotSet = new Field(FieldType.Toggle, SR.LABEL_VALUE_NOT_SET) { ToggleGroup = "income" };
            switch (atts[AccountAttributes.IncomeEligibility_Key]) {
                default: incomeNotSet.IsChecked = true; break;
                case AccountAttributes.IncomeEligibility_Working: incomeWorking.IsChecked = true; break;
                case AccountAttributes.IncomeEligibility_LookingForWork: incomeLooking.IsChecked = true; break;
                case AccountAttributes.IncomeEligibility_HealthProblem: incomeHealth.IsChecked = true; break;
                case AccountAttributes.IncomeEligibility_Retired: incomeRetired.IsChecked = true; break;
            }
            st.Add(incomeWorking);
            st.Add(incomeLooking);
            st.Add(incomeHealth);
            st.Add(incomeRetired);
            st.Add(incomeNotSet);

            st.El("h3", text: SR.LABEL_SKILLS_AND_SERVICES, margin: marg);
            st.Div("instructions", text: SR.LABEL_SKILLS_AND_SERVICES_INTRO);

            var skills = new StackPanel();
            skills.HorizontalAlignment = Alignment.Stretch;
            for (int i = 0; i < atts.Count; i++) {
                var a = atts[i];
                if (a.Name == AccountAttributes.SkillOrService_Key
                    && !String.IsNullOrEmpty(a.Value)) {
                    skills.Add(new SkillEditor(new AccountAttributes.SkillCsv(a.Value)));
                }
            }
            if (skills.Html.childElementCount == 0) {
                skills.Add(new SkillEditor(new AccountAttributes.SkillCsv()));
            }
            st.Add(skills);

            st.Add(new Button(ButtonStyle.BlackOutline, SR.LABEL_ADD_ANOTHER_ITEM, () => {
                skills.Add(new SkillEditor(new AccountAttributes.SkillCsv()));
            }, margin: new Thickness(15, 0)));

            st.El("h3", text: SR.LABEL_SECURITY, margin: marg);
            var newPassFields = new StackPanel();
            newPassFields.HorizontalAlignment = Alignment.Stretch;

            newPassFields.Div("instructions")
                .Html.innerHTML = @"Not everyone on earth owns a computer.
                <br/><br/><b>If you only have momentary access</b> to this smartphone or PC, please select <em>Make up a secret pass phrase</em> and enter a sentence that you will always remember.
                <br/><br/><b>If you are able to save and backup a file</b>, choose <em>Generate and save my key file</em> instead, and keep a couple of copies of it with loved ones. 
                <br/><br/>Pass phrases and key files are irrecoverable if lost so you will need to make a new account from scratch if you forget or lose yours. The culture of Civil Money is such that new accounts are just as good as old ones, as long as you're honest &mdash; a lost account is not the end of the world, but you may have to explain to people what's happened.";

            var useKeyFile = new Field(FieldType.Toggle, "Generate and save my key file (recommended)") { ToggleGroup = "auth" };
            var usePass = new Field(FieldType.Toggle, "Make up a secret pass phrase") { ToggleGroup = "auth" };

            _NewPass = new Field(FieldType.Textbox,
                isNew ? SR.LABEL_SECRET_PASS_PHRASE
                : _CurrentPrivateKeySchemeID == PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000 ? SR.LABEL_CHANGE_MY_PASS_PHRASE : SR.LABEL_CHANGE_MY_PRIVATE_KEY);
            var newPass2 = new Field(FieldType.Textbox, SR.LABEL_REENTER_PASS_PHRASE);
            var pkStack = new StackPanel();
            pkStack.Margin.Value = new Thickness(15, 0, 30, 60);
            Button pkSave = null;

            pkStack.Add(new Button(ButtonStyle.BigRed, SR.LABEL_GENERATE_A_NEW_KEY, (b) => {
                b.Loading();
                b.Text = SR.LABEL_STATUS_GENERATING_NEW_KEY + " ...";
                JSCryptoFunctions.Identity.BeginRSAKeyGen(new AsyncRequest<RSAKeyRequest>() {
                    Item = new RSAKeyRequest(),
                    OnComplete = (newKey) => {
                        _NewRSAKey = newKey.Item.Output;
                        var pkcs1 = CM.Cryptography.ASN.ToPKCS1(CM.Cryptography.ASN.FromRSAParameters(_NewRSAKey));
                        // PKCS#8
                        var pk = CM.Cryptography.ASN.BytesToBase64Blob("RSA PRIVATE KEY",
                            pkcs1.GetBytes(true));
                        var id = name != null ? name.Value : _Account.ID;
                        object bl;
                        var props = new dom.FilePropertyBag() {
                            type = "application/octet-stream",
                        };
                        try {
                            bl = new Retyped.dom.File(new[] { pk }, id + ".key", props);
                        } catch {
                            bl = new Retyped.dom.Blob(new[] { pk }, props);
                        }
                        b.Remove();
                        var a = pkSave.Html.As<dom.HTMLAnchorElement>();
                        a.href = dom.URL.createObjectURL(bl);
                        a.download = id + ".key";
                        pkSave.Visible = true;
                        a.click();

                    }
                });
            }));
            pkSave = new Button(ButtonStyle.BlackOutline, "Done! Save my key file", () => { });
            pkSave.HrefMode = true;
            pkSave.Margin.Value = new Thickness(15);
            pkStack.Add(pkSave);
            pkSave.Visible = false;

            newPassFields.Add(useKeyFile);
            newPassFields.Add(pkStack);

            newPassFields.Add(usePass);
            newPassFields.Add(_NewPass);
            newPassFields.Add(newPass2);

            useKeyFile.ValueOrCheckedChanged += (e) => {
                _NewPrivateKeySchemeID = PrivateKeySchemeID.KeyWithheld;
                _NewPass.Visible = false;
                newPass2.Visible = false;
                pkStack.Visible = true;
            };
            usePass.ValueOrCheckedChanged += (e) => {
                _NewPass.Visible = true;
                newPass2.Visible = true;
                pkStack.Visible = false;
                _NewPrivateKeySchemeID = PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000;
            };
            newPassFields.Visible = isNew;
            st.Add(newPassFields);

            usePass.IsChecked = _CurrentPrivateKeySchemeID == PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000;
            useKeyFile.IsChecked = _CurrentPrivateKeySchemeID == PrivateKeySchemeID.KeyWithheld;
            _NewPass.Visible = usePass.IsChecked;
            newPass2.Visible = usePass.IsChecked;
            pkStack.Visible = useKeyFile.IsChecked;

            Field changePass = new Field(FieldType.Toggle, _CurrentPrivateKeySchemeID == PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000 ? SR.LABEL_CHANGE_MY_PASS_PHRASE : SR.LABEL_CHANGE_MY_PRIVATE_KEY);
            changePass.ValueOrCheckedChanged += (e) => {
                newPassFields.Visible = e.IsChecked;
            };
            if (!isNew) {
                st.Add(changePass);
            }

            if (!isNew) {
                _SigningBox = new SignatureBox(_Account);
                st.Add(_SigningBox);
            }

            st.El("h3", text: "Pledge", margin: marg);
            var honour = new Field(FieldType.Toggle, "");
            honour.LabelElement.Html.innerHTML = SR.HTML_I_PROMISE_TO_FOLLOW_THE_HONOUR_CODE;
            honour.Margin.Value = new Thickness(15, 0, 30, 0);
            st.Add(honour);
            st.El("div", className: "honour-code", margin: new Thickness(0, 0, 30, 0)).Html.innerHTML = SR.HTML_CIVIL_MONEY_HONOUR_CODE;

            var bar = new ButtonBar(SR.LABEL_CONTINUE, (b) => {
                if (ISO31662.GetName(region.Value) == null) {
                    country.SetError(SR.LABEL_PLEASE_SELECT_YOUR_REGION);
                    return;
                }

                if (newPassFields.Visible) {
                    if (useKeyFile.IsChecked) {
                        if (_NewRSAKey == null) {
                            useKeyFile.SetError("You need to generate and save your key file.");
                            return;
                        }
                    } else {
                        if (string.IsNullOrEmpty(_NewPass.Value)) {
                            _NewPass.SetError(SR.LABEL_PASSWORD_REQUIRED);
                            return;
                        }
                        if (_NewPass.Value != newPass2.Value) {
                            newPass2.SetError(SR.LABEL_PASSWORD_REENTRY_MISMATCH);
                            return;
                        }
                    }
                }
                if (isNew) {
                    if (!Helpers.IsIDValid(_LastCheckedID)) {
                        if (string.IsNullOrEmpty(_LastCheckedID)) {
                            name.SetError(SR.LABEL_ACCOUNT_NAME_REQUIRED);
                        } else {
                            name.SetError(SR.LABEL_ACCOUNT_NAME_INSTRUCTIONS);
                        }
                        return;
                    }
                    _Account.ID = _LastCheckedID;
                    _Account.CreatedUtc = System.DateTime.UtcNow;
                    _Account.UpdatedUtc = _Account.CreatedUtc;
                    _Account.APIVersion = Constants.APIVersion;

                    if (ISO31662.GetName(_Account.ID) != null) {
                        Notification.Show(
                            "Government Authorization Required",
                            "Governing Authority taxation accounts require special setup and signing by the Civil Money steering group. Please contact hello@civil.money to begin a formal vetting process which involves an in-person key handover to your country's elected representative.");
                        return;
                    }
                }
                _Account.Iso31662Region = region.Value;

                atts[AccountAttributes.IncomeEligibility_Key] =
                  incomeWorking.IsChecked ? AccountAttributes.IncomeEligibility_Working
                  : incomeLooking.IsChecked ? AccountAttributes.IncomeEligibility_LookingForWork
                  : incomeHealth.IsChecked ? AccountAttributes.IncomeEligibility_HealthProblem
                  : incomeRetired.IsChecked ? AccountAttributes.IncomeEligibility_Retired
                  : "";

                for (int i = 0; i < atts.Count; i++)
                    if (atts[i].Name == AccountAttributes.SkillOrService_Key
                        // || atts[i].Name == AccountAttributes.PushNotification_Key
                        )
                        atts.RemoveAt(i--);

                for (uint i = 0; i < skills.Html.childElementCount; i++) {
                    var ed = skills.Html.children.item(i)["__el"] as SkillEditor;
                    if (ed != null && !String.IsNullOrWhiteSpace(ed.Value.Value))
                        atts.Append(AccountAttributes.SkillOrService_Key, ed.Value.ToString());
                }

                _Account.ReplaceAttributes(atts);

                b.Loading();
                if (newPassFields.Visible) {
                    OnSaveWithPassChange(b);
                } else {
                    OnSave(b);
                }
            }, SR.LABEL_CANCEL, () => {
                dom.window.history.back();
            });
            bar.Visible = false;
            honour.ValueOrCheckedChanged += (e) => {
                bar.Visible = e.IsChecked;
            };

            _MainFeedback = st.Div();
            st.Add(bar);
            st.AnimFadeInOut(Times.Normal);

            Add(st);
        }

        private void OnDupeCheck(Field e) {
            var id = e.Value;

            if (_DupeSearch != null && _DupeSearch.Item.ID == id)
                return;

            if (_DupeSearch != null)
                _DupeSearch.IsCancelled = true;

            _DupeSearch = null;
            _LastCheckedID = null;

            if (!Helpers.IsIDValid(id)) {
                e.SetError(SR.LABEL_ACCOUNT_NAME_INSTRUCTIONS);
            } else {
                _DupeSearch = new AsyncRequest<FindAccountRequest>() {
                    Item = new FindAccountRequest(id),
                    OnComplete = (sender) => {
                        var req = sender as AsyncRequest<FindAccountRequest>;
                        if (req != _DupeSearch || req.IsCancelled) return; // stale search

                        e.LabelElement.TextContent = SR.LABEL_ACCOUNT_NAME;
                        if (req.Result == CMResult.S_OK) {
                            var a = req.Item.Output.Cast<Schema.Account>();
                            e.SetError(String.Format(SR.LABEL_ACCOUNT_BLANK_IS_ALREADY_TAKEN, a.ID));
                        } else if (req.Result == CMResult.E_Item_Not_Found) {
                            if (req.Item.Output != null && !req.Item.Output.ConsensusOK) {
                                // Not enough peers to know for certain
                                e.SetError(CMResult.E_Not_Enough_Peers.GetLocalisedDescription());
                            } else {
                                e.ClearError();
                                _LastCheckedID = req.Item.ID;
                                e.LabelElement.TextContent = String.Format(SR.LABEL_ACCOUNT_BLANK_LOOKS_OK, req.Item.ID) + " 👍";
                            }
                        } else {
                            // Some other error, network probably
                            e.SetError(
                                SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER
                                + " " + req.Result.GetLocalisedDescription());
                        }
                    }
                };
                e.LabelElement.TextContent = SR.LABEL_STATUS_CHECKING_ACCOUNT_NAME + " ...";
                App.Instance.Client.TryFindAccount(_DupeSearch);
            }
        }

        private void OnProgress(AsyncRequest<PutRequest> req) {
            req.Item.UpdateUIProgress();
        }

        private void OnSave(Button b) {
            _Button = b;
            var prog = new ServerProgressIndicator();
            prog.Update(ServerProgressIndicatorStatus.Waiting, 0, SR.LABEL_PLEASE_WAIT);
            prog.Show();
            _Prog = prog;
            _Account.SignData(new AsyncRequest<Schema.DataSignRequest>() {
                Item = new Schema.DataSignRequest(_Account.GetSigningData()) {
                    PasswordOrRSAPrivateKey = _SigningBox.PasswordOrPrivateKey
                },
                OnComplete = (req) => {
                    if (req.Result == CMResult.S_OK) {
                        _Account.AccountSignature = req.Item.Transforms[0].Output;
                        prog.Update(ServerProgressIndicatorStatus.Waiting, 0, SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
                        var put = new AsyncRequest<PutRequest>() {
                            Item = new PutRequest(_Account) { UI = prog },
                            OnProgress = OnProgress,
                            OnComplete = OnSaveComplete
                        };
                        App.Instance.Client.TryPut(put);
                    } else {
                        prog.Finished(ServerProgressIndicatorStatus.Error,
                                     SR.LABEL_STATUS_A_PROBLEM_OCCURRED,
                                     SR.LABEL_STATUS_SIGNING_FAILED,
                                   () => { });
                        b.LoadingDone();
                    }
                }
            }, JSCryptoFunctions.Identity);
        }

        private void OnSaveComplete(AsyncRequest<PutRequest> req) {
            req.Item.UpdateUIProgress();
            if (req.Result == CMResult.S_OK) {
                _Prog.Finished(ServerProgressIndicatorStatus.Success,
                    SR.LABEL_STATUS_ACCOUNT_UPDATED_SUCCESSFULLY, null, () => {
                        App.Instance.Navigate("/" + _Account.ID);
                    });
            } else {
                _Prog.Finished(ServerProgressIndicatorStatus.Error,
                     SR.LABEL_STATUS_A_PROBLEM_OCCURRED,
                     req.Result.GetLocalisedDescription(),
                     () => {
                     });
                _Button.LoadingDone();
            }
        }

        private void OnSaveWithPassChange(Button b) {
            _Button = b;
            var changePassRequest = new Schema.PasswordRequest();
            changePassRequest.OldSchemeID = _CurrentPrivateKeySchemeID;
            changePassRequest.NewSchemeID = _NewPrivateKeySchemeID;

            if (_SigningBox != null) {
                if (changePassRequest.OldSchemeID == PrivateKeySchemeID.KeyWithheld) {
                    changePassRequest.OldRSAPrivateKey = _SigningBox.PasswordOrPrivateKey;
                } else {
                    changePassRequest.OldPass = System.Text.Encoding.UTF8.GetString(_SigningBox.PasswordOrPrivateKey);
                }
            }

            if (changePassRequest.NewSchemeID == PrivateKeySchemeID.KeyWithheld) {
                changePassRequest.NewRSAPrivateKey = _NewRSAKey.D;
                changePassRequest.NewRSAPublicKey = _NewRSAKey.Modulus;
            } else {
                changePassRequest.NewPass = _NewPass.Value;
            }

            _Prog = new ServerProgressIndicator();
            _Prog.Update(ServerProgressIndicatorStatus.Waiting, 0, SR.LABEL_PLEASE_WAIT);
            _Prog.Show();
            _Account.ChangePasswordAndSign(new AsyncRequest<Schema.PasswordRequest>() {
                Item = changePassRequest,
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
                    _Prog.Update(ServerProgressIndicatorStatus.Waiting, res.ProgressPercent, msg);
                },
                OnComplete = (done) => {
                    if (done.Result.Success) {
                        var put = new AsyncRequest<PutRequest>() {
                            Item = new PutRequest(_Account) { UI = _Prog },
                            OnComplete = OnSaveComplete,
                            OnProgress = OnProgress
                        };
                        App.Instance.Client.TryPut(put);
                    } else {
                        _Prog.Finished(ServerProgressIndicatorStatus.Error,
                                      SR.LABEL_STATUS_A_PROBLEM_OCCURRED,
                                      done.Result.GetLocalisedDescription(),
                                      () => { });
                        dom.console.log(done.Result.ToString());
                        b.LoadingDone();
                    }
                }
            }, JSCryptoFunctions.Identity);
        }
        private class SkillEditor : Element {
            public AccountAttributes.SkillCsv Value;

            public SkillEditor(AccountAttributes.SkillCsv value)
                : base(className: "skill-edit") {
                Value = value;
                var sel = new dom.HTMLSelectElement();
                sel.appendChild(new dom.HTMLOptionElement() { value = ((int)AccountAttributes.SkillLevel.Amateur).ToString(), text = SR.LABEL_SKILL_LEVEL_AMATEUR });
                sel.appendChild(new dom.HTMLOptionElement() { value = ((int)AccountAttributes.SkillLevel.Qualified).ToString(), text = SR.LABEL_SKILL_LEVEL_QUALIFIED });
                sel.appendChild(new dom.HTMLOptionElement() { value = ((int)AccountAttributes.SkillLevel.Experienced).ToString(), text = SR.LABEL_SKILL_LEVEL_EXPERIENCED });
                sel.appendChild(new dom.HTMLOptionElement() { value = ((int)AccountAttributes.SkillLevel.Certified).ToString(), text = SR.LABEL_SKILL_LEVEL_CERTIFIED });
                sel.value = ((int)value.Level).ToString();
                sel.onchange = (e) => {
                    value.Level = (AccountAttributes.SkillLevel)int.Parse(sel.value);
                };
                var tb = new dom.HTMLInputElement();
                tb.placeholder = SR.LABEL_ENTER_SKILL_OR_SERVICE;
                tb.value = value.Value;
                tb.onchange = (e) => {
                    value.Value = tb.value;
                };
                Html.appendChild(sel);
                Html.appendChild(tb);
            }
        }
    }
}