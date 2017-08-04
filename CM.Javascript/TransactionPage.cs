#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using Bridge.Html5;

namespace CM.Javascript {

    /// <summary>
    /// A full-page transaction viewer.
    /// </summary>
    internal class TransactionPage : Page {
        private HTMLDivElement _Holder;
        private string _ID;
        private TransactionInfo _Info;
        private Feedback _MainFeedback;
        private Page _PreviousPage;
        public TransactionPage(string id) {
            _ID = id;
            _PreviousPage = App.Identity.CurrentPage;
        }

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
            Element.ClassName = "transactionpage";
            Element.Div("top").H1(SR.TITLE_TRANSACTION_DETAILS);
            _MainFeedback = new Feedback(Element);
            _MainFeedback.Set(Assets.SVG.Wait, FeedbackType.Default, SR.LABEL_STATUS_CONTACTING_NETWORK);
            _Holder = Element.Div();

            if (_PreviousPage != null)
                Element.Div("button-row").Button("Close", (e) => {
                    App.Identity.CurrentPage = _PreviousPage;
                });
            App.Identity.Client.TryFindTransaction(new AsyncRequest<FindTransactionRequest>() {
                Item = new FindTransactionRequest(_ID),
                OnComplete = (r) => {
                    if (r.Result.Success) {
                        _Info = new TransactionInfo(_Holder, r.Item.Output, true);
                        _MainFeedback.Hide();
                    } else {
                        _MainFeedback.Set(Assets.SVG.Warning, FeedbackType.Error, r.Result.GetLocalisedDescription());
                    }
                }
            });
        }

        public void OnTransactionChanged(Schema.Transaction t) {
            if (_Info != null
                && _Info.Transaction.ID == t.ID) {
                _Holder.Clear();
                _Info = new TransactionInfo(_Holder, t, true);
            }
            if (_PreviousPage is AccountPage)
                ((AccountPage)_PreviousPage).OnTransactionChanged(t);
        }
    }
}