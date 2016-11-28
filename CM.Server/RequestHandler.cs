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

namespace CM.Server {

    internal delegate void ProcessRequestDelegate(Connection conn, Message m);

    /// <summary>
    /// Encapsulates all server message processing logic
    /// </summary>
    internal class RequestHandler {

        /// <summary>
        /// Use to track whether or not we are even reachable. We don't want to pester the network
        /// perpetually if nobody can reach us.
        /// </summary>
        public DateTime LastInboundPing;

        private Dictionary<string, ProcessRequestDelegate> _Actions;
        private Server _Server;

        public RequestHandler(Server owner) {
            _Server = owner;
            _Actions = new Dictionary<string, ProcessRequestDelegate>() {
                { "PING", Ping },
                { "FIND", FindResponsiblePeer },
                { "GET", GetItem },
                { "PUT", PutItem },
                { "QUERY-COMMIT", QueryCommit },
                { "COMMIT", CommitItem },
                { "LIST", ListItems },
                { "SYNC", Sync },
                { "SUBSCRIBE", Subscribe }
            };
        }

      
        public async void ProcessRequest(Connection conn, Message m) {
            ProcessRequestDelegate del;
            if (_Actions.TryGetValue(m.Request.Action, out del))
                del(conn, m);
            else {
                await conn.Reply(m, CMResult.E_Invalid_Action);
            }
        }

        private async void CommitItem(Connection conn, Message m) {
            await conn.Reply(m, await _Server.Storage.Commit(m.Request.FirstArgument, sendPushNotifications: true));
        }

        private async void FindResponsiblePeer(Connection conn, Message m) {
            var req = m.Cast<FindResponsiblePeerRequest>();
            var res = await _Server.DHT.FindResponsiblePeer(req);
            await conn.Reply(m, res.Response.Code, res);
        }

        private async void GetItem(Connection conn, Message m) {
            var path = m.Request.AllArguments;
            if (String.IsNullOrWhiteSpace(path)) {
                await conn.Reply(m, CMResult.E_Invalid_Request);
                return;
            }
            NamedValueList query = null;
            int querIdx = path.IndexOf('?');
            if (querIdx > -1) {
               query = SslWebContext.ParseQuery(path);
               path = path.Substring(0, querIdx);
            }
            IStorable item;
            var status = _Server.Storage.Get(path, out item);
            if (query != null) {
                DateTime calcDate;
                if (item is Account
                    && Helpers.DateFromISO8601(query["calculations-date"], out calcDate)) {
                    var a = item as Account;
                    _Server.Storage.FillAccountCalculations(a, calcDate);
                }
            }
            var res = item as Message;
            await conn.Reply(m, status, res);
        }

        private async void ListItems(Connection conn, Message m) {
            ListResponse list;
            var res = _Server.Storage.List(m.Cast<ListRequest>(), out list);
            await conn.Reply(m, res, list);
        }

        private async void Ping(Connection conn, Message m) {
            var ping = m.Cast<PingRequest>();

            // Make sure we can reply without any I/O errors before
            // doing anything further with this caller.
            if (await conn.Reply(m, CMResult.S_OK, new PingResponse() {
                YourIP = conn.RemoteEndpoint.Address.ToString(),
                MyIP = _Server.DHT.MyIP,
                PredecessorEndpoint = _Server.DHT.Predecessor,
                SuccessorEndpoint = _Server.DHT.Successor,
                Seen = _Server.DHT.GetSeenList(),
            })) {
                LastInboundPing = DateTime.UtcNow;

                // If the ping contains an end-point then the caller is trying
                // to participate in the DHT. Validate its reported IP
                // and consider it as a predecessor.
                var ep = ping.EndPoint;
                if (ep != null) {
                    string ip = conn.RemoteEndpoint.Address.ToString();
                    if (ep.StartsWith(ip + ":")) {
                        _Server.DHT.TryAddToSeen(ep);
                        _Server.DHT.UpdatePredecessor(ep);
                    }
                }
            }
        }

        private async void PutItem(Connection conn, Message m) {
            var path = m.Request.AllArguments;
            if (String.IsNullOrWhiteSpace(path)) {
                await conn.Reply(m, CMResult.E_Invalid_Request);
                return;
            }
            var parts = path.Split('/');

            IStorable item = null;
            // Ensure that the PUT path matches the actual item.
            switch (parts[0]) {
                case Constants.PATH_ACCNT: {
                        var a = m.Cast<Account>();
                        if (a.ID == parts[1])
                            item = a;
                    }
                    break;
                case Constants.PATH_TRANS: {
                        var t = m.Cast<Transaction>();
                        // TRANS/{created utc} {payee} {payer} -> "{created utc} {payee} {payer}"
                        string id = path.Substring(Constants.PATH_TRANS.Length + 1);
                        if (t.ID == id)
                            item = t;
                    }
                    break;
                case Constants.PATH_VOTES: {
                        var v = m.Cast<Vote>();
                        // VOTES/{PropositionID}
                        if (v.PropositionID.ToString() == parts[1]
                            && v.VoterID == parts[2])
                            item = v;
                    }
                    break;
            }

            if (item == null) {
                await conn.Reply(m, CMResult.E_Invalid_Object_Path);
                return;
            }

            var res = await _Server.Storage.Put(item);
            await conn.Reply(m, res.Code, null, res.Token);
        }

        private async void QueryCommit(Connection conn, Message m) {
            DateTime d;
            var path = m.Request.AllArguments;
            var res = _Server.Storage.QueryCommitStatus(path, out d);
            await conn.Reply(m, res, null, (d != DateTime.MinValue ? Helpers.DateToISO8601(d) : null));
        }

        private async void Sync(Connection conn, Message m) {
            await _Server.SyncManager.OnSyncAnnounceReceived(m.Cast<SyncAnnounce>(), conn);
            await conn.Reply(m, CMResult.S_OK);
        }

        private async void Subscribe(Connection conn, Message m) {
            _Server.Storage.AddSubscription(conn, m.Request.FirstArgument);
            await conn.Reply(m, CMResult.S_OK);
        }

    }
}