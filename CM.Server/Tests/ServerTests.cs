#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

#if TESTS
using System;
using System.IO;
using System.Collections.Generic;
using CM;
using Xunit;
using CM.Schema;

namespace CM.Server.Tests
{
    // https://xunit.github.io/docs/getting-started-dotnet-core.html

    public class ServerTests {
        string _TestDataFolder;

        public ServerTests() {

            _TestDataFolder = Path.Combine(AppContext.BaseDirectory, "TestData");
            if (Directory.Exists(_TestDataFolder))
                Directory.Delete(_TestDataFolder, true);
            Directory.CreateDirectory(_TestDataFolder);

        }

        /// <summary>
        /// General high-level test of the Storage class for Accounts, Transactions and Votes.
        /// </summary>
        [Fact]
        public async void BasicOfflineStorageOperations() {

            int numAccounts = 10;
            int numTransactions = 10;
            int numVotes = 10;

            var store = new Storage(_TestDataFolder, null, new Log(null));
            store.IsOperatingInTestMode = true;

            // ACCOUNT STORAGE
            var accounts = new System.Collections.Generic.List<String>();
            for (int i = 0; i < numAccounts; i++) {
                var a = TestHelpers.CreateAccount();
                var putRes = await store.Put(a);
                Assert.Equal(CMResult.S_OK, putRes.Code);
                Assert.NotNull(putRes.Token);
                DateTime d;
                Assert.Equal(CMResult.S_OK, store.QueryCommitStatus(a.Path, out d));
                Assert.Equal(d, a.UpdatedUtc);
                Assert.Equal(CMResult.S_OK, await store.Commit(putRes.Token));
                accounts.Add(a.ID);
                IStorable reloaded;
                Assert.Equal(CMResult.S_OK, store.Get("ACCNT/" + a.ID, out reloaded));
                Assert.Equal(reloaded.UpdatedUtc, a.UpdatedUtc);
                Assert.Equal(reloaded.Path, a.Path);

                if (i % 50 == 0) {
                    store.PerformHouseKeeping();
                }
            }

            // ACCOUNT LOAD

            var accounts2 = new System.Collections.Generic.List<Schema.Account>();
            for (int i = 0; i < accounts.Count; i++) {
                IStorable reloaded;
                Assert.Equal(CMResult.S_OK, store.Get("ACCNT/" + accounts[i], out reloaded));
                var a = reloaded as Schema.Account;
                Assert.Equal(accounts[i], a.ID);
                accounts2.Add(a);

            }

            // TRANSACTION STORAGE

            var rnd = new Random();
            var balances = new Dictionary<string, decimal>();
            var transes = new Dictionary<string, List<string>>();

            for (int i = 0; i < numTransactions; i++) {

                var payee = accounts2[rnd.Next(0, accounts2.Count - 1)];
                Schema.Account payer = payee;
                while (payer == payee)
                    payer = accounts2[rnd.Next(0, accounts2.Count - 1)];

                var time = DateTime.UtcNow;
                var t = new Schema.Transaction();
                t.APIVersion = 1;
                t.Amount = decimal.Parse((rnd.NextDouble() * 100).ToString("N6"));
                t.PayeeID = payee.ID;
                t.PayerID = payer.ID;
                t.PayerStatus = Schema.PayerStatus.Accept;
                t.PayerRegion = payer.Iso31662Region;
                t.PayerTag = Guid.NewGuid().ToString();
                t.Memo = "Test number " + (i + 1);
                t.CreatedUtc = time;
                t.PayerUpdatedUtc = t.CreatedUtc;
                var sign = new AsyncRequest<Schema.DataSignRequest>() {
                    Item = new Schema.DataSignRequest(t.GetPayerSigningData()) {
                        Password = System.Text.Encoding.UTF8.GetBytes(payer.ID)
                    }
                };

                payer.SignData(sign, CM.Server.CryptoFunctions.Identity);
                Assert.Equal(CMResult.S_OK, sign.Result);
                t.PayerSignature = sign.Item.Transforms[0].Output;
                var putRes = await store.Put(t);
                Assert.Equal(CMResult.S_OK, putRes.Code);
                DateTime updated;
                Assert.Equal(CMResult.S_OK, store.QueryCommitStatus(t.Path, out updated));
                Assert.Equal(updated, t.UpdatedUtc);
                Assert.Equal(CMResult.S_OK, await store.Commit(putRes.Token));

                IStorable istore;
                Assert.Equal(store.Get(t.Path, out istore), CMResult.S_OK);
                var t2 = istore as Schema.Transaction;
                Assert.Equal(t2.ToContentString(), t.ToContentString());

                t2.PayeeRegion = payee.Iso31662Region;
                t2.PayeeStatus = Schema.PayeeStatus.Accept;
                t2.PayeeUpdatedUtc = DateTime.UtcNow;
                sign = new AsyncRequest<Schema.DataSignRequest>() {
                    Item = new Schema.DataSignRequest(t2.GetPayeeSigningData()) {
                        Password = System.Text.Encoding.UTF8.GetBytes(payee.ID)
                    }
                };
                payee.SignData(sign, CM.Server.CryptoFunctions.Identity);
                Assert.Equal(CMResult.S_OK, sign.Result);
                t2.PayeeSignature = sign.Item.Transforms[0].Output;
                putRes = await store.Put(t2);
                Assert.Equal(CMResult.S_OK, putRes.Code);
                Assert.Equal(CMResult.S_OK, store.QueryCommitStatus(t2.Path, out updated));
                Assert.Equal(updated, t2.UpdatedUtc);
                Assert.Equal(CMResult.S_OK, await store.Commit(putRes.Token));

                decimal bal;
                if (!balances.TryGetValue(t.PayerID, out bal)) {
                    bal = Helpers.CalculateAccountBalance(0, 0);
                }
                bal -= t.Amount;
                balances[t.PayerID] = bal;

                if (!balances.TryGetValue(t.PayeeID, out bal)) {
                    bal = Helpers.CalculateAccountBalance(0, 0);
                }
                bal += t.Amount;
                balances[t.PayeeID] = bal;
                List<string> list;
                if (!transes.TryGetValue(t.PayerID, out list)) {
                    list = new List<string>();
                    transes[t.PayerID] = list;
                }
                list.Add(t.ID);
                if (!transes.TryGetValue(t.PayeeID, out list)) {
                    list = new List<string>();
                    transes[t.PayeeID] = list;
                }
                list.Add(t.ID);

                // Transaction IDs restrict max 1 per second per payer/payee pair.
                while ((DateTime.UtcNow - time).TotalSeconds < 1)
                    System.Threading.Thread.Sleep(1);
            }

            // VOTE STORAGE
            Dictionary<string, List<Schema.Vote>> voters = new Dictionary<string, List<Schema.Vote>>();
            Dictionary<uint, int> propositions = new Dictionary<uint, int>();
            for (int i = 0; i < numVotes; i++) {

                // create
                var a = accounts2[rnd.Next(0, accounts2.Count - 1)];
                var now = DateTime.UtcNow;
                List<Schema.Vote> tmp;
                if (!voters.TryGetValue(a.ID, out tmp)) {
                    tmp = new List<Schema.Vote>();
                    voters[a.ID] = tmp;
                }
                var v = new Schema.Vote() {
                    APIVersion = 1,
                    VoterID = a.ID,
                    CreatedUtc = now,
                    PropositionID = (uint)(tmp.Count + 1),
                    Value = (i & 1) != 0,
                    UpdatedUtc = now
                };

                if (!propositions.ContainsKey(v.PropositionID))
                    propositions[v.PropositionID] = 0;

                propositions[v.PropositionID]++;

                var sign = new AsyncRequest<Schema.DataSignRequest>() {
                    Item = new Schema.DataSignRequest(v.GetSigningData()) {
                        Password = System.Text.Encoding.UTF8.GetBytes(a.ID)
                    }
                };
                a.SignData(sign, CM.Server.CryptoFunctions.Identity);
                Assert.Equal(CMResult.S_OK, sign.Result);

                // var sanity = new AsyncRequest<DataVerifyRequest>() {
                //     Item = new DataVerifyRequest() {
                //         DataDateUtc = v.UpdatedUtc,
                //         Input = v.GetSigningData(),
                //         Signature = v.Signature
                //     }
                // };
                // a.VerifySignature(sanity, CryptoFunctions.Identity);
                // Assert.Equal(CMResult.S_OK, sanity.Result);

                v.Signature = sign.Item.Transforms[0].Output;
                var putRes = await store.Put(v);
                Assert.Equal(CMResult.S_OK, putRes.Code);
                DateTime updated;
                Assert.Equal(CMResult.S_OK, store.QueryCommitStatus(v.Path, out updated));
                Assert.Equal(updated, v.UpdatedUtc);
                Assert.Equal(CMResult.S_OK, await store.Commit(putRes.Token));

                // get
                IStorable istore;
                Assert.Equal(store.Get(v.Path, out istore), CMResult.S_OK);
                var v2 = istore as Schema.Vote;
                Assert.Equal(v2.ToContentString(), v.ToContentString());

                // modify
                v2.Value = (i & 1) == 0;
                v2.UpdatedUtc = now.AddSeconds(1);
                sign = new AsyncRequest<Schema.DataSignRequest>() {
                    Item = new Schema.DataSignRequest(v2.GetSigningData()) {
                        Password = System.Text.Encoding.UTF8.GetBytes(a.ID)
                    }
                };
                a.SignData(sign, CM.Server.CryptoFunctions.Identity);
                Assert.Equal(CMResult.S_OK, sign.Result);
                v2.Signature = sign.Item.Transforms[0].Output;
                putRes = await store.Put(v2);
                Assert.Equal(CMResult.S_OK, putRes.Code);
                Assert.Equal(CMResult.S_OK, store.QueryCommitStatus(v.Path, out updated));
                Assert.Equal(updated, v2.UpdatedUtc);
                Assert.Equal(CMResult.S_OK, await store.Commit(putRes.Token));
                tmp.Add(v2);

            }

            // ACCOUNT CALCULATIONS
            foreach (var kp in balances) {

                IStorable reloaded;
                Assert.Equal(CMResult.S_OK, store.Get("ACCNT/" + kp.Key, out reloaded));
                var a = reloaded as Schema.Account;
                Assert.Equal(kp.Key, a.ID);
                store.FillAccountCalculations(a, DateTime.UtcNow);

                var bal = Helpers.CalculateAccountBalance(a.AccountCalculations.RecentCredits.GetValueOrDefault(), a.AccountCalculations.RecentDebits.GetValueOrDefault());
                Assert.Equal(kp.Value, bal);
            }

            // LIST

            // Account transactions
            foreach (var kp in transes) {
                Schema.ListResponse res;
                var list = kp.Value;
                var paginated = new List<string>();
                uint start = 0;

                uint total = 0;
                decimal credits = 0;
                decimal debits = 0;
                do {
                    try {
                        Assert.Equal(CMResult.S_OK,
                            store.List(new Schema.ListRequest() {
                                Request = new Message.RequestHeader("LIST", "", "ACCNT/" + kp.Key + "/TRANS"),
                                UpdatedUtcFromInclusive = DateTime.UtcNow.AddMinutes(-10),
                                UpdatedUtcToExclusive = DateTime.UtcNow,
                                Sort = "UPD-UTC DESC",
                                Max = 3,
                                StartAt = start
                            }, out res));
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine(ex);
                        break;
                    }
                    if (total == 0) {
                        total = res.Total;
                    }
                    Assert.Equal(res.Total, total);
                    Assert.Equal(res.StartAt, start);
                    for (int i = 0; i < res.Values.Count; i++) {
                        if (res.Values[i].Name == "ITEM") {
                            var info = res.Values[i].Value.Split(' ');
                            paginated.Add(info[0] + " " + info[1] + " " + info[2]);
                            if (info[1] == kp.Key) { // payee
                                credits += Helpers.CalculateTransactionDepreciatedAmountForPayee(
                                    DateTime.UtcNow,
                                    Helpers.DateFromISO8601(info[0]), decimal.Parse(info[3]),
                                    (Schema.PayeeStatus)int.Parse(info[5]));

                            } else {
                                debits += Helpers.CalculateTransactionDepreciatedAmountForPayer(
                                    DateTime.UtcNow,
                                    Helpers.DateFromISO8601(info[0]), decimal.Parse(info[3]),
                                    (Schema.PayeeStatus)int.Parse(info[5]),
                                    (Schema.PayerStatus)int.Parse(info[6]));
                            }
                        }
                    }
                    start += res.Count;
                } while (start < total);
                Assert.Equal(list.Count, paginated.Count);
                var bal = Helpers.CalculateAccountBalance(credits, debits);
                Assert.Equal(balances[kp.Key], bal);
            }

            // Votes
            foreach (var kp in voters) {
                Schema.ListResponse res;
                var list = kp.Value;
                var paginated = new List<VoteIndex>();
                uint start = 0;
                uint total = 0;
                do {
                    try {
                        Assert.Equal(CMResult.S_OK,
                            store.List(new Schema.ListRequest() {
                                Request = new Message.RequestHeader("LIST", "", "ACCNT/" + kp.Key + "/VOTES"),
                                UpdatedUtcFromInclusive = DateTime.UtcNow.AddMinutes(-10),
                                UpdatedUtcToExclusive = DateTime.UtcNow.AddSeconds(10), // because we test updates stamped +10 secs ahead
                                Sort = "UPD-UTC DESC",
                                Max = 3,
                                StartAt = start
                            }, out res));
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine(ex);
                        break;
                    }
                    if (total == 0) {
                        total = res.Total;
                    }
                    Assert.Equal(res.Total, total);
                    Assert.Equal(res.StartAt, start);
                    for (int i = 0; i < res.Values.Count; i++) {
                        if (res.Values[i].Name == "ITEM") {
                            var info = new VoteIndex(res.Values[i].Value);
                            paginated.Add(info);
                        }
                    }
                    start += res.Count;
                } while (start < total);
                Assert.Equal(list.Count, paginated.Count);

            }

            // All votes
            foreach (var kp in propositions) {
                Schema.ListResponse res;
                var paginated = new List<VoteIndex>();
                uint start = 0;
                uint total = 0;
                do {
                    try {
                        Assert.Equal(CMResult.S_OK,
                            store.List(new Schema.ListRequest() {
                                Request = new Message.RequestHeader("LIST", "", "VOTES/" + kp.Key),
                                UpdatedUtcFromInclusive = DateTime.UtcNow.AddMinutes(-10),
                                UpdatedUtcToExclusive = DateTime.UtcNow.AddSeconds(10), // because we test updates stamped +10 secs ahead
                                Sort = "UPD-UTC DESC",
                                Max = 3,
                                StartAt = start
                            }, out res));
                    } catch (Exception ex) {
                        System.Diagnostics.Debug.WriteLine(ex);
                        break;
                    }
                    if (total == 0) {
                        total = res.Total;
                    }
                    Assert.Equal(res.Total, total);
                    Assert.Equal(res.StartAt, start);
                    for (int i = 0; i < res.Values.Count; i++) {
                        if (res.Values[i].Name == "ITEM") {
                            var info = new VoteIndex(res.Values[i].Value);
                            paginated.Add(info);
                        }
                    }
                    start += res.Count;
                } while (start < total);
                Assert.Equal(kp.Value, paginated.Count);
            }
        }

        /// <summary>
        /// Emulates a corrupt DHT index backup on disk. 
        /// </summary>
        [Fact]
        public void CorruptLinearHashTableRecovery() {
            string value = "oasifh aoihs ofihao ifhoai sh fiohas oifhoais h foihas oifh aosifh.";
            var c = new Storage.Container(null, _TestDataFolder, "corruptme");
            c.Set("TEST", value);
            c.Close();
            c = null;
            System.IO.File.WriteAllText(Path.Combine(_TestDataFolder, "corruptme.htindex-bak"), "garbage");
            c = new Storage.Container(null, _TestDataFolder, "corruptme");
            var foo = c.Get("TEST");
            Assert.Null(foo);
            c.Close();
            c = null;
        }

        /// <summary>
        /// Checks SslWebContext.ParseQuery for correctness.
        /// </summary>
        [Fact]
        public void CanParseQueryStrings() {
            var ar = CM.Server.SslWebContext.ParseQuery("http://foo.com/?value1=a");
            Assert.Equal(ar["value1"], "a");
            ar = CM.Server.SslWebContext.ParseQuery("http://foo.com/?value1");
            Assert.Equal(ar["value1"], "");
            ar = CM.Server.SslWebContext.ParseQuery("http://foo.com/?value1=a%20spaced&value2=second=bad?val&");
            Assert.Equal(ar["value1"], "a spaced");
            Assert.Equal(ar["value2"], "second=bad?val");

        }

        /// <summary>
        /// A basic transaction signing test.
        /// </summary>
        [Fact]
        public void SignatureVerification() {
           
            var t = new Transaction(@"VER: 1
UTC: 2016-09-09T23:20:12
PYR-ID: test4
PYR-REG: CA-NS
PYR-STAT: Accept
PYR-UTC: 2016-09-09T23:20:12
PYE-ID: test1
AMNT: 1.000000");


            Account a = new Account(@"ID: test4
UTC: 2016-09-09T13:45:24
UPD-UTC: 2016-09-09T13:45:24
VER: 1
REG: CA-NS
PRIKEY: 0,x9BvhyrVEf0H+Jn3Em3IorpEgG/KbJI8J3I5PvnvwhU=,X6NWw4N/4rOH6glznr3ym+ibFvfv904tj4+bKV38dgCfNzc+1AE2Kh+DE1E6ZxskxPvGXgvUH7NYPdZ2gX/FcF73S7F32ITn2tCp/QXzjzM4nZisPidY1a2dX3xxun8Yrs2pHwP/vkOhtEOaGtX0drdpPFjXJSDhw1hbmLK1I85QwpbKPMogScmn6pdeRmzw
PUBKEY: 2016-09-09T13:45:24,dLiNyruff8skKIhpQSNhYz66nzba9QKxrAyvGT+M2MHS5B5wZLmkvhT+/aomQW4vN8HUuBiHfs38+C9EBRm5k1vSBnhKjNmJInsydfWMWaklKE6yC2P3imJCBnBzSIGx63PIGHQ9x8n+k1pKQnU1xSciB9/PFuyWEYOyyE5Kxyk=,
SIG: DXrF2EUGSR1oniAp17ZNTN7OEssaErJ9I12Fs+fqy9MAQw4I3ARjHKzkN0ftfEnJ6tK5sz4TvKMrvZkEAC8eUfy7PBUVFw5PZRht9gNrjR7Nod4P+4SpRpUR20Wj2JjUV4/IxqmP6+v0CM+qh3snzaMGzCUcW/08UdKYnLvgtbM=
CALC-REP: 54.100000
CALC-CREDITS: 105.000000
CALC-DEBITS: 7.000000
CALC-LAST-TRANS: 2016-09-09T23:24:19");

            var req = new AsyncRequest<DataSignRequest>() {
                Item = new DataSignRequest(t.GetPayerSigningData()) {
                    Password = System.Text.Encoding.UTF8.GetBytes("123")
                }
            };
            a.SignData(req, CM.Server.CryptoFunctions.Identity);
            var res = Convert.ToBase64String(req.Item.Transforms[0].Output);
            System.Diagnostics.Debug.WriteLine(res);
            var expected = "AH6wP/k7uatpaiR2c70PdzC+BeYbj0LTtoBDy91FJsahfwJo8lG+5tawuFcosRpFH+t/ZJzVLNmB9lJhYzVO/uB1c+HBzq/Crgt8xgO/XU+guuf4iy4TxzHqPrrHndWg9A3P3oa25tBqfNn8z+cKG8Lw3VzzqYgee+7FeI0NGRs=";

            Assert.Equal(res, expected);

            var verify = new AsyncRequest<DataVerifyRequest>() {
                Item = new DataVerifyRequest() {
                    DataDateUtc = t.PayerUpdatedUtc,
                    Input = t.GetPayerSigningData(),
                    Signature = Convert.FromBase64String(res)
                }
            };
            a.VerifySignature(verify, CM.Server.CryptoFunctions.Identity);
            Assert.Equal(verify.Result, CMResult.S_OK);

        }

        /// <summary>
        /// Stress the LinearHashTable to validate multiple concurrent
        /// insert/retrieval attempts and also cause flushes/compacting etc.
        /// </summary>
        [Fact]
        public void LinearHashTableStressTest() {
            
            LinearHashTable<string, string> Test = new LinearHashTable<string, string>(
              System.IO.Path.Combine(_TestDataFolder, "DHTStress"),
              (string str) => {//OnHashKey
                  if (str == null)
                        return 0;
                    var length = str.Length;
                    int hash = length;
                    for (int i = 0; i != length; ++i)
                        hash = (hash ^ str[i]) * 16777619;
                    return hash;
              },
              (k, st) => { st.Write(k); }, (st) => { return st.ReadString(); },
              (k, st) => { st.Write(k ?? String.Empty); }, (st) => { return st.ReadString(); }
              );
 

            System.Threading.Tasks.Parallel.For(0, 10000, (i) =>
            // for (int i = 0; i < ar.Count; i++)
            {
                string key = i.ToString();
                string v = "hello from " + key;
                Test.Set(key, v);
                string v2;
                Test.TryGetValue(key, out v2);
                Assert.Equal(v2, v);
            }
            );

        }
    }
}
#endif