#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;

namespace CM.Javascript {

    /// <summary>
    /// An account registration page.
    /// </summary>
    internal class RegisterPage : Page {
        /// <summary>
        /// Can be set by any page. Cleared when the register page is actually navigated to. This
        /// only "sticks" for one registration page visit.
        /// </summary>
        public static string ReturnPath; 
        private string _ReturnPath; 

        public RegisterPage() {
            // Return path applies once only.
            _ReturnPath = ReturnPath;
            ReturnPath = null;
        }

        public override string Title {
            get {
                return SR.LABEL_CREATE_MY_ACCOUNT;
            }
        }

        public override string Url {
            get {
                return "/register";
            }
        }

        public override void Build() {
            Element.ClassName = "registerpage";
            var form = Element.Div();
            form.H1(SR.LABEL_CREATE_MY_ACCOUNT);
            form.Div(null, SR.HTML_REGISTER_INTRO);
            form.Div(null, "&nbsp;");
            form.H3(SR.LABEL_ACCOUNT_NAME);
            var nameDiv = form.Div();
            var accountName = nameDiv.TextBox("");

            accountName.AddEventListener(EventType.KeyPress, (Event e) => {
                var ev = (KeyboardEvent)e;
                if ((ev.KeyCode != '-' && ev.KeyCode != '.' && !char.IsLetterOrDigit((char)ev.KeyCode))) {
                    e.PreventDefault();
                    e.StopPropagation();
                }
            });
            var nameFeedback = new Feedback(nameDiv);
            form.H3(SR.LABEL_REGION);
            var regionDiv = form.Div();
            var country = regionDiv.Select();
            var region = regionDiv.Select();
            var _RegionFeedback = new Feedback(regionDiv);
            region.Style.Display = Display.None;

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

            form.H3(SR.LABEL_SECRET_PASS_PHRASE);
            var pass1Div = form.Div();
            var pass1 = pass1Div.Password();
            var pass1Feedback = new Feedback(pass1Div);
            pass1Feedback.Set(Assets.SVG.CircleUnknown, FeedbackType.Default,
                SR.LABEL_NEW_PASSWORD_INSTRUCTIONS);
            form.H3(SR.LABEL_REENTER_PASS_PHRASE);
            var pass2Div = form.Div();
            var pass2 = pass2Div.Password();
            var pass2Feedback = new Feedback(pass2Div);
            form.H3(" ");
            var agreement = form.Div().CheckBox(
                SR.HTML_I_PROMISE_TO_FOLLOW_THE_HONOUR_CODE
                + SR.HTML_CIVIL_MONEY_HONOUR_CODE);

            form.H3(" ");
            var button = form.Div("button-row").Button(SR.LABEL_CREATE_MY_ACCOUNT, string.Empty, className: "green-button");
            button.Style.Display = Display.None;
            pass2.OnChange = (e) => {
                if (pass1.Value == pass2.Value) {
                    pass2Feedback.Hide();
                }
            };
            agreement.OnChange = (e) => {
                button.Style.Display = agreement.Checked ? Display.Inline : Display.None;
            };

            string lastCheckedID = null;
            AsyncRequest<FindAccountRequest> dupeSearch = null;
            var progress = new Feedback(Element, big: true);
            var serverStatus = Element.Div("statusvisual");
            var returnButtons = Element.Div();
          
            button.OnClick = (e) => {
                if (ISO31662.GetName(region.Value) == null) {
                    _RegionFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_PLEASE_SELECT_YOUR_REGION);
                    region.ScrollTo();
                    return;
                }
                if (string.IsNullOrEmpty(pass1.Value)) {
                    pass2Feedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_PASSWORD_REQUIRED);
                    pass1.ScrollTo();
                    return;
                }
                if (pass1.Value != pass2.Value) {
                    pass2Feedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_PASSWORD_REENTRY_MISMATCH);
                    pass1.ScrollTo();
                    return;
                }
                if (!Helpers.IsIDValid(lastCheckedID)) {
                    if (string.IsNullOrEmpty(lastCheckedID))
                        nameFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_ACCOUNT_NAME_REQUIRED);
                    accountName.ScrollTo();
                    return;
                }
                var a = new Schema.Account();
                a.ID = lastCheckedID;
                a.CreatedUtc = System.DateTime.UtcNow;
                a.UpdatedUtc = a.CreatedUtc;
                a.APIVersion = Constants.APIVersion;
                a.Iso31662Region = region.Value;

                if (ISO31662.GetName(a.ID) != null) {
                    // For new governing authority registrations...

                    // Region must equal the ID
                    if (a.Iso31662Region != a.ID.ToUpper()) {
                        _RegionFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_PLEASE_SELECT_YOUR_REGION);
                        region.ScrollTo();
                        return;
                    }

                    // We need to prompt for a Governing Authority signature
                    // which the Civil Money steering group has to generate after
                    // a thorough verification process.
                    a.Values[Schema.AccountAttributes.GoverningAuthority_Key]
                    = Window.Prompt("This ID requires a Governing Authority signature for timestamp " + a.Values["UTC"] 
                    + ". Please paste it below.",
                    "");
                }

                form.Style.Display = Display.None;
                serverStatus.Clear();

                a.ChangePasswordAndSign(new AsyncRequest<Schema.PasswordRequest>() {
                    Item = new Schema.PasswordRequest() {
                        NewPass = pass1.Value
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
                        progress.Set(Assets.SVG.Wait, FeedbackType.Default, res.ProgressPercent + "% " + msg + "...");
                    },
                    OnComplete = (done) => {
                        if (done.Result.Success) {
                            serverStatus.Clear();
                            var prog = new ServerProgressIndicator(serverStatus);
                            prog.SetMainGlyph(Assets.SVG.Wait);
                            prog.Show();
                            var put = new AsyncRequest<PutRequest>() {
                                Item = new PutRequest(a) { UI = prog },
                                OnComplete = (sender) => {
                                    var req = sender as AsyncRequest<PutRequest>;
                                    req.Item.UpdateUIProgress();
                                    if (req.Result == CMResult.S_OK) {
                                        progress.Set(Assets.SVG.CircleTick, FeedbackType.Success,
                                            SR.LABEL_STATUS_ACCOUNT_CREATED_SUCCESFULLY);
                                        var options = returnButtons.Div("button-row center");
                                        if (_ReturnPath != null) {
                                            options.Button(SR.LABEL_CONTINUE, _ReturnPath);
                                            ReturnPath = null;
                                        }
                                        options.Button(SR.LABEL_GO_TO_YOUR_ACCOUNT, "/" + a.ID);
                                        prog.SetMainGlyph(Assets.SVG.CircleTick);
                                    } else {
                                        progress.Set(Assets.SVG.CircleError, FeedbackType.Error,
                                            SR.LABEL_STATUS_A_PROBLEM_OCCURRED
                                            + ": " + req.Result.GetLocalisedDescription());
                                        form.Style.Display = Display.Block;
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
                            form.Style.Display = Display.Block;
                            progress.Set(Assets.SVG.CircleError, FeedbackType.Error,
                                SR.LABEL_STATUS_A_PROBLEM_OCCURRED + ": " + done.Result.GetLocalisedDescription());
                            System.Console.WriteLine(done.Result.ToString());
                        }
                    }
                }, JSCryptoFunctions.Identity);
            };

            accountName.OnKeyUp = (e) => {
                var id = accountName.Value;

                if (dupeSearch != null && dupeSearch.Item.ID == id)
                    return;

                button.Disabled = true;

                if (dupeSearch != null)
                    dupeSearch.IsCancelled = true;

                dupeSearch = null;

                if (!Helpers.IsIDValid(id)) {
                    nameFeedback.Set(Assets.SVG.Warning, FeedbackType.Default, SR.LABEL_ACCOUNT_NAME_INSTRUCTIONS);
                } else {
                    dupeSearch = new AsyncRequest<FindAccountRequest>() {
                        Item = new FindAccountRequest(id),
                        OnComplete = (sender) => {
                            var req = sender as AsyncRequest<FindAccountRequest>;
                            if (req != dupeSearch || req.IsCancelled) return; // stale search
                            lastCheckedID = req.Item.ID;
                            if (req.Result == CMResult.S_OK) {
                                var a = req.Item.Output.Cast<Schema.Account>();
                                nameFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, System.String.Format(SR.LABEL_ACCOUNT_BLANK_IS_ALREADY_TAKEN, HtmlEncode(a.ID)));
                            } else if (req.Result == CMResult.E_Item_Not_Found) {
                                if (req.Item.Output != null && !req.Item.Output.ConsensusOK) {
                                    // Not enough peers to know for certain
                                    nameFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, CMResult.E_Not_Enough_Peers.GetLocalisedDescription());
                                } else {
                                    button.Disabled = false;
                                    nameFeedback.Set(Assets.SVG.CircleTick, FeedbackType.Success,
                                        System.String.Format(SR.LABEL_ACCOUNT_BLANK_LOOKS_OK, HtmlEncode(lastCheckedID)));
                                }
                            } else {
                                // Some other error, network probably
                                nameFeedback.Set(Assets.SVG.Warning, FeedbackType.Error,
                                    SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER
                                    + " " + req.Result.GetLocalisedDescription());
                            }
                        }
                    };
                    nameFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CHECKING_ACCOUNT_NAME + " ...");
                    App.Identity.Client.TryFindAccount(dupeSearch);
                }
            };
        }
    }
}