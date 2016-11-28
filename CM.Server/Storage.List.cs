#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using CM.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM.Server {
    partial class Storage {
        struct AccountListCacheItem  {
            public string ID;
            public DateTime UpdatedUtc;
        }
        List<AccountListCacheItem> _AccountListCache;
        TimeSpan _AccountListCacheAge;
        ListResponse ListAccounts(ListRequest req) {
            var all = _AccountListCache;
            if ((Clock.Elapsed - _AccountListCacheAge).TotalSeconds > 30)
                all = null;
            if (all == null) {
                all = new List<AccountListCacheItem>();
                var accountsFolder = System.IO.Path.Combine(_Folder, Constants.PATH_ACCNT);
                if (System.IO.Directory.Exists(accountsFolder)) {
                    var files = System.IO.Directory.GetFiles(accountsFolder, "*.htindex", System.IO.SearchOption.AllDirectories);
                    for (int i = 0; i < files.Length; i++) {
                        try {
                            var id = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(System.IO.Path.GetFileNameWithoutExtension(files[i]).Replace("_", "/")));
                            all.Add(new AccountListCacheItem() {
                                 UpdatedUtc = System.IO.File.GetLastWriteTimeUtc(files[i]),
                                 ID = id
                            });
                        } catch { }
                    }
                    all.Sort((a,b)=> { return String.Compare(a.ID, b.ID); });
                }
                if (all.Count != 0) {
                    _AccountListCache = all;
                    _AccountListCacheAge = Clock.Elapsed;
                }
            }

            switch (req.Sort) {
                case "ID ASC":
                default:
                    // already sorted by ID
                    break;
                case "ID DESC":
                    all = new List<AccountListCacheItem>(all); // clone and sort
                    all.Sort((a, b) => { return String.Compare(a.ID, b.ID) * -1; });
                    break;
                case "UPD-UTC ASC":
                    all = new List<AccountListCacheItem>(all); // clone and sort
                    all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc); });
                    break;
                case "UPD-UTC DESC":
                    all = new List<AccountListCacheItem>(all); // clone and sort
                    all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; });
                    break;
            }

            var res = new ListResponse();
            int end = GetEndIndex(req, all.Count);
            res.StartAt = req.StartAt;
            res.Total = (uint)all.Count;
            res.Count = (uint)Math.Max(0, end - req.StartAt);
            for (int i = (int)req.StartAt; i < end; i++) {
                res.Values.Append("ITEM", all[i].ID);
            }
            return res;
        }

        static int GetEndIndex(ListRequest req, int resultCount) {
            if (req.StartAt > resultCount 
                || req.Max == uint.MaxValue 
                || req.Max == int.MaxValue)
                return resultCount;
            else
                return Math.Min((int)(req.StartAt + (req.Max == 0 ? 1000 : req.Max)), resultCount);
        }

        ListResponse ListAccountTransactions(ListRequest req, string id) {
            var container = _Root.Open(GetAccountDataFilePath(id));
            var keys = container.GetKeys();
            var all = new List<TransactionIndex>();
            for (int i = 0; i < keys.Length; i++) {
                var key = keys[i];
                if (key.StartsWith(Constants.PATH_TRANS + "/")) {
                    var transID = key.Substring(Constants.PATH_TRANS.Length + 1);
                    DateTime d;
                    string payer, payee;
                    if (Helpers.TryParseTransactionID(transID, out d, out payee, out payer)) {
                        var indexData = container.Get(key);

                        // This shouldn't happen unless there's disk corruption.
                        System.Diagnostics.Debug.Assert(indexData != null, "Missing transaction index data.");

                        if (indexData != null) {
                            var idx = new TransactionIndex(indexData);
                            all.Add(idx);
                        } 
                    }
                }
            }

            var from = req.UpdatedUtcFromInclusive;
            var to = req.UpdatedUtcToExclusive;
            all.RemoveAll(t => t.UpdatedUtc < from || t.UpdatedUtc >= to);

            switch (req.Sort) {
                case "UTC ASC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc); }); break;
                case "UTC DESC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc) * -1; }); break;
                case "UPD-UTC ASC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc); }); break;
                default:
                case "UPD-UTC DESC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; }); break;
            }

            var res = new ListResponse();
            int end = GetEndIndex(req, all.Count);
            res.StartAt = req.StartAt;
            res.Total = (uint)all.Count;
            res.Count = (uint)Math.Max(0, end - req.StartAt);

            for (int i = (int)req.StartAt; i < end; i++) {

                res.Values.Append("ITEM", all[i].ToString());
            }

            return res;
        }

        ListResponse ListAllTransactions(ListRequest req) {
            var fromDate = req.UpdatedUtcFromInclusive;
            fromDate = new DateTime(fromDate.Year, fromDate.Month, 1);
            var toDate = req.UpdatedUtcToExclusive;
            toDate = toDate.Date;
            var containers = new Dictionary<string, Container>();
            var dic = new Dictionary<string, TransactionIndex>();
            while (fromDate <= toDate) {
                var c = _Root.Open(GetTransactionDataFilePath(fromDate));
                containers.Add(fromDate.ToString("yyyy-MM-dd"), c);
                var keys = c.GetKeys();
                // Keys are t.ID "/" t.UpdatedUtc
                for (int i = 0; i < keys.Length; i++) {
                    var k = keys[i];
                    var idx = k.IndexOf('/');
                    if (idx == -1)
                        continue;
                    var id = k.Substring(0, idx);
                    var updated = Helpers.DateFromISO8601(k.Substring(idx + 1));
                    TransactionIndex x;
                    if (!dic.TryGetValue(id, out x)
                        || x.UpdatedUtc < updated) {
                        DateTime created;
                        string payee, payer;
                        if (Helpers.TryParseTransactionID(id, out created, out payee, out payer)) {
                            dic[id] = new TransactionIndex() {
                                ID = id,
                                CreatedUtc = created,
                                UpdatedUtc = updated,
                                Payee = payee,
                                Payer = payer,
                            };
                        }
                    }
                }
                fromDate = fromDate.AddDays(1);
            }
            var all = dic.Values.ToList();
            var from = req.UpdatedUtcFromInclusive;
            var to = req.UpdatedUtcToExclusive;
            all.RemoveAll(t => t.UpdatedUtc < from || t.UpdatedUtc >= to);

            switch (req.Sort) {
                default:
                case "UPD-UTC DESC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; }); break;
                case "UPD-UTC ASC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc); }); break;
                case "UTC ASC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc); }); break;
                case "UTC DESC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc) * -1; }); break;
                // Secondary sort on these are UPD-UTC DESC, which will be the most common user case.
                case "PYR-ID ASC": all.Sort((a, b) => { int i = String.Compare(a.Payer, b.Payer); if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "PYR-ID DESC": all.Sort((a, b) => { int i = String.Compare(a.Payer, b.Payer) * -1; if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "PYE-ID ASC": all.Sort((a, b) => { int i = String.Compare(a.Payee, b.Payee); if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "PYE-ID DESC": all.Sort((a, b) => { int i = String.Compare(a.Payee, b.Payee) * -1; if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "AMNT ASC": all.Sort((a, b) => { int i = a.Amount.CompareTo(b.Amount); if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "AMNT DESC": all.Sort((a, b) => { int i = a.Amount.CompareTo(b.Amount) * -1; if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
            }

            var res = new ListResponse();
            int end = GetEndIndex(req, all.Count);
            res.StartAt = req.StartAt;
            res.Total = (uint)all.Count;
            res.Count = (uint)Math.Max(0, end - req.StartAt);
            for (int i = (int)req.StartAt; i < end; i++) {
                var x = all[i];
                var container = containers[x.CreatedUtc.ToString("yyyy-MM-dd")];
                var key = x.ID + "/" + Helpers.DateToISO8601(x.UpdatedUtc);
                var data = container.Get(key);
                System.Diagnostics.Debug.Assert(data != null, "Missing transaction record during search.");
                if (data == null)
                    continue;
                res.Values.Append("ITEM", new TransactionIndex(new Transaction(data)).ToString());
            }
            return res;
        }

        ListResponse ListRegionTransactions(string region, ListRequest req) {
            var fromDate = req.UpdatedUtcFromInclusive;
            fromDate = new DateTime(fromDate.Year, fromDate.Month, fromDate.Day);
            var toDate = req.UpdatedUtcToExclusive;
            toDate = new DateTime(toDate.Year, toDate.Month, toDate.Day);
            var all = new List<TransactionIndex>();
            // REGIONS/{code}/{yyyy-MM-dd}.htindex
            while (fromDate <= toDate) {
                var c = _Root.Open(GetRegionDataFilePath(region, fromDate));
                var keys = c.GetKeys();
                // Keys are t.ID "/" t.UpdatedUtc
                for (int i = 0; i < keys.Length; i++) {
                    DateTime d;
                    string payer, payee;
                    var key = keys[i];
                    if (Helpers.TryParseTransactionID(key, out d, out payee, out payer)) {
                        var idx = new TransactionIndex(c.Get(key));
                        all.Add(idx);
                    }
                }
                fromDate = fromDate.AddDays(1);
            }

            var from = req.UpdatedUtcFromInclusive;
            var to = req.UpdatedUtcToExclusive;
            all.RemoveAll(t => t.UpdatedUtc < from || t.UpdatedUtc >= to);

            switch (req.Sort) {
                default:
                case "UPD-UTC DESC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; }); break;
                case "UPD-UTC ASC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc); }); break;
                case "UTC ASC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc); }); break;
                case "UTC DESC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc) * -1; }); break;
                // Secondary sort on these are UPD-UTC DESC, which will be the most common user case.
                case "PYR-ID ASC": all.Sort((a, b) => { int i = String.Compare(a.Payer, b.Payer); if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "PYR-ID DESC": all.Sort((a, b) => { int i = String.Compare(a.Payer, b.Payer) * -1; if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "PYE-ID ASC": all.Sort((a, b) => { int i = String.Compare(a.Payee, b.Payee); if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "PYE-ID DESC": all.Sort((a, b) => { int i = String.Compare(a.Payee, b.Payee) * -1; if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "AMNT ASC": all.Sort((a, b) => { int i = a.Amount.CompareTo(b.Amount); if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; }); break;
                case "AMNT DESC": all.Sort((a, b) => { int i = a.Amount.CompareTo(b.Amount) * -1; if (i == 0) i = a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; return i; });  break;

            }

            var res = new ListResponse();
            int end = GetEndIndex(req, all.Count);
            res.StartAt = req.StartAt;
            res.Total = (uint)all.Count;
            res.Count = (uint)Math.Max(0, end - req.StartAt);

            for (int i = (int)req.StartAt; i < end; i++) {

                res.Values.Append("ITEM", all[i].ToString());
            }

            return res;
        }


        ListResponse ListAccountVotes(ListRequest req, string id) {
            var container = _Root.Open(GetAccountDataFilePath(id));
            var keys = container.GetKeys();
            var all = new List<VoteIndex>();
            for (int i = 0; i < keys.Length; i++) {
                var key = keys[i];
                // Account vote indexes are stored in /VOTES/{PropositionID}
                if (key.StartsWith(Constants.PATH_VOTES + "/")) {
                    var indexData = container.Get(key);

                    // This shouldn't happen unless there's disk corruption.
                    System.Diagnostics.Debug.Assert(indexData != null, "Missing vote index data");

                    if (indexData != null) {
                        var idx = new VoteIndex(indexData);
                        all.Add(idx);
                    }
                }
            }

            var from = req.UpdatedUtcFromInclusive;
            var to = req.UpdatedUtcToExclusive;
            all.RemoveAll(v => v.UpdatedUtc < from || v.UpdatedUtc >= to);

            switch (req.Sort) {
                case "UTC ASC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc); }); break;
                case "UTC DESC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc) * -1; }); break;
                case "UPD-UTC ASC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc); }); break;
                default:
                case "UPD-UTC DESC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; }); break;
            }

            var res = new ListResponse();
            int end = GetEndIndex(req, all.Count);
            res.StartAt = req.StartAt;
            res.Total = (uint)all.Count;
            res.Count = (uint)Math.Max(0, end - req.StartAt);

            for (int i = (int)req.StartAt; i < end; i++) {
                res.Values.Append("ITEM", all[i].ToString());
            }

            return res;
        }
        ListResponse ListAllVotes(ListRequest req, string propositionID) {
            var container = _Root.Open(GetVotesDataFilePath(propositionID));
            var keys = container.GetKeys();
            var dic = new Dictionary<string, VoteIndex>();
            for (int i = 0; i < keys.Length; i++) {
                // Keys are VoterID "/" CreatedUtc "/" UpdatedUtc
                var k = keys[i];
                var idx1 = k.IndexOf('/');
                var idx2 = k.IndexOf('/', idx1 + 1);
                if (idx1 == -1
                    || idx2 == -1)
                    continue;
                var id = k.Substring(0, idx1);
                idx1++;
                var created = Helpers.DateFromISO8601(k.Substring(idx1, idx2 - idx1));
                var updated = Helpers.DateFromISO8601(k.Substring(idx2 + 1));
                VoteIndex x;
                if (!dic.TryGetValue(id, out x)
                    || x.UpdatedUtc < updated) {
                    dic[id] = new VoteIndex() {
                        CreatedUtc = created,
                        VoterID = id,
                        UpdatedUtc = updated,
                    };
                }
            }
            
            var all = dic.Values.ToList();
            var from = req.UpdatedUtcFromInclusive;
            var to = req.UpdatedUtcToExclusive;
            all.RemoveAll(t => t.UpdatedUtc < from || t.UpdatedUtc >= to);

            switch (req.Sort) {
                case "UTC ASC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc); }); break;
                case "UTC DESC": all.Sort((a, b) => { return a.CreatedUtc.CompareTo(b.CreatedUtc) * -1; }); break;
                case "UPD-UTC ASC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc); }); break;
                default:
                case "UPD-UTC DESC": all.Sort((a, b) => { return a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1; }); break;
                case "VTR-ID ASC": all.Sort((a, b) => { return String.Compare(a.VoterID, b.VoterID); }); break;
                case "VTR-ID DESC": all.Sort((a, b) => { return String.Compare(a.VoterID, b.VoterID) * -1; }); break;
            }

            var res = new ListResponse();
            int end = GetEndIndex(req, all.Count);
            res.StartAt = req.StartAt;
            res.Total = (uint)all.Count;
            res.Count = (uint)Math.Max(0, end - req.StartAt);
            for (int i = (int)req.StartAt; i < end; i++) {
                var x = all[i];
                var key = x.VoterID + "/" + Helpers.DateToISO8601(x.CreatedUtc) + "/" + Helpers.DateToISO8601(x.UpdatedUtc);
                var data = container.Get(key);
                System.Diagnostics.Debug.Assert(data != null, "Missing vote record during search.");
                if (data == null)
                    continue;
                res.Values.Append("ITEM", new VoteIndex(new Vote(data)).ToString());
            }
            return res;
        }
    }
}
