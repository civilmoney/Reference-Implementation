#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using Bridge.Html5;
using System;
using System.Collections.Generic;

namespace CM.Javascript {

    /// <summary>
    /// A base class for listed Civil Money data.
    /// </summary>
    internal abstract class ListResult {
        public List<string> Corroborators = new List<string>();
        public string Data;
        public HTMLDivElement Element;
        public Action<ListResult, bool> OnCheckedChanged;
        public Action<ListResult> OnClick;
        protected HTMLElement Counter;
        protected HTMLDivElement Inner;
        protected string OriginalPath;

        public ListResult(string data, string path) {
            Data = data;
            OriginalPath = path;
            Element = new HTMLDivElement() { ClassName = "res" };
        }

        public abstract string UniqueKey { get; }

        public abstract void Build();

        public void OnCorroborated(string peer) {
            if (Corroborators.Contains(peer))
                return;
            Corroborators.Add(peer);
            if (Counter != null)
                Counter.InnerHTML = Corroborators.Count.ToString();
        }
    }

    /// <summary>
    /// A base class for scrollable/paginated list components.
    /// </summary>
    internal class PagedList<T>
        where T : ListResult {
        public Action<ListResult, bool> OnCheckedChanged;
        public Action<ListResult> OnClick;
        private Dictionary<string, ListResult> _Data;
        private HTMLElement _Div;
        private Feedback _Feedback;
        private bool _IsQueryRunning;
        private int _PageSize;
        private string _Path;
        private HTMLElement _Results;
        private int _StartAt;
        private int _Total;

        public PagedList(HTMLElement parent, string path) {
            _Path = path;
            _Div = parent.Div("list");
            _Results = _Div.Div();
            _Feedback = new Feedback(_Div);
            _Data = new Dictionary<string, ListResult>();
            _Total = 0;
            _PageSize = 10;
        }

        public bool IsScrollAtBottom {
            get {
                var bottom = _Results.Position().Y + _Results.ScrollHeight;
                var windowBottom = Window.ScrollY + Window.InnerHeight;
                return (bottom < windowBottom);
            }
        }

        public bool TryFindResult(string key, out ListResult r) {
            return _Data.TryGetValue(key, out r);
        }

        public void UpdateResults(bool force = false) {
            if (_StartAt > _Total && !force)
                return;
            if (_IsQueryRunning)
                return;
            _IsQueryRunning = true;
            _Feedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);
            App.Identity.Client.TryList(_Path, new AsyncRequest<Schema.ListRequest>() {
                Item = new Schema.ListRequest() {
                    APIVersion = Constants.APIVersion,
                    StartAt = (uint)_StartAt,
                    Max = (uint)_PageSize,
                    UpdatedUtcFromInclusive = DateTime.UtcNow.AddYears(-2),
                    UpdatedUtcToExclusive = DateTime.UtcNow.AddDays(1), // just in case of bad clocks
                    Sort = "UPD-UTC DESC",
                },
                OnProgress = (prog) => {
                    _Feedback.Set(Assets.SVG.Wait, FeedbackType.Default,
                        prog.ProgressPercent + "% " + SR.LABEL_STATUS_CONTACTING_NETWORK);
                },
                OnComplete = (sender) => {
                    if (sender.Result == CMResult.S_OK) {
                        if (_Data.Count == 0)
                            _Feedback.Set(Assets.SVG.CircleUnknown, FeedbackType.Default,
                                SR.LABEL_NO_ITEMS_FOUND);
                        else
                            _Feedback.Hide();
                    } else {
                        _Feedback.Set(Assets.SVG.CircleError, FeedbackType.Default,
                            sender.Result.GetLocalisedDescription());
                    }
                    _IsQueryRunning = false;
                }
            }, ResultSink);
            _StartAt += _PageSize;
        }

        private void ResultSink(Peer p, Schema.ListResponse e) {
            if (_Total < e.Total)
                _Total = (int)e.Total;
            for (int i = 0; i < e.Values.Count; i++) {
                var v = e.Values[i];
                if (v.Name == "ITEM") {
                    ListResult res = Activator.CreateInstance<T>(v.Value, _Path);
                    ListResult existing;
                    if (!_Data.TryGetValue(res.UniqueKey, out existing)) {
                        existing = res;
                        res.OnClick = OnClick;
                        res.OnCheckedChanged = OnCheckedChanged;
                        res.Build();
                        _Data[res.UniqueKey] = res;
                        _Results.AppendChild(res.Element);
                    }
                    existing.OnCorroborated(p.EndPoint);
                }
            }
        }
    }

    /// <summary>
    /// A UI component for displaying a scrollable/paginated list of transactions.
    /// </summary>
    internal class TransactionListResult : ListResult {
        private string _ID;
        private Schema.TransactionIndex _Index;
        private HTMLDivElement _Info;
        private string _Key;

        public TransactionListResult(string data, string path)
            : base(data, path) {
            _Index = new Schema.TransactionIndex(data);
            _ID = path.Split('/')[1]; // The account we're looking at
            _Key = _Index.ID;
        }

        public override string UniqueKey {
            get {
                return _Key;
            }
        }

        public override void Build() {
            Inner = Element.Div("summary");
            //2016-06-14T18:47:02 abc test1 1207.000000 2016-06-14T18:47:02 0 1 payee-region payer-region

            bool isPayee = _ID == _Index.Payee;
            var otherPerson = isPayee ? _Index.Payer : _Index.Payee; // payee/payer

            var checkHolder = Inner.Div("checkregion");
            if (isPayee
                && _Index.PayeeStatus == Schema.PayeeStatus.NotSet
                && _Index.PayerStatus == Schema.PayerStatus.Accept) {
                var check = checkHolder.CheckBox("");
                check.OnChange = (e) => {
                    if (OnCheckedChanged != null)
                        OnCheckedChanged(this, check.Checked);
                };
            }
            var region = Inner.Div("hitregion");
            region.AddEventListener(EventType.MouseUp, OnRowClick);

            Counter = region.Div().Span(Corroborators.Count.ToString(), "counter");
            var date = region.Div(null, _Index.CreatedUtc.ToString("yyyy-MM-dd")) as HTMLElement;
            var party = region.Div(null, Page.HtmlEncode(otherPerson)) as HTMLElement;

            var amount = Helpers.CalculateTransactionDepreciatedAmount(DateTime.UtcNow, _Index.CreatedUtc, _Index.Amount)
                * (isPayee ? 1 : -1);

            string prefix = "";
            switch (_Index.PayeeStatus) {
                case Schema.PayeeStatus.Accept:
                case Schema.PayeeStatus.NotSet:
                    switch (_Index.PayerStatus) {
                        case Schema.PayerStatus.NotSet: // Payer with NotSet is technically invalid
                        case Schema.PayerStatus.Accept: {
                                // use amount
                                if (_Index.PayeeStatus == Schema.PayeeStatus.NotSet)
                                    prefix = SR.LABEL_PAYEE_STATUS_NOTSET;
                            }
                            break;

                        case Schema.PayerStatus.Cancel:
                            prefix = SR.LABEL_PAYER_STATUS_CANCEL;
                            break;

                        case Schema.PayerStatus.Dispute:
                            prefix = Assets.SVG.Warning.ToString(16, 16, "#000000") + " " + SR.LABEL_PAYER_STATUS_DISPUTE;
                            break;
                    }
                    break;

                case Schema.PayeeStatus.Decline: prefix = SR.LABEL_PAYEE_STATUS_DECLINE; break;
                case Schema.PayeeStatus.Refund: prefix = SR.LABEL_PAYEE_STATUS_REFUND; break;
            }
            var amountDiv = region.Div("amount").Amount(amount, prefix: prefix, roundTo2DP: false);
        }

        public void Update(Schema.Transaction t) {
            _Index.UpdatedUtc = t.UpdatedUtc;
            _Index.PayeeStatus = t.PayeeStatus;
            _Index.PayerStatus = t.PayerStatus;
            Element.Clear();
            Build();
            if (_Info != null) { // was it expanded? rebuild that too
                _Info = new HTMLDivElement();
                BuildTransactionInfo(t);
                Element.AppendChild(_Info);
            }
        }

        private void BuildTransactionInfo(Schema.Transaction t) {
            if (_Info == null)
                return;
            new TransactionInfo(_Info, t, false);
        }

        private void OnRowClick(Event e) {
            if (OnClick != null)
                OnClick(this);
            if (_Info != null) {
                _Info.RemoveEx();
                _Info = null;
                return;
            }
            _Info = new HTMLDivElement() {
                ClassName = "info"
            };
            var f = new Feedback(_Info);
            f.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);
            App.Identity.Client.TryFindTransaction(new AsyncRequest<FindTransactionRequest>() {
                Item = new FindTransactionRequest(_Key),
                OnComplete = (r) => {
                    if (r.Result.Success) {
                        BuildTransactionInfo(r.Item.Output);
                        f.Hide();
                    } else {
                        f.Set(Assets.SVG.Warning, FeedbackType.Default,
                            r.Result.GetLocalisedDescription());
                    }
                }
            });
            Element.AppendChild(_Info);
        }
    }
}