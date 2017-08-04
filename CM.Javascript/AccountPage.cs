#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using CM.Javascript.Assets;
using Bridge.Html5;
using System.Collections.Generic;
using System;
using CM.Schema;

namespace CM.Javascript {
    class AccountPage : Page {
        string _ID;
        Feedback _Feedback;
        Schema.Account _Account;
        public static Schema.Account Prefetched;
        public AccountPage(string id) {
            _ID = id;
       
        }

        HTMLElement _Rating;
        HTMLElement _Balance;
        HTMLDivElement _AccountAttributes;
        MultiSelect _MultiSelect;
        PagedList<TransactionListResult> _TransList;
        HTMLDivElement _Top;

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
            //col-sm-12 col-md-8 col-md-offset-2
            Element.ClassName = "accountpage";
 
            _Top = Element.Div("top");

            var title = _Top.Div("left").H1(HtmlEncode(_ID));
            var repBal = _Top.Div("right");
            _Rating = repBal.Div("rep");
            _Balance = repBal.Div("balance");
   
            _Feedback = new Feedback(Element);
           
            var prog = new ServerProgressIndicator(Element);
            if (!Helpers.IsIDValid(_ID)) {
                _Feedback.Set(
                    SVG.Warning, FeedbackType.Error,
                    CMResult.E_Account_ID_Invalid.GetLocalisedDescription()
                    );

                return;
            }
            // _Feedback.Set(SVG.Wait, FeedbackType.Default,
            //       SR.LABEL_LOADING_PLEASE_WAIT);
            

            if (Prefetched != null && String.Equals(Prefetched.ID, _ID, StringComparison.OrdinalIgnoreCase)) {
                _Account = Prefetched;
                Prefetched = null;
                OnAccountLoaded();
            } else {
                prog.SetMainGlyph(SVG.Wait);
                prog.Show();

                var req = new AsyncRequest<FindAccountRequest>() {
                    Item = new FindAccountRequest(_ID) {
                        UI = prog
                    }                  
                };
                req.OnComplete = (sender) =>
                {
                    var r = sender as AsyncRequest<FindAccountRequest>;
                    _Account = r.Item.Output;
                    
                    if (_Account == null || !_Account.Response.IsSuccessful) {
                        var res = r.Result;
                        if (res == CMResult.S_OK)
                            res = CMResult.E_Item_Not_Found;
                        prog.SetMainGlyph(SVG.Warning);
                        _Feedback.Set(
                          SVG.Warning, FeedbackType.Error,
                          res.GetLocalisedDescription()
                          );
                        return;
                    }
                    _ID = _Account.ID; // Correct casing
                    title.InnerHTML = HtmlEncode(_ID);

                    prog.SetMainGlyph(SVG.CircleTick);
                    prog.Remove();
                    OnAccountLoaded();
                };
                App.Identity.Client.TryFindAccount(req);
            }
        }

        void OnAccountLoaded() {

            App.Identity.Client.Subscribe(_Account.ID);

            // Make sure all ModificationSignatures are correct.
            var keys = _Account.GetAllPublicKeys();
            var verify = new AsyncRequest<DataVerifyRequest>()
            {
                Item = new DataVerifyRequest()
            };
            for (int i = 1; i < keys.Count; i++) {
                verify.Item.DataDateUtc = keys[i].EffectiveDate.AddSeconds(-1);
                verify.Item.Input = keys[i].GetModificationSigningData();
                verify.Item.Signature = keys[i].ModificationSignature;
                _Account.VerifySignature(verify, JSCryptoFunctions.Identity);
                if (verify.Result != CMResult.S_OK) {
                    _Feedback.Set(
                        SVG.Warning, FeedbackType.Error,
                        "There's a problem with the account data.");
                    return;
                }
            }

            // Check governing authority keys if required
            var gaName = ISO31662.GetName(_Account.ID);
            if (gaName != null) {
                var check = new AsyncRequest<bool>();
                _Account.CheckIsValidGoverningAuthority(check, JSCryptoFunctions.Identity);
                if (!check.Item) {
                    _Feedback.Set(
                        SVG.Warning, FeedbackType.Error,
                        "There's a problem with the account data.");
                    return;
                } else {
                    _Top.Div("row margins").H2(
                        String.Format("Governing authority for {0}", gaName));

                }

            }

            if (!_Account.ConsensusOK) {
                _Feedback.Set(
                 SVG.Warning, FeedbackType.Error,
                 "The information on this page could not be fully corroborated. Refresh to try again."
                 );
            } else {
                _Feedback.Hide();
            }

            HistoryManager.Instance.AddAccountToViewHistory(_Account.ID);
            _AccountAttributes = Element.Div();
            RenderAccountInfo();

            var trans = Element.Div("details").Div("row");
            trans.H2(SR.TITLE_TRANSACTION_HISTORY);
            _TransList = new PagedList<TransactionListResult>(trans, Constants.PATH_ACCNT + "/" + _ID + "/" + Constants.PATH_TRANS);
            _TransList.OnCheckedChanged = OnCheckedChange;
            _TransList.OnClick = OnClick;
            _TransList.UpdateResults();
        }

        void OnScroll(Event e) {
            if (_TransList != null
                && _TransList.IsScrollAtBottom)
                _TransList.UpdateResults();
        }
        public override void OnAdded() {
            Window.OnScroll = OnScroll;
           
        }
        public override void OnRemoved() {
            Window.OnScroll = null;
        }
        public void RefreshBalance() {
            var req = new AsyncRequest<FindAccountRequest>() {
                Item = new FindAccountRequest(_ID)
            };
            req.OnComplete = (sender) =>
            {
                var r = sender as AsyncRequest<FindAccountRequest>;
                _Account = r.Item.Output;
                if (_Account == null || !_Account.Response.IsSuccessful) {
                    var res = r.Result;
                    if (res == CMResult.S_OK)
                        res = CMResult.E_Item_Not_Found;
                    _Feedback.Set(
                      SVG.Warning, FeedbackType.Error,
                      res.GetLocalisedDescription()
                      );
                    return;
                }
                RenderAccountInfo();
            };
            App.Identity.Client.TryFindAccount(req);
        }
      
        void RenderAccountInfo() {

   
            _AccountAttributes.Clear();
            _Rating.Clear();
            _Balance.Clear();
           

            var calcs = _Account.AccountCalculations;
            if (calcs != null && calcs.RecentCredits != null && calcs.RecentDebits != null) {
                decimal rr;
                RecentReputation rep;
                Helpers.CalculateRecentReputation(calcs.RecentCredits.Value, calcs.RecentDebits.Value, out rr, out rep);
                var amount = Helpers.CalculateAccountBalance(calcs.RecentCredits.Value, calcs.RecentDebits.Value);
                _Balance.Amount(amount, prefix: Constants.Symbol, roundTo2DP: true);
                _Balance.Div("equal", "USD " + (amount < 0 ? "-" : "") + " $" + Math.Abs(amount * Constants.USDExchange).ToString("N0"));
                _Rating.AmountReputation(rr);
                _Rating.H3("").Reputation(rep);
            } else {
                _Balance.Amount("//c ", "?", "**");
                _Rating.Amount("", "*", "");
                _Rating.H3(SVG.CircleUnknown.ToString(16, 16, "#CCCCCC") + " Unknown reputation");
            }

            //right = row.Div("cell-half");
           
            var details = _AccountAttributes.Div("details");
            var row = details.Div("row makepayment");
            row.Button(SR.LABEL_MAKE_A_PAYMENT, "/" + _ID + "/pay", className: "green-button");

            row = details.Div("row");
            var left = row.Div("cell-twothird");
            var right = row.Div("cell-third right");

            left.Div(null).H2(SR.LABEL_ACCOUNT_ATTRIBUTES);
            var att = left.Div("row attr");
            att.Div("cell-half", SR.LABEL_ACCOUNT_AGE + ":");
            var age = (System.DateTime.UtcNow - _Account.CreatedUtc);
            att.Div("cell-half", age.TotalDays / 365 > 1 ? string.Format(SR.LABEL_YEARS_OLD, ((int)age.TotalDays / 365))
                : string.Format(SR.LABEL_DAYS_OLD, ((int)age.TotalDays)));

            att = left.Div("row attr");
            var atts = _Account.CollectAttributes();
            att.Div("cell-half", SR.LABEL_INCOME_ELIGIBILITY + ":");
            var eligibility = atts[AccountAttributes.IncomeEligibility_Key];
            switch (eligibility) {
                case AccountAttributes.IncomeEligibility_Working: eligibility = SR.LABEL_INCOME_ELIGIBILITY_WORKING; break;
                case AccountAttributes.IncomeEligibility_LookingForWork: eligibility = SR.LABEL_INCOME_ELIGIBILITY_LOOKING_FOR_WORK; break;
                case AccountAttributes.IncomeEligibility_HealthProblem: eligibility = SR.LABEL_INCOME_ELIGIBILITY_HEALTH_PROBLEM; break;
                case AccountAttributes.IncomeEligibility_Retired: eligibility = SR.LABEL_INCOME_ELIGIBILITY_RETIRED; break;
                default: eligibility = SR.LABEL_VALUE_NOT_SET; break;
            }
            att.Div("cell-half", eligibility);
            att = left.Div("row attr");
            att.Div("cell-half", SR.LABEL_REGION + ":");
            att.Div("cell-half").A(HtmlEncode(ISO31662.GetName(_Account.Iso31662Region)),
                "/regions/" + _Account.Iso31662Region);

            att = left.Div("row attr");
            att.Div("cell-half", SR.LABEL_SKILLS_AND_SERVICES + ":");
            var skills = _Account.GetSkillsSummary();
            if (skills.Length == 0)
                skills = SR.LABEL_VALUE_NOT_SET;
            else
                skills = HtmlEncode(skills).Replace("\n", "<br/>");
            att.Div("cell-half", skills);
            left.Div(null).A(SR.LABEL_EDIT_ACCOUNT, "/" + _ID + "/edit");
            left.Div(null).A(SR.LABEL_REQUEST_A_PAYMENT, "/" + _ID + "/link");
            //right.Div("qr", QRCode.GenerateQRCode(Constants.TrustedSite + "/" + _ID, 128, 128));

            //right.Div(null).H4(SR.TITLE_OWN_THIS_ACCOUNT);

            //right.Div(null).Span("<a href=\"/civilmoneylogos.svg\" target=\"_blank\">Acceptance Logos</a>");
            //right.Div(null).A(SR.LABEL_EDIT_ACCOUNT, "/" + _ID + "/edit");

        }
 
        
        void OnCheckedChange(ListResult r, bool check) {
            if (check) {
                if (_MultiSelect == null) {
                    _MultiSelect = new MultiSelect(_ID);
                    Element.AppendChild(_MultiSelect.Element);
                }
                _MultiSelect.Add(r);
            } else {
                if (_MultiSelect != null)
                    _MultiSelect.Remove(r);
                if (_MultiSelect.Count == 0) {
                    _MultiSelect.Remove();
                    _MultiSelect = null;
                }
            }
        }
        public void OnTransactionChanged(Schema.Transaction t) {
            ListResult r;
            if (!_TransList.TryFindResult(t.ID, out r))
                return;
            var tr = r as TransactionListResult;
            tr.Update(t);

            if (_MultiSelect != null) {
                _MultiSelect.Remove(r);
                if (_MultiSelect.Count == 0) {
                    _MultiSelect.Remove();
                    _MultiSelect = null;
                }
            }
        }

        class MultiSelect {
            public HTMLDivElement Element;
            HTMLElement _Title;
            List<ListResult> _SelectedItems;
            string _MyID;
            public int Count { get { return _SelectedItems.Count; } }
            public void Add(ListResult item) {
                if (!_SelectedItems.Contains(item))
                    _SelectedItems.Add(item);
                OnSelectedTransactionsChanged();
            }

            public void Remove(ListResult item) {
                _SelectedItems.Remove(item);
                OnSelectedTransactionsChanged();
            }
            public MultiSelect(string myID) {
                _MyID = myID;
                _SelectedItems = new List<ListResult>();
                Element = new HTMLDivElement() { ClassName = "multiselect" };
                _Title = Element.H2("");
                var row = Element.Div("button-row");
                row.Button(SR.LABEL_PAYEE_STATUS_ACCEPT_VERB, (e) => {
                    OpenAuthorisationDialog((int)Schema.PayeeStatus.Accept);
                },  className: "green-button");
                row.Button(SR.LABEL_PAYEE_STATUS_DECLINE_VERB, (e) => {
                    OpenAuthorisationDialog((int)Schema.PayeeStatus.Decline);
                });
            }
            void OpenAuthorisationDialog(int status) {
                var ar = new List<string>();
                for (int i = 0; i < _SelectedItems.Count; i++) {
                    ar.Add(_SelectedItems[i].Data);
                }
                App.Identity.CurrentPage = new AuthorisePage(_MyID, status, ar.ToArray());
            }
            public void Remove() {
                Element.RemoveEx();
            }
            void OnSelectedTransactionsChanged() {
                _Title.InnerHTML = _SelectedItems.Count + " transaction(s) selected";
                
            }
        }
        void OnClick(ListResult r) {

        }
    }

}