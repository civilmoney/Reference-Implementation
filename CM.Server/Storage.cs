#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using CM.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CM.Server {

    /// <summary>
    /// The Storage class is the meat of the Civil Money server,
    /// handling validation, corroboration and storage for of all
    /// object types.
    /// </summary>
    internal partial class Storage : IDisposable {

        /// <summary>
        /// True only when a unit test has instantiated this class.
        /// Disables a few network classes for local storage testing.
        /// </summary>
        internal bool IsOperatingInTestMode;

        private static readonly System.Globalization.CultureInfo _InvariantCulture = System.Globalization.CultureInfo.InvariantCulture;
        private DistributedHashTable _DHT;
        private string _Folder;
        private bool _IsDisposed;
        private Log _Log;
        private System.Collections.Concurrent.ConcurrentDictionary<string, PendingItem> _PendingCommits;
        private System.Collections.Concurrent.ConcurrentDictionary<string, List<Connection>> _Subscriptions;

        private Container _Root;

        /// <summary>
        /// Constructor for a new backing store.
        /// </summary>
        /// <param name="folder">The folder to keep files in.</param>
        /// <param name="dht">The server's DHT instance for querying other peers on the network.</param>
        public Storage(string folder, DistributedHashTable dht, Log log) {
            _Log = log;
            _Folder = folder;
            _DHT = dht;
            _Root = new Container(null, folder, "root");
            _PendingCommits = new System.Collections.Concurrent.ConcurrentDictionary<string, PendingItem>();
            _Subscriptions = new System.Collections.Concurrent.ConcurrentDictionary<string, List<Connection>>();
        }

       // public event Action<string> AccountCreated;
        public event Action<IStorable> ObjectModified;

        /// <summary>
        /// Subscribes a remote client connection to receive NOTIFY packets.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="id"></param>
        public void AddSubscription(Connection conn, string id) {
            id = id.ToLower();
            lock (conn.SubscriptionSync) {
                conn.ConnectionLost += RemoveSubscriptions;
                if (conn.SubscribedIDs == null)
                    conn.SubscribedIDs = new List<string>();
                if (!conn.SubscribedIDs.Contains(id))
                    conn.SubscribedIDs.Add(id);
            }
            List<Connection> connections;
            if (!_Subscriptions.TryGetValue(id, out connections)) {
                connections = new List<Connection>();
                _Subscriptions[id] = connections;
            }
            lock (connections) {
                if (!connections.Contains(conn))
                    connections.Add(conn);
            }
        }

        void RemoveSubscription(Connection conn, string id) {
            lock (conn.SubscriptionSync) {
                if (conn.SubscribedIDs != null)
                    conn.SubscribedIDs.Remove(id);
            }
            List<Connection> connections;
            if (!_Subscriptions.TryGetValue(id, out connections))
                return;
            lock (connections) {
                connections.Remove(conn);
            }
        }
        void RemoveSubscriptions(Connection conn) {
            conn.ConnectionLost -= RemoveSubscriptions;
            var ar = conn.SubscribedIDs;
            if (ar == null) return;
            for (int i = 0; i < ar.Count; i++) {
                RemoveSubscription(conn, ar[i]);
            }
        }
        /// <summary>
        /// Commits the specified token to the backing store after corroborating
        /// with the network that at least MinimumNumberOfCopies elsewhere
        /// are also prepared to commit the same version.
        /// </summary>
        /// <param name="token">The token to commit.</param>
        /// <returns>S_OK, E_Item_Not_Found, E_Not_Enough_Peers.</returns>
        public async Task<CMResult> Commit(string token, 
            bool skipNetworkCorroboration = false, 
            bool sendPushNotifications = false) {
            PendingItem item;
            if (!_PendingCommits.TryGetValue(token, out item))
                return CMResult.E_Item_Not_Found;

            if (item.HasCommitted) {
                // Already committed successfully.
                return CMResult.S_OK;
            }

            // Query the network for other peers about to successfully commit
            // the same object.
            var path = item.Item.Path;
            int success = 0;
            if (!IsOperatingInTestMode && !skipNetworkCorroboration) {
                var peers = new List<string>();
                await FindResponsiblePeersForPath(peers, path);
                if (peers.Count < Constants.MinimumNumberOfCopies) {
                    return CMResult.E_Not_Enough_Peers;
                }

                int maxTries = 3;
                await peers.ForEachAsync(3,
                    async (ep) => {
                        using (var conn = await _DHT.Connect(ep)) {
                            if (conn.IsValid) {
                                for (int i = 0; i < maxTries; i++) {
                                    var res = await conn.Connection.SendAndReceive("QUERY-COMMIT", null, path);
                                    if (res.Response.IsSuccessful) {
                                        DateTime updatedUtc;
                                        if (Helpers.DateFromISO8601(res.Response.Arguments[0], out updatedUtc)
                                            && updatedUtc == item.Item.UpdatedUtc) {
                                            // Looking good - this server has also validated and
                                            // is prepared to commit the same copy as me.
                                            return true;
                                        } else if (updatedUtc > item.Item.UpdatedUtc) {
                                            // the server's newer version is never going to lower
                                            // than ours, so give up.
                                            return false;
                                        }
                                    }
                                    // give the peer some time before retrying
                                    await Task.Delay(1000);
                                }
                            }
                        }
                        return false;

                    }, (ep, ok) => {
                        if (ok)
                            System.Threading.Interlocked.Increment(ref success);
                    });
            } else {
                success = Constants.MinimumNumberOfCopies;
            }

            if (success < Constants.MinimumNumberOfCopies)
                return CMResult.E_Not_Enough_Peers;

            if (item.Item is Account) {
                StoreAccount(item.Item as Account);
            } else if (item.Item is Transaction) {
                StoreTransaction(item.Item as Transaction);
            } else if (item.Item is Vote) {
                StoreVote(item.Item as Vote);
            } else {
                throw new NotImplementedException();
            }

            // It's important to not remove the token directly after commit,
            // as other DHT peers may be running late and QueryCommitStatus
            // after we've committed. Housekeeping will remove it after
            // a couple of minutes.
            item.HasCommitted = true;

            if (sendPushNotifications && !item.PushNotificationsSent) {
                item.PushNotificationsSent = true;
                SendPushNotifications(item.PushNotify, path);
                SendSubscribeNotifications(item.Item);
            }

           
            

            return CMResult.S_OK;
        }
       
        async void SendSubscribeNotifications(IStorable item) {
            List<Connection> ar = new List<Connection>();
            if (item is Account) {
                var a = item as Account;
                List<Connection> connections;
                if (_Subscriptions.TryGetValue(a.ID.ToLower(), out connections)
                    && connections != null) {
                    lock (connections) {
                        for (int i = 0; i < connections.Count; i++) {
                            if (!connections[i].IsConnected) {
                                connections.RemoveAt(i);
                                i--;
                            }
                        }
                        ar.AddRange(connections);
                    }
                   
                }
            } else if (item is Transaction) {
                var t = item as Transaction;
                List<Connection> connections;
                if (_Subscriptions.TryGetValue(t.PayeeID.ToLower(), out connections) && connections != null) {
                    lock (connections) {
                        for (int i = 0; i < connections.Count; i++) {
                            if (!connections[i].IsConnected) {
                                connections.RemoveAt(i);
                                i--;
                            }
                        }
                        ar.AddRange(connections);
                    }
                }
                if (_Subscriptions.TryGetValue(t.PayerID.ToLower(), out connections) && connections != null) {
                    lock (connections) {
                        for (int i = 0; i < connections.Count; i++) {
                            if (!connections[i].IsConnected) {
                                connections.RemoveAt(i);
                                i--;
                            }
                        }
                        ar.AddRange(connections);
                    }
                }
            }
            await ar.ForEachAsync(2, async (conn) => {
                var res = await conn.SendAndReceive("NOTIFY", (Message)item, item.Path);
                return res != null && res.Response.IsSuccessful;
            }, (conn, success) => {
                if (!success) {
                    RemoveSubscriptions(conn);
                }
            });
        }


        /// <summary>
        /// Populates an account's AccountCalculations structure according to this local server's
        /// transaction data.
        /// </summary>
        /// <param name="a">The account to populate.</param>
        /// <param name="reportingTimeUtc">
        /// The moment in time to calculate for. Typically DateTime.UtcNow however it can be used for
        /// credit history.
        /// </param>
        public void FillAccountCalculations(Account a, DateTime reportingTimeUtc) {
            var id = a.ID;
            var calc = a.AccountCalculations = new AccountCalculations(a);
            decimal credits = 0;
            decimal debits = 0;
            var container = _Root.Open(GetAccountDataFilePath(id));
            var keys = FilterTransactionKeys(container.GetKeys());
            DateTime lastTransaction = DateTime.MinValue;
            var months = new bool[14];
            for (int i = 0; i < keys.Count; i++) {
                var key = keys[i];
                var data = container.Get(key);
                var idx = new TransactionIndex(data);
                if (idx.CreatedUtc > reportingTimeUtc)
                    continue;
               if (idx.UpdatedUtc > lastTransaction)
                    lastTransaction = idx.UpdatedUtc;
                if (idx.Payee == id) {
                    credits += Helpers.CalculateTransactionDepreciatedAmountForPayee(reportingTimeUtc, idx.CreatedUtc, idx.Amount, idx.PayeeStatus);
                } else {
                    debits += Helpers.CalculateTransactionDepreciatedAmountForPayer(reportingTimeUtc, idx.CreatedUtc, idx.Amount, idx.PayeeStatus, idx.PayerStatus);
                }
                int monthsAgo = (int)((reportingTimeUtc - idx.CreatedUtc).TotalDays / 30);
                if (monthsAgo >= 0
                    && monthsAgo <= 13
                    && idx.PayeeStatus == PayeeStatus.Accept
                    && idx.PayerStatus == PayerStatus.Accept) {
                    months[monthsAgo] = true;
                }
            }

            decimal rr;
            RecentReputation rep;
            Helpers.CalculateRecentReputation(credits, debits, out rr, out rep);
            calc.RecentReputation = rr;
            calc.RecentCredits = credits;
            calc.RecentDebits = debits;
            if (lastTransaction != DateTime.MinValue)
                calc.LastTransactionUtc = lastTransaction;
            int monthsWithActivity = 0;
            for (int i = 0; i < months.Length; i++)
                if (months[i])
                    monthsWithActivity++;
            // We'll accept any 12 out of the last/current 14 months.
            calc.IsEligibleForVoting = monthsWithActivity >= 12 && rep == RecentReputation.Good;

        }

        /// <summary>
        /// Gets a storable item from the disk persisted backing store.
        /// </summary>
        /// <param name="path">The path to get</param>
        /// <param name="item">A pointer to receive the item.</param>
        /// <returns>S_OK if successful, otherwise E_Item_Not_Found.</returns>
        public CMResult Get(string path, out IStorable item) {
            item = null;
            if (path == null) throw new ArgumentNullException("path");
            ThrowIfDisposed();
            if (path.StartsWith(Constants.PATH_ACCNT, StringComparison.OrdinalIgnoreCase)) {
                // ACCNT/{ID}
                string id = path.Substring(Constants.PATH_ACCNT.Length + 1);
                if (!Helpers.IsIDValid(id)) {
                    return CMResult.E_Account_ID_Invalid;
                }
                item = TryFindLocalAccountCopy(id);
                return item != null ? CMResult.S_OK : CMResult.E_Item_Not_Found;
            } else if (path.StartsWith(Constants.PATH_TRANS, StringComparison.OrdinalIgnoreCase)) {
                string id = path.Substring(Constants.PATH_TRANS.Length + 1);
                DateTime created;
                string payee, payer;
                if (!Helpers.TryParseTransactionID(id, out created, out payee, out payer))
                    return CMResult.E_Invalid_Object_Path;
                item = TryFindLocalTransactionCopy(created, payee, payer);
                return item != null ? CMResult.S_OK : CMResult.E_Item_Not_Found;
            } else if (path.StartsWith(Constants.PATH_VOTES, StringComparison.OrdinalIgnoreCase)) {
                // VOTES/{Proposition ID}/{account ID}
                var str = path.Substring(Constants.PATH_VOTES.Length + 1);
                var idx = str.IndexOf('/');
                if (idx == -1)
                    return CMResult.E_Invalid_Object_Path;
                string propID = str.Substring(0, idx);
                string accountID = str.Substring(idx + 1);
                uint propositionID;
                if (!uint.TryParse(propID, out propositionID)
                    || !Helpers.IsIDValid(accountID))
                    return CMResult.E_Invalid_Object_Path;
                item = TryFindLocalVoteCopy(propositionID, accountID);
                return item != null ? CMResult.S_OK : CMResult.E_Item_Not_Found;
            } else {
                return CMResult.E_Invalid_Object_Path;
            }
        }

        /// <summary>
        /// Lists the contents of the requested path.
        /// </summary>
        /// <param name="req">The list requests</param>
        /// <param name="res">Pointer to receive a list response</param>
        /// <returns>S_OK or an error code.</returns>
        public CMResult List(ListRequest req, out ListResponse res) {
            if (req == null)
                throw new ArgumentNullException("req");

            res = null;
            var path = req.Request.AllArguments;
            if (String.IsNullOrEmpty(path))
                return CMResult.E_Invalid_Object_Path;

            if (req.UpdatedUtcFromInclusive >= req.UpdatedUtcToExclusive
                || req.UpdatedUtcFromInclusive == DateTime.MinValue
                || req.UpdatedUtcToExclusive == DateTime.MaxValue) {
                return CMResult.E_Invalid_Request;
            }

            // All accounts
            if (path.Equals(Constants.PATH_ACCNT + "/", StringComparison.OrdinalIgnoreCase)) {
                res = ListAccounts(req);
            }
            // Account transactions/votes
            else if (path.StartsWith(Constants.PATH_ACCNT + "/", StringComparison.OrdinalIgnoreCase)) {
                var args = path.Split('/');
                var id = args[1];
                switch (args[2]) {
                    case Constants.PATH_TRANS: // ACCNT/{ID}/TRANS/
                        res = ListAccountTransactions(req, id);
                        break;
                    case Constants.PATH_VOTES: // ACCNT/{ID}/VOTES/
                        res = ListAccountVotes(req, id);
                        break;
                    default:
                        return CMResult.E_Invalid_Object_Path;
                }
            }
            // All transactions
            else if (path.Equals(Constants.PATH_TRANS + "/", StringComparison.OrdinalIgnoreCase)) {
                res = ListAllTransactions(req);
            }
            // All votes for a particular proposition
            else if (path.StartsWith(Constants.PATH_VOTES + "/", StringComparison.OrdinalIgnoreCase)) {
                var prop = path.Substring((Constants.PATH_VOTES + "/").Length);
                uint propID;
                if(!uint.TryParse(prop, out propID))
                    return CMResult.E_Invalid_Object_Path;
                res = ListAllVotes(req, propID.ToString());
            }
            // Transactions for a particular region
            else if (path.StartsWith(Constants.PATH_REGIONS + "/", StringComparison.OrdinalIgnoreCase)) {
                string region = path.Split('/')[1];
                res = ListRegionTransactions(region, req);
            } else {
                return CMResult.E_Invalid_Object_Path;
            }

            return CMResult.S_OK;
        }

        /// <summary>
        /// Cleans up any pending commits that didn't get finished.
        /// </summary>
        public void PerformHouseKeeping() {
            // Clean up pending commits
            var items = _PendingCommits.ToArray();
            for (int i = 0; i < items.Length; i++) {
                if (items[i].Value.IsExpired) {
                    PendingItem item;
                    _PendingCommits.TryRemove(items[i].Key, out item);
                }
            }

            // Close idle containers
            CloseContainers(true);
        }
        public struct PutResult {
            public PutResult(string token) {
                Code = CMResult.S_OK;
                Token = token;
            }
            public PutResult(CMResult code) {
                Code = code;
                Token = null;
            }
            public CMResult Code;
            public string Token;
        }
        /// <summary>
        /// Validates and creates a commit token for item storage.
        /// </summary>
        /// <param name="item">The item to store.</param>
        /// <returns>S_OK or a validation error code.</returns>
        public async Task<PutResult> Put(IStorable item) {
       
            if (item == null) throw new ArgumentNullException("item");

            ThrowIfDisposed();
            var notifications = new List<string>();
            if (item is Account) {
                var a = (Account)item;
                var req = new AsyncRequest<ValidateRequest<Account>>();
                req.Item = new ValidateRequest<Account>();
                req.Item.New = a;
                if (!IsOperatingInTestMode) {
                    req.Item.Old = await TryFindOnNetwork<Account>(a.Path);
                } else
                    req.Item.Old = TryFindLocalAccountCopy(a.ID);
                await Validate(req, notifications);
                if (req.Result != CMResult.S_OK) {
                    return new PutResult(req.Result);
                }
            } else if (item is Transaction) {
                var t = (Transaction)item;
                var req = new AsyncRequest<ValidateRequest<Transaction>>();
                req.Item = new ValidateRequest<Transaction>();
                req.Item.New = t;
                if (!IsOperatingInTestMode)
                    req.Item.Old = await TryFindOnNetwork<Transaction>(t.Path);
                else
                    req.Item.Old = TryFindLocalTransactionCopy(t.CreatedUtc, t.PayeeID, t.PayerID);
                await Validate(req, notifications);
                if (req.Result != CMResult.S_OK)
                    return new PutResult(req.Result);
            } else if (item is Vote) {
                var v = (Vote)item;
                var req = new AsyncRequest<ValidateRequest<Vote>>();
                req.Item = new ValidateRequest<Vote>();
                req.Item.New = v;
                if (!IsOperatingInTestMode)
                    req.Item.Old = await TryFindOnNetwork<Vote>(v.Path);
                else
                    req.Item.Old = TryFindLocalVoteCopy(v.PropositionID, v.VoterID);
                await Validate(req, notifications);
                if (req.Result != CMResult.S_OK)
                    return new PutResult(req.Result);
            } else {
                throw new NotSupportedException("Unable to store object of type " + item.GetType());
            }

            var pend = new PendingItem(item, notifications);
            var commitToken = Guid.NewGuid().ToString();
            while (!_PendingCommits.TryAdd(commitToken, pend))
                commitToken = Guid.NewGuid().ToString();
            return new PutResult(commitToken);
        }

        public CMResult QueryCommitStatus(string path, out DateTime updatedUtc) {
            ThrowIfDisposed();
            updatedUtc = DateTime.MinValue;
            if (String.IsNullOrWhiteSpace(path)) return CMResult.E_Invalid_Request;
            // If multiple pending commits exist for the same unique identifier,
            // peers must return the highest UPDATED-UTC.
            foreach (var o in _PendingCommits.Values) {
                if (o.IsExpired)
                    continue;
                if (o.Item.Path == path) {
                    if (updatedUtc < o.Item.UpdatedUtc)
                        updatedUtc = o.Item.UpdatedUtc;
                }
            }
            // Check the local record as well
            IStorable current;
            Get(path, out current);
            if (current != null && updatedUtc < current.UpdatedUtc)
                updatedUtc = current.UpdatedUtc;

            return updatedUtc != DateTime.MinValue ? CMResult.S_OK : CMResult.E_Item_Not_Found;
        }

        public async Task<T> TryFindOnNetwork<T>(string path)
                            where T : Message, IStorable {
            var peers = new List<string>();

            await FindResponsiblePeersForPath(peers, path);

            List<T> copies = new List<T>();

            await peers.ForEachAsync(3,
                async (ep) => {
                    using (var conn = await _DHT.Connect(ep)) {
                        if (conn.IsValid) {
                            return await conn.Connection.SendAndReceive("GET", null, path);
                        } else {
                            return null;
                        }
                    }
                }, (ep, res) => {
                    if (res != null 
                        && res.Response.IsSuccessful) {
                        lock (copies)
                            copies.Add(res.Cast<T>());
                    }
                });

            T account;
            Helpers.CheckConsensus(copies, out account);
            return account;
        }

        internal async Task FindResponsiblePeersForAccount(List<string> peers, string id) {
            string[] copyIDs = new string[Constants.NumberOfCopies];
            for (int i = 0; i < copyIDs.Length; i++) {
                copyIDs[i] = "copy" + (i+1) + id;
            }

            await copyIDs.ForEachAsync(2, async (copyID) => {
                return await _DHT.FindResponsiblePeer(new FindResponsiblePeerRequest() {
                    MaxHopCount = Constants.MaxHopCount,
                    DHTID = Helpers.DHT_ID(copyID)
                });
            }, (copy, reply) => {
                if (reply.Response.IsSuccessful) {
                    lock (peers)
                        if (reply.PeerEndpoint != null
                            && !peers.Contains(reply.PeerEndpoint))
                            peers.Add(reply.PeerEndpoint);
                }
            });
        }

        private static void AppendPushNotifications(List<string> notifications, Account acc) {
            if (notifications == null)
                return; 
            var atts = acc.CollectAttributes();
            for (int i = 0; i < atts.Count; i++) {
                var a = atts[i];
                if (a.Name == AccountAttributes.PushNotification_Key
                    && !String.IsNullOrEmpty(a.Value)) {
                    var notify = new AccountAttributes.PushNotificationCsv(a.Value);
                    if (!notifications.Contains(notify.HttpUrl)) {
                        notifications.Add(notify.HttpUrl);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a sorted list of all transaction paths
        /// </summary>
        private static List<string> FilterTransactionKeys(string[] keys) {
            var ar = new List<string>();
            for (int i = 0; i < keys.Length; i++) {
                if (keys[i].StartsWith(Constants.PATH_TRANS)) {
                    ar.Add(keys[i]);
                }
            }
            ar.Sort();
            return ar;
        }
        private static List<string> FilterVoteKeys(string[] keys) {
            var ar = new List<string>();
            for (int i = 0; i < keys.Length; i++) {
                if (keys[i].StartsWith(Constants.PATH_VOTES)) {
                    ar.Add(keys[i]);
                }
            }
            ar.Sort();
            return ar;
        }
        private static string GetAccountDataFilePath(string id) {
            // Because of potential file system limitations we're going to partition
            // accounts into folders based on their first two base64 characters,
            // that way we don't end up with millions of containers all in
            // one folder.
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(id.ToLower()))
                .Replace("/", "_");
            return Constants.PATH_ACCNT + "/" + b64.Substring(0, 2) + "/" + b64;
        }

        private static string GetRegionDataFilePath(string isoRegioncode, DateTime createdUtc) {
            // Regional transactions are kept under REGION/{code}/{yyyy-MM-dd}.htindex
            return Constants.PATH_REGIONS + "/" + isoRegioncode + "/" + createdUtc.ToString("yyyy-MM-dd");
        }

        private static string GetTransactionDataFilePath(DateTime createdUtc) {
            // Transactions are kept under TRANS/yyyy-MM/{dd}.htindex
            return Constants.PATH_TRANS + "/" + createdUtc.ToString("yyyy-MM") + "/" + createdUtc.Day;
        }

        private static string GetVotesDataFilePath(string propositionID) {
            // Votes are kept under VOTES/{Proposition ID base64}.htindex
            var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(propositionID))
                .Replace("/", "_");
            return Constants.PATH_VOTES + "/" + b64;
        }

        private void CloseContainers(bool idleOnly) {
            var st = new Stack<Container>();
            st.Push(_Root);
            while (st.Count > 0) {
                var c = st.Pop();
                if (!idleOnly || c.ShouldClose)
                    c.Close();
                else
                    c.Flush();
                var ar = c.CollectChildren();
                foreach (var child in ar)
                    st.Push(child);
            }
        }

        private async Task FindResponsiblePeersForPath(List<string> peers, string path) {
            int queryIdx = path.IndexOf('?');
            if (queryIdx > -1)
                path = path.Substring(0, queryIdx);
            if (path.StartsWith(Constants.PATH_ACCNT)) {
                string id = path.Substring(Constants.PATH_ACCNT.Length + 1);
                if (id.IndexOf('/') > -1)
                    id = id.Substring(0, id.IndexOf('/'));
                if (Helpers.IsIDValid(id))
                    await FindResponsiblePeersForAccount(peers, id);
                else
                    throw new ArgumentException("Invalid Account ID in path.");
            } else if (path.StartsWith(Constants.PATH_TRANS)) {
                string id = path.Substring(Constants.PATH_TRANS.Length + 1);
                DateTime created;
                string payee, payer;
                if (Helpers.TryParseTransactionID(id, out created, out payee, out payer)) {
                    await FindResponsiblePeersForAccount(peers, payee);
                    await FindResponsiblePeersForAccount(peers, payer);
                } else
                    throw new ArgumentException("Invalid transaction ID in path.");
            } else if (path.StartsWith(Constants.PATH_VOTES)) {
                var parts = path.Split('/');
                // VOTES/{PropositionID}/{ID}
                if (Helpers.IsIDValid(parts[2]))
                    await FindResponsiblePeersForAccount(peers, parts[2]);
            } else {
                throw new ArgumentException($"Invalid path for DHT searching '{path}'");
            }
        }

        private async void SendPushNotifications(List<string> endpoints, string path) {
            //
            // Push notifications simply receive a HTTP GET with header
            // X-Civil-Money-Notify: { modified object path }
            // .. it is up to the sink to then corroborate with the network
            // all on its own.
            //
            for (int i = 0; i < endpoints.Count; i++) {
                try {
                    Uri uri;
                    if (Uri.TryCreate(endpoints[i], UriKind.Absolute, out uri)) {
                        var req = System.Net.HttpWebRequest.CreateHttp(uri);
                        req.Method = "GET";
                        req.Headers["X-Civil-Money-Notify"] = path;
                        var res = await req.GetResponseAsync();
                        res.Dispose();
                    }
                } catch { }
            }
        }

        private void StoreAccount(Account a) {
            var container = _Root.Open(GetAccountDataFilePath(a.ID));
            var key = Constants.PATH_ACCNT + "/" + Helpers.DateToISO8601(a.UpdatedUtc);
            container.Set(key, a.ToContentString());
            ObjectModified?.Invoke(a);
        }

        private void StoreTransaction(Transaction t) {
            var container = _Root.Open(GetTransactionDataFilePath(t.CreatedUtc));
            var key = t.ID + "/" + Helpers.DateToISO8601(t.UpdatedUtc);
            container.Set(key, t.ToContentString());

            var link = t.Path;
            var payee = _Root.Open(GetAccountDataFilePath(t.PayeeID));
            var payer = _Root.Open(GetAccountDataFilePath(t.PayerID));
            var region = _Root.Open(GetRegionDataFilePath(t.PayeeRegion, t.CreatedUtc));

            var index = new TransactionIndex(t).ToString();
            payee.Set(link, index);
            payer.Set(link, index);
            region.Set(link, index);
            ObjectModified?.Invoke(t);
        }

        private void StoreVote(Vote v) {
            // Votes are stored in VOTES/{PropositionID}.htdata key '{VoterID}/{CreatedUtc}/{UpdatedUtc}'
            var container = _Root.Open(GetVotesDataFilePath(v.PropositionID.ToString()));
            var path = v.VoterID + "/" + Helpers.DateToISO8601(v.CreatedUtc) + "/" + Helpers.DateToISO8601(v.UpdatedUtc);
            container.Set(path, v.ToContentString());

            // VoteIndex is kept in ACCNT/{VoterID}/VOTES/{PropositionID}
            var account = _Root.Open(GetAccountDataFilePath(v.VoterID));
            var link = Constants.PATH_VOTES + "/" + v.PropositionID;
            var index = new VoteIndex(v).ToString();
            account.Set(link, index);
            ObjectModified?.Invoke(v);
        }

        private Vote TryFindLocalVoteCopy(uint propositionID, string id) {
            Account a = TryFindLocalAccountCopy(id);
            if (a == null)
                return null;
            var accountContainer = _Root.Open(GetAccountDataFilePath(id));
            var link = Constants.PATH_VOTES + "/" + propositionID;
            var indexData = accountContainer.Get(link);
            if (indexData == null)
                return null;
            VoteIndex index = new VoteIndex(indexData);

            var container = _Root.Open(GetVotesDataFilePath(propositionID.ToString()));
            var path = id + "/" + Helpers.DateToISO8601(index.CreatedUtc) + "/" + Helpers.DateToISO8601(index.UpdatedUtc);
            var data = container.Get(path);
            if (data == null)
                return null;

            // Validate the vote
            var v = new Vote(data);
            AsyncRequest<DataVerifyRequest> verify = new AsyncRequest<DataVerifyRequest>();
            verify.Item = new DataVerifyRequest() {
                DataDateUtc = v.UpdatedUtc,
                Input = v.GetSigningData(),
                Signature = v.Signature
            };
            a.VerifySignature(verify, CryptoFunctions.Identity);
            if (verify.Result != CMResult.S_OK) {
                return null;
            }
            return v;
        }

        private void ThrowIfDisposed() {
            if (_IsDisposed)
                throw new ObjectDisposedException("Storage");
        }
        public bool DoesAccountExist(string id) {
            var container = _Root.Open(GetAccountDataFilePath(id));
            return container.Exists;
        }
        private Account TryFindLocalAccountCopy(string id) {
            var container = _Root.Open(GetAccountDataFilePath(id));
            var keys = container.GetKeys();

            // We want the newest dated copy
            DateTime d;
            DateTime latest = DateTime.MinValue;
            string key = null;
            for (int i = 0; i < keys.Length; i++) {
                var k = keys[i];
                if (k.StartsWith(Constants.PATH_ACCNT + "/")
                    && Helpers.DateFromISO8601(k.Substring(6), out d)
                    && d > latest) {
                    latest = d;
                    key = k;
                }
            }

            if (key == null) {
                // Account doesn't exist inside account container. This can happen if the process
                // exits during write. The data file exists, but it's empty/invalid. We should delete
                // it so List("ACCNT/") doesn't pick up the account ID any more. The account will
                // sync back in eventually if still applicable.
                if (container.Exists) {
                    container.PermanentlyDelete();
                    _Log.Write(this, LogLevel.WARN, "Deleting {0} because of missing account key.", id);
                }
                return null;
            }

            string data = container.Get(key);
           
            if (data == null) {
                // We shouldn't end up here. It looks like key exists for the id but its data does
                // not. Assume corruption and let it sync back.
                container.PermanentlyDelete();
                _Log.Write(this, LogLevel.WARN, "Deleting {0} because of missing account data.", id);
                return null;
            }

            var a = new Account(data);
            System.Diagnostics.Debug.Assert(String.Compare(a.ID, id,true)==0,
                String.Format("ID and data store mismatch. This must NEVER happen. Expected '{0}', got '{1}'.",
                id, a.ID));
            if (String.Compare(a.ID, id, true) != 0)
                return null;

            // We validate our own data. Civil Money trusts nothing.
            AsyncRequest<DataVerifyRequest> verify = new AsyncRequest<DataVerifyRequest>();
            verify.Item = new DataVerifyRequest() {
                DataDateUtc = a.UpdatedUtc,
                Input = a.GetSigningData(),
                Signature = a.AccountSignature
            };
            a.VerifySignature(verify, CryptoFunctions.Identity);

            System.Diagnostics.Debug.Assert(verify.Result == CMResult.S_OK,
              String.Format("Local storage signature error on account '{0}'", a.ID));

            if (verify.Result != CMResult.S_OK) {
                return null;
            }

            return a;
        }

        private Transaction TryFindLocalTransactionCopy(DateTime d, string payee, string payer) {
            var container = _Root.Open(GetTransactionDataFilePath(d));
            var path = Helpers.DateToISO8601(d) + " " + payee + " " + payer + "/";
            string bestKey = null;
            DateTime updatedUtc = DateTime.MinValue;
            var keys = container.GetKeys();
            for (int i = 0; i < keys.Length; i++) {
                var k = keys[i];
                if (k.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
                    var time = Helpers.DateFromISO8601(k.Substring(path.Length));
                    if (time > updatedUtc) {
                        updatedUtc = time;
                        bestKey = k;
                    }
                }
            }
            if (bestKey == null)
                return null;
            var data = container.Get(bestKey);
            return new Transaction(data);
        }

        private async Task Validate(AsyncRequest<ValidateRequest<Transaction>> e, List<string> notifications) {
            AsyncRequest<DataVerifyRequest> ver;

            var t = e.Item.New;

            if (t.APIVersion != 1) {
                e.Completed(CMResult.E_Unknown_API_Version);
                return;
            }

            // Created UTC, Amount, Payer and Payee IDs and regions are required and read-only upon creation.

            if (String.IsNullOrWhiteSpace(t.PayeeID)) {
                e.Completed(CMResult.E_Transaction_PayeeID_Required);
                return;
            }

            if (String.IsNullOrWhiteSpace(t.PayerID)) {
                e.Completed(CMResult.E_Transaction_PayerID_Required);
                return;
            }

            if (String.Equals(t.PayerID, t.PayeeID, StringComparison.OrdinalIgnoreCase)) {
                e.Completed(CMResult.E_Transaction_Payer_Payee_Must_Differ);
                return;
            }

            if (ISO31662.GetName(t.PayerRegion) == null) {
                e.Completed(CMResult.E_Transaction_Payer_Region_Required);
                return;
            }

            if (t.Amount < Constants.MinimumTransactionAmount) {
                e.Completed(CMResult.E_Transaction_Invalid_Amount);
                return;
            }

            // Transactions must not be post-dated
            if (t.CreatedUtc > DateTime.UtcNow.AddMinutes(Constants.MaxAllowedTimestampErrorInMinutes)) {
                e.Completed(CMResult.E_Transaction_Created_Utc_Out_Of_Range);
                return;
            }

            // The payer's signature and updated utc is required in order to create a new transaction.
            if (t.PayerSignature == null) {
                e.Completed(CMResult.E_Transaction_Payer_Signature_Required);
                return;
            }
            if (t.PayerUpdatedUtc < t.CreatedUtc) {
                e.Completed(CMResult.E_Transaction_Payer_Updated_Utc_Out_Of_Range);
                return;
            }

            if ((t.PayeeTag != null && System.Text.Encoding.UTF8.GetByteCount(t.PayeeTag) > 48)
              || (t.PayerTag != null && System.Text.Encoding.UTF8.GetByteCount(t.PayerTag) > 48)) {
                e.Completed(CMResult.E_Transaction_Tag_Too_Long);
                return;
            }

            if ((t.Memo != null && System.Text.Encoding.UTF8.GetByteCount(t.Memo) > 48)) {
                e.Completed(CMResult.E_Transaction_Memo_Too_Long);
                return;
            }

            Account payer;
            if (!IsOperatingInTestMode)
                payer = await TryFindOnNetwork<Account>(Constants.PATH_ACCNT + "/" + t.PayerID);
            else
                payer = TryFindLocalAccountCopy(t.PayerID);

            if (payer == null) {
                e.Completed(CMResult.E_Transaction_Payer_Not_Found);
                return;
            }

            ver = new AsyncRequest<DataVerifyRequest>() {
                Item = new DataVerifyRequest() {
                    Input = t.GetPayerSigningData(),
                    Signature = t.PayerSignature,
                    DataDateUtc = t.PayerUpdatedUtc
                }
            };
            payer.VerifySignature(ver, CryptoFunctions.Identity);
            if (ver.Result != CMResult.S_OK) {
                Server.DebugDump("Invalid payer signature", t.ToContentString());
                e.Completed(CMResult.E_Transaction_Invalid_Payer_Signature);
                return;
            }

            if (t.PayeeUpdatedUtc != DateTime.MinValue
                && t.PayeeUpdatedUtc < t.CreatedUtc) {
                e.Completed(CMResult.E_Transaction_Payee_Updated_Utc_Out_Of_Range);
                return;
            }
            Account payee;
            if (!IsOperatingInTestMode)
                payee = await TryFindOnNetwork<Account>(Constants.PATH_ACCNT + "/" + t.PayeeID);
            else
                payee = TryFindLocalAccountCopy(t.PayeeID);
            if (payee == null) {
                e.Completed(CMResult.E_Transaction_Payee_Not_Found);
                return;
            }
            if (t.PayeeSignature != null) {
                // Require Payee's region upon signing
                if (ISO31662.GetName(t.PayeeRegion) == null) {
                    e.Completed(CMResult.E_Transaction_Payee_Region_Required);
                    return;
                }

                ver = new AsyncRequest<DataVerifyRequest>() {
                    Item = new DataVerifyRequest() {
                        Input = t.GetPayeeSigningData(),
                        Signature = t.PayeeSignature,
                        DataDateUtc = t.PayeeUpdatedUtc
                    }
                };
                payee.VerifySignature(ver, CryptoFunctions.Identity);
                if (ver.Result != CMResult.S_OK) {
                    Server.DebugDump("Invalid payee signature", t.ToContentString());
                    e.Completed(CMResult.E_Transaction_Invalid_Payee_Signature);
                    return;
                }
            } else {
                if (t.PayeeStatus != PayeeStatus.NotSet) {
                    e.Completed(CMResult.E_Transaction_Payee_Status_Invalid);
                    return;
                }
            }

            // Check for disallowed modifications and status changes

            var old = e.Item.Old;
            if (old != null) {
                if (old.UpdatedUtc > t.UpdatedUtc) {
                    e.Completed(CMResult.E_Object_Superseded);
                    return;
                }
                if (old.Amount != t.Amount) {
                    e.Completed(CMResult.E_Transaction_Amount_Is_Readonly);
                    return;
                }
                if (old.CreatedUtc != t.CreatedUtc) {
                    e.Completed(CMResult.E_Transaction_Created_Utc_Is_Readonly);
                    return;
                }
                if (old.PayerID != t.PayerID) {
                    e.Completed(CMResult.E_Transaction_Payer_Is_Readonly);
                    return;
                }
                if (old.PayeeID != t.PayeeID) {
                    e.Completed(CMResult.E_Transaction_Payee_Is_Readonly);
                    return;
                }
                if (old.Memo != t.Memo) {
                    e.Completed(CMResult.E_Transaction_Memo_Is_Readonly);
                    return;
                }
                if (old.PayeeStatus != PayeeStatus.NotSet
                    && old.PayeeRegion != t.PayeeRegion) {
                    e.Completed(CMResult.E_Transaction_Payee_Region_Is_Readonly);
                    return;
                }
                if (old.PayerRegion != t.PayerRegion) {
                    e.Completed(CMResult.E_Transaction_Payer_Region_Is_Readonly);
                    return;
                }
               
                if (!Helpers.IsPayeeStatusChangeAllowed(old.PayeeStatus, t.PayeeStatus)) {
                    e.Completed(CMResult.E_Transaction_Payee_Status_Change_Not_Allowed);
                    return;
                }

                if (!Helpers.IsPayerStatusChangeAllowed(old.PayerStatus, t.PayerStatus)) {
                    e.Completed(CMResult.E_Transaction_Payer_Status_Change_Not_Allowed);
                    return;
                }
                
                // Payers can only cancel if the payee has not yet responded.
                if (t.PayerStatus == PayerStatus.Cancel
                    && t.PayeeStatus != PayeeStatus.NotSet) {
                    e.Completed(CMResult.E_Transaction_Payer_Status_Change_Not_Allowed);
                    return;
                }
            } else {
                if (t.PayerStatus != PayerStatus.Accept
                    && t.UpdatedUtc == t.CreatedUtc) {
                    e.Completed(CMResult.E_Transaction_Payer_Accept_Status_Required);
                    return;
                }
            }

            var localPayee = TryFindLocalAccountCopy(t.PayeeID);
            if (localPayee == null || localPayee.UpdatedUtc < payee.UpdatedUtc) {
                var req = new AsyncRequest<ValidateRequest<Account>>();
                req.Item = new ValidateRequest<Account>();
                req.Item.New = payee;
                req.Item.Old = localPayee;
                await Validate(req, null);
                if (req.Result == CMResult.S_OK) {
                    StoreAccount(payee);
                } 
            }

            var localPayer = TryFindLocalAccountCopy(t.PayerID);
            if (localPayer == null || localPayer.UpdatedUtc < payer.UpdatedUtc) {
                var req = new AsyncRequest<ValidateRequest<Account>>();
                req.Item = new ValidateRequest<Account>();
                req.Item.New = payer;
                req.Item.Old = localPayer;
                await Validate(req, null);
                if (req.Result == CMResult.S_OK) {
                    StoreAccount(payer);
                }
            }

            AppendPushNotifications(notifications, payee);
            AppendPushNotifications(notifications, payer);
            e.Completed(CMResult.S_OK);
        }

        private async Task Validate(AsyncRequest<ValidateRequest<Account>> e, List<string> notifications) {
            ThrowIfDisposed();

            var a = e.Item.New;
            var existing = e.Item.Old;

            if (a.APIVersion != 1) {
                e.Completed(CMResult.E_Unknown_API_Version);
                return;
            }

            // Account IDs must be valid.
            if (!Helpers.IsIDValid(a.ID)) {
                e.Completed(CMResult.E_Account_ID_Invalid);
                return;
            }

            // Regions must be valid.
            if (ISO31662.GetName(a.Iso31662Region) == null) {
                e.Completed(CMResult.E_Account_Invalid_Region);
                return;
            }

            if (a.CreatedUtc > DateTime.UtcNow.AddMinutes(Constants.MaxAllowedTimestampErrorInMinutes)) {
                e.Completed(CMResult.E_Account_Created_Utc_Out_Of_Range);
                return;
            }

            if (a.UpdatedUtc > DateTime.UtcNow.AddMinutes(Constants.MaxAllowedTimestampErrorInMinutes)) {
                e.Completed(CMResult.E_Account_Updated_Utc_Out_Of_Range);
                return;
            }

            if (ISO31662.GetName(a.ID) != null) {
                // require governing authority attribute if the account
                // name is an ISO3166-2 subdivision code.
                var check = new AsyncRequest<bool>();
                a.CheckIsValidGoverningAuthority(check, CryptoFunctions.Identity);
                if (!check.Item) {
                    e.Completed(CMResult.E_Account_Governing_Authority_Attribute_Required);
                    return;
                }

                // Iso31662Region must also equal the ID
                if (a.Iso31662Region != a.ID.ToUpper()) {
                    e.Completed(CMResult.E_Account_Invalid_Region);
                    return;
                }
            }

            if (a.AccountSignature == null) {
                e.Completed(CMResult.E_Account_Signature_Error);
                Server.DebugDump("Missing account signature", a.ToContentString());
                return;
            }

            AsyncRequest<DataVerifyRequest> verify = new AsyncRequest<DataVerifyRequest>();
            verify.Item = new DataVerifyRequest() {
                DataDateUtc = a.UpdatedUtc,
                Input = a.GetSigningData(),
                Signature = a.AccountSignature
            };
            a.VerifySignature(verify, CryptoFunctions.Identity);
            if (verify.Result != CMResult.S_OK) {
                e.Completed(CMResult.E_Account_Signature_Error);
                Server.DebugDump("Invalid account signature", a.ToContentString());
                return;
            }

            if (existing != null) {
                // The ID cannot be changed.
                if (a.ID != existing.ID) {
                    e.Completed(CMResult.E_Account_IDs_Are_Readonly);
                    return;
                }

                // CREATED-UTC is read-only
                if (a.CreatedUtc != existing.CreatedUtc) {
                    e.Completed(CMResult.E_Account_Created_Utc_Is_Readonly);
                    return;
                }

                if (a.UpdatedUtc < existing.UpdatedUtc) {
                    // potentially obsolete or out-dated record
                    e.Completed(CMResult.E_Account_Updated_Utc_Is_Old);
                    return;
                }

                var newKeys = a.GetAllPublicKeys();
                var oldKeys = existing.GetAllPublicKeys();

                // Validate public/private key addition
                if (newKeys.Count != oldKeys.Count) {
                    // Make sure number of keys are not FEWER than existing
                    if (newKeys.Count < oldKeys.Count) {
                        // This shouldn't happen (in theory UpdatedUtc will catch the scenario
                        // of stale records reaching us, but we want to be thorough.)
                        e.Completed(CMResult.E_Account_Too_Few_Public_Keys);
                        return;
                    }

                    if (newKeys.Count != oldKeys.Count + 1) {
                        // Our server is missing public keys. To resolve this conflict without
                        // creating an attack vector we must ask the network for proof, at the
                        // cost of performance.
                        Account networkCopy = await TryFindOnNetwork<Account>(a.ID);

                        if (networkCopy == null || !networkCopy.ConsensusOK) {
                            e.Completed(CMResult.E_Account_Cant_Corroborate);
                            return;
                        }
                        var networkKeys = networkCopy.GetAllPublicKeys();
                        if (newKeys.Count > networkKeys.Count + 1) {
                            // Not even the network knows about the missing RSA keys, so something fishy must be afoot.
                            e.Completed(CMResult.E_Account_Cant_Corroborate_Public_Keys);
                            return;
                        }

                        // Swap out our stale existing copy with the network's fully corroborated copy.
                        existing = networkCopy;
                        oldKeys = networkKeys;
                    }

                    // Servers must reject Account key additions if the newest entry does not equal UPDATED-UTC.
                    if (newKeys[newKeys.Count - 1].EffectiveDate != a.UpdatedUtc) {
                        e.Completed(CMResult.E_Account_Invalid_New_Public_Key_Date);
                        return;
                    }
                }

                // PUBLIC-KEYS MUST NOT be removed from or modified in the list, only new ones appended.
                for (int i = 0; i < newKeys.Count && i < oldKeys.Count; i++) {
                    if (!Helpers.IsHashEqual(newKeys[i].Key, oldKeys[i].Key)) {
                        e.Completed(CMResult.E_Account_Public_Key_Mismatch);
                        return;
                    }
                    if (newKeys[i].EffectiveDate != oldKeys[i].EffectiveDate) {
                        e.Completed(CMResult.E_Account_Public_Key_Mismatch);
                        return;
                    }
                }

                // Make sure all ModificationSignatures are correct.
                for (int i = 1; i < newKeys.Count; i++) {
                    verify.Item.DataDateUtc = newKeys[i].EffectiveDate.AddSeconds(-1);
                    verify.Item.Input = newKeys[i].GetModificationSigningData();
                    verify.Item.Signature = newKeys[i].ModificationSignature;
                    a.VerifySignature(verify, CryptoFunctions.Identity);
                    if (verify.Result != CMResult.S_OK) {
                        e.Completed(CMResult.E_Account_Public_Key_Mismatch);
                        return;
                    }
                }
            }

            AppendPushNotifications(notifications, a);

            e.Completed(CMResult.S_OK);
        }

        private async Task Validate(AsyncRequest<ValidateRequest<Vote>> e, List<string> notifications) {
            ThrowIfDisposed();

            var v = e.Item.New;
            var existing = e.Item.Old;

            if (v.APIVersion != 1) {
                e.Completed(CMResult.E_Unknown_API_Version);
                return;
            }

            // Account IDs must be valid.
            if (!Helpers.IsIDValid(v.VoterID)) {
                e.Completed(CMResult.E_Account_ID_Invalid);
                return;
            }

            // Votes cannot be post-dated
            if (v.CreatedUtc > DateTime.UtcNow.AddMinutes(Constants.MaxAllowedTimestampErrorInMinutes)) {
                e.Completed(CMResult.E_Vote_Created_Utc_Out_Of_Range);
                return;
            }

            if (v.UpdatedUtc > DateTime.UtcNow.AddMinutes(Constants.MaxAllowedTimestampErrorInMinutes)) {
                e.Completed(CMResult.E_Vote_Updated_Utc_Out_Of_Range);
                return;
            }

            Account a;
            if(IsOperatingInTestMode)
                a = TryFindLocalAccountCopy(v.VoterID);
            else
                a = await TryFindOnNetwork<Account>(Constants.PATH_ACCNT + "/" + v.VoterID);

            if (a == null) {
                e.Completed(CMResult.E_Vote_Account_Not_Found);
                return;
            }
  
            AsyncRequest<DataVerifyRequest> verify = new AsyncRequest<DataVerifyRequest>();
            verify.Item = new DataVerifyRequest() {
                DataDateUtc = v.UpdatedUtc,
                Input = v.GetSigningData(),
                Signature = v.Signature
            };
            a.VerifySignature(verify, CryptoFunctions.Identity);
            if (verify.Result != CMResult.S_OK) {
                e.Completed(CMResult.E_Vote_Signature_Error);
                Server.DebugDump("Invalid vote signature", v.ToContentString()+"\r\n========\r\n"+a.ToContentString());
                return;
            }

            // Vote cannot be dated prior to account creation date
            if (v.UpdatedUtc < a.CreatedUtc) {
                e.Completed(CMResult.E_Vote_Updated_Utc_Out_Of_Range);
                return;
            }

            if (existing != null) {
                // CREATED-UTC is read-only
                if (v.CreatedUtc != existing.CreatedUtc) {
                    e.Completed(CMResult.E_Vote_Created_Utc_Is_Readonly);
                    return;
                }

                if (v.UpdatedUtc < existing.UpdatedUtc) {
                    // potentially obsolete or out-dated record
                    e.Completed(CMResult.E_Vote_Updated_Utc_Is_Old);
                    return;
                }
                
            }

            AppendPushNotifications(notifications, a);

            e.Completed(CMResult.S_OK);
        }

        public class ValidateRequest<T> {
            public T New;
            public T Old;
        }

        /// <summary>
        /// A container is used to partition data into smaller chunks. The ultimate
        /// backing store is a disk-persisted linear hash table which is capable of
        /// inserting and fetching from millions of records very quickly.
        ///
        /// - Accounts get their own container with a list of object IDs relating to it.
        /// - Transactions are stored in containers which rotate daily.
        /// - Votes are filed under their Proposition ID
        /// </summary>
        internal class Container : IDisposable {
            public DateTime LastAccessedUtc;
            public string Name;

            public Container Parent;

            public string Path;

            /// <summary>
            /// In order to effectively search the contents of a container
            /// we need to keep a record of all keys in the dictionary. We'll
            /// reserve a __keys entry for that purpose. Keys are delimited with
            /// a \0 on either side.
            /// </summary>
            private const string KEYS_FIELD = "__keys";

            private System.Collections.Concurrent.ConcurrentDictionary<string, Container> _Children;
            private LinearHashTable<string, string> _Data;
            private System.Security.Cryptography.HashAlgorithm _Hasher;
            private object _Sync;
            private string[] _CachedKeys;
            private static readonly string[] Empty = new string[0];
            // Most fetches are just to get the same object over and over again. Cache this. 
            private string _LastGetKey;
            private string _LastGetValue;

            public Container(Container parent, string folder, string name) {
                Parent = parent;
                _Children = new System.Collections.Concurrent.ConcurrentDictionary<string, Container>();

                Path = folder;
                Name = name;
                _Hasher = System.Security.Cryptography.SHA1.Create();
                _Sync = new object();
            }

            public bool Exists {
                get {
                    return System.IO.File.Exists(System.IO.Path.Combine(Path, Name + ".htindex"));
                }
            }

            /// <summary>
            /// Returns true the file backing store is current open.
            /// </summary>
            public bool IsOpen {
                get {
                    return _Data != null;
                }
            }

            /// <summary>
            /// Returns true if the data store hasn't been used in a while.
            /// We should monitor containers periodically and close anything
            /// that's sitting around doing nothing to free up resources.
            /// </summary>
            public bool ShouldClose {
                get { return _Data != null && (DateTime.UtcNow - LastAccessedUtc).TotalSeconds > 10; }
            }

            public static string OnDeserialiseKey(BinaryReader br) {
                return br.ReadString();
            }

            public static int OnHashKey(string s) {
                if (s == null)
                    return 0;
                var length = s.Length;
                int hash = length;
                for (int i = 0; i != length; ++i)
                    hash = (hash ^ s[i]) * 16777619;
                return hash;
            }

            public static void OnSerialiseKey(string value, BinaryWriter bw) {
                bw.Write(value);
            }

            public static void OnSerialiseValue(string value, BinaryWriter bw) {
                if (String.IsNullOrWhiteSpace(value)) {
                    bw.Write((int)0);
                    return;
                }
                var b = System.Text.Encoding.UTF8.GetBytes(value);
                bw.Write(b.Length);
                bw.Write(0); // reserved flags
                bw.Write(b);
            }

            /// <summary>
            /// Closes the data store for this container.
            /// </summary>
            public void Close() {
                lock (_Sync) {
                    if (_Data == null)
                        return;
                    _Data.Dispose();
                    _Data = null;
                }
            }

            public void PermanentlyDelete() {
                lock (_Sync) {
                    Close();
                    _CachedKeys = null;
                    _LastGetKey = null;
                    _LastGetValue = null;

                    var file = System.IO.Path.Combine(Path, Name);
                    var extensions = new string[] {
                       ".htindex",
                       ".htindex-bak",
                       ".htdata"
                    };
                    string deletedExt = ".deleted" + DateTime.Now.ToString("yyyyMMddHHmmss");
                    try {
                        for (int i = 0; i < extensions.Length; i++) {
                            if (System.IO.File.Exists(file + extensions[i]))
                                System.IO.File.Move(file + extensions[i], file + extensions[i] + deletedExt);
                        }
                    } catch {
                        // Something using it somehow? Roll back for consistency..
                        for (int i = 0; i < extensions.Length; i++) {
                            if (System.IO.File.Exists(file + extensions[i] + deletedExt))
                                System.IO.File.Move(file + extensions[i] + deletedExt, file + extensions[i]);
                        }
                    }
                }
            }

            /// <summary>
            /// Gets an array of child containers
            /// </summary>
            /// <returns></returns>
            public Container[] CollectChildren() {
                return _Children.Values.ToArray();
            }

            public void Dispose() {
                Close();
                _Hasher?.Dispose();
            }

            public void Flush() {
                lock (_Sync) {
                    if (_Data == null)
                        return;
                    _Data.Flush();
                }
            }

            public string Get(string key) {
                lock (_Sync) {
                    if (_LastGetKey == key) {
                        return _LastGetValue;
                    }
                    if (!Exists)
                        return null;
                    EnsureHashTable();
                    string v;
                    _Data.TryGetValue(key, out v);
                    _LastGetKey = key;
                    _LastGetValue = v;
                    return v;
                }
            }
            
            public string[] GetKeys() {
                lock (_Sync) {
                    if (_CachedKeys != null)
                        return _CachedKeys;
                    if (!Exists)
                        return Empty;
                   
                    EnsureHashTable();
                    string keys;
                    _Data.TryGetValue(KEYS_FIELD, out keys);
                    string[] ar = keys == null ? Empty 
                        : keys.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                    _CachedKeys = ar;
                    return ar;
                }
            }

            public Container Open(string path) {
                lock (_Sync) {
                    var s = path;
                    string nextPath = null;
                    if (s.IndexOf("/") > -1) {
                        s = s.Substring(0, s.IndexOf("/"));
                        nextPath = path.Substring(path.IndexOf("/") + 1);
                    }
                    Container c;
                    if (!_Children.TryGetValue(s, out c)) {
                        if (nextPath != null)
                            c = new Container(this, System.IO.Path.Combine(Path, s), null);
                        else
                            c = new Container(this, Path, s);
                        _Children[s] = c;
                    }
                    if (nextPath == null)
                        return c;
                    else
                        return c.Open(nextPath);
                }
            }

            public void Set(string key, string value) {
                lock (_Sync) {
                    EnsureHashTable();
                    _Data.Set(key, value);
                    string keys;
                    _Data.TryGetValue(KEYS_FIELD, out keys);
                    var delimitedKey = "\0" + key + "\0";
                    if (keys == null || keys.IndexOf(delimitedKey) == -1) {
                        keys += delimitedKey;
                        _Data.Set(KEYS_FIELD, keys);
                        _CachedKeys = keys.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
                    }
                    if (_LastGetKey == key) {
                        _LastGetValue = value;
                    }
                }
            }

            public override string ToString() {
                return Path + "\\" + Name;
            }

            private static string OnDeserialiseValue(BinaryReader br) {
                var len = br.ReadInt32();
                if (len == 0)
                    return null;
                var flags = br.ReadInt32();
                return System.Text.Encoding.UTF8.GetString(br.ReadBytes(len));
            }

            private void AttemptReset() {
                var file = System.IO.Path.Combine(Path, Name);
                var ext = new[] { ".htindex", ".htindex-bak", ".htdata" };
                for (int i = 0; i < ext.Length; i++) {
                    if (File.Exists(file + ext[i]))
                        File.Delete(file + ext[i]);
                }
                _Data = new LinearHashTable<string, string>(
                          System.IO.Path.Combine(Path, Name),
                          OnHashKey,
                          // Compression is only a space gain for large values, not shortish keys.
                          OnSerialiseKey, OnDeserialiseKey,
                          OnSerialiseValue, OnDeserialiseValue);
            }

            private void EnsureHashTable() {
                if (_Data == null) {
                    if (!Directory.Exists(Path))
                        Directory.CreateDirectory(Path);
                    try {
                        _Data = new LinearHashTable<string, string>(
                           System.IO.Path.Combine(Path, Name),
                           OnHashKey,
                           // Compression is only a space gain for large values, not shortish keys.
                           OnSerialiseKey, OnDeserialiseKey,
                           OnSerialiseValue, OnDeserialiseValue);
                    } catch (LinearHashTableCorruptionException) {
                        // Basically all we can do is delete and start over.
                        // Not a big deal as the data will sync back eventually.
                        AttemptReset();
                    }
                }
                LastAccessedUtc = DateTime.UtcNow;
            }
        }

        private class PendingItem {
            public DateTime CreatedUtc = DateTime.UtcNow;

            public IStorable Item;

            public PendingItem(IStorable o, List<string> notifications) {
                Item = o;
                PushNotify = notifications;
            }

            public bool HasCommitted { get; set; }
            public bool IsExpired { get { return (DateTime.UtcNow - CreatedUtc).TotalMinutes > 5; } }
            public bool PushNotificationsSent { get; set; }
            public List<string> PushNotify { get; private set; }
        }

        #region IDisposable Support

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_IsDisposed) {
                _IsDisposed = true;
                CloseContainers(false);
                _Root?.Dispose();
            }
        }

        #endregion IDisposable Support
    }
}