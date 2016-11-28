#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

#if DEBUG
//#define VERBOSE_DEBUG
#endif

using Bridge.Html5;
using CM.Schema;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#pragma warning disable CS1998

namespace CM.Javascript {

    internal enum PeerState {

        /// <summary>
        /// We've never tried this peer before.
        /// </summary>
        Unknown,

        /// <summary>
        /// Socket opened successfully, waiting on Ping reply.
        /// </summary>
        Connecting,

        /// <summary>
        /// We've connected and a valid Ping reply has been received.
        /// </summary>
        Connected,

        /// <summary>
        /// The connection was working once, but is now disconnected.
        /// </summary>
        Disconnected,

        /// <summary>
        /// The connection was attempted once, but it didn't work.
        /// </summary>
        Broken
    }

    /// <summary>
    /// A reference implementation for client message exchange with DHT peers.
    /// </summary>
    internal class Peer {
#if VERBOSE_DEBUG
        public static bool VerboseDebug = true;
#else
        public static bool VerboseDebug = false;
#endif
        public List<string> DesiredSubscribedIDs = new List<string>();
        public byte[] DHT_ID;
        public string EndPoint;
        public bool IsEndPointAnIP;
        public int Port;
        public string PredecessorEndpoint;
        public List<string> SentSubscribedIDs = new List<string>();
        public string SuccessorEndpoint;
        public string SupposedIPEndPoint;
        private System.Collections.Generic.List<Action> _ConnectionNotifications;
        private DateTime _LastCommunication;
        private Action<Peer, string> _OnError;
        private Action<Peer, Message> _OnIncomingRequest;
        private Action<Peer> _OnStateChanged;
        private Client _Owner;
        private CM.MessageReader _Reader;
        private WebSocket _Socket;
        private PeerState _State;
        private System.Collections.Generic.Dictionary<string, SendAndReceiveRequest> _WaitHandles;

        public Peer(Client owner,
            string endpoint,
            Action<Peer> onStateChanged,
            Action<Peer, string> onError,
            Action<Peer, Message> onIncomingMessage) {
            _Owner = owner;
            EndPoint = endpoint;
            _OnStateChanged = onStateChanged;
            _OnError = onError;
            _OnIncomingRequest = onIncomingMessage;
            _WaitHandles = new System.Collections.Generic.Dictionary<string, SendAndReceiveRequest>();
            _ConnectionNotifications = new System.Collections.Generic.List<Action>();

            var parts = endpoint.Split(':');
            if (parts.Length > 1) {
                Port = int.Parse(parts[1]);
            } else {
                Port = Constants.WebSocketTransport == "wss" ? 443 : 80;
            }
            // If the endpoint is an IP we can use it for DHT.
            IsEndPointAnIP = parts[0].Match(@"^\d+\.\d+.\d+.\d+$") != null;
            if (IsEndPointAnIP) {
                DHT_ID = Helpers.DHT_IDForEndpoint(endpoint);
            }
        }

        public TimeSpan IdleTime {
            get {
                if (_State == PeerState.Connected)
                    return DateTime.UtcNow - _LastCommunication;
                else
                    return TimeSpan.Zero;
            }
        }

        public PeerState State { get { return _State; } }

        public void BeginConnect(Action onConnected) {
            if (_Socket != null
                && (_State == PeerState.Connected || _State == PeerState.Connecting)) {
                if (onConnected != null)
                    onConnected();
                return;
            }

            // defer
            if (onConnected != null)
                _ConnectionNotifications.Add(onConnected);

            if (_Reader == null)
                _Reader = new CM.MessageReader(OnMessage, OnError);

            _State = PeerState.Connecting;
            _OnStateChanged(this);

            string host = null;
            // Attempt to use seedX.civil.money if applicable.
            for (int i = 0; i < Constants.Seeds.Length; i++) {
                if (EndPoint == Constants.Seeds[i].EndPoint) {
                    host = Constants.Seeds[i].Domain;
                    break;
                }
            }
            // We will be connecting over WSS/TLS so must use our pseudo *.untrusted-server.com
            // host name and wild-card cert in order to pass web browser checks, even though we
            // don't trust servers by design.
            if (host == null)
                host = CM.DNS.EndpointToUntrustedDomain(EndPoint, true);
            var ep = Constants.WebSocketTransport + "://" + host;
            Console.WriteLine(ep);
            _Socket = new WebSocket(ep, Constants.WebSocketProtocol);

            _Socket.OnOpen = (o) => {
                _OnStateChanged(this);
                _LastCommunication = DateTime.UtcNow;
                Ping();
            };
            _Socket.OnClose = (o) => {
                Console.WriteLine("Socket Close: " + o.Reason);
                HandleClosure();
            };
            // This event is basically useless. There's never
            // any contextual information.
            //_Socket.OnError = (o) => {
            //    Console.WriteLine("Socket Error: " + o);
            //};
            _Socket.OnMessage = (o) => {
                _Reader.Write(o.Data.As<string>());
            };
        }

        public void Disconnect() {
            if (_Socket != null) {
                _Socket.Close(CloseEvent.StatusCode.CLOSE_NORMAL);
                HandleClosure();
            }
        }

        public void Ping() {
            SendAndReceive("PING", new PingRequest(), null, (p, e) => {
                var pong = e.Cast<PingResponse>();
                if (pong.YourIP != null) {
                    _State = PeerState.Connected;
                    UpdateInfoFromPingResponse(pong);
                    ReestablishSubscriptions();
                } else {
                    _State = PeerState.Broken;
                    if (_Socket != null)
                        _Socket.Close();
                }
                // OnStateChanged will be raised by either
                // UpdateInfoFromPingResponse or Socket.OnClose
            });
        }

        public async Task<bool> Reply(Message original, CMResult status, Message playload = null, params string[] args) {
            var m = playload ?? new Message();
            m.Response = new Message.ResponseHeader(status, original.Request.NOnce, args);
            var s = m.ToResponseString();
            try {
                _LastCommunication = DateTime.UtcNow;
                _Socket.Send(s);
                return true;
            } catch {
                return false;
            }
        }

        public void SendAndReceive(string action, Message payload, string[] args, Action<Peer, Message> onResult) {
            BeginConnect(() => {
                if (payload == null)
                    payload = new Message();
                payload.Request = new Message.RequestHeader(action, Guid.NewGuid().ToString().Replace("-", "").Substring(0, 5), args);

                if (_Socket == null || _Socket.ReadyState != WebSocket.State.Open) {
                    onResult(this, new Message() { Response = new Message.ResponseHeader(CMResult.E_Not_Connected, payload.Request.NOnce) });
                    return;
                }

                var s = payload.ToRequestString();
                var req = new SendAndReceiveRequest() {
                    Request = payload,
                    OnComplete = onResult
                };
                _WaitHandles[payload.Request.NOnce] = req;

                try {
                    if (VerboseDebug)
                        Console.WriteLine("[" + this.EndPoint + "] > " + payload.ToRequestString());
                    _LastCommunication = DateTime.UtcNow;
                    _Socket.Send(s);
                } catch (Exception ex) {
                    OnError("SendAndReceive failed with " + ex.Message);
                    onResult(this, new Message() { Response = new Message.ResponseHeader(CMResult.E_General_Failure, payload.Request.NOnce) });
                }
            });
        }

        public override string ToString() {
            return SupposedIPEndPoint ?? EndPoint;
        }

        private void FailPendingRequests() {
            var keys = new List<string>();
            foreach (var k in _WaitHandles.Keys)
                keys.Add(k);
            for (int i = 0; i < keys.Count; i++) {
                SendAndReceiveRequest req;
                if (_WaitHandles.TryGetValue(keys[i], out req)) {
                    req.Response = new Message() { Response = new Message.ResponseHeader(CMResult.E_Not_Connected, keys[i]) };
                    req.OnComplete(this, req.Response);
                    _WaitHandles.Remove(keys[i]);
                }
            }
        }

        private void FlushConnectionNotifications() {
            while (_ConnectionNotifications.Count > 0) {
                if (_ConnectionNotifications[0] != null)
                    _ConnectionNotifications[0]();
                _ConnectionNotifications.RemoveAt(0);
            }
        }

        private void HandleClosure() {
            if (_Socket == null)
                return;
            _Socket = null;
            SentSubscribedIDs.Clear();
            if (_State == PeerState.Connected)
                _State = PeerState.Disconnected;
            else if (_State != PeerState.Disconnected)
                _State = PeerState.Broken;
            _OnStateChanged(this);
            FlushConnectionNotifications();
            FailPendingRequests();
        }

        private void OnError(string error) {
            _OnError(this, error);
        }

        private void OnMessage(Message m) {
            if (m.Response != null && m.Response.IsValid) {
                if (VerboseDebug)
                    Console.WriteLine("[" + this.EndPoint + "] < " + m.ToResponseString());
                SendAndReceiveRequest req;
                if (_WaitHandles.TryGetValue(m.Response.NOnce, out req)) {
                    req.Response = m;
                    req.OnComplete(this, m);
                    _WaitHandles.Remove(m.Response.NOnce);
                } else {
                    OnError("Unexpected Nonce " + m.Response.NOnce);
                }
            } else if (m.Request != null && m.Request.IsValid) {
                if (VerboseDebug)
                    Console.WriteLine("[" + this.EndPoint + "] < " + m.ToRequestString());
                _OnIncomingRequest(this, m);
            } else {
                OnError("Invalid message received.");
            }
        }

        private void ReestablishSubscriptions() {
            for (int i = 0; i < DesiredSubscribedIDs.Count; i++) {
                TrySubscribe(DesiredSubscribedIDs[i]);
            }
        }

        private void TrySubscribe(string accountid) {
            if (SentSubscribedIDs.Contains(accountid))
                return;
            SendAndReceive("SUBSCRIBE", null, new string[] { accountid }, (p, res) => {
                if (res.Response.IsSuccessful
                    && !SentSubscribedIDs.Contains(accountid)) {
                    SentSubscribedIDs.Add(accountid);
                }
            });
        }

        private void UpdateInfoFromPingResponse(PingResponse e) {
            if (!IsEndPointAnIP) {
                SupposedIPEndPoint = e.MyIP + ":" + Port;
                DHT_ID = Helpers.DHT_IDForEndpoint(SupposedIPEndPoint);
            }
            SuccessorEndpoint = e.SuccessorEndpoint;
            PredecessorEndpoint = e.PredecessorEndpoint;

            var peers = e.Seen;
            if (peers != null)
                _Owner.AddPotentialPeers(peers);
            _OnStateChanged(this);
            FlushConnectionNotifications();
        }

        private class SendAndReceiveRequest {
            public Action<Peer, Message> OnComplete;
            public Message Request;
            public Message Response;
        }
    }
}