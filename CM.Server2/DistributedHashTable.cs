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
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    internal class DistributedHashTable {
        public bool ArePeersFullyPopulated;
        public BigInteger[] LookupTable;
        public BigInteger? MyDHT_ID;
        public string MyEndpoint;
        public string MyIP;
        public string[] Peers;
        public string Predecessor;
        public BigInteger? PredecessorID;
        public System.Collections.Concurrent.ConcurrentDictionary<string, SeenEndPoint> Seen = new System.Collections.Concurrent.ConcurrentDictionary<string, SeenEndPoint>();
        public System.Net.IPAddress ServiceIP;
        public int ServicePort;
        public string Successor;
        public BigInteger? SuccessorID;
        private static readonly BigInteger MaxValue;

        private bool _IsInPingRandom;
        private bool _IsInPoll;
        private bool _IsInUpdatingOne;
        private TimeSpan _LastStabiliseTime;
        private Log _Log;
        private int _ScanIndex;

        static DistributedHashTable() {
            var b = new byte[Constants.DHTIDSize];
            for (int i = 0; i < b.Length; i++) {
                b[i] = 0xFF;
            }
            MaxValue = Helpers.FromBigEndian(b);
        }

        public DistributedHashTable(Log log) {
            _Log = log;
        }

        /// <summary>
        /// The amount of time since we last got a working Predecessor
        /// and Successor.
        /// </summary>
        public TimeSpan StabilisedDuration {
            get {
                if (Predecessor == null || Successor == null || MyDHT_ID == null || !ArePeersFullyPopulated)
                    return TimeSpan.Zero;
                return Clock.Elapsed - _LastStabiliseTime;
            }
        }

        public static bool IsInRange(BigInteger value, BigInteger min, BigInteger max) {
            if (min == max)
                return true;
            if (min < max)
                return value > min && value <= max;
            else // Wrap-around
                return value > min || value <= max;
        }

        public async Task<ConnectionHandle> Connect(string endpoint, bool forceRetry = false) {
            if (endpoint == null) throw new ArgumentNullException("endpoint");

            SeenEndPoint e;
            if (!Seen.TryGetValue(endpoint, out e)) {
                var ep = Helpers.ParseEP(endpoint);
                if (ep == null)
                    throw new ArgumentException("endpoint is invalid.");
                e = new SeenEndPoint(ep);
                Seen[endpoint] = e;
            }

            bool hasLock = Monitor.TryEnter(e.Sync, 1000);
            try {
                for (int i = 0; i < e.Connections.Count; i++) {
                    var c = e.Connections[i];
                    if (Monitor.TryEnter(c, 100)) {
                        try {
                            if (!c.IsBusy) {
                                if (c.IsConnected)
                                    return new ConnectionHandle(c);
                                else
                                    e.Connections.RemoveAt(i--);
                            }
                        } finally {
                            Monitor.Exit(c);
                        }
                    }
                }
            } finally {
                if (hasLock)
                    Monitor.Exit(e.Sync);
            }

            if (!forceRetry && !e.CanConnect)
                return new ConnectionHandle(null);

            System.Net.WebSockets.WebSocket sock = null;
            try {
                // Unfortunately Microsoft .netcore 2.1 ClientWebSocket is useless because there's no way to
                // connect from specific IP, which is highly problematic because the client's
                // remote IP is important to the DHT.
                //
                // Also there's no way to ignore invalid certificate errors as ServicePointManager
                // isn't used.
                //
                // var uri = new Uri("wss://" + DNS.EndpointToUntrustedDomain(endpoint, true));
                // sock = new System.Net.WebSockets.ClientWebSocket();
                // sock.Options.AddSubProtocol(Constants.WebSocketProtocol);
                // await sock.ConnectAsync(uri, CancellationToken.None)
                //       .ConfigureAwait(false);

                sock = await TryCreateSocket(ServiceIP, e.EndPoint,
                  DNS.EndpointToUntrustedDomain(endpoint, false),
                  Constants.WebSocketProtocol, CancellationToken.None).ConfigureAwait(false);
            } catch {
                sock.Dispose();
                sock = null;
            }
            if (sock != null) {
                e.FailingSince = TimeSpan.Zero;
                e.FailureCount = 0;
                e.LastSuccessfulConnectionTime = Clock.Elapsed;
            } else {
                e.FailureCount++;
                if (e.FailingSince == TimeSpan.Zero)
                    e.FailingSince = Clock.Elapsed;
                return new ConnectionHandle(null);
            }

            var conn = new Connection(e.EndPoint, sock);

            var handle = new ConnectionHandle(conn);

            // ConnectionHandle will mark as Busy before
            // adding to connections.
            hasLock = Monitor.TryEnter(e.Sync, 100);
            try {
                e.Connections.Add(conn);
            } finally {
                if (hasLock)
                    Monitor.Exit(e.Sync);
            }

            // Upon WebSocket close, connections will remove themselves from
            // the pool automatically.
            conn.ProcessOutboundAsync(Remove);

            return handle;
        }

        public async Task<FindResponsiblePeerResponse> FindResponsiblePeer(FindResponsiblePeerRequest req) {
            var visited = (req.HopList ?? String.Empty).Split(new char[] { ',' },
                StringSplitOptions.RemoveEmptyEntries).ToList();

            var res = new FindResponsiblePeerResponse();
            res.Response = new Message.ResponseHeader(CMResult.S_OK, req.Request != null ? req.Request.NOnce : null);

            if (visited.Count > req.MaxHopCount) {
                res.HopList = String.Join(",", visited);
                res.Response.Code = CMResult.E_Max_Hops_Reached;
                return res;
            }

            if (visited.Contains(MyEndpoint)) {
                res.HopList = String.Join(",", visited);
                res.Response.Code = CMResult.E_Max_Hops_Reached;
                return res;
            }

            visited.Add(MyEndpoint);
            res.HopList = String.Join(",", visited);

            var idBytes = req.DHTID;

            if (idBytes == null) {
                res.Response.Code = CMResult.E_Invalid_Request;
                return res;
            }

            BigInteger myID = MyDHT_ID.GetValueOrDefault();
            BigInteger succID = SuccessorID.GetValueOrDefault();
            if (myID == BigInteger.Zero
                || succID == BigInteger.Zero) {
                res.PeerEndpoint = MyEndpoint;
                res.Response.Code = CMResult.E_Not_Enough_Peers;
                return res;
            }

            var query = Helpers.FromBigEndian(idBytes);

            if (IsInRange(query, myID, succID)) {
                res.PeerEndpoint = MyEndpoint;// Successor;
                return res;
            }

            string ep = null;
            // Check our Peer lookup table for a peer's ID
            // that sits in between us and the query value.
            for (int i = Peers.Length - 1; i >= 0; i--) {
                var p = Peers[i];
                if (p != null
                    && !IsSelf(p)
                    && IsInRange(Helpers.DHT_IDi(p), myID, query)
                    && CanProbablyConnect(p)) {
                    ep = p;
                    break;
                }
            }

            if (ep == null) {
                // Also check against seen servers
                var seen = Seen.Values.ToArray();
                for (int i = 0; i < seen.Length; i++) {
                    var peer = seen[i];
                    if (IsInRange(peer.DHT_ID, myID, query)
                        && peer.CanConnect) {
                        ep = peer.EndPoint.ToString();
                        break;
                    }
                }
            }

            // If no peers at all, return myself and E_Not_Enough_Peers
            if (ep == null) {
                res.PeerEndpoint = MyEndpoint;
                res.Response.Code = CMResult.E_Not_Enough_Peers;
                return res;
            }

            // Forward the request on
            try {
                using (var conn = await Connect(ep)) {
                    if (!conn.IsValid)
                        return res;
                    var fwd = new FindResponsiblePeerRequest();
                    fwd.DHTID = idBytes;
                    fwd.HopList = res.HopList;
                    fwd.MaxHopCount = req.MaxHopCount;
                    var fwdResp = await conn.Connection.SendAndReceive("FIND", fwd);
                    return fwdResp.Cast<FindResponsiblePeerResponse>();
                }
            } catch {
                return res;
            }
        }

        public string GetSeenList() {
            var ar = Seen.Values.ToList();
            ar.RemoveAll(x => x.LastSuccessfulConnectionTime == TimeSpan.Zero || x.FailureCount > 0);
            ar.Sort();
            return String.Join(",", ar);
        }

        public bool IsCurrentlyResponsible(BigInteger query) {
            BigInteger myID = MyDHT_ID.GetValueOrDefault();
            BigInteger succID = SuccessorID.GetValueOrDefault();
            if (myID == BigInteger.Zero || succID == BigInteger.Zero)
                return false;
            return IsInRange(query, myID, succID);
        }

        public void PerformConnectionHousekeeping() {
            var all = Seen.ToArray();
            int count = 0;
            for (int i = 0; i < all.Length; i++) {
                var p = all[i].Value;
                count += p.CloseIdleConnections();
                if (!p.CanConnect
                    && (Clock.Elapsed - p.FailingSince).TotalMinutes > 60) {
                    Seen.TryRemove(all[i].Key, out p);
                    _Log.Write(this, LogLevel.INFO, "Removed {0} from seen peers.", all[i].Key);
                }
            }
            if (count > 0) {
                System.Diagnostics.Debug.WriteLine("Cleaned up {0} idle connections.", count);
                //_Log.Write(this, LogLevel.INFO, "Cleaned up {0} idle connections.", count);
            }
        }

        /// <summary>
        /// This is to help protect against peers separating off into
        /// their own circles. By pinging random peers, we potentially
        /// update their predecessors, kicking off the process of
        /// re-balancing the network.
        /// </summary>
        public async Task PingRandomNode() {
            _IsInPingRandom = true;
            try {
                var seen = Seen.Values.ToArray();
                SeenEndPoint toPing = null;

                for (int i = 0; i < seen.Length; i++) {
                    if ((!seen[i].HasTriedRandomlyPinging || (Clock.Elapsed - seen[i].LastRandomPing).TotalMinutes > 30)
                        && (toPing == null || seen[i].LastRandomPing < toPing.LastRandomPing)) {
                        toPing = seen[i];
                    }
                }

                if (toPing != null) {
                    toPing.HasTriedRandomlyPinging = true;
                    toPing.LastRandomPing = Clock.Elapsed;
                    using (var succ = await Connect(toPing.EndPoint.ToString(), forceRetry: true)) {
                        if (succ.IsValid) {
                            var res = await succ.Connection.SendAndReceive("PING", new PingRequest() {
                                EndPoint = MyEndpoint
                            });
                        }
                    }
                }
            } finally {
                _IsInPingRandom = false;
            }
        }

        public async Task Poll() {
            _IsInPoll = true;
            //_Log.Write(this, LogLevel.INFO, "Poll.");
            try {
                var seen = Seen.Values.ToArray();

                // Is the predecessor still valid?
                if (!await CanDefinitelyConnect(Predecessor)) {
                    Predecessor = null;
                    PredecessorID = null;
                }

                // Is the Successor valid?
                if (!(await CanDefinitelyConnect(Successor))) {
                    Successor = null;
                    SuccessorID = null;

                    // Try all known peers
                    for (int i = 0; i < seen.Length; i++) {
                        var peer = seen[i];
                        if (peer.CanConnect
                            && !peer.HasTriedAsSuccessor
                            && !IsSelf(peer.EndPoint.ToString())) {
                            peer.HasTriedAsSuccessor = true;
                            Successor = peer.EndPoint.ToString();
                            SuccessorID = peer.DHT_ID;
                            break;
                        }
                    }
                    if (Successor == null) {
                        // No known good peers to connect to, re-flag everything as
                        // eligible for connection next poll.
                        for (int i = 0; i < seen.Length; i++) {
                            seen[i].HasTriedAsSuccessor = false;
                        }
                        return;
                    }
                }

                // Make sure the successor is still alive and that we're equal to or in-between its
                // predecessor and successor.
                bool successorChanged = false;
                do {
                    var ep = Successor;

                    if (ep == null)
                        break;

                    using (var succ = await Connect(ep)) {
                        if (succ.IsValid) {
                            var res = await succ.Connection.SendAndReceive("PING", new PingRequest() {
                                EndPoint = MyEndpoint
                            });

                            if (res == null) {
                                // Invalid successor
                                _Log.Write(this, LogLevel.INFO, "PING {0} failed", ep);
                                successorChanged = false;
                                Successor = null;
                                SuccessorID = null;
                                break;
                            }

                            var ping = res.Cast<PingResponse>();

                            // Check our public facing end-point
                            var myIP = ping.YourIP;
                            if (myIP != null && myIP != MyIP) {
                                TryChangeMyIP(myIP);
                            }

                            // We want to make that the successor's predecessor peer is ourselves. If
                            // it's not and the peer is in between ourselves and our current successor,
                            // then we change our successor to the peer in between.
                            var pred = ping.PredecessorEndpoint;
                            var myID = MyDHT_ID != null ? MyDHT_ID.Value : BigInteger.Zero;
                            if (!res.Response.IsSuccessful)
                                _Log.Write(this, LogLevel.INFO, "PING {0} {1} (Pred: {2})", ep, res.Response.Code, pred);

                            if (pred != null
                                && pred != Successor
                                && pred != MyEndpoint
                                && myID != BigInteger.Zero) {
                                var predID = Helpers.DHT_IDi(pred);
                                var succID = Helpers.DHT_IDi(ep);
                                if (IsInRange(predID, myID, succID)) {
                                    // If the peer's predecessor is in between us and the peer,
                                    // then the predecessor becomes our new successor.
                                    if (Successor != pred) {
                                        Successor = pred;
                                        SuccessorID = predID;
                                        successorChanged = true;
                                        _LastStabiliseTime = Clock.Elapsed;
                                        _Log.Write(this, LogLevel.INFO, "New successor {0}", Successor);
                                    }
                                }
                            } else {
                                successorChanged = false;
                            }

                            // Update seen database
                            var peerSeen = (ping.Seen ?? String.Empty).Split(',');
                            for (int i = 0; i < peerSeen.Length; i++)
                                TryAddToSeen(peerSeen[i]);
                        } else {
                            // Invalid successor
                            successorChanged = false;
                            Successor = null;
                            SuccessorID = null;
                        }
                    }
                } while (successorChanged);
            } finally {
                _IsInPoll = false;
            }
        }

        /// <summary>
        /// Adds the specified endpoint to the Seen dictionary, if valid.
        /// </summary>
        public void TryAddToSeen(string endpoint) {
            SeenEndPoint e;
            if (!Seen.TryGetValue(endpoint, out e)) {
                var ep = Helpers.ParseEP(endpoint);
                if (ep != null) {
                    e = new SeenEndPoint(ep);
                    Seen[endpoint] = e;
                }
            }
        }

        /// <summary>
        /// Changes the DHT public endpoint and SelfDHT_ID. If connections
        /// are possible with other peers, a consensus is required by a few
        /// other peers before the IP is changed.
        /// </summary>
        /// <param name="newPublicIP">The new proposed IP to use.</param>
        public async void TryChangeMyIP(string newPublicIP) {
            var myPublicEndPoint = newPublicIP + ":" + ServicePort;

            if (MyEndpoint == myPublicEndPoint)
                return;

            int positive = 0;
            int negative = 0;
            int tests = 0;
            // Potentially new public IP, confer with at least
            // a few peers other than our Successor.
            var seen = Seen.Values.ToArray();
            for (int i = 0; i < seen.Length && tests < 3; i++) {
                if (seen[i].EndPoint.ToString() != Successor
                    && seen[i].CanConnect) {
                    using (var test = await Connect(seen[i].EndPoint.ToString())) {
                        if (test.IsValid) {
                            var m = await test.Connection.SendAndReceive("PING", new PingRequest());
                            if (m != null) {
                                var conf = m.Cast<PingResponse>();
                                if (conf.YourIP == newPublicIP)
                                    positive++;
                                else
                                    negative++;
                                tests++;
                            }
                        }
                    }
                }
            }

            if (positive < negative || tests == 0)
                return;

            _Log.Write(this, LogLevel.INFO, "New public IP {0}", newPublicIP);
            MyIP = newPublicIP;
            MyEndpoint = myPublicEndPoint;
            MyDHT_ID = Helpers.DHT_IDi(myPublicEndPoint);
            LookupTable = new BigInteger[Constants.DHTIDSize * 8];
            Peers = new string[LookupTable.Length];
            for (int i = 0; i < LookupTable.Length; i++) {
                LookupTable[i] = MyDHT_ID.Value + BigInteger.ModPow(2, i, MaxValue);
            }
            Successor = null;
            SuccessorID = null;
            Predecessor = null;
            PredecessorID = null;
        }

        public async Task TryUpdateOne() {
            _IsInUpdatingOne = true;
            try {
                var succ = Successor;
                if (succ == null || MyEndpoint == null)
                    return;
                var req = new FindResponsiblePeerRequest();
                req.MaxHopCount = Constants.MaxHopCount;
                req.DHTID = LookupTable[_ScanIndex].ToBigEndian(Constants.DHTIDSize);
                if (req.DHTID == null)
                    return; // Our lookup table may have just been reset.
                var res = await FindResponsiblePeer(req);
                if (res.PeerEndpoint == null)
                    return;
                if (await CanDefinitelyConnect(res.PeerEndpoint))
                    Peers[_ScanIndex] = res.PeerEndpoint;
                _ScanIndex++;
                if (_ScanIndex == Peers.Length) {
                    _ScanIndex = 0;
                    // We're stable once the peer lookup table is fully
                    // populated
                    bool isFullyPopulated = true;
                    for (int i = 0; i < Peers.Length; i++) {
                        if (Peers[i] == null) {
                            isFullyPopulated = false;
                            break;
                        }
                    }
                    ArePeersFullyPopulated = isFullyPopulated;
                }
            } finally {
                _IsInUpdatingOne = false;
            }
        }

        public async void UpdatePredecessor(string endpoint) {
            if (!CanProbablyConnect(endpoint)
                || MyDHT_ID == null)
                return;
            var id = Helpers.DHT_IDi(endpoint);
            if (Predecessor == null
                || IsInRange(id, PredecessorID.Value, MyDHT_ID.Value)) {
                using (var test = await Connect(endpoint)) {
                    if (test.IsValid) {
                        if (Predecessor != endpoint) {
                            PredecessorID = id;
                            Predecessor = endpoint;
                            _Log.Write(this, LogLevel.INFO, "New predecessor {0}", Predecessor);
                            _LastStabiliseTime = Clock.Elapsed;
                        }
                    }
                }
            }
        }

        private async Task<bool> CanDefinitelyConnect(string endpoint) {
            if (endpoint == null)
                return false;
            if (IsConnectedTo(endpoint))
                return true;
            using (var test = await Connect(endpoint)) {
                if (test.IsValid) {
                    return true;
                }
            }
            return false;
        }

        private bool CanProbablyConnect(string endpoint) {
            if (String.IsNullOrWhiteSpace(endpoint)
                || endpoint == MyEndpoint)
                return false;
            SeenEndPoint e;
            Seen.TryGetValue(endpoint, out e);

            return e == null // Assume we can
                || e.CanConnect;
        }

        private bool IsConnectedTo(string endpoint) {
            if (endpoint == null) throw new ArgumentNullException("endpoint");
            SeenEndPoint e;
            Seen.TryGetValue(endpoint, out e);
            return e != null && e.Connections.Count > 0;
        }

        private bool IsSelf(string ep) {
            return ep != null && ep == MyEndpoint;
        }

        private void Remove(Connection conn) {
            SeenEndPoint e;
            Seen.TryGetValue(conn.RemoteEndpoint.ToString(), out e);
            bool hasLock = Monitor.TryEnter(e.Sync, 100);
            try {
                e.Connections.Remove(conn);
            } finally {
                if (hasLock)
                    Monitor.Exit(e.Sync);
            }
        }

        private async Task<System.Net.WebSockets.WebSocket> TryCreateSocket(IPAddress localNetworkInterface, IPEndPoint remoteEndpoint, string hostName, string protocol, CancellationToken token) {
            System.Net.Security.SslStream ssl;
            var c = new Socket(remoteEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            c.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);

            c.Bind(new IPEndPoint(localNetworkInterface, 0));
            c.SendTimeout = 10000;
            const int connectTimeout = 5 * 1000;

            try {
#if !FULL_CLR
                var connectTask = c.ConnectAsync(remoteEndpoint.Address, remoteEndpoint.Port);
                using (var waitCancel = new CancellationTokenSource()) {
                    await Task.WhenAny(connectTask, Task.Delay(connectTimeout, waitCancel.Token));
                    waitCancel.Cancel();
                }
                if (!connectTask.IsCompleted || !c.Connected) {
                    c.Dispose();
                    return null;
                }
#else
                c.Connect(remoteEndpoint.Address, remoteEndpoint.Port);
#endif
                ssl = new System.Net.Security.SslStream(new NetworkStream(c), false, WeTrustNoServersAnyway);
            } catch {
                c.Dispose();
                return null;
            }
            try {
                var authenticateTask = ssl.AuthenticateAsClientAsync(hostName, null,
                    System.Security.Authentication.SslProtocols.Tls
                    | System.Security.Authentication.SslProtocols.Tls11
                    | System.Security.Authentication.SslProtocols.Tls12,
                    false);
                using (var waitCancel = new CancellationTokenSource()) {
                    await Task.WhenAny(authenticateTask, Task.Delay(connectTimeout, waitCancel.Token));
                    waitCancel.Cancel();
                }
#if !FULL_CLR
                if (!authenticateTask.IsCompletedSuccessfully) {
                    throw new TimeoutException("TLS authenticate timed out.");
                }
#endif
            } catch {
                ssl.Dispose();
                c.Dispose();
                return null;
            }

            var s = new System.Text.StringBuilder();
            s.CRLF("GET / HTTP/1.1");
            s.CRLF("Host: " + hostName);
            s.CRLF("Connection: Upgrade");
            s.CRLF("Upgrade: Websocket");
            s.CRLF("Sec-WebSocket-Version: 13");
            var key = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            s.CRLF("Sec-WebSocket-Key: " + key);
            if (protocol != null)
                s.CRLF("Sec-WebSocket-Protocol: " + protocol);
            s.CRLF("");
            var b = System.Buffers.ArrayPool<byte>.Shared.Rent(1024);
            var len = System.Text.Encoding.ASCII.GetBytes(s.ToString(), b);
            System.Buffers.ArrayPool<byte>.Shared.Return(b);
            await ssl.WriteAsync(b, 0, len).ConfigureAwait(false);

            // Validating key or anything is a waste of time because no peers
            // are ever truly trusted by design, ever. WebSocket will throw
            // later on if anything's actually wrong.
            int eoh = 0;
            int v = 0;
            while (eoh != 0xd0a0d0a
                && ssl.CanRead
                && (v = ssl.ReadByte()) > 0) {
                eoh <<= 8;
                eoh |= v;
            }

            return System.Net.WebSockets.WebSocket.CreateFromStream(ssl, false, protocol, TimeSpan.FromMinutes(1));
        }

        private bool WeTrustNoServersAnyway(object sender,
            System.Security.Cryptography.X509Certificates.X509Certificate certificate,
            System.Security.Cryptography.X509Certificates.X509Chain chain,
            System.Net.Security.SslPolicyErrors sslPolicyErrors) {
            return true;
        }
        /// <summary>
        /// Provides using/IDisposable pattern for connection management.
        /// </summary>
        public class ConnectionHandle : IDisposable {

            public ConnectionHandle(Connection conn) {
                IsValid = conn != null;
                if (conn != null) {
                    System.Diagnostics.Debug.Assert(!conn.IsBusy);
                    conn.IsBusy = true;
                    Connection = conn;
                }
            }

            public Connection Connection { get; private set; }
            public bool IsValid { get; private set; }

            void IDisposable.Dispose() {
                IsValid = false;
                if (Connection != null)
                    Connection.IsBusy = false;
            }
        }

        /// <summary>
        /// Records the health and holds a connection pool for peers
        /// seen in the network.
        /// </summary>
        public class SeenEndPoint : IComparable<SeenEndPoint> {
            public readonly BigInteger DHT_ID;

            public readonly System.Net.IPEndPoint EndPoint;

            public readonly TimeSpan FirstSeen;

            public List<Connection> Connections = new List<Connection>();

            public TimeSpan FailingSince;

            public int FailureCount;

            public bool HasTriedAsSuccessor;
            public bool HasTriedRandomlyPinging;
            public TimeSpan LastRandomPing;
            public TimeSpan LastSuccessfulConnectionTime;

            internal readonly object Sync = new object();

            public SeenEndPoint(System.Net.IPEndPoint ep) {
                EndPoint = ep;
                DHT_ID = Helpers.DHT_IDi(ep.ToString());
                FirstSeen = Clock.Elapsed;
            }

            public bool CanConnect {
                get {
                    return FailureCount == 0 || (
                        // Begin a retry window 1 minute after any failures ...
                        (Clock.Elapsed - FailingSince).TotalSeconds > 60
                        // ... up to a max of 4 minutes.
                        && (Clock.Elapsed - FailingSince).TotalMinutes < 5
                        );
                }
            }

            public bool IsConnected { get { return Connections.Count > 0; } }

            /// <summary>
            /// Closes idle connections and returns the number of
            /// connections that were closed.
            /// </summary>
            /// <returns>The number of connections affected.</returns>
            public int CloseIdleConnections() {
                int count = 0;
                bool hasLock = Monitor.TryEnter(Sync, 100);
                try {
                    for (int i = 0; i < Connections.Count; i++) {
                        if (Connections[i].IdleTime.TotalSeconds > 120) {
                            if (Connections[i].IsBusy)
                                System.Diagnostics.Debug.WriteLine("DHT Connection looks hung warning");
                            Connections[i].Close();
                            //Connections[i].Dispose();
                            Connections.RemoveAt(i--);
                            count++;
                        }
                    }
                } finally {
                    if (hasLock)
                        Monitor.Exit(Sync);
                }
                return count;
            }

            public int CompareTo(SeenEndPoint other) {
                return FailingSince.CompareTo(other.FailingSince);
            }

            public override string ToString() {
                return EndPoint.ToString();
            }
        }
    }
}