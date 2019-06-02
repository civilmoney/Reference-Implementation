#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using CM.Schema;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {
    internal partial class AuthoritativeDomainReporter {


        private readonly Map _ActivityMap = new Map();
        private readonly ConcurrentDictionary<string, Dictionary<string, RevenueItem>> _GlobalTransactionLookupByPayee = new ConcurrentDictionary<string, Dictionary<string, RevenueItem>>();
        private readonly ConcurrentDictionary<string, Dictionary<string, RevenueItem>> _GlobalTransactionLookupByPayer = new ConcurrentDictionary<string, Dictionary<string, RevenueItem>>();
        private readonly ConcurrentDictionary<string, RevenueItemCache> _MemCache = new ConcurrentDictionary<string, RevenueItemCache>();
        private readonly ConcurrentDictionary<string, SummaryItemCache> _SummaryCache = new ConcurrentDictionary<string, SummaryItemCache>();


        [Flags]
        public enum TaxRevenueFlags : int {
            None = 0,

            /// <summary>
            /// We flag transactions as ineligible for tax revenue purposes if money appears to have
            /// flowed back and forth between a payer/payee in a more or less circular fashion.
            /// </summary>
            Ineligible = 1 << 0
        }

        /// <summary>
        /// A transaction is either Settled or not for tax revenue purposes.
        /// </summary>
        public enum TransactionStatus : int {
            Unknown = 0,
            Settled,
            Cancelled,
        }

        /// <summary>
        /// Tax collection is pretty straight forward. We just query every node with this:
        ///
        /// CMD LIST
        /// PATH: TRANS/
        /// UPD-UTC: {last query time}
        /// SORT: UPD-UTC ASC
        ///
        /// We'll dump all data into massive text files, one for each calendar date,
        /// and then process it later. This will enable offline analysis/testing/debugging of revenue data.
        /// </summary>
        private async void CollectTransactionsAsync(CancellationToken token) {
            var files = new Dictionary<string, System.IO.StreamWriter>();
            try {
                _IsTransactionPollInProgress = true;
                _LastTransactionPoll = Clock.Elapsed;

                var now = DateTime.UtcNow;

                var ar = NetworkScan.Peers.Keys.ToArray();

                if (!System.IO.Directory.Exists(_FolderRawData))
                    System.IO.Directory.CreateDirectory(_FolderRawData);
                _Log.Write(this, LogLevel.INFO, "CollectTransactions started");
                for (int i = 0; i < ar.Length && !token.IsCancellationRequested; i++) {
                    var epString = ar[i];
                    var ep = Helpers.ParseEP(epString);
                    try {
                        using (var conn = await _DHT.Connect(epString)) {
                            if (conn.IsValid) {
                                // When was the last time we queried this guy?
                                DateTime timeFrom = DateTime.MinValue;
                                string lastPollString;
                                string dictionaryKey = FindOrCreateIDForIPAddress(ep) + "_lstqry_trans";
                                if (_Persisted.TryGetValue(dictionaryKey, out lastPollString))
                                    timeFrom = Helpers.DateFromISO8601(lastPollString);
                                if (timeFrom == DateTime.MinValue)
                                    timeFrom = Constants.MinimumSaneDateTime;

                                uint start = 0;
                                uint total = 0;
                                var items = new List<string>();
                                do {
                                    var m = await conn.Connection.SendAndReceive("LIST", new ListRequest() {
                                        APIVersion = Constants.APIVersion,
                                        Sort = "UPD-UTC ASC",
                                        UpdatedUtcFromInclusive = timeFrom,
                                        UpdatedUtcToExclusive = now,
                                        StartAt = start,
                                        Max = PAGINATION_SIZE
                                    }, Constants.PATH_TRANS + "/");
                                    Schema.ListResponse res = m.Cast<Schema.ListResponse>();
                                    if (total == 0)
                                        total = res.Total;
                                    for (int x = 0; x < res.Values.Count; x++) {
                                        if (res.Values[x].Name == "ITEM") {
                                            var idx = new TransactionIndex(res.Values[x].Value);
                                            var date = idx.CreatedUtc.ToString("yyyy-MM-dd");

                                            System.IO.StreamWriter sw;
                                            if (!files.TryGetValue(date, out sw)) {
                                                var dataDumpFile = System.IO.Path.Combine(_FolderRawData, date + "-trans.txt");
                                                sw = new System.IO.StreamWriter(System.IO.File.OpenWrite(dataDumpFile));
                                                sw.BaseStream.Position = sw.BaseStream.Length;
                                                files.Add(date, sw);
                                            }

                                            // The data we log is the {end-point} +" "+ {transaction index}
                                            // Always use Windows line-endings for portability.
                                            sw.Write(epString + " " + res.Values[x].Value + "\r\n");
                                            sw.Flush();
                                        }
                                    }
                                    start += res.Count;
                                } while (start < total);

                                // No problems
                                _Persisted.Set(dictionaryKey, Helpers.DateToISO8601(now));
                            }
                        }
                    } catch (Exception ex) {
                        // Any problems.. move along..
                        _Log.Write(this, LogLevel.INFO, "Transaction collection with {0} failed: {1}", epString, ex.Message);
                    }
                }
                _Log.Write(this, LogLevel.INFO, "CollectTransactions finished");
            } finally {
                foreach (var s in files.Values) {
                    s.Flush();
                    s.Dispose();
                }
                // Inexplicably, this finaliser code sometimes doesn't run
                // even though no async tasks can deadlock or await indefinitely.
                _IsTransactionPollInProgress = false;
            }
        }

        /// <summary>
        /// Takes the raw data dumps and generates tax revenue for all regions. The way this works is:
        /// - RevenueItems are stored in a file under /report-data/compiled/region/year-month.dat
        ///   where year and month is that of the transaction ID's CREATED UTC.
        /// - As transaction dumps are generated, we create a Witness record for each sender.
        /// - Consensus is a simple tally of witness on the highest timestamp.
        /// </summary>
        private void CompileTransactions(CancellationToken token) {
            try {
                _IsTransactionCompileInProgress = true;
                _LastTransactionCompile = Clock.Elapsed;

                if (!System.IO.Directory.Exists(_FolderRawData))
                    return;

                if (!System.IO.Directory.Exists(_FolderCompiledData))
                    System.IO.Directory.CreateDirectory(_FolderCompiledData);
                _Log.Write(this, LogLevel.INFO, "CompileTransactions started");
                var invalidatedRegions = new HashSet<string>();
                var now = DateTime.UtcNow;
                DateTime lastSuccessfulCompilation = DateTime.MinValue;
                string s;
                string dictionaryKey = "last-compile-trans";
                if (_Persisted.TryGetValue(dictionaryKey, out s))
                    lastSuccessfulCompilation = Helpers.DateFromISO8601(s);

                // Which files have any modification since last time?
                var dir = new System.IO.DirectoryInfo(_FolderRawData);
                var files = dir.GetFileSystemInfos("*-trans.txt", System.IO.SearchOption.TopDirectoryOnly);
                var queue = new List<string>();
                for (int i = 0; i < files.Length; i++)
                    if (files[i].LastWriteTimeUtc > lastSuccessfulCompilation)
                        queue.Add(files[i].FullName);
                queue.Sort();
                var trans = new TransactionIndex();
                for (int i = 0; i < queue.Count; i++) {
                    using (var dr = new System.IO.StreamReader(System.IO.File.OpenRead(queue[i]))) {
                        long lastOffset = 0;
                        var offsetDictionaryKey = queue[i] + "_offset";
                        if (_Persisted.TryGetValue(offsetDictionaryKey, out s))
                            lastOffset = long.Parse(s);
                        dr.BaseStream.Position = lastOffset;
                        string line;
                        while ((line = dr.ReadLine()) != null) {
                            if (line.Length == 0)
                                continue;
                            var idx = line.IndexOf(' ');
                            if (idx == -1)
                                continue;
                            var peer = line.Substring(0, idx);
                            var ep = Helpers.ParseEP(peer);
                            var peerID = FindOrCreateIDForIPAddress(ep);
                            trans.Parse(line.Substring(idx + 1));

                            bool isSettled = trans.PayeeStatus == PayeeStatus.Accept

                            // Mob mentalities might cause payers to dispute a particular region en-masse.
                            // For this reason a transaction is settled for tax purposes as long as the 
                            // payee is accepting it.

                            // && trans.PayerStatus == PayerStatus.Accept
                            ;

                            var id = trans.ID;
                            var cache = LoadRegionMonth(trans.PayeeRegion, trans.CreatedUtc.Year, trans.CreatedUtc.Month);
                            if (cache == null)
                                continue;
                            RevenueItem item;
                            if (!cache.Dic.TryGetValue(trans.ID, out item)) {
                                item = new RevenueItem();
                                item.ID = id;
                                item.PayerRegion = trans.PayerRegion;
                                item.Amount = trans.Amount;
                                IndexPayerPayee(item);
                                cache.Dic[id] = item;
                            }

                            lock (item)
                                if (item.TryAddVote(peerID, (uint)(trans.UpdatedUtc - trans.CreatedUtc).TotalMilliseconds,
                                    isSettled ? TransactionStatus.Settled : TransactionStatus.Cancelled)) {
                                    cache.IsInvalidated = true;
                                    if (!invalidatedRegions.Contains(trans.PayeeRegion))
                                        invalidatedRegions.Add(trans.PayeeRegion);
                                }
                        }
                        _Persisted.Set(offsetDictionaryKey, dr.BaseStream.Position.ToString());
                    }
                }

                SaveRegions();

                // Discard cached summaries
                var toRemove = new List<string>();
                foreach (var kp in _SummaryCache) {
                    var region = kp.Key.Split('/')[0];
                    if (invalidatedRegions.Contains(region)) {
                        toRemove.Add(kp.Key);
                    }
                }

                foreach (var key in toRemove) {
                    SummaryItemCache c;
                    _SummaryCache.TryRemove(key, out c);
                }

                _Persisted.Set(dictionaryKey, Helpers.DateToISO8601(now));
                _Log.Write(this, LogLevel.INFO, "CompileTransactions finished");
            } catch (Exception ex) {
                _Log.Write(this, LogLevel.FAULT, "CompileTransactions crashed: " + ex);
            } finally {
                _IsTransactionCompileInProgress = false;
            }
        }

        private uint FindOrCreateIDForIPAddress(IPEndPoint ep) {
            lock (_CurrentIPPrimaryKeyLock) {
                // For production we go by IP, for development/testing
                // there's only every 1 IP so we use the whole end-point
#if DEBUG
                string key = "ip_" + ep.ToString();
#else
                string key = "ip_" + ep.Address.ToString();
#endif
                string id;
                if (_CurrentIPPrimaryKey == 0) {
                    if (_Persisted.TryGetValue("CurrentIPPrimaryKey", out id)) {
                        _CurrentIPPrimaryKey = uint.Parse(id);
                    }
                }
                if (!_Persisted.TryGetValue(key, out id)) {
                    _CurrentIPPrimaryKey++;
                    id = _CurrentIPPrimaryKey.ToString();
                    _Persisted.Set("CurrentIPPrimaryKey", id);
                    _Persisted.Set(key, id);
                    _Persisted.Flush(); // it's important that IDs never collide due to write error.
                }
                return uint.Parse(id);
            }
        }

        /// <summary>
        /// Compiles a list of records for tax revenue reporting of a specific time range.
        /// </summary>
        internal SummaryItemCache GetTaxSummaryItems(string region, DateTime from, DateTime to) {
            SummaryItemCache cache;
            string cacheKey = region.ToUpper() + "/" + from.ToString("s") + "/" + to.ToString("s");
            if (_SummaryCache.TryGetValue(cacheKey, out cache))
                return cache;
            var ar = new List<SummaryItem>();
            var cur = from;
            while (cur < to) {
                var dic = LoadRegionMonth(region, cur.Year, cur.Month).Dic;
                var list = dic.Values.ToList();
                list.Sort();
                foreach (var t in list) {
                    DateTime stamp;
                    bool isSettled;
                    if (t.TryGetConsensus(out isSettled, out stamp)
                        && isSettled
                        && stamp >= from && stamp <= to) {
                        ar.Add(new SummaryItem() {
                            Date = stamp,
                            ID = t.ID,
                            PayerRegion = t.PayerRegion,
                            Amount = t.Amount,
                            // Tax revenue depreciates exactly the same as transactions.
                            Revenue = Helpers.CalculateTransactionDepreciatedAmount(DateTime.UtcNow, stamp, (t.Amount * Constants.TaxRate))
                        });
                    }
                }
                cur = cur.AddMonths(1);
            }
            ShenanigansFilter(ar, from, to);
            ar.Sort();
            cache = new SummaryItemCache();
            cache.GeneratedUtc = DateTime.UtcNow;
            cache.Items = ar;
            _SummaryCache[cacheKey] = cache;
            return cache;
        }

        
        

        /// <summary>
        /// Indexes a revenue item under GlobalTransactionLookupByPayee
        /// and GlobalTransactionLookupByPayer which is used for tax
        /// eligibility determination.
        /// </summary>
        private void IndexPayerPayee(RevenueItem item) {
            DateTime created;
            string payeeID, payerID;
            if (!Helpers.TryParseTransactionID(item.ID, out created, out payeeID, out payerID))
                throw new ArgumentException("Invalid transaction ID during IndexPayerPayee.");
            Dictionary<string, RevenueItem> payeeDic;
            if (!_GlobalTransactionLookupByPayee.TryGetValue(payeeID, out payeeDic)) {
                payeeDic = new Dictionary<string, RevenueItem>();
                _GlobalTransactionLookupByPayee[payeeID] = payeeDic;
            }
            payeeDic[item.ID] = item;
            Dictionary<string, RevenueItem> payerDic;
            if (!_GlobalTransactionLookupByPayer.TryGetValue(payerID, out payerDic)) {
                payerDic = new Dictionary<string, RevenueItem>();
                _GlobalTransactionLookupByPayer[payerID] = payerDic;
            }
            payerDic[item.ID] = item;
            lock (_ActivityMap)
                _ActivityMap.AddRelationship(payerID, payeeID);
        }

        /// <summary>
        /// Gets revenue data for the specified region's month and year. This is mem-cached
        /// for performance reasons.
        /// </summary>
        private RevenueItemCache LoadRegionMonth(string region, int year, int month) {
            // Input validation
            if (ISO31662.GetName(region) == null)
                return null;

            // For the first few years of PM memory use should be low for caching everything
            string regionYearMonthKey = region + "/" + year + "/" + month;
            RevenueItemCache cache;

            if (_MemCache.TryGetValue(regionYearMonthKey, out cache))
                return cache;

            // Load from disk /taxes/ca-ns/2016-04.dat
            var file = Path.Combine(Path.Combine(_Folder, FOLDER_REPORT_DATA), FOLDER_COMPILED);
            file = Path.Combine(file, region);
            file = Path.Combine(file, year + "-" + month + ".dat");

            var dic = new ConcurrentDictionary<string, RevenueItem>();
            if (File.Exists(file)) {
                using (var fs = new System.IO.FileStream(file, FileMode.Open, FileAccess.Read)) {
                    using (var br = new BinaryReader(fs)) {
                        while (fs.Position < fs.Length) {
                            var item = new RevenueItem(br);
                            dic[item.ID] = item;
                            IndexPayerPayee(item);
                        }
                    }
                }
            }
            cache = new RevenueItemCache();
            cache.Dic = dic;
            _MemCache[regionYearMonthKey] = cache;
            return cache;
        }

        /// <summary>
        /// Flushes regional RevenueItem data to disk.
        /// </summary>
        private void SaveRegion(string region, int year, int month, RevenueItemCache c) {
            lock (c) {
                var file = Path.Combine(_FolderCompiledData, region);
                if (!Directory.Exists(file))
                    Directory.CreateDirectory(file);
                file = Path.Combine(file, year + "-" + month + ".dat");
                using (var fs = new System.IO.FileStream(file, FileMode.OpenOrCreate, FileAccess.Write)) {
                    using (var bw = new BinaryWriter(fs)) {
                        fs.SetLength(0);
                        foreach (RevenueItem item in c.Dic.Values)
                            item.Serialise(bw);
                        c.IsInvalidated = false;
                    }
                }
            }
        }

        /// <summary>
        /// Flushes all invalidated regions to disk
        /// </summary>
        private void SaveRegions() {
            lock (_MemCache)
                foreach (var kp in _MemCache) {
                    if (kp.Value.IsInvalidated) {
                        var parts = kp.Key.Split('/');
                        var region = parts[0];
                        int year = int.Parse(parts[1]);
                        int month = int.Parse(parts[2]);
                        SaveRegion(region, year, month, kp.Value);
                    }
                }
        }

        /// <summary>
        /// Removes payments where TO + FROM are switched for the same amount, which could lead to
        /// false taxation revenue.
        /// </summary>
        private void ShenanigansFilter(List<SummaryItem> ar, DateTime from, DateTime to) {
            // The routine:
            // For each payment..
            for (int i = 0; i < ar.Count; i++) {
                var t = ar[i];
                // Find transactions where the payer was a recipient
                DateTime created;
                string payeeID, payerID;
                Helpers.TryParseTransactionID(ar[i].ID, out created, out payeeID, out payerID);

                // Look for a relationship between the payee with money ending up at the payer
                var linkedList = _ActivityMap.CalculateDistance(payeeID, payerID);
                if (linkedList != null && linkedList.Count < 4) {
                    // Money has changed hands between a few parties, sum amounts within the past 7
                    // days with each party. If they're with a significant ballpark, we will flag it
                    var n = linkedList.First;
                    bool shouldFlag = n.Next != null;
                    while (n.Next != null) {
                        Dictionary<string, RevenueItem> activity;
                        if (!_GlobalTransactionLookupByPayer.TryGetValue(n.Value, out activity)) {
                            shouldFlag = false; // we don't have any info on this person
                            break;
                        }

                        // We're looking for an ID with <reasonable date>/<next guy>/<this guy>
                        string search = "/" + n.Next.Value + "/";
                        decimal sumOfPaymentsMade = 0;
                        foreach (var id in activity.Keys) {
                            if (id.IndexOf(search) == -1)
                                continue;
                            // found the next guy
                            var date = DateTime.Parse(id.Substring(0, id.IndexOf('/')));
                            if (date > t.Date // occurred after
                               || (t.Date - date) > TimeSpan.FromDays(7) // or beyond 7 days prior
                                )
                                continue;
                            sumOfPaymentsMade += activity[id].Amount;
                        }

                        // Is the amount within the same ballpark of this transaction?
                        // we'll define ballpark as +/- 25%
                        if (sumOfPaymentsMade == 0 || Math.Abs(t.Amount - sumOfPaymentsMade) > t.Amount * 0.25m) {
                            shouldFlag = false; // if not, then the amount doesn't seem all that suspect
                            break;
                        }

                        // keep following the money trail
                        n = n.Next;
                    }
                    if (shouldFlag) {
                        t.Flags |= TaxRevenueFlags.Ineligible;
                        ar[i] = t;
                    }
                }
            }
        }

        public void Dispose() {
            _Persisted?.Dispose();
        }

        /// <summary>
        /// This is the final tax revenue line item, which can be easily cached for decent performance.
        /// </summary>
        public struct SummaryItem : IComparable<SummaryItem> {
            public decimal Amount;
            public DateTime Date;
            public TaxRevenueFlags Flags;
            public string ID;
            public string PayerRegion;
            public decimal Revenue;

            public int CompareTo(SummaryItem other) {
                return Date.CompareTo(other.Date) * -1;
            }
        }

        public class Intervals {

            /// <summary>
            /// How often we do a complete network scan.
            /// </summary>
            public TimeSpan NetworkPoll = TimeSpan.FromMinutes(10);

            /// <summary>
            /// How often we compile transaction data to calculate
            /// regional tax revenue.
            /// </summary>
            public TimeSpan TransactionCompile = TimeSpan.FromHours(2);

            /// <summary>
            /// How often we query every node for recent transactions
            /// </summary>
            public TimeSpan TransactionPoll = TimeSpan.FromHours(1);


            /// <summary>
            /// How often we compile vote data to calculate
            /// vote results.
            /// </summary>
            public TimeSpan VoteCompile = TimeSpan.FromHours(2);

            /// <summary>
            /// How often we query every node for recent votes
            /// </summary>
            public TimeSpan VotePoll = TimeSpan.FromHours(1);
        }

        /// <summary>
        /// The point of this is to the detect relationships between payees/payers so that small
        /// payment loops for similar amounts can be flagged as ineligible for tax revenue generation.
        /// </summary>
        private class Map {
            private Dictionary<string, Node> Nodes = new Dictionary<string, Node>();

            public void AddRelationship(string payer, string payee) {
                Node a, b;
                Nodes.TryGetValue(payer, out a);
                Nodes.TryGetValue(payee, out b);
                if (a == null) {
                    a = new Node() { ID = payer };
                    Nodes[payer] = a;
                }
                if (b == null) {
                    b = new Node() { ID = payee };
                    Nodes[payee] = b;
                }
                for (int i = 0; i < a.Payees.Length; i++)
                    if (a.Payees[i].ID == payee)
                        return;
                Array.Resize(ref a.Payees, a.Payees.Length + 1);
                a.Payees[a.Payees.Length - 1] = b;
            }

            public LinkedList<string> CalculateDistance(string payer, string payee) {
                // space between
                var st = new Stack<Tmp>();
                var seen = new HashSet<Node>();
                st.Push(new Tmp() { Node = Nodes[payer] });
                var depths = new Stack<int>();
                depths.Push(1);

                // Payee -> X -> Y -> Payer
                while (st.Count > 0) {
                    var n = st.Pop();
                    seen.Add(n.Node);
                    int depth = depths.Pop();
                    if (n.Node.ID == payee) {
                        return n.ToLinkedList();
                    }
                    for (int i = 0; i < n.Node.Payees.Length; i++) {
                        var n2 = n.Node.Payees[i];
                        if (!seen.Contains(n2)) {
                            st.Push(new Tmp() { Node = n2, Previous = n });
                            depths.Push(depth + 1);
                        }
                    }
                }
                return null;
            }

            private class Node {
                public string ID;
                public Node[] Payees = new Node[0];

                public override string ToString() {
                    return ID + " (" + Payees.Length + ")";
                }
            }

            private class Tmp {
                public Node Node;
                public Tmp Previous;

                public LinkedList<string> ToLinkedList() {
                    var ll = new LinkedList<string>();
                    Tmp t = this;
                    while (t != null) {
                        ll.AddFirst(t.Node.ID);
                        t = t.Previous;
                    }
                    return ll;
                }

                public override string ToString() {
                    return Node.ID;
                }
            }
        }

        /// <summary>
        /// Describes a regional tax revenue line item and keeps track of status and how many peers
        /// have corroborated it etc.
        /// </summary>
        public class RevenueItem : IComparable<RevenueItem> {

            /// <summary>
            /// The original transaction amount
            /// </summary>
            public decimal Amount;

            /// <summary>
            /// Flags for ineligibility or shenanigans
            /// </summary>
            public TaxRevenueFlags Flags;

            /// <summary>
            /// The transaction UniqueID, which includes CREATED-UTC + "/" + Payee + "/" + Payer
            /// </summary>
            public string ID;

            /// <summary>
            /// The region of the payer
            /// </summary>
            public string PayerRegion;

            /// <summary>
            /// Collection of witnesses on the transaction
            /// </summary>
            public List<Witness> Witnesses = new List<Witness>();

            public RevenueItem() {
            }

            public RevenueItem(BinaryReader br) {
                ID = br.ReadString();
                PayerRegion = br.ReadString();
                Amount = br.ReadDecimal();
                Flags = (TaxRevenueFlags)br.ReadInt32();
                uint count = br.ReadUInt32();
                for (int i = 0; i < count; i++) {
                    Witnesses.Add(new Witness() {
                        IP = br.ReadUInt32(),
                        MillisecondsSinceCreationUtc = br.ReadUInt32(),
                        Status = br.ReadInt32()
                    });
                }
            }

            public int CompareTo(RevenueItem other) {
                return String.Compare(ID, other.ID);
            }

            public void Serialise(BinaryWriter bw) {
                bw.Write(ID);
                bw.Write(PayerRegion);
                bw.Write(Amount);
                bw.Write((int)Flags);
                uint count = (uint)Witnesses.Count;
                bw.Write(count);
                for (uint i = 0; i < count; i++) {
                    var v = Witnesses[(int)i];
                    bw.Write(v.IP);
                    bw.Write(v.MillisecondsSinceCreationUtc);
                    bw.Write((int)v.Status);
                }
            }

            /// <summary>
            /// Checks for a duplicate entry and returns true if a new entry was appended.
            /// </summary>
            public bool TryAddVote(uint ip, uint timestamp, TransactionStatus status) {
                for (int i = 0; i < Witnesses.Count; i++) {
                    var v = Witnesses[i];
                    if (v.IP == ip && v.MillisecondsSinceCreationUtc == timestamp)
                        return false;
                }
                Witnesses.Add(new Witness() {
                    IP = ip,
                    MillisecondsSinceCreationUtc = timestamp,
                    Status = (int)status
                });
                return true;
            }

            public bool TryGetConsensus(out bool isSettled, out DateTime timestamp) {
                // What's the consensus?

                uint bestTime = 0;
                int count = 0; // number of records for the best time stamp
                int settledCount = 0;
                for (int i = 0; i < Witnesses.Count; i++) {
                    var v = Witnesses[i];
                    if (v.MillisecondsSinceCreationUtc > bestTime) {
                        bestTime = v.MillisecondsSinceCreationUtc;
                        count = 0;
                        settledCount = 0;
                    }
                    if (v.MillisecondsSinceCreationUtc == bestTime) {
                        count++;
                        if ((TransactionStatus)v.Status == TransactionStatus.Settled)
                            settledCount++;
                    }
                }

                // Require that everybody agree. This might change later.
                isSettled = settledCount == count;
                timestamp = DateTime.Parse(ID.Substring(0, ID.IndexOf(' '))).AddMilliseconds((double)bestTime);

                // Require MinimumNumberOfCopies servers to agree. This might increase later.
                return count >= Constants.MinimumNumberOfCopies;
            }
        }

        /// <summary>
        /// Tracks revenue dictionary states and whether or not it needs to be re-serialised to disk.
        /// </summary>
        public class RevenueItemCache {
            public ConcurrentDictionary<string, RevenueItem> Dic;

            /// <summary>
            /// True if it needs to be serialised to disk
            /// </summary>
            public bool IsInvalidated;
        }

        /// <summary>
        /// Mem-caches a regional report
        /// </summary>
        public class SummaryItemCache {
            public DateTime GeneratedUtc;
            public List<SummaryItem> Items;
        }

        /// <summary>
        /// Represents a particular end-point's 'Updated UTC' and 'Status' for a particular
        /// transaction or vote, which we treat as a confirmation for record's current value.
        /// </summary>
        public class Witness {
            public uint IP;

            /// <summary>
            /// To save space we'll peg the timestamp against the first part of the ID:
            /// (uint)(UPDATED-UTC - CREATED-UTC).TotalMilliseconds
            /// </summary>
            public uint MillisecondsSinceCreationUtc;

            public int Status;
        }
    }
}
