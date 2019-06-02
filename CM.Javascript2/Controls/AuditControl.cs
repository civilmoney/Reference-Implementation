using CM.Schema;
using Retyped;
using SichboUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CM.JS.Controls {

    internal class AuditControl : Element {
        private string _AccountID;
        private Dictionary<string, Item> _Data;
        private TimeSeriesGraph _Graph;
        private bool _IsQueryRunning;
        private Element _Items;
        private int _PageSize;
        private string _Path;
        private bool _ShowActions;
        private int _StartAt;
        private TickBar _TickActions;
        private int _Total;

        public AuditControl(string path, bool showActions = false, bool showGraph = true) {
            _Path = path;
            _Total = 0;
            _PageSize = 10;
            _AccountID = _Path.Split('/')[1];   // The account we're looking at
            _ShowActions = showActions;
            _Data = new Dictionary<string, Item>();
            var st = new StackPanel();
            st.HorizontalAlignment = Alignment.Stretch;
            if (!showGraph) {
                st.El("div", style: "text-align:center;font-weight:bold;", text: "Audit data for " + _AccountID);
            } else {
                _Graph = new SichboUI.TimeSeriesGraph(new Vector2(800, 400));
                st.Div().Html.appendChild(_Graph.Element);//margin:new Thickness(30)
            }
            _Items = st.Div();
            _Items.Margin.Value = new Thickness(30, 0, 0, 0);
            _Items.VerticalAlignment = Alignment.Top;
            Add(st);
        }

        public bool IsScrollAtBottom {
            get {
                return _Items.Bounds.Bottom - dom.window.innerHeight < 100;
            }
        }

        public void UpdateResults(bool force = false) {
            if (_StartAt > _Total && !force)
                return;
            if (_IsQueryRunning)
                return;
            _IsQueryRunning = true;
            OSD.Show(SR.LABEL_STATUS_CONTACTING_NETWORK);
            App.Instance.Client.TryList(_Path, new AsyncRequest<Schema.ListRequest>() {
                Item = new Schema.ListRequest() {
                    APIVersion = Constants.APIVersion,
                    StartAt = (uint)_StartAt,
                    Max = (uint)_PageSize,
                    UpdatedUtcFromInclusive = DateTime.UtcNow.AddYears(-2),
                    UpdatedUtcToExclusive = DateTime.UtcNow.AddDays(1), // just in case of bad clocks
                    Sort = "UPD-UTC DESC",
                },
                OnProgress = (prog) => {
                    OSD.Show(prog.ProgressPercent.ToString("N0") + "% " + SR.LABEL_STATUS_CONTACTING_NETWORK);
                },
                OnComplete = (sender) => {
                    OSD.Clear();
                    if (sender.Result == CMResult.S_OK) {
                        RefreshTimeSeries();
                        if (_Data.Count == 0) {
                            _Items.Html.Div("instructions", style: "text-align:center;padding:80px 0;", text: SR.LABEL_NO_ITEMS_FOUND);
                        }
                    } else {
                        _Items.Html.Div("instructions", style: "text-align:center;padding:80px 0;", text: sender.Result.GetLocalisedDescription());
                    }
                    _IsQueryRunning = false;
                }
            }, ResultSink);
            _StartAt += _PageSize;
        }

        protected override void OnAddedOverride() {
            Html.PollUntilRemoved(2000, OnScroll, OSD.Clear);
        }

        private void InsertResultSorted(Item res) {
            var it = _Items.Html.firstElementChild;
            while (it != null) {
                var item = it["item"].As<Item>();
                if (item.Value.UpdatedUtc < res.Value.UpdatedUtc) {
                    _Items.Html.insertBefore(res.Element, it);
                }
                it = it.nextElementSibling;
            }
            _Items.Html.appendChild(res.Element);
        }

        private void OnItemTicked(Item item, bool ticked) {
            if (_TickActions == null || _TickActions.Html.parentElement == null) {
                _TickActions = new TickBar(this);
                dom.document.body.appendChild(_TickActions.Html);
                _TickActions.Play();
            }
            _TickActions.OnItemTicked(item, ticked);
        }

        private void OnScroll() {
            if (IsScrollAtBottom) {
                UpdateResults();
            }
        }

        private void RefreshTimeSeries() {
            if (_Graph == null)
                return;

            var credits = new TimeSeriesGraph.Series() {
                Label = "Credits",
                Color = Colors.C1,
                Format = ".00"
            };
            var debits = new TimeSeriesGraph.Series() {
                Label = "Debits",
                Color = Colors.DarkText,
                Format = ".00"
            };
            var d = new TimeSeriesGraph.Data();
            d.Title = "Audit data for " + _AccountID;
            d.Series.Add(debits);
            d.Series.Add(credits);
            DateTime min = DateTime.UtcNow;
            foreach (var item in _Data.Values) {
                // Ignore canceled or disputed payments.
                if (item.Value.PayerStatus != PayerStatus.Accept)
                    continue;

                // Ignore inbound payments not yet accepted.
                if (item.Value.PayeeStatus != PayeeStatus.Accept
                    && String.Equals(item.Value.Payee, _AccountID, StringComparison.OrdinalIgnoreCase))
                    continue;

                var amount = item.ReportAmount;

                var list = amount >= 0 ? credits : debits;
                var day = item.Value.CreatedUtc.Date;
                if (min > day)
                    min = day;

                var v = list.Values.FirstOrDefault(x => x.Utc == day);
                if (v == null) {
                    v = new TimeSeriesGraph.TimeValue() { Utc = day };
                    list.Values.Add(v);
                }
                v.Value += (double)Math.Abs(amount);
            }
            credits.Values.Sort();
            debits.Values.Sort();
            _Graph.Update(TimeSeriesGraph.GraphInterval.Day, min, DateTime.UtcNow, d);
        }
        private void ResultSink(Peer p, Schema.ListResponse e) {
            if (_Total < e.Total)
                _Total = (int)e.Total;

            for (int i = 0; i < e.Values.Count; i++) {
                var v = e.Values[i];
                if (v.Name == "ITEM") {
                    var idx = new Schema.TransactionIndex(v.Value);
                    if (!_Data.TryGetValue(idx.ID, out var existing)) {
                        existing = new Item(_AccountID,
                            idx,
                            ontick: (_ShowActions ? new Action<Item, bool>(OnItemTicked) : null)
                            );
                        _Data[idx.ID] = existing;
                        InsertResultSorted(existing);
                    }
                    existing.OnCorroborated(p.EndPoint);
                }
            }
        }

        public class Item {
            public dom.HTMLInputElement CheckField;
            public CMResult CommitStatus;
            public dom.HTMLElement Element;
            public decimal ReportAmount;
            public dom.HTMLInputElement TagField;
            public Schema.DataSignRequest.Transform TempTransform;
            public Schema.Transaction Transaction;
            public Schema.TransactionIndex Value;
            private dom.HTMLElement _CommitError;
            private Action<Item, bool> _OnTick;
            private bool _Tag;
            private string _ThisAccount;
            private dom.HTMLElement _Wait;

            public Item(string thisAccount,
                            Schema.TransactionIndex value,
                bool showTagField = false,
                 Action<Item, bool> ontick = null) {
                _ThisAccount = thisAccount;
                Value = value;
                Element = new dom.HTMLDivElement() {
                    className = "list-row"
                };
                Element["item"] = this;
                _Tag = showTagField;
                _OnTick = ontick;

                Build();
            }

            public void BeginCommit(Action<Item, CMResult> onComplete) {
                if (_CommitError != null)
                    _CommitError.RemoveEx();
                _CommitError = null;

                var put = new AsyncRequest<PutRequest>() {
                    Item = new PutRequest(Transaction),
                    OnProgress = (sender) => {
                        //(sender as AsyncRequest<PutRequest>).Item.UpdateUIProgress();
                    },
                    OnComplete = (putRes) => {
                        putRes.Item.UpdateUIProgress();
                        CommitStatus = putRes.Result;
                        onComplete(this, putRes.Result);
                        ClearWait();
                        if (!putRes.Result.Success) {
                            _CommitError = Element.Div("error",
                                text: putRes.Result.GetLocalisedDescription());
                        }
                    }
                };
                ShowWait();
                App.Instance.Client.TryPut(put);
            }

            public void BeginCorroborate() {
                ShowWait();
                App.Instance.Client.TryFindTransaction(new AsyncRequest<FindTransactionRequest>() {
                    Item = new FindTransactionRequest(Value.ID),
                    OnComplete = (r) => {
                        ClearWait();
                        if (r.Result.Success) {
                            Transaction = r.Item.Output;
                            Update(Transaction);
                        } else {
                        }
                    }
                });
            }

            public void ClearWait() {
                _Wait?.RemoveEx();
                _Wait = null;
            }

            public void OnCorroborated(string endpoint) {
            }

            public void ShowWait() {
                if (_Wait != null)
                    return;
                _Wait = Element.Div("wait");
            }

            public void Update(Schema.Transaction t) {
                Value.UpdatedUtc = t.UpdatedUtc;
                Value.PayeeStatus = t.PayeeStatus;
                Value.PayerStatus = t.PayerStatus;
                Build();
            }
            private void Build() {
                Element.Clear();
                bool isPayee = String.Compare(_ThisAccount, Value.Payee, true) == 0;
                var otherPerson = isPayee ? Value.Payer : Value.Payee; // payee/payer
                var amount = Helpers.CalculateTransactionDepreciatedAmount(DateTime.UtcNow, Value.CreatedUtc, Value.Amount)
                                // * (isPayee ? 1 : -1)
                                ;
                string status = "";
                string ico = "";
                switch (Value.PayeeStatus) {
                    case Schema.PayeeStatus.Accept:
                    case Schema.PayeeStatus.NotSet:
                        switch (Value.PayerStatus) {
                            case Schema.PayerStatus.NotSet: // Payer with NotSet is technically invalid
                            case Schema.PayerStatus.Accept: {
                                    if (Value.PayeeStatus == Schema.PayeeStatus.NotSet) {
                                        status = SR.LABEL_PAYEE_STATUS_NOTSET;
                                        ico = "pending";
                                    }
                                    ReportAmount = Value.Amount * (isPayee ? 1 : -1);
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

                var date = Element.Div("date");// HH:mm:ss
                if (_OnTick != null && isPayee && Value.PayeeStatus == PayeeStatus.NotSet) {
                    CheckField = date.Span("ch").appendChild(new dom.HTMLInputElement() {
                        type = "checkbox",
                        onchange = OnTick,
                    });
                }
                date.Span(text: Value.UpdatedUtc.ToString("yyyy-MM-dd"));
                if (Transaction != null && _Tag) {
                    TagField = date.Div().TextBox(
                        placeholder: SR.LABEL_TAG,
                        text: isPayee ? Transaction.PayeeTag : Transaction.PayerTag);
                }
                Element.className = "list-row " + ico + (isPayee ? " in" : " out");
                Element.Div("dir").A(url: "/" + Value.ID.Replace(" ", "+"), onClick: App.Instance.AnchorNavigate);

                var info = Element.Div("sum");
                info.Div("person").A(text: otherPerson, url: "/" + otherPerson).target = "_blank";
                info.Div().Amount(amount, roundTo2DP: true, status: status);
            }

            private void OnTick(dom.Event e) {
                _OnTick(this, e.currentTarget.As<dom.HTMLInputElement>().@checked);
            }
        }

        private class TickBar : Element {
            private AuditControl _Owner;
            private List<Item> _Ticked = new List<Item>();

            public TickBar(AuditControl owner) : base(style: "background:#fff;") {
                _Owner = owner;
                VerticalAlignment = Alignment.Bottom;
                var bar = new ButtonBar(SR.LABEL_PAYEE_STATUS_ACCEPT_VERB, OnAcceptTicked,
                      SR.LABEL_CANCEL, OnClearTicked,
                     SR.LABEL_PAYEE_STATUS_DECLINE_VERB, OnDeclineTicked);
                bar.Margin.Value = new Thickness(15);
                Add(bar);
            }

            public void OnItemTicked(Item item, bool ticked) {
                if (!ticked) {
                    _Ticked.Remove(item);
                } else {
                    _Ticked.Add(item);
                }
                if (_Ticked.Count == 0) {
                    Remove();
                }
            }

            protected override Vector2 ArrangeOverride(Vector2 size) {
                size = base.ArrangeOverride(size);
                App.Instance.Margin.Value = new Thickness(0, 0, size.Y, 0);
                return size;
            }

            protected override void OnRemovedOverride() {
                App.Instance.Margin.Value = new Thickness(0);
                base.OnRemovedOverride();
            }

            private void DoThing(PayeeStatus status) {
                var el = new Element();
                var auth = new Controls.AuthoriseControl(_Ticked[0].Value.Payee, (int)status, (changed) => {
                    if (changed) {
                        _Owner.UpdateResults(force: true);
                    }
                    App.Instance.CloseModal();
                }, _Ticked.Select(x => x.Value).ToArray());
                auth.Margin.Value = new Thickness(30);
                el.Div(style: "background:#fff;overflow-x:hidden;overflow-y:auto;").Add(auth);
                App.Instance.ShowModal(el);
                OnClearTicked();
            }

            private void OnAcceptTicked(Button b) {
                DoThing(PayeeStatus.Accept);
            }

            private void OnClearTicked() {
                var ar = _Ticked.ToArray();
                for (int i = 0; i < ar.Length; i++) {
                    ar[i].CheckField.@checked = false;
                }
                Remove();
            }

            private void OnDeclineTicked(Button b) {
                DoThing(PayeeStatus.Decline);
            }
        }
    }
}