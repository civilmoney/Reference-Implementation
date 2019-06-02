using CM.Schema;
using Retyped;
using SichboUI;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CM.JS.Controls {

    internal delegate Task<FieldSearchResult> FieldSearchDelegate(FieldSearchArgs e);

    internal enum FieldType {
        Textbox,
        Pass,
        Textarea,
        Select,
        Email,
        Toggle,
        Search,
        Account,
        Amount
    }

    internal class Field : Element {
        public Element Next { get; set; }

        private static Element _ScrollingTo;
        private static int _UniqueField;
        private AsyncRequest<FindAccountRequest> _AccountSearch;
        private Account _AmountAccount;
        private int _DefaultHeight;
        private Element _Error;
        private string _Label;
        private string _LastError;
        private string _LastRaisedValue;
        private Element _lbl;
        private FieldSearchDelegate _OnSearch;
        private Element _Results;
        private int _SearchToke;
        private string _SearchVal;
        private FieldType _Type;
        private Element _Val;

        public Field(FieldType type,
            string label, string value = "", string placeholder = "",
            bool @checked = false,
            FieldSearchDelegate onSearch = null,
            string fieldID = null,
            int border = 1)
            : base(style: type == FieldType.Toggle ? ""
                  : $"background: #fff;border-radius:15px;border:{border}px solid transparent;"
                  ) {
            _OnSearch = onSearch;
            _Type = type;
            _Label = label;
            _lbl = new Element(tagName: "label", text: label, style: "line-height: 1em;");
            _lbl.Margin.Value = new Thickness(15, 15, 0, type == FieldType.Toggle ? 0 : 15);
            _lbl.VerticalAlignment = Alignment.Top;
            _lbl.Height.Value = 15;
            if (type == FieldType.Toggle)
                Class = "onoff";

            _Val = new Element(tagName:
                type == FieldType.Pass
                || type == FieldType.Textbox
                || type == FieldType.Email
                || type == FieldType.Toggle
                || type == FieldType.Search
                || type == FieldType.Account
                || type == FieldType.Amount ? "input"
                : type == FieldType.Textarea ? "textarea"
                : type == FieldType.Select ? "select"
                : throw new NotImplementedException());

            _Val.MinHeight.Value = 15;
            _Val.Margin.Value = new Thickness(15 + 15 + 10, 15, 15, 15);
            _Val.Html.id = fieldID == null ? "_f" + (++_UniqueField) : fieldID;

            var input = _Val.Html.As<Retyped.dom.HTMLInputElement>();
            input.autocomplete = "off";

            input.placeholder = placeholder;
            switch (type) {
                case FieldType.Email:
                    input.type = "email";
                    input.autocomplete = "email";
                    break;

                case FieldType.Pass:
                    input.type = "password";
                    input.autocomplete = "current-password";
                    break;

                case FieldType.Amount:
                    input.type = "number";
                    input.step = "any";
                    El("div", style: "font-weight:900;pointer-events:none;",
                        text: "//c",
                        halign: Alignment.Left,
                        margin: _Val.Margin.Value);
                    _Val.Margin.Value = new Thickness(_Val.Margin.Value.Top, _Val.Margin.Value.Right, _Val.Margin.Value.Bottom, _Val.Margin.Value.Left + 30);
                    break;

                case FieldType.Toggle:
                    input.type = "checkbox";
                    _Val.Html.style.display = "none";
                    _Val.Margin.Reset();
                    input.@checked = @checked;
                    break;

                case FieldType.Textarea:
                    new Retyped.dom.MutationObserver((mutations, observer) => {
                        if (DefaultHeight == 0) {
                            _Val.Height.Value = _Val.Html.offsetHeight;
                        }
                    }).observe(_Val.Html, new Retyped.dom.MutationObserverInit() {
                        attributes = true,
                        attributeFilter = new[] { "style" }
                    });
                    break;
            }
            if (type == FieldType.Account) {
                _OnSearch = new FieldSearchDelegate(OnSearchAccount);
            }
            if (type == FieldType.Search || type == FieldType.Account) {
                _SearchVal = value;
                ResolveSearchVal();
            } else {
                input.value = value;
            }

            _Val.VerticalAlignment = Alignment.Top;
            Margin.Value = new Thickness(0, 0, 15, 0);

            _lbl.Html.setAttribute("for", _Val.Html.id);

            _Val.Html.addEventListener("change", (e) => {
                RaiseChanged();
            });
            _Val.Html.addEventListener("keyup", (e) => {
                RaiseChanged();
            });
            if (type == FieldType.Toggle) {
                // order matters for css rule
                _lbl.VerticalAlignment = Alignment.Center;
                _lbl.Height.Reset();
                Add(_Val);
                Add(_lbl);
                _lbl.Html.setAttribute("tabindex", "0");
                _lbl.Html.onfocus = (_) => {
                    _Error?.Remove();
                    _Error = null;
                };
            } else {
                _lbl.Html.style.color = Colors.DarkText;
                _Val.Html.style.color = Colors.DarkText;
                _Val.Html.style.fontWeight = "bold";
                Html.style.borderColor = Colors.LightBorder;
                _Val.Html.onfocus = (_) => {
                    _Error?.Remove();
                    _Error = null;
                    _lbl.Html.style.color = Colors.FieldActive;
                    Html.style.borderColor = Colors.FieldActive;
                    if (border != 0)
                        Html.style.backgroundColor = Colors.LightBlue;
                    if (IsSearch) {
                        if (_Results != null && _Results.Parent == null)
                            Add(_Results);
                    }
                };
                _Val.Html.onblur = (_) => {
                    Html.style.borderColor = Colors.LightBorder;
                    _lbl.Html.style.color = Colors.DarkText;
                    if (border != 0)
                        Html.style.backgroundColor = "#fff";
                    //   m_Results?.Remove();
                };

                Add(_lbl);
                Add(_Val);
            }

            if (IsSearch) {
                _Val.Html.addEventListener("keyup", (Retyped.dom.Event e) => {
                    ResolveSearchKeyword();
                });
            }
        }
        public event Action<Field> ValueOrCheckedChanged;

        public Schema.Account AccountValue { get; set; }

        public Schema.Account AmountAccount {
            get => _AmountAccount;
            set {
                if (_AmountAccount != value) {
                    _AmountAccount = value;
                    if (AmountValue != null)
                        ValidateAmount();
                }
            }
        }

        public decimal? AmountValue {
            get {
                var str = _Val.Html.As<dom.HTMLInputElement>().value.Replace(SR.CHAR_THOUSAND_SEPERATOR, "");
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
        }

        public int DefaultHeight {
            get => _DefaultHeight;
            set {
                _Val.Html.style.resize = "none";
                _DefaultHeight = value;
                _Val.Height.Value = value;
            }
        }

        public bool IsChecked {
            get => _Val.Html.As<Retyped.dom.HTMLInputElement>().@checked;
            set {
                _Val.Html.As<Retyped.dom.HTMLInputElement>().@checked = value;
            }
        }

        public Element LabelElement => _lbl;

        public string ToggleGroup { get; set; }

        public string Value {
            get => IsSearch ? _SearchVal : _Val.Html.As<Retyped.dom.HTMLInputElement>().value;
            set {
                if (IsSearch) {
                    _SearchVal = value;
                    ResolveSearchVal();
                } else {
                    _Val.Html.As<Retyped.dom.HTMLInputElement>().value = value;
                    RaiseChanged();
                }
            }
        }

        public Element ValueElement => _Val;

        private bool IsSearch => _Type == FieldType.Search || _Type == FieldType.Account;

        public static string GetAmountFeedback(decimal amount, Account a = null) {
            var feedback = String.Format(SR.LABEL_AMOUNT_HINT,
                     System.Math.Round(amount * 50, 2).ToString("N2"),
                    System.Math.Round(amount / 1, 2).ToString("N2"));
            if (a != null
                && a.AccountCalculations != null
                && a.AccountCalculations.RecentCredits != null
                && a.AccountCalculations.RecentDebits != null) {
                //_AmountFeedback
                decimal rep;
                RecentReputation name;
                var calcs = a.AccountCalculations;
                Helpers.CalculateRecentReputation(calcs.RecentCredits.Value,
                    calcs.RecentDebits.Value + amount,
                    out rep, out name);
                var balance = Helpers.CalculateAccountBalance(calcs.RecentCredits.Value, calcs.RecentDebits.Value + amount);

                feedback += "\n" + String.Format(SR.LABEL_REMAINING_BALANCE_HINT, balance, name.ToLocalisedName() + " (" + rep.ToString() + ")");
            }
            return feedback;
        }

        public void AddOption(string value, string label) {
            _Val.Html.appendChild(new Retyped.dom.HTMLOptionElement() { value = value, text = label });
        }

        public void ClearError() {
            _Error?.Remove();
            _Error = null;
            _LastError = null;
        }

        public void RaiseChanged() {
            var v = _Type == FieldType.Toggle ? (IsChecked ? "1" : "0")
                : _Type == FieldType.Account ? AccountValue?.ID
                : Value;
            if (_LastRaisedValue != v) {
                _LastRaisedValue = v;
                if (_Type == FieldType.Amount)
                    ValidateAmount();
                ProcessToggleGroup();
                ValueOrCheckedChanged?.Invoke(this);
            }
        }

        public void SetError(string msg) {
            if (_Error != null
                && _Error.Parent != null
                && _LastError == msg)
                return; // already there
            _Error?.Remove();
            _Error = null;

            _LastError = msg;
            _Error = new Element(text: msg, style: "background:#cc0000;color:#fff;padding:5px 15px;border-radius:15px 15px 15px 0;");
            _Error["iserror"] = "1";
            _Error.VerticalAlignment = Alignment.Top;
            _Error.Margin.Value = new Thickness(0, 0, 0, 15);
            _Error.RelativeTransform.Value = new RelativeTransform(1, 1, 0, 0, 0, 0, 0);
            _Error.AnimFadeInOut()
                .AnimFlyIn(new SichboUI.RelativeTransform(1, 1, 0, 0, 0, -.3f, 0), _Error.RelativeTransform.Value, ease: Easing.CubicBackOut)
                .AnimFlyOut(new SichboUI.RelativeTransform(1, 1, 0, 0, 0, .3f, 0), ease: Easing.CubicBackIn);
            var st = Parent as StackPanel;
            st.InsertStackChildAt(st.StackChildIndex(this), _Error);

            if (_ScrollingTo == null || _ScrollingTo.Bounds.Y > this.Bounds.Y)
                _ScrollingTo = _Error;
            Retyped.dom.setTimeout((e) => {
                if (_ScrollingTo != null) {
                    _ScrollingTo.ScrollIntoView();
                    _ScrollingTo = null;
                }
            }, 500);
        }

        protected override void OnFocusing() {
            _Val.Html.focus();
        }

        protected override void OnOKPressed(KeyEventArgs e) {
            if (Next != null && !e.IsDown) {
                Next.Focus();
                e.IsHandled = true;
            }
        }

        private async Task<FieldSearchResult> OnSearchAccount(FieldSearchArgs e) {
            var res = new FieldSearchResult() {
                Items = new List<FieldSearchResult.Item>()
            };
            if (_AccountSearch != null)
                _AccountSearch.IsCancelled = true;
            if (!Helpers.IsIDValid(e.Query)) {
                SetError(SR.LABEL_ACCOUNT_NAME_INSTRUCTIONS);
                return res;
            }
            ClearError();

            bool isComplete = false;
            _AccountSearch = new AsyncRequest<FindAccountRequest>() {
                Item = new FindAccountRequest(e.Query),
                OnComplete = (req) => {
                    isComplete = true;
                    if (req != _AccountSearch || req.IsCancelled)
                        return; // stale search

                    if (req.Result == CMResult.S_OK) {
                        AccountValue = req.Item.Output.Cast<Schema.Account>();
                        res.Total = 1;
                        res.Items.Add(new FieldSearchResult.Item() {
                            Value = AccountValue.ID,
                            Label = AccountValue.ID
                        });
                    } else {
                        AccountValue = null;
                    }
                }
            };
            App.Instance.Client.TryFindAccount(_AccountSearch);
            while (!isComplete) {
                await Task.Delay(100);
            }
            return res;
        }

        private void OnSearchResSelected(dom.Event e) {
            var item = e.currentTarget["item"].As<FieldSearchResult.Item>();
            _SearchVal = item.Value;
            _Val.Html.As<Retyped.dom.HTMLInputElement>().value = item.Label;
            _Results?.Remove();
            _Results = null;
            _lbl.TextContent = _Label + " ✔";
        }

        private void ProcessToggleGroup() {
            if (_Type != FieldType.Toggle || String.IsNullOrEmpty(ToggleGroup))
                return;
            if (!_Val.Html.As<dom.HTMLInputElement>().@checked)
                return;
            var cur = Parent.Html.firstElementChild;
            while (cur != null) {
                if (cur != Html) {
                    var f = cur["__el"] as Field;
                    if (f != null && f.ToggleGroup == ToggleGroup)
                        f.IsChecked = false;
                }
                cur = cur.nextElementSibling;
            }
        }
        private async void ResolveSearchKeyword() {
            var toke = ++_SearchToke;
            await Task.Delay(300);
            if (toke != _SearchToke)
                return; // superseded.
            if (String.IsNullOrWhiteSpace(_Val.Html.As<Retyped.dom.HTMLInputElement>().value)) {
                _SearchVal = "";
                _Results?.Remove();
                _Results = null;
                _lbl.TextContent = _Label;
                return;
            }
            if (_Results == null && _Type != FieldType.Account) {
                _Results = Div(className: "field-results");
                // m_Results.VerticalAlignment = Alignment.Bottom;
                _Results.Height.Value = 200;
                _Results.Margin.Value = new Thickness(70, 8, 8, 4);
            }
            BringToFront();
            _Results?.Clear();
            _lbl.TextContent = _Label + " ⏳";
            _SearchVal = "";
            var query = _Val.Html.As<Retyped.dom.HTMLInputElement>().value;
            var res = await _OnSearch(new FieldSearchArgs() {
                Query = query,
                StartAt = 0,
                PageSize = 100
            });

            if (_Type == FieldType.Account) {
                _lbl.TextContent = _Label + (res.Items.Count == 1 ? " ✔" : " ⚠" + SR.TITLE_NOT_FOUND);
                RaiseChanged();
            } else {
                _Results.Clear();
                _lbl.TextContent = _Label + " " + (String.IsNullOrEmpty(_SearchVal) ? "(not yet set)" : "✔");
                if (res.Total == 0) {
                    _Results.Html.Div("row").Div(
                        "col", text: $"No results for '{query}'.",
                        style: "padding-bottom:15px;");
                    return;
                }
                for (int i = 0; i < res.Items.Count; i++) {
                    _Results.Html.Div("row").Button("col", text: res.Items[i].Label, onClick: OnSearchResSelected)["item"] = res.Items[i];
                }
            }
        }

        private async void ResolveSearchVal() {
            if (String.IsNullOrWhiteSpace(_SearchVal)) {
                _Val.Html.As<Retyped.dom.HTMLInputElement>().value = "";
                _lbl.TextContent = _Label;
            } else {
                _lbl.TextContent = _Label + " ⏳";
                var res = await _OnSearch(new FieldSearchArgs() {
                    Value = _SearchVal
                });
                if (res.Total == 1) {
                    _Val.Html.As<Retyped.dom.HTMLInputElement>().value = res.Items[0].Label;
                    _lbl.TextContent = _Label + " ✔";
                } else {
                    _lbl.TextContent = _Label + " ⚠";
                }
            }
        }

        private void ValidateAmount() {
            decimal amount = AmountValue.GetValueOrDefault();

            if (amount >= Constants.MinimumTransactionAmount) {
                var feedback = GetAmountFeedback(amount, AmountAccount);

                if (_Error != null && _Error["iserror"].As<string>() == "1") {
                    _Error?.Remove();
                    _Error = null;
                }
                if (_Error == null) {
                    _Error = new Element(text: feedback, style: "white-space: pre-wrap;");
                    _Error.VerticalAlignment = Alignment.Top;
                    _Error.Margin.Value = new Thickness(-10, 15, 15, 15);
                    _Error.RelativeTransform.Value = new RelativeTransform(1, 1, 0, 0, 0, 0, 0);
                    _Error.AnimFadeInOut()
                        .AnimFlyIn(new SichboUI.RelativeTransform(1, 1, 0, 0, 0, -.3f, 0), _Error.RelativeTransform.Value, ease: Easing.CubicBackOut)
                        .AnimFlyOut(new SichboUI.RelativeTransform(1, 1, 0, 0, 0, .3f, 0), ease: Easing.CubicBackIn);
                } else {
                    _Error.TextContent = feedback;
                }
                var st = Parent as StackPanel;
                st.InsertStackChildAt(st.StackChildIndex(this) + 1, _Error);
            } else {
                SetError(SR.LABEL_THE_AMOUNT_IS_INVALID);
            }
        }
    }

    internal class FieldSearchArgs {
        public int PageSize { get; set; }
        public string Query { get; set; }
        public int StartAt { get; set; }
        public string Value { get; set; }
    }

    internal class FieldSearchResult {
        public string Error { get; set; }
        public List<Item> Items { get; set; } = new List<Item>();
        public int Total { get; set; }

        public class Item {
            public string Label { get; set; }
            public string Value { get; set; }
        }
    }
}