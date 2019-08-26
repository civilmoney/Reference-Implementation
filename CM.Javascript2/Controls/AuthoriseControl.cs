using SichboUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Retyped;
using CM.Schema;

namespace CM.JS.Controls {
    class AuthoriseControl : Element {

        public AuthoriseControl(string id, int newStatus, Action<bool> onDone, params Schema.TransactionIndex[] items) {
            
            
            this.MaxWidth.Value = 1000;

            var st = new StackPanel();
            st.HorizontalAlignment = Alignment.Stretch;
            string title1 = null;
            string title2 = null;

            bool isPayee = String.Equals(id, items[0].Payee, StringComparison.OrdinalIgnoreCase);
            if (items.Length == 1) {
                title2 = isPayee ? items[0].Payer : items[0].Payee;
            } else {
                title2 = "multiple transactions";
            }


            if (isPayee) {
                var s = (Schema.PayeeStatus)newStatus;
                switch (s) {
                    case Schema.PayeeStatus.Accept: title1 = "Accept money from"; break;
                    case Schema.PayeeStatus.Decline: title1 = "Decline money from"; break;
                    case Schema.PayeeStatus.Refund: title1 = "Refund money to"; break;
                }
            } else {
                var s = (Schema.PayerStatus)newStatus;
                switch (s) {
                    case Schema.PayerStatus.Accept: title1 = "Accept money for"; break;
                    case Schema.PayerStatus.Dispute: title1 = "Dispute money with"; break;
                    case Schema.PayerStatus.Cancel: title1 = "Cancel money with"; break;
                }
            }

            st.El("h1",
                text: title1 + " " + title2,
                margin: new Thickness(0, 0, 15, 0));

            var div = st.Div().Html;
            decimal total = 0;
            var ar = new List<AuditControl.Item>();
            for (int i = 0; i < items.Length; i++) {
                var item = new AuditControl.Item(id, items[i], showTagField: true);
                ar.Add(item);
                item.BeginCorroborate();
                div.appendChild(item.Element);
                total += items[i].Amount;
            }

            if (items.Length > 1) {
                st.El("h2",
                    style:"font-size:3em;",
                    margin: new Thickness(15, 0)).Html.Amount(total, "total: ");
            }

            Add(st);

            App.Instance.Client.TryFindAccount(new AsyncRequest<FindAccountRequest>() {
                Item = new FindAccountRequest(id),
                OnComplete = (accountRequest) => {
                    if (accountRequest.Result == CMResult.S_OK) {

                        var a = accountRequest.Item.Output.Cast<Schema.Account>();
                        var sig = new SignatureBox(a);
                        st.Add(sig);
                        st.Add(new Controls.ButtonBar(SR.LABEL_CONTINUE,
                            (b) => {
                                b.Loading();

                                var sign = new AsyncRequest<Schema.DataSignRequest>();
                                sign.Item = new Schema.DataSignRequest();
                                for (int i = 0; i < ar.Count; i++) {
                                    var item = ar[i];
                                    var t = item.Transaction;
                                    if (isPayee) {
                                        // payee side
                                        t.PayeeTag = item.TagField.value;
                                        t.PayeeStatus = (Schema.PayeeStatus)newStatus;
                                        t.PayeeUpdatedUtc = DateTime.UtcNow;
                                        if (t.PayeeRegion == null)
                                            t.PayeeRegion = a.Iso31662Region;
                                        item.TempTransform = new Schema.DataSignRequest.Transform(t.GetPayeeSigningData());
                                    } else {
                                        t.PayerTag = item.TagField.value;
                                        t.PayerStatus = (Schema.PayerStatus)newStatus;
                                        t.PayerUpdatedUtc = DateTime.UtcNow;
                                        if (t.PayerRegion == null)
                                            t.PayerRegion = a.Iso31662Region;
                                        item.TempTransform = new Schema.DataSignRequest.Transform(t.GetPayerSigningData());
                                    }
                                    sign.Item.Transforms.Add(item.TempTransform);
                                }
                                var prog = new ServerProgressIndicator();
                                prog.Update(ServerProgressIndicatorStatus.Waiting, ar.Count + 1, SR.LABEL_STATUS_SIGNING_INFORMATION + " ...");
                                prog.Show();
                                sign.Item.PasswordOrRSAPrivateKey = sig.PasswordOrPrivateKey;
                                sign.OnComplete = (req) => {
                                    if (req.Result == CMResult.S_OK) {
                                        prog.Update(ServerProgressIndicatorStatus.Waiting, 1,
                                           SR.LABEL_STATUS_CONTACTING_NETWORK + " ...");
                                        int completed = 0;
                                        for (int i = 0; i < ar.Count; i++) {
                                            var item = ar[i];
                                            if (isPayee)
                                                item.Transaction.PayeeSignature = item.TempTransform.Output;
                                            else
                                                item.Transaction.PayerSignature = item.TempTransform.Output;
                                            item.BeginCommit((sender, hr) => {
                                                completed++;
                                                if (completed == ar.Count) {
                                                    b.LoadingDone();
                                                    OnAllTransactionsCommitted(prog, ar, onDone);
                                                }
                                            });
                                        }
                                    } else {
                                        prog.Finished(ServerProgressIndicatorStatus.Error,
                                         SR.LABEL_STATUS_A_PROBLEM_OCCURRED, SR.LABEL_STATUS_SIGNING_FAILED,
                                         () => {

                                         });
                                        b.LoadingDone();
                                    }
                                };
                                a.SignData(sign, JSCryptoFunctions.Identity);

                            }, SR.LABEL_CANCEL,
                            () => onDone(false)));

                    } else {

                        Notification.Show(SR.LABEL_STATUS_A_PROBLEM_OCCURRED,
                            accountRequest.Result.GetLocalisedDescription());

                        onDone(false);

                    }
                }
            });
        }


        private void OnAllTransactionsCommitted(ServerProgressIndicator prog, List<AuditControl.Item> ar, Action<bool> onDone) {

            // So what happened?
            bool didAllTransactionWork = true;
            bool didAllFail = true;
            for (int i = 0; i < ar.Count; i++) {
                if (ar[i].CommitStatus != CMResult.S_OK) {
                    didAllTransactionWork = false;
                } else {
                    didAllFail = false;
                }
            }

            if (ar.Count == 1) {
                // Only 1 transaction, simple..
                if (didAllTransactionWork) {
                    prog.Finished(ServerProgressIndicatorStatus.Success,
                        SR.LABEL_STATUS_TRANSACTION_UPDATED_SUCCESSFULLY,
                        null,
                        () => onDone(true));
                } else {
                    prog.Finished(ServerProgressIndicatorStatus.Error,
                      SR.LABEL_STATUS_A_PROBLEM_OCCURRED,
                       ar[0].CommitStatus.GetLocalisedDescription(),
                       () => {
                       });
                }
            } else {
                // For a multi-transaction situation try to summarise
                // the outcome, but basically if there's a mix of OK/failed
                // commits, they need to start over.
                if (didAllFail) {
                    prog.Finished(ServerProgressIndicatorStatus.Error,
                     SR.LABEL_STATUS_A_PROBLEM_OCCURRED,
                      SR.LABEL_STATUS_NO_TRANSACTIONS_UPDATED,
                      () => {
                      });

                } else {
                    if (didAllTransactionWork) {
                        prog.Finished(ServerProgressIndicatorStatus.Success,
                              SR.LABEL_STATUS_ALL_TRANSACTIONS_UPDATED,
                        null,
                        () => onDone(true));
                    } else {
                        // Remove the ones that worked
                        for (int i = 0; i < ar.Count; i++) {
                            if (ar[i].CommitStatus == CMResult.S_OK) {
                                ar[i].Element.RemoveEx();
                                ar.RemoveAt(i--);
                            }
                        }
                        prog.Finished(ServerProgressIndicatorStatus.Success,
                             SR.LABEL_STATUS_SOME_TRANSACTIONS_FAILED,
                       null,
                       () => { });
                    }

                }
            }
        }

    }
}
