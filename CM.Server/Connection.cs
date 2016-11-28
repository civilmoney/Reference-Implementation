#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    /// <summary>
    /// Messaging layer for inbound or outbound WebSocket connections
    /// </summary>
    public class Connection : IDisposable {
        private CancellationTokenSource _CloseToken;
        private SslWebSocket _Inbound;
        private bool _IsClosing;
        private bool _IsDisposed;
        private TimeSpan _LastUsed;
        private ProcessRequestDelegate _OnRequest;
        private SslWebSocket _Outbound;
        private SemaphoreSlim _SocketLock; // Asynchronous sockets don't support multiple concurrent writes.
        private System.Collections.Concurrent.ConcurrentDictionary<string, SendAndReceiveRequest> _WaitHandles;
        public System.Collections.Generic.List<string> SubscribedIDs;
        public object SubscriptionSync;
        public event Action<Connection> ConnectionLost;
        /// <summary>
        /// Constructor for an inbound connection attempt.
        /// </summary>
        internal Connection(SslWebSocket socket,
            ProcessRequestDelegate onRequest) : this() {
            _OnRequest = onRequest;
            _Inbound = socket;
            RemoteEndpoint = _Inbound.RemoteEndPoint;
            SubscriptionSync = new object();
        }

        /// <summary>
        /// Constructor for an outbound connection to another peer.
        /// </summary>
        public Connection(SslWebSocket socket) : this() {
            RemoteEndpoint = socket.RemoteEndPoint;
            DHT_ID = Helpers.DHT_IDi(RemoteEndpoint.ToString());
            _Outbound = socket;
        }

        private Connection() {
            _SocketLock = new SemaphoreSlim(1);
            _LastUsed = Clock.Elapsed;
            _CloseToken = new CancellationTokenSource();
            _WaitHandles = new System.Collections.Concurrent.ConcurrentDictionary<string, SendAndReceiveRequest>();
        }

        public System.Numerics.BigInteger DHT_ID { get; private set; }

        public TimeSpan IdleTime {
            get {
                return Clock.Elapsed - _LastUsed;
            }
        }

        public bool IsBusy { get; set; }

        public bool IsConnected {
            get {
                var ctx = _Outbound?.Context;
                if (ctx != null && ctx.IsConnected)
                    return true;
                ctx = _Inbound?.Context;
                if (ctx != null && ctx.IsConnected)
                    return true;
                return false;
            }
        }

        public IPEndPoint RemoteEndpoint { get; private set; }

        public async void Close(WebSocketCloseStatus status = WebSocketCloseStatus.NormalClosure, string error = null) {
            try {
                _IsClosing = true;
                if (_Outbound != null && _Outbound.State == WebSocketState.Open)
                    await _Outbound.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, error, CancellationToken.None).ConfigureAwait(false);
                else if (_Inbound != null && _Inbound.State == WebSocketState.Open)
                    await _Inbound.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, error, CancellationToken.None).ConfigureAwait(false);
            } catch { }

            this.Dispose();
        }

        public async Task ProcessInboundAsync() {
            try {
                var reader = new MessageReader(OnMessageFromInboundConnection, OnError);
                byte[] b = new byte[1024];
                var conn = _Inbound;
                Debug.Assert(conn.State == WebSocketState.Open);
                while (conn.State == WebSocketState.Open && !_CloseToken.IsCancellationRequested) {
                    var receiveResult = await conn.ReceiveAsync(new ArraySegment<byte>(b), _CloseToken.Token).ConfigureAwait(false);
                    if (_CloseToken.IsCancellationRequested)
                        break;
                    switch (receiveResult.MessageType) {
                        case WebSocketMessageType.Text:
                            if (conn.Context.RequestHeaders["User-Agent"] != null) {
                                Debug.WriteLine(" > " + Encoding.UTF8.GetString(b, 0, receiveResult.Count));
                            }
                            reader.Write(b, 0, receiveResult.Count);
                            break;

                        case WebSocketMessageType.Close:
                            if (conn.State == WebSocketState.Open)
                                await conn.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
                            break;

                        case WebSocketMessageType.Binary:
                            await conn.CloseOutputAsync(WebSocketCloseStatus.InvalidMessageType, string.Empty, CancellationToken.None).ConfigureAwait(false);
                            break;
                    }
                }
            } catch (OperationCanceledException) { }
#if !DEBUG
            catch { } 
#endif
            ConnectionLost?.Invoke(this);
        }

        public async void ProcessOutboundAsync(Action<Connection> onclosed) {     
            try {
                byte[] b = new byte[1024];
                var reader = new MessageReader(OnMessageFromOutboundConnection, OnError);
                var conn = _Outbound;
                Debug.Assert(conn.State == WebSocketState.Open);

                while (conn.State == WebSocketState.Open && !_CloseToken.IsCancellationRequested) {
                    var receiveResult = await conn.ReceiveAsync(new ArraySegment<byte>(b), _CloseToken.Token).ConfigureAwait(false);
                    if (_CloseToken.IsCancellationRequested)
                        break;
                    switch (receiveResult.MessageType) {
                        case WebSocketMessageType.Text:
                            reader.Write(b, 0, receiveResult.Count);
                            break;

                        case WebSocketMessageType.Close:
                            Debug.Assert(conn.State != WebSocketState.Open); // our WebSocket class should have already closed
                            //await conn.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, _CloseToken.Token).ConfigureAwait(false);
                            break;

                        case WebSocketMessageType.Binary:
                            await conn.CloseOutputAsync(WebSocketCloseStatus.InvalidMessageType, string.Empty, _CloseToken.Token).ConfigureAwait(false);
                            break;
                    }
                }
            } catch (OperationCanceledException) { }
#if !DEBUG
            catch { } // Unlike Tasks, async void exceptions will take out the whole application.
#endif
            onclosed?.Invoke(this);
        }

        public async Task<bool> Reply(Message original, CMResult status, Message playload = null, params string[] args) {
            if (_Outbound != null) {
                throw new InvalidOperationException("Don't call SendAndReceive on an outbound connection.");
            }

            if (_Inbound == null || _IsDisposed)
                return false;

            _LastUsed = Clock.Elapsed;
            var m = playload ?? new Message();
            m.Response = new Message.ResponseHeader(status, original.Request.NOnce, args);
            var b = Encoding.UTF8.GetBytes(m.ToResponseString());
            bool hasLock = false;
            try {
                _SocketLock.Wait(_CloseToken.Token);
                hasLock = true;
                await _Inbound.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                return true;
            } catch {
                return false;
            } finally {
                if (hasLock)
                    _SocketLock.Release();
            }
        }

        public async Task<Message> SendAndReceive(string action, Message payload, params string[] args) {
            if (_Inbound != null && action != "NOTIFY") {
                throw new InvalidOperationException("Don't call SendAndReceive on an inbound connection except for NOTIFY.");
            }
            _LastUsed = Clock.Elapsed;

            var m = payload != null ? payload.Clone() : new Message(); // Allow the caller to repeat/broadcast the same message multiple times concurrently
            m.Request = new Message.RequestHeader(action, Guid.NewGuid().ToString().Replace("-", "").Substring(0, 5), args);
            var conn = _Outbound ?? _Inbound;
            if (conn == null || conn.State != WebSocketState.Open || _IsClosing)
                return NOT_CONNECTED(m);
            var b = Encoding.UTF8.GetBytes(m.ToRequestString());
            using (var req = new SendAndReceiveRequest(m)) {
                _WaitHandles[m.Request.NOnce] = req;
                bool hasLock = false;
                bool requestSent = false;
                try {
                    // SemaphoreSlim.Wait(token) has an uncatchable ArgumentNullException bug:
                    // SemaphoreSlim.CancellationTokenCanceledEventHandler -> Monitor.Enter
                    _SocketLock.Wait();//_CloseToken.Token
                    hasLock = true;
                    if (conn.State != WebSocketState.Open)
                        return NOT_CONNECTED(m);

                    await conn.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);

                    requestSent = true;

                    if (!(await req.WaitForReplyAsync().ConfigureAwait(false))
                        || _IsClosing
                        || conn.State != WebSocketState.Open)
                        return new Message() { Response = new Message.ResponseHeader(CMResult.E_Timeout_Waiting_On_Reply, m.Request.NOnce) };

                    Debug.Assert(req.Response != null, "Response should not be null here.");

                    return req.Response;
                } catch (Exception ex) {
                    OnError("SendAndReceive failed with " + ex.Message);
                    return new Message() { Response = new Message.ResponseHeader(CMResult.E_General_Failure, m.Request.NOnce) };
                } finally {
                    if (hasLock && !_IsDisposed) {
                        try {
                            _SocketLock?.Release();
                        } catch (ObjectDisposedException) { }
                    }
                    if (!requestSent) {
                        SendAndReceiveRequest rem;
                        _WaitHandles.TryRemove(m.Request.NOnce, out rem);
                    }
                }
            }
        }

        public override string ToString() {
            return this.RemoteEndpoint.ToString();
        }

        private static Message NOT_CONNECTED(Message payload) {
            return new Message() { Response = new Message.ResponseHeader(CMResult.E_Not_Connected, payload.Request.NOnce) };
        }

        private void CancelPendingSendAndReceive() {
            // Signal any pending requests to give up
            var pending = _WaitHandles.ToArray();
            for (int i = 0; i < pending.Length; i++) {
                SendAndReceiveRequest req;
                if (_WaitHandles.TryRemove(pending[i].Key, out req)) {
                    req.SetResponse(null);
                }
            }
        }

        private void OnError(string err) {
            Debug.WriteLine("[" + this + "] " + err);
        }

        /// <summary>
        /// Raises the inbound request handler, which is currently always RequestHandler.ProcessRequest.
        /// </summary>
        private void OnMessageFromInboundConnection(Message m) {
            _LastUsed = Clock.Elapsed;
            if (m.Response != null) {
                OnMessageFromOutboundConnection(m);
                return;
            }
            Debug.Assert(m.Request != null);
            if (!m.Request.IsValid)
                return;
            // Inbound request. Hand off to the RequestHandler.
            _OnRequest?.Invoke(this, m);
        }

        /// <summary>
        /// Handles replies during SendAndReceive.
        /// </summary>
        private void OnMessageFromOutboundConnection(Message m) {
            _LastUsed = Clock.Elapsed;
            Debug.Assert(m.Response != null);

            if (!m.Response.IsValid)
                return;
            SendAndReceiveRequest req;
            string error = null;
            int c = _WaitHandles.Count;
            if (_WaitHandles.TryRemove(m.Response.NOnce, out req)) {
                if (req.Response == null) {
                    req.SetResponse(m);
                } else {
                    error = String.Format("The same NOnce '{0}' can't be sent twice.", m.Response.NOnce ?? String.Empty);
                }
            } else if (!_IsClosing) { // pending operation cancelled.
                var nonce = m.Response.NOnce ?? String.Empty;
#if DEBUG
                Debug.Assert(false, "Invalid NOnce");
#endif
                error = String.Format("Invalid NOnce '{0}' " + c, nonce);
            }
            if (error != null) {
                // Protocol violation.
                OnError(error);
                Close(WebSocketCloseStatus.ProtocolError, error);
            }
        }

        /// <summary>
        /// Used by SendAndReceive so that a corresponding message from ProcessOutboundAsync can be
        /// detected and returned to its caller.
        /// </summary>
        private class SendAndReceiveRequest : IDisposable {
            private AutoResetEvent _Wait;

            public SendAndReceiveRequest(Message req) {
                Request = req;
                _Wait = new AutoResetEvent(false);
            }

            public Message Request { get; private set; }

            public Message Response { get; private set; }

            public void Dispose() {
                _Wait.Dispose();
            }

            /// <summary>
            /// Sets the reply message and signals WaitForReplyAsync to return.
            /// </summary>
            public void SetResponse(Message m) {
                Response = m;
                try {
                    _Wait.Set();
                } catch (ObjectDisposedException) { }
            }

            /// <summary>
            /// The whole point of this is to not consume a worker thread which just sits there
            /// blocking whilst waiting for the response to signal. This way we wait for a short
            /// moment, if it hasn't arrived, the task ends and frees up the thread pool slot.
            /// </summary>
            /// <returns>
            /// True if the response came in within Constants.MessageReplyTimeoutMs, otherwise false.
            /// </returns>
            public async Task<bool> WaitForReplyAsync() {
                int time = Constants.MessageReplyTimeoutMs;
                while (Response == null && time > 0) {
                    await Task.Run(() => { _Wait.WaitOne(50); }).ConfigureAwait(false);
                    time -= 50;
                }
                return Response != null;
            }
        }

#region IDisposable Support

        public void Dispose() {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing) {
            if (!_IsDisposed) {
                _IsDisposed = true;
                _IsClosing = true;
                _CloseToken?.Cancel();
                CancelPendingSendAndReceive();
                if (disposing) {
                    _CloseToken?.Dispose();
                    _Inbound?.Dispose();
                    _Inbound = null;
                    _Outbound?.Dispose();
                    _Outbound = null;
                    _SocketLock?.Dispose();
                    _SocketLock = null;
                    _OnRequest = null;
                }
            }
        }

#endregion IDisposable Support
    }
}