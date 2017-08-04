#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using CM.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CM.Javascript {

    public class FindAccountRequest {
        public string ID;
        public Account Output;
        internal CM.Javascript.ServerProgressIndicator UI;

        public FindAccountRequest(string id) {
            ID = id;
        }
    }

    public class FindTransactionRequest {
        public string ID;
        public Transaction Output;
        //internal CM.Javascript.ServerProgressIndicator UI;

        public FindTransactionRequest(string id) {
            ID = id;
        }
    }

    public class FindVoteRequest {
        public Vote Output;
        public uint PropositionID;
        public string VoterID;

        public FindVoteRequest(string accountid, uint propositionID) {
            VoterID = accountid;
            PropositionID = propositionID;
        }
    }

    /// <summary>
    /// A reference client implementation.
    /// </summary>
    internal class Client {

        /// <summary>
        /// https://xyz.civil.money
        /// </summary>
        public string CurrentAuthoritativeServer;

        public List<Peer> Peers = new List<Peer>();
        private Dictionary<string, CachedResponsiblePeers> _CachedResponsiblePeers = new Dictionary<string, CachedResponsiblePeers>();
        private Dictionary<string, PeerNotifyArgs> _PeerNotifications;
        private List<QueueItem> _RequestQueue;
        private int TimeoutSeconds = 5;

        public Client() {
            _RequestQueue = new List<QueueItem>();
            _PeerNotifications = new Dictionary<string, PeerNotifyArgs>();
            Task.Run(ProcessQueue);
        }

        public event Action<PeerNotifyArgs> PeerNotifiesReceived;
        public event Action<Peer> PeerRemoved;
        public event Action<Peer> PeerStateChanged;

        public void AddPotentialPeers(string seenList) {
            if (seenList == null)
                return;
            var ar = seenList.Split(',');
            for (int i = 0; i < ar.Length; i++)
                FindOrCreatePeer(ar[i]);
        }

        public void JoinNetwork() {
            for (int i = 0; i < Constants.Seeds.Length && i < 3; i++) {
                var p = FindOrCreatePeer(Constants.Seeds[i].EndPoint);
                if (p != null && p.State == PeerState.Unknown)
                    p.BeginConnect(null);
            }
        }

        /// <summary>
        /// Used by the client to query authoritative seeds.
        /// </summary>
        public void QueryAuthoritiveServer(AsyncRequest<HttpRequest> onResult) {
            if (String.IsNullOrWhiteSpace(onResult.Item.Url)
                   || !onResult.Item.Url.StartsWith("/api/"))
                throw new Exception("Invalid API call attempt");

            var ar = new List<string>();
            var host = Constants.Seeds[0].Domain;
#if DEBUG
            host = DNS.EndpointToUntrustedDomain(host, true);
#endif
            QueryAuthoritiveServerImpl(host, ar, onResult);
        }

        /// <summary>
        /// Subscribes for instant notifications for the specified account id.
        /// </summary>
        /// <param name="accountid"></param>
        public void Subscribe(string accountid) {
            var task = new QueueItem();
            task.Start = DateTime.UtcNow;
            task.Request = new AsyncRequest<string>() { Item = accountid };
            task.OnGotMultipleWorkingPeers = (servers, ireq) => {
                var req = ireq as AsyncRequest<string>;
                if (req.IsCancelled) {
                    req.Completed(CMResult.E_Operation_Cancelled);
                    return;
                }
                FindResponsiblePeersForPath(servers, Constants.PATH_ACCNT + "/" + accountid, req, (peers) => {
                    if (peers.Count == 0 || req.IsCancelled) {
                        req.Completed(req.IsCancelled ? CMResult.E_Operation_Cancelled
                                : CMResult.E_Not_Enough_Peers);
                        return;
                    }
                    int completedCount = 0;

                    for (int i = 0; i < peers.Count; i++) {
                        if (!peers[i].DesiredSubscribedIDs.Contains(accountid))
                            peers[i].DesiredSubscribedIDs.Add(accountid);
                        if (!peers[i].SentSubscribedIDs.Contains(accountid)) {
                            peers[i].SendAndReceive("SUBSCRIBE", null, new string[] { accountid }, (p, res) => {
                                completedCount++;
                                if (res.Response.IsSuccessful)
                                    p.SentSubscribedIDs.Add(accountid);
                                if (completedCount == peers.Count) {
                                    req.Completed(CMResult.S_OK);
                                }
                            });
                        }
                    }
                }, null);
            };
            _RequestQueue.Add(task);
        }

        public void TryFindAccount(AsyncRequest<FindAccountRequest> e) {
            var task = new QueueItem();
            task.Start = DateTime.UtcNow;
            task.Request = e;
            task.OnGotMultipleWorkingPeers = (servers, ireq) => {
                var req = ireq as AsyncRequest<FindAccountRequest>;
                if (req.IsCancelled) {
                    req.Completed(CMResult.E_Operation_Cancelled);
                    return;
                }
                TryFindOnNetwork<Account>(servers,
                    Constants.PATH_ACCNT + "/" + req.Item.ID + "?calculations-date=" + Helpers.DateToISO8601(DateTime.UtcNow),
                    e,
                    (a) => {
                        if (e.IsCancelled) {
                            req.Completed(CMResult.E_Operation_Cancelled);
                        } else {
                            req.Item.Output = a;
                            req.Completed(a.Response.Code);
                        }
                    },
                    ui: e.Item.UI);
            };
            _RequestQueue.Add(task);
        }

        public void TryFindTransaction(AsyncRequest<FindTransactionRequest> e) {
            var task = new QueueItem();
            task.Start = DateTime.UtcNow;
            task.Request = e;
            task.OnGotMultipleWorkingPeers = (servers, ireq) => {
                var req = ireq as AsyncRequest<FindTransactionRequest>;
                TryFindOnNetwork<Transaction>(servers, Constants.PATH_TRANS + "/" + req.Item.ID, e,
                    (t) => {
                        req.Item.Output = t;
                        req.Completed(t.Response.Code);
                    });
            };
            _RequestQueue.Add(task);
        }

        public void TryFindVote(AsyncRequest<FindVoteRequest> e) {
            var task = new QueueItem();
            task.Start = DateTime.UtcNow;
            task.Request = e;
            task.OnGotMultipleWorkingPeers = (servers, ireq) => {
                var req = ireq as AsyncRequest<FindVoteRequest>;
                if (req.IsCancelled) {
                    req.Completed(CMResult.E_Operation_Cancelled);
                    return;
                }
                TryFindOnNetwork<Vote>(servers, Constants.PATH_VOTES + "/" + e.Item.PropositionID + "/" + req.Item.VoterID, e,
                    (v) => {
                        if (e.IsCancelled) {
                            req.Completed(CMResult.E_Operation_Cancelled);
                        } else {
                            req.Item.Output = v;
                            req.Completed(v.Response.Code);
                        }
                    });
            };
            _RequestQueue.Add(task);
        }

        public void TryList(string path, AsyncRequest<Schema.ListRequest> e, Action<Peer, ListResponse> resultSink) {
            var task = new QueueItem();
            task.Start = DateTime.UtcNow;
            task.Request = e;
            task.OnGotMultipleWorkingPeers = (servers, req) => {
                FindResponsiblePeersForPath(servers, path, e,
                    (peers) => {
                        if (e.IsCancelled) {
                            req.Completed(CMResult.E_Operation_Cancelled);
                        } else if (peers.Count < Constants.MinimumNumberOfCopies) {
                            req.Completed(CMResult.E_Not_Enough_Peers);
                        } else {
                            int completedCount = 0;
                            for (int i = 0; i < peers.Count; i++) {
                                peers[i].SendAndReceive("LIST", e.Item, new string[] { path },
                                    (p, m) => {
                                        e.UpdateProgress((int)((completedCount / (double)peers.Count) * 100));
                                        resultSink(p, m.Cast<ListResponse>());
                                        completedCount++;
                                        if (completedCount == peers.Count
                                            || m.Response.Code != CMResult.S_OK) {
                                            e.Completed(m.Response.Code);
                                        }
                                    });
                            }
                        }
                    }, null);
            };
            _RequestQueue.Add(task);
        }

        public void TryPut(AsyncRequest<PutRequest> e) {
            var path = e.Item.Item.Path;
            var task = new QueueItem();
            task.Start = DateTime.UtcNow;
            task.Request = e;
            task.OnGotMultipleWorkingPeers = (servers, req) => {
                FindResponsiblePeersForPath(servers, path, e,
                    (peers) => {
                        if (e.IsCancelled) {
                            req.Completed(CMResult.E_Operation_Cancelled);
                        } else if (peers.Count < Constants.MinimumNumberOfCopies) {
                            req.Completed(CMResult.E_Not_Enough_Peers);
                        } else {
                            int completedCount = 0;
                            for (int i = 0; i < peers.Count; i++) {
                                var status = new CommitStatus(e, peers[i], (Message)e.Item.Item, path);
                                e.Item.Statuses.Add(status);
                                status.Put((s) => {
                                    completedCount++;
                                    if (completedCount == peers.Count) {
                                        BeginQueryCommitPhase(req as AsyncRequest<PutRequest>);
                                    }
                                });
                            }
                        }
                    }, e.Item.UI);
            };
            _RequestQueue.Add(task);
        }

        /// <summary>
        /// Removes the specified ID for subscription notifications. Remote peers
        /// will still send the notifications, but the message will be ignored
        /// unless the peer is resubscribed.
        /// </summary>
        /// <param name="id"></param>
        public void Unsubscribe(string id) {
            for (int i = 0; i < Peers.Count; i++) {
                Peers[i].DesiredSubscribedIDs.Remove(id);
            }
        }

        /// <summary>
        /// Calls COMMIT on responsible DHT peers who have
        /// indicated that they're OK to proceed.
        /// </summary>
        private void BeginFinalCommitPhase(AsyncRequest<PutRequest> req) {
            var e = req.Item;
            var toCommit = new List<CommitStatus>();
            for (int i = 0; i < e.Statuses.Count; i++) {
                var stat = e.Statuses[i];
                if (stat.Token == null || !stat.IsConfirmed)
                    continue;
                toCommit.Add(stat);
            }

            if (toCommit.Count < Constants.MinimumNumberOfCopies) {
                // Not enough peers are giving the all-clear to proceed.
                var result = GetResultConsensus(e.Statuses, CMResult.E_Not_Enough_Peers);
                req.Completed(result);
                return;
            }

            int completed = 0;
            for (int i = 0; i < toCommit.Count; i++) {
                toCommit[i].Commit((p) => {
                    completed++;
                    if (completed == toCommit.Count) {
                        // We're done
                        ReturnFinalCommitResponse(req);
                    }
                });
            }
        }

        /// <summary>
        /// Calls QUERY-COMMIT on all responsible DHT peers to see whether
        /// they're OK to proceed.
        /// </summary>
        private void BeginQueryCommitPhase(AsyncRequest<PutRequest> req) {
            var e = req.Item;

            var tocheck = new List<CommitStatus>();
            for (int i = 0; i < e.Statuses.Count; i++) {
                var stat = e.Statuses[i];
                if (stat.Token == null)
                    continue;
                tocheck.Add(stat);
            }

            if (tocheck.Count < Constants.MinimumNumberOfCopies) {
                // Not enough peers returned a commit token. See if there's a
                // consensus regarding a data validity error amongst peers.
                var result = GetResultConsensus(e.Statuses, CMResult.E_Not_Enough_Peers);
                req.Completed(result);
                return;
            }

            int checkedCount = 0;
            for (int i = 0; i < tocheck.Count; i++) {
                tocheck[i].QueryCommit((p) => {
                    checkedCount++;
                    if (checkedCount == tocheck.Count) {
                        BeginFinalCommitPhase(req);
                    }
                });
            }
        }

        private void Find(Peer server, string copyID, Action<string> onResult) {
            server.SendAndReceive("FIND", new FindResponsiblePeerRequest() {
                DHTID = Helpers.DHT_ID(copyID),
                MaxHopCount = Constants.MaxHopCount,
                HopList = ""
            }, null, (p, e) => {
                var res = e.Cast<FindResponsiblePeerResponse>();
                var hops = res.HopList ?? String.Empty;
                Console.WriteLine(copyID + " = " + res.PeerEndpoint + " (" + hops.Split(',').Length + " hops)");
                if (res.Response.IsSuccessful
                    && res.PeerEndpoint != null) {
                    onResult(res.PeerEndpoint);
                } else {
                    onResult(null);
                }
            });
        }

        private Peer FindOrCreatePeer(string endpoint) {
            if (endpoint == null || endpoint.Length == 0)
                return null;
            Peer p = null;
            for (int x = 0; x < Peers.Count; x++) {
                p = Peers[x];
                if (p.EndPoint == endpoint
                    || p.SupposedIPEndPoint == endpoint)
                    return p;
            }
            p = new Peer(this,
                    endpoint,
                    OnPeerStateChange,
                    OnPeerError,
                    OnPeerRequest);
            Peers.Add(p);
            OnPeerStateChange(p);
            return p;
        }

        /// <summary>
        /// Copies of objects are stored in the P2P network in multiple locations. The naming
        /// convention for locating any object across the network is:
        ///
        /// DHT_HASHID("copy" + number + "id")
        ///
        /// </summary>
        /// <param name="id">The ID to fetch.</param>
        /// <param name="numberOfPlacesToLook">The number of copies expected to be on the network.</param>
        /// <param name="onResult">Receives a list of peers that 'could' be holding a copy of the object.</param>
        private void FindResponsiblePeersForAccount(List<Peer> servers, string id, IAsyncRequest req, int numberOfPlacesToLook, Action<List<Peer>> onResult, ServerProgressIndicator ui) {
            CachedResponsiblePeers cached;
            if (_CachedResponsiblePeers.TryGetValue(id, out cached)
                && cached.Age.TotalSeconds < 120) {
                if (ui != null)
                    ui.AppendPeers(cached.Peers);
                onResult(cached.Peers);
                return;
            }

            var ar = new List<string>();
            if (req.IsCancelled) {
                onResult(new List<Peer>());
                return;
            }
            var peers = new List<string>();
            int done = 0;
            Action<string> OnDone = (ep) => {
                if (ep != null && !peers.Contains(ep)) {
                    peers.Add(ep);
                }
                done++;
                if (done == numberOfPlacesToLook) {
                    var res = new List<Peer>();
                    for (int i = 0; i < peers.Count; i++) {
                        var p = FindOrCreatePeer(peers[i]);
                        if (p != null && !res.Contains(p)) {
                            res.Add(p);
                            if (ui != null)
                                ui.AppendPeer(p);
                        }
                    }

                    // Cache positive results for a short while.
                    if (res.Count >= Constants.MinimumNumberOfCopies) {
                        _CachedResponsiblePeers[id] = new CachedResponsiblePeers() {
                            Peers = res
                        };
                    };

                    onResult(res);
                }
            };
            Console.WriteLine("Finding using " + servers.Count + " servers");
            // JavaScript is single-threaded but we can get semi async
            // through the web sockets by dispatching queries to multiple
            // servers. This also doubles as a consensus security feature.
            for (int i = 0; i < numberOfPlacesToLook; i++)
                Find(servers[i % servers.Count], "copy" + (i + 1) + id, OnDone);
        }

        private void FindResponsiblePeersForPath(List<Peer> servers, string path, IAsyncRequest req, Action<List<Peer>> onResult, ServerProgressIndicator ui) {
            int queryIdx = path.IndexOf('?');
            if (queryIdx > -1)
                path = path.Substring(0, queryIdx);
            if (path.StartsWith(Constants.PATH_ACCNT)) {
                string id = path.Substring(Constants.PATH_ACCNT.Length + 1);
                if (id.IndexOf('/') > -1)
                    id = id.Substring(0, id.IndexOf('/'));
                if (Helpers.IsIDValid(id)) {
                    FindResponsiblePeersForAccount(servers, id, req, Constants.NumberOfCopies, onResult, ui);
                } else
                    throw new ArgumentException("Invalid Account ID in path.");
            } else if (path.StartsWith(Constants.PATH_TRANS)) {
                string id = path.Substring(Constants.PATH_TRANS.Length + 1);
                DateTime created;
                string payee, payer;
                if (Helpers.TryParseTransactionID(id, out created, out payee, out payer)) {
                    FindResponsiblePeersForAccount(servers, payee, req, Constants.NumberOfCopies, (a) => {
                        FindResponsiblePeersForAccount(servers, payer, req, Constants.NumberOfCopies, (b) => {
                            // Merge the payee + payer destination lists
                            for (int i = 0; i < a.Count; i++) {
                                if (!b.Contains(a[i]))
                                    b.Add(a[i]);
                            }
                            onResult(b);
                        }, ui);
                    }, ui);
                } else
                    throw new ArgumentException("Invalid Transaction ID in path.");
            } else if (path.StartsWith(Constants.PATH_VOTES)) {
                var parts = path.Split('/');
                // VOTES/{PropositionID}/{ID}
                if (Helpers.IsIDValid(parts[2]))
                    FindResponsiblePeersForAccount(servers, parts[2], req, Constants.NumberOfCopies, onResult, ui);
                else
                    throw new ArgumentException("Invalid Vote path.");
            } else {
                throw new ArgumentException("Invalid path for DHT searching");
            }
        }

        private CMResult GetResultConsensus(List<CommitStatus> ar, CMResult @default) {
            CMResult consensus = @default;
            int consensusCount = 0;
            for (int i = 0; i < ar.Count; i++) {
                var stat = ar[i];
                if (stat.CommitResponse.HasValue) {
                    int c = ar.Count(x => x.CommitResponse != null && x.CommitResponse.Value == stat.CommitResponse.Value);
                    if (consensusCount < c) {
                        consensusCount = c;
                        consensus = stat.CommitResponse.Value;
                    }
                } else if (stat.QueryCommitResponse.HasValue) {
                    int c = ar.Count(x => x.QueryCommitResponse != null && x.QueryCommitResponse.Value == stat.QueryCommitResponse.Value);
                    if (consensusCount < c) {
                        consensusCount = c;
                        consensus = stat.QueryCommitResponse.Value;
                    }
                } else if (stat.PutResponse.HasValue) {
                    int c = ar.Count(x => x.PutResponse != null && x.PutResponse.Value == stat.PutResponse.Value);
                    if (consensusCount < c) {
                        consensusCount = c;
                        consensus = stat.PutResponse.Value;
                    }
                }
            }
            return consensus;
        }

        private void OnPeerError(Peer p, string error) {
            Console.WriteLine("[" + p.EndPoint + "] ! " + error);
        }

        private void OnPeerNotifyReceived(Peer p, Message m) {
            var path = m.Request.FirstArgument;
            PeerNotifyArgs args;
            IStorable item = null;
            if (path.StartsWith(Constants.PATH_ACCNT, StringComparison.OrdinalIgnoreCase)) {
                Account a = m.Cast<Account>();
                if (!p.DesiredSubscribedIDs.Contains(a.ID.ToLower()))
                    return; // no longer subscribed.
                item = a;
            } else if (path.StartsWith(Constants.PATH_TRANS, StringComparison.OrdinalIgnoreCase)) {
                Transaction t = m.Cast<Transaction>();
                if (!p.DesiredSubscribedIDs.Contains(t.PayeeID.ToLower()) && !p.DesiredSubscribedIDs.Contains(t.PayerID.ToLower()))
                    return; // no longer subscribed.
                item = t;
            }
            if (item == null)
                return;
            if (!_PeerNotifications.TryGetValue(path, out args)) {
                args = new PeerNotifyArgs(item);
            }
            args.Peers.Add(p);
            if (PeerNotifiesReceived != null)
                PeerNotifiesReceived(args);
        }

        private async void OnPeerRequest(Peer p, Message m) {
            if (m.Request.Action == "NOTIFY") {
                await p.Reply(m, CMResult.S_OK);

                OnPeerNotifyReceived(p, m);
            } else {
                await p.Reply(m, CMResult.E_General_Failure);
            }
        }

        private void OnPeerStateChange(Peer p) {
            // Kill duplicates based on SupposedIPEndPoint for host name resolution
            for (int x = 0; x < Peers.Count; x++) {
                var tmp = Peers[x];
                if (tmp != p
                    && tmp.EndPoint == p.SupposedIPEndPoint) {
                    Peers.RemoveAt(x--);
                    if (PeerRemoved != null)
                        PeerRemoved(tmp);
                }
            }

            Peers.Sort((a, b) => {
                // sort by "working" first
                return (a.State == PeerState.Connected).CompareTo(b.State == PeerState.Connected) * -1;
            });
            if (PeerStateChanged != null)
                PeerStateChanged(p);
        }

        private async Task ProcessQueue() {
            PeerState tryState = PeerState.Unknown;
            while (true) {
                Peer oldestIdleConnection = null;
                var working = new List<Peer>();
                for (int i = 0; i < Peers.Count; i++) {
                    var p = Peers[i];
                    if (p.State == PeerState.Connected) {
                        working.Add(p);
                        var idle = p.IdleTime;
                        if (idle.TotalSeconds > 60
                            && p.DesiredSubscribedIDs.Count == 0
                            && (oldestIdleConnection == null || oldestIdleConnection.IdleTime < idle)) {
                            oldestIdleConnection = p;
                        }
                    }
                }
                if (working.Count >= 2) {
                    // After successful connection to at least a couple of peers
                    // working peers, reset the re-connection test state
                    tryState = PeerState.Unknown;
                    if (_RequestQueue.Count > 0) {
                        _RequestQueue[0].OnGotMultipleWorkingPeers(working, _RequestQueue[0].Request);
                        _RequestQueue.RemoveAt(0);
                    }
                } else if (_RequestQueue.Count > 0 && Peers.Count == 0) {
                    JoinNetwork();
                } else {
                    // Try and obtain a working peer
                    bool isTryingSomething = false;
                    for (int i = 0; i < Peers.Count && !isTryingSomething; i++) {
                        if (Peers[i].State == tryState) {
                            Peers[i].BeginConnect(null);
                            isTryingSomething = true;
                            //break; // Bridge.NET bug won't compile this
                        }
                    }
                    if (!isTryingSomething) {
                        if (tryState == PeerState.Unknown)
                            // No peers in the current try state,
                            // re-try any disconnected peers
                            tryState = PeerState.Disconnected;
                        else if (tryState == PeerState.Disconnected)
                            // No disconnected peers either? try broken ones
                            tryState = PeerState.Broken;
                    }
                }

                // Time-out any requests that are not working
                for (int i = 0; i < _RequestQueue.Count; i++) {
                    var r = _RequestQueue[i];
                    if ((DateTime.UtcNow - r.Start).TotalSeconds > TimeoutSeconds) {
                        r.Request.Completed(CMResult.E_Connect_Attempt_Timeout);
                        _RequestQueue.RemoveAt(i--);
                    }
                }

                if (working.Count > 2) {
                    // Close idle an connection (oldest first)

                    int openConnections = 0;
                    for (int i = 0; i < Peers.Count; i++) {
                        var peer = Peers[i];
                        if (peer.State == PeerState.Connected) {
                            openConnections++;
                        }
                    }
                    // we want to maintain a reasonable number
                    if (openConnections > 5
                        && oldestIdleConnection != null) {
                        Console.WriteLine("Closing idle connection " + oldestIdleConnection.EndPoint);
                        oldestIdleConnection.Disconnect();
                    }
                }

                await Task.Delay(
                    // Go fast only if the queue is not empty
                    // and there are "untested" peers
                    _RequestQueue.Count == 0
                    || tryState != PeerState.Unknown ? 1000
                    : 250);
            }
        }

        private void QueryAuthoritiveServerImpl(string server, List<string> tried, AsyncRequest<HttpRequest> onResult) {
            tried.Add(server);
            onResult.Item.Attempts++;

            var r = new Bridge.Html5.XMLHttpRequest();
            r.OnTimeout = (ev) => {
                QueryAuthoritiveServerTryNextOrGiveUp(tried, r, onResult);
            };
            r.OnError = (ev) => {
                QueryAuthoritiveServerTryNextOrGiveUp(tried, r, onResult);
            };
            r.OnReadyStateChange = () => {
                switch (r.ReadyState) {
                    case Bridge.Html5.AjaxReadyState.Done:
                        onResult.Item.Status = r.StatusText;
                        onResult.Item.StatusCode = r.Status;
                        onResult.Item.Content = r.ResponseText;
                        onResult.Completed(r.Status == 200 ? CMResult.S_OK : CMResult.S_False);
                        break;

                    case Bridge.Html5.AjaxReadyState.HeadersReceived:
                        onResult.Item.Status = r.StatusText;
                        break;

                    case Bridge.Html5.AjaxReadyState.Loading:
                        break;

                    case Bridge.Html5.AjaxReadyState.Opened:
                        break;

                    case Bridge.Html5.AjaxReadyState.Unsent:
                        break;
                }
            };
            try {
                var url = onResult.Item.Url;
                // Use https://*.civil.money if available, otherwise, untrusted-server.com.
                var domain = server.IndexOf(DNS.AUTHORITATIVE_DOMAIN) == -1 ? DNS.EndpointToUntrustedDomain(server, true) : server;
                CurrentAuthoritativeServer = "https://" + domain;
                if (onResult.Item.Post == null) {
                    r.Open("GET", "https://" + domain + url, true);
                    r.Send();
                } else {
                    r.Open("POST", "https://" + domain + url, true);
                    r.Send(onResult.Item.Post);
                }
            } catch {
                QueryAuthoritiveServerTryNextOrGiveUp(tried, r, onResult);
            }
        }

        private void QueryAuthoritiveServerTryNextOrGiveUp(List<string> tried, Bridge.Html5.XMLHttpRequest r, AsyncRequest<HttpRequest> onResult) {
            string next = null;
            for (int i = 0; i < Constants.Seeds.Length; i++) {
                if (!tried.Contains(Constants.Seeds[i].Domain)) {
                    next = Constants.Seeds[i].Domain;
                    break;
                }
            }
            if (next != null) {
                QueryAuthoritiveServerImpl(next, tried, onResult);
            } else {
                onResult.Item.Status = r.StatusText;
                onResult.Item.StatusCode = r.Status;
                onResult.Completed(CMResult.S_False);
            }
        }

        /// <summary>
        /// Interprets a final completion status based on
        /// which DHT peers worked and which didn't.
        /// </summary>
        private void ReturnFinalCommitResponse(AsyncRequest<PutRequest> req) {
            var e = req.Item;
            var ar = new List<CommitStatus>();
            for (int i = 0; i < e.Statuses.Count; i++) {
                var stat = e.Statuses[i];
                if (stat.CommitResponse != CMResult.S_OK)
                    continue;
                ar.Add(stat);
            }
            CMResult result = CMResult.E_Account_Cant_Corroborate;
            if (ar.Count < Constants.MinimumNumberOfCopies) {
                // What happened
                result = GetResultConsensus(e.Statuses, CMResult.E_General_Failure);
                if (ar.Count != 0) {
                    // We're potentially in an inconsistent state here, which is bad...
                    // Enough peers indicated that they were OK to proceed just moments ago,
                    // but for whatever reason, one or more have been unable to commit or
                    // have become unreachable.
                } else {
                    // All peers failed. Most likely the client disconnected right
                    // before or during our call to commit.
                }
            } else {
                result = CMResult.S_OK;
            }
            req.Completed(result);
        }

        private void TryFindOnNetwork<T>(List<Peer> servers, string path, IAsyncRequest req, Action<T> onResult, ServerProgressIndicator ui = null)
            where T : Message, IStorable {
            FindResponsiblePeersForPath(servers, path, req, (peers) => {
                if (peers.Count == 0 || req.IsCancelled) {
                    // No peers
                    onResult(new Message() {
                        Response = new Message.ResponseHeader(
                            req.IsCancelled ? CMResult.E_Operation_Cancelled
                            : CMResult.E_Not_Enough_Peers, null)
                    }.As<T>());
                    return;
                }
                int completedCount = 0;
                List<T> copies = new List<T>();
                for (int i = 0; i < peers.Count; i++) {
                    if (ui != null)
                        ui.Update(peers[i], Assets.SVG.Wait, SR.LABEL_STATUS_CONNECTING);
                    peers[i].SendAndReceive("GET", null, new string[] { path }, (p, res) => {
                        completedCount++;
                        //if (res.Response.IsSuccessful) {
                        // Let empty error messages propagate to the caller, so they
                        // can get a useful response status code.
                        copies.Add(res.Cast<T>());
                        if (ui != null) {
                            ui.Update(p,
                                res.Response.IsSuccessful ? Assets.SVG.CircleTick : Assets.SVG.CircleUnknown,
                                res.Response.IsSuccessful ? SR.LABEL_STATUS_OK : res.Response.Code.GetLocalisedDescription());
                        }
                        //}
                        if (completedCount == peers.Count) {
                            T account;
                            Helpers.CheckConsensus(copies, out account);
                            onResult(account);
                        }
                    });
                }
            }, ui);
        }

        public class HttpRequest {
            public int Attempts;

            public string Content;
            public string Post;
            public string Status;

            public int StatusCode;

            public string Url;

            public HttpRequest(string apiPath) {
                Url = apiPath;
            }
        }

        private class CachedResponsiblePeers {
            public List<Peer> Peers;

            private DateTime _CreatedUtc = DateTime.UtcNow;

            public TimeSpan Age {
                get {
                    return DateTime.UtcNow - _CreatedUtc;
                }
            }
        }

        private class QueueItem {

            // public Action<Peer, IAsyncRequest> OnGotWorkingPeer;
            public Action<List<Peer>, IAsyncRequest> OnGotMultipleWorkingPeers;

            public IAsyncRequest Request;
            public DateTime Start;
        }
    }

    internal class CommitStatus {
        public CMResult? CommitResponse;
        public int CommitTries;
        public bool IsCommitted;
        public bool IsConfirmed;
        public bool IsInError;
        public Message Item;
        public Peer Peer;
        public CMResult? PutResponse;
        public CMResult? QueryCommitResponse;
        public string Status;

        public string StatusDetails;

        public string Token;

        private AsyncRequest<PutRequest> Owner;

        private string Path;

        public CommitStatus(AsyncRequest<PutRequest> owner, Peer p, Message item, string path) {
            Peer = p;
            Path = path;
            Item = item;
            Owner = owner;
        }

        public void Commit(Action<CommitStatus> onComplete) {
            Status = SR.LABEL_STATUS_COMITTING_DATA + " ...";
            if (Owner.OnProgress != null)
                Owner.OnProgress(Owner);
            Peer.SendAndReceive("COMMIT", null, new string[] { Token }, (p, res) => {
                CommitResponse = res.Response.Code;
                if (CommitResponse == CMResult.S_OK) {
                    Status = SR.LABEL_STATUS_OK + " :)";
                    StatusDetails = null;
                    IsCommitted = true;
                    IsInError = false;
                } else {
                    Status = SR.LABEL_STATUS_ERROR_CLICK_FOR_DETAILS;
                    StatusDetails = res.Response.Code.ToString();
                    IsInError = true;
                }
                if (Owner.OnProgress != null)
                    Owner.OnProgress(Owner);
                onComplete(this);
            });
        }

        public void Put(Action<CommitStatus> onComplete) {
            Status = SR.LABEL_STATUS_CONNECTING + " ...";
            IsInError = false;
            IsCommitted = false;
            IsConfirmed = false;
            if (Owner.OnProgress != null)
                Owner.OnProgress(Owner);
            Peer.SendAndReceive("PUT",
                Item,
                new string[] { Path },
                (p, res) => {
                    PutResponse = res.Response.Code;
                    if (res.Response.IsSuccessful) {
                        Token = res.Response.Arguments[0];
                        //Status = "Looking good";
                        StatusDetails = null;
                    } else {
                        Status = SR.LABEL_STATUS_ERROR_CLICK_FOR_DETAILS;
                        StatusDetails = res.Response.Code.ToString();
                        IsInError = true;
                    }
                    if (Owner.OnProgress != null)
                        Owner.OnProgress(Owner);
                    onComplete(this);
                }
            );
        }

        public void QueryCommit(Action<CommitStatus> onComplete) {
            if (CommitTries < 5) {
                CommitTries++;

                Status = SR.LABEL_STATUS_CORROBORATING + " ." + new string('.', CommitTries);
                if (Owner.OnProgress != null)
                    Owner.OnProgress(Owner);
                Peer.SendAndReceive("QUERY-COMMIT", null, new string[] { Path }, (p, res) => {
                    QueryCommitResponse = res.Response.Code;
                    if (QueryCommitResponse == CMResult.S_OK) {
                        var updatedUtc = Helpers.DateFromISO8601(res.Response.Arguments[0]);
                        if (updatedUtc == ((IStorable)Item).UpdatedUtc) {
                            IsConfirmed = true;
                            IsInError = false;
                            StatusDetails = null;
                        } else {
                            // Some other commit is beating us to it
                            QueryCommitResponse = CMResult.E_Object_Superseded;
                        }
                        onComplete(this);
                    } else {
                        // retry
                        IsInError = true;
                        QueryCommit(onComplete);
                    }
                });
            } else {
                // Not working...
                Status = SR.LABEL_STATUS_ERROR_CLICK_FOR_DETAILS;
                StatusDetails = QueryCommitResponse.Value.ToString();
                if (Owner.OnProgress != null)
                    Owner.OnProgress(Owner);
                onComplete(this);
            }
        }
    }

    internal class PeerNotifyArgs {
        public IStorable Item;

        public List<Peer> Peers;

        public PeerNotifyArgs(IStorable item) {
            Item = item;
            Peers = new List<Peer>();
        }
    }

    internal class PutRequest {
        public IStorable Item;
        public List<CommitStatus> Statuses;
        internal CM.Javascript.ServerProgressIndicator UI;

        public PutRequest(IStorable item) {
            Item = item;
            Statuses = new List<CommitStatus>();
        }

        public void UpdateUIProgress() {
            if (UI == null)
                return;
            for (int i = 0; i < Statuses.Count; i++) {
                var st = Statuses[i];
                Assets.SVG glyph = st.IsCommitted ?
                    Assets.SVG.CircleTick
                    : st.IsInError ? Assets.SVG.CircleError
                    : Assets.SVG.Wait;
                UI.Update(st.Peer, glyph, st.Status, st.StatusDetails);
            }
        }
    }
}