using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bridge.Html5;

namespace CM.Javascript {
    class AccountInputBox {
        HTMLDivElement _El;
        HTMLInputElement _Input;
        public HTMLElement Element { get { return _El; } }
        public HTMLInputElement Input { get { return _Input; } }
        HTMLDivElement _Rep;
        HTMLDivElement _Attributes;
        HTMLAnchorElement _Bal;
        Feedback _Feedback;
        public Schema.Account Account;
        public Action<Schema.Account> OnAccountChanged;
        bool _ShowGlyph;
        

        public AccountInputBox(HTMLElement parent, string id = null, bool goGlyph=false, string watermark=null) {
            _El = parent.Div("accountinputbox");
            
            _ShowGlyph = goGlyph;
            if (id != null) {
                // readonly when set
                _El.AddClass("readonly");
                _El.Div("val").Span(Page.HtmlEncode(id));
                Task.Run(() => { FindAccount(id); });
            } else {
                _El.AddClass("focusable");
                var accountName = _Input = _El.Div("val").TextBox("");
                accountName.Placeholder = watermark??SR.LABEL_ACCOUNT_NAME;
                accountName.AddEventListener(EventType.KeyPress, (Event e) => {
                    var ev = (KeyboardEvent)e;
                    if (ev.ShiftKey || (ev.KeyCode != '-' && !char.IsLetterOrDigit((char)ev.KeyCode))) {
                        e.PreventDefault();
                        e.StopPropagation();
                        if (ev.KeyCode == 13) {
                            if (Account != null
                            && goGlyph) {
                                App.Identity.Navigate("/" + Account.ID);
                            }
                        }
                    }
                });
                accountName.OnFocus += (e) => {
                    _El.AddClass("focused-input");
                };
                accountName.OnBlur += (e) => {
                    _El.RemoveClass("focused-input");
                };
                accountName.OnKeyUp += (e) => {
                    Task.Run(() => { FindAccount(accountName.Value); });
                };


            }
           
            var rb = _El.Div("repbal");
            _Rep = rb.Div("rep");
            _Bal = rb.A("", "#", "bal");
            if (!goGlyph)
                _Bal.Target = "_blank";
            _Attributes = parent.Div("accountinputboxatts");
            ResetInfo();
           
            _Feedback = new Feedback(parent.Div());
        }
        AsyncRequest<FindAccountRequest> _Search = null;
        public void SetFeedbackIfNoneAlready(Assets.SVG glyph, FeedbackType type, string msg) {
            if (!_Feedback.IsShowing) {
                _Feedback.Set(glyph, type, msg);
            }
        }
        void ResetInfo() {

            _Rep.Clear();
            _Bal.Clear();
            _Attributes.Clear();
            _Rep.Style.Display = Display.None;
            _Bal.Style.Display = Display.None;
            _Attributes.Style.Display = Display.None;
            _Bal.Href = "#";
            _Bal.Amount("", "-", "");
        }
        
        void FindAccount(string id) {

            if (_Search != null && _Search.Item.ID == id)
                return;

            if (_Search != null)
                _Search.IsCancelled = true;
            
            ResetInfo();

            Account = null;
            _Search = null;
            
            if (OnAccountChanged != null)
                OnAccountChanged(null);

            if (!Helpers.IsIDValid(id)) {
                _Feedback.Set(Assets.SVG.CircleUnknown,
                    FeedbackType.Default, 
                    SR.LABEL_ACCOUNT_NAME_INSTRUCTIONS);
            } else {
                _Search = new AsyncRequest<FindAccountRequest>() {
                    Item = new FindAccountRequest(id)
                };

                _Search.OnComplete = (sender) => {
                    var req = sender as AsyncRequest<FindAccountRequest>;
                    if (req != _Search || req.IsCancelled) return; // stale search
                   
                    if (req.Result == CMResult.S_OK) {
                        
                        var a = req.Item.Output.Cast<Schema.Account>();
                        Account = a;
                        var calc = a.AccountCalculations;
                        if (calc != null
                        && calc.RecentCredits!=null 
                        && calc.RecentDebits!=null
                        && calc.RecentReputation!=null) {
                            decimal rr;
                            RecentReputation rep;
                            Helpers.CalculateRecentReputation(calc.RecentCredits.Value, calc.RecentDebits.Value, out rr, out rep);
                            _Rep.Clear();
                            _Bal.Clear();
                            _Rep.Reputation(rep, true, false).ClassName="glyph";
                            _Rep.AmountReputation(rr);
                            _Rep.Reputation(rep, false, true).ClassName = "lab";
                            _Bal.Amount(Helpers.CalculateAccountBalance(calc.RecentCredits.Value, calc.RecentDebits.Value), prefix: Constants.Symbol);
                           
                        }
                        if (_ShowGlyph)
                            _Bal.Span(Assets.SVG.CircleRight.ToString(16, 16, "#ffffff"), "glyph");

                        _Bal.Div("label", SR.LABEL_BALANCE);
                        _Bal.Href = "/"+a.ID;
                      

                        _Rep.Style.Display = Display.TableCell;
                        _Bal.Style.Display = Display.TableCell;
                        _Attributes.InnerHTML = a.GetOneLineAttributeSummaryHtml();
                        _Attributes.Style.Display = Display.Block;
                        _Feedback.Hide();
                        if (OnAccountChanged != null)
                            OnAccountChanged(a);

                        AccountPage.Prefetched = a;

                    } else if (req.Result == CMResult.E_Item_Not_Found) {
                        _Feedback.Set(Assets.SVG.Warning,
                            FeedbackType.Default,
                            String.Format(SR.LABEL_STATUS_ACCOUNT_NOT_FOUND, Page.HtmlEncode(id)),
                            SR.LABEL_RETRY, ()=> {
                                _Feedback.Set(Assets.SVG.Wait,
                                    FeedbackType.Default, 
                                    SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
                                App.Identity.Client.TryFindAccount(_Search);
                            });
                    } else {
                        // Some other error, network probably
                        _Feedback.Set(Assets.SVG.Warning,
                            FeedbackType.Default,
                            SR.LABEL_STATUS_PROBLEM_REACHING_A_SERVER+" " + req.Result.GetLocalisedDescription(),
                            SR.LABEL_RETRY, () => {
                                _Feedback.Set(Assets.SVG.Wait,
                                    FeedbackType.Default, 
                                    SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
                                App.Identity.Client.TryFindAccount(_Search);
                            });
                    }
                };
                _Feedback.Set(Assets.SVG.Wait, FeedbackType.Default, 
                    SR.LABEL_STATUS_CONTACTING_NETWORK+ " ...");
                App.Identity.Client.TryFindAccount(_Search);
            }
        }
    }
}
