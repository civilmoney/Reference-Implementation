#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    public enum WebSocketMessageType {
        Text = 0,
        Binary = 1,
        Close = 2
    }

    public enum WebSocketState {
        None = 0,
        Connecting = 1,
        Open = 2,
        CloseSent = 3,
        CloseReceived = 4,
        Closed = 5,
        Aborted = 6
    }

    public enum WebSocketCloseStatus {
        NormalClosure = 1000,
        EndpointUnavailable = 1001,
        ProtocolError = 1002,
        InvalidMessageType = 1003,
        Empty = 1005,
        InvalidPayloadData = 1007,
        PolicyViolation = 1008,
        MessageTooBig = 1009,
        MandatoryExtension = 1010,
        InternalServerError = 1011
    }
    
    /// <summary>
    /// The function signatures more or less match Microsoft's for ease of code interchange/testing
    /// with .NET runtime versions which couldn't be ported to .NET Core/Mono/etc.
    /// </summary>
    /// <remarks>permessage-deflate is not currently tested/supported.</remarks>
    public class SslWebSocket : IDisposable {
        private const string DEFLATE_EXTENSION = "permessage-deflate";
#pragma warning disable CS0649
        private bool _IsDeflateEnabled;
#pragma warning restore CS0649
        private SslWebContext _Context;
        public WebSocketCloseStatus ClosedReason;
        public string ClosedReasonString;
        public int MaxFrameSize = 4096;
        public WebSocketState State;

        private Frame _CurrentFrame = new Frame();
        private int _CurrentFramePos;
        private byte[] _FrameHeadBuffer;
        private DeflateStream _Compressor;

        private WebSocketMessageType _LastType;
        private object _StateLock;

        public SslWebContext Context {
            get {
                return _Context;
            }
        }

        private enum OpCode : byte {
            Continuation = 0,
            Text,
            Binary,
            Reserved3,
            Reserved4,
            Reserved5,
            Reserved6,
            Reserved7,
            Close = 8,
            Ping,
            Pong,
        }

        public IPEndPoint RemoteEndPoint {
            get {
                var c = _Context;
                return c != null ? c.RemoteEndPoint : null;
            }
        }

        public SslWebSocket(SslWebContext context) {
            _Context = context;
            _StateLock = new object();

            State = WebSocketState.Connecting;
        }

        public void Abort() {
            if (_Context != null)
                _Context.Close();
            State = WebSocketState.Aborted;
        }

        public async Task CloseOutputAsync(WebSocketCloseStatus reason, string msg, CancellationToken token) {
            var ctx = _Context;
            lock (_StateLock) {
                if (ctx == null)
                    return;
                if (State != WebSocketState.Open) {
                    ctx.Close();
                    if (State != WebSocketState.Aborted)
                        State = WebSocketState.Closed;
                    return;
                }
            }
            short code = (short)reason;
            var ar = new List<byte>();
            ar.Add((byte)(code >> 8));
            ar.Add((byte)(code & 0xff));
            if (!String.IsNullOrWhiteSpace(msg))
                ar.AddRange(Encoding.UTF8.GetBytes(msg));
            try {
                await Write(OpCode.Close, ar.ToArray(), 0, ar.Count, true, new CancellationTokenSource(1000).Token);
            } catch {
            }
            lock (_StateLock) {
                if (State == WebSocketState.CloseReceived) {
                    State = WebSocketState.Closed;
                } else {
                    State = WebSocketState.CloseSent;
                }
                ctx.Close();
            }
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token) {
            var res = new WebSocketReceiveResult();
            var ctx = _Context;
            skipPingOrPong:

            try {
                if (_CurrentFrame.Length == 0 || _CurrentFramePos == _CurrentFrame.Length) {
                    await _CurrentFrame.ReadAsync(ctx, token);
                    _CurrentFramePos = 0;
                }
            } catch {
                State = WebSocketState.Aborted;
                res.MessageType = WebSocketMessageType.Close;
                return res;
            }

            if (ctx == null || !ctx.IsConnected) {
                State = WebSocketState.Aborted;
                res.MessageType = WebSocketMessageType.Close;
                return res;
            }

            switch (_CurrentFrame.OpCode) {
                case OpCode.Continuation:
                case OpCode.Binary:
                case OpCode.Text:
                case OpCode.Close:
                    res.EndOfMessage = _CurrentFrame.Fin;
                    int toRead = Math.Min(buffer.Count, (int)_CurrentFrame.Length - _CurrentFramePos);
                    if (_IsDeflateEnabled && _CurrentFrame.RSV1) {
                        res.Count = await ctx.ReadCompressedAsync(buffer.Array, buffer.Offset, toRead, token);
                    } else {
                        res.Count = await ctx.ReadAsync(buffer.Array, buffer.Offset, toRead, token).ConfigureAwait(false);
                    }
                    _CurrentFramePos += res.Count;
                    if (_CurrentFrame.IsMasked) {
                        for (int i = buffer.Offset; i < buffer.Offset + res.Count; i += 4) {
                            var tmp = buffer.Array[i] << 24
                                | buffer.Array[i + 1] << 16
                                | buffer.Array[i + 2] << 8
                                | buffer.Array[i + 3];
                            tmp ^= _CurrentFrame.MaskingKey;
                            buffer.Array[i] = (byte)(tmp >> 24);
                            buffer.Array[i + 1] = (byte)(tmp >> 16);
                            buffer.Array[i + 2] = (byte)(tmp >> 8);
                            buffer.Array[i + 3] = (byte)(tmp & 0xff);
                        }
                    }
                    res.MessageType =
                        _CurrentFrame.OpCode == OpCode.Text ? WebSocketMessageType.Text
                        : _CurrentFrame.OpCode == OpCode.Binary ? WebSocketMessageType.Binary
                        : _CurrentFrame.OpCode == OpCode.Close ? WebSocketMessageType.Close
                        : _LastType;
                    _LastType = res.MessageType;
                    if (_CurrentFrame.OpCode == OpCode.Close) {
                        bool shouldClose = false;
                        lock (_StateLock) {
                            if (res.Count >= 2) {
                                var reason = buffer.Array[buffer.Offset] << 8 | buffer.Array[buffer.Offset + 1];
                                ClosedReason = (WebSocketCloseStatus)reason;
                                if (res.Count > 2) {
                                    ClosedReasonString = Encoding.UTF8.GetString(buffer.Array, buffer.Offset + 2, res.Count - 2);
                                }
                            }
                            if (State == WebSocketState.CloseSent) {
                                State = WebSocketState.Closed;
                            } else {
                                State = WebSocketState.CloseReceived;
                                shouldClose = true;
                            }
                        }
                        if (shouldClose) {
                            await CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, token);
                            ctx.Close();
                        }
                    }
                    break;

                case OpCode.Pong:
                    // a uni-directional heartbeat;
                    goto skipPingOrPong;
                case OpCode.Ping: {
                        byte[] tmp = new byte[_CurrentFrame.Length];
                        int cur = 0;
                        while (cur < tmp.Length) {
                            var prev = cur;
                            cur += await ctx.ReadAsync(tmp, cur, tmp.Length - cur, token);
                            if (prev == cur)
                                break;
                        }
                        await Write(OpCode.Pong, tmp, 0, tmp.Length, true, token);
                        goto skipPingOrPong;
                    }
            }
            return res;
        }

        public async Task SendAsync(ArraySegment<byte> buffer,
            WebSocketMessageType type, bool endOfMessage,
            System.Threading.CancellationToken token) {
            var code = type == WebSocketMessageType.Binary ? OpCode.Binary
                : type == WebSocketMessageType.Text ? OpCode.Text
                : OpCode.Close;
            await Write(code, buffer.Array, buffer.Offset, buffer.Count, endOfMessage, token);
        }

        internal async Task<bool> TryNegotiateAsync(string demandProtocol, string demandOrigin, System.Threading.CancellationToken token) {
            var ctx = _Context;
            var headers = ctx.RequestHeaders;
            var protocols = headers["sec-websocket-protocol"];
            var key = headers["sec-websocket-key"];
            var version = headers["sec-websocket-version"];
            var extensions = headers["sec-websocket-extensions"];
            var origin = headers["origin"];
            if (String.IsNullOrWhiteSpace(protocols))
                protocols = null;
            if (String.IsNullOrWhiteSpace(key))
                key = null;
            if (String.IsNullOrWhiteSpace(extensions))
                extensions = null;
            bool isRequestSupported = key != null;
            if (demandProtocol != null || protocols != null) {
                if ((protocols == null && demandProtocol != null)
                    || (protocols != null && demandProtocol == null)
                    || (protocols != null && protocols.IndexOf(demandProtocol) == -1)) {
                    isRequestSupported = false;
                }
            }
            var s = new StringBuilder();
            if (demandOrigin != null && String.Compare(origin, demandOrigin, true) != 0) {
                s.CRLF("HTTP/1.1 403 Forbidden");
            } else if (version != "13") {
                s.CRLF("HTTP/1.1 426 Upgrade Required");
                s.CRLF("Sec-WebSocket-Version: 13");
            } else if (isRequestSupported) {
                s.CRLF("HTTP/1.1 101 Switching Protocols");
                s.CRLF("Upgrade: websocket");
                s.CRLF("Connection: Upgrade");
                s.Append("Sec-WebSocket-Accept: ");
                using (var sha1 = System.Security.Cryptography.SHA1.Create())
                    s.CRLF(Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))));
                if (!String.IsNullOrWhiteSpace(demandProtocol))
                    s.CRLF("Sec-WebSocket-Protocol: " + demandProtocol);
                if (extensions != null) {
                    if (extensions.IndexOf(DEFLATE_EXTENSION) > -1
                        && extensions.IndexOf("server_no_context_takeover") > -1) {
#if DEBUG
                        throw new NotImplementedException("Need to test deflate");
#endif
                        // _IsDeflateEnabled = true;
                        // _Compressor = new DeflateStream(new System.IO.MemoryStream(), CompressionMode.Compress);
                        // s.CRLF("Sec-WebSocket-Extensions: " + DEFLATE_EXTENSION + "; client_no_context_takeover");
                    }
                }
                State = WebSocketState.Open;
            } else {
                s.CRLF("HTTP/1.1 101 400 Bad Request");
            }
            s.CRLF("");
            try {
                await ctx.WriteAsync(s.ToString(), token);
                return true;
            } catch {
                return false;
            }
        }

        private async Task<bool> Write(OpCode code, byte[] data, int offset, int count, bool endOfMessage, System.Threading.CancellationToken token) {
            var f = new Frame();
            f.OpCode = code;
            var ctx = _Context;

            if (ctx == null || !ctx.IsConnected)
                return false;

            if (_IsDeflateEnabled) {
                f.RSV1 = true; // flags as compressed
                if (count > 3 // Skip the UTF8 BOM
                   && data[offset] == 239
                   && data[offset + 1] == 187
                   && data[offset + 2] == 191) {
                    offset += 3;
                }
                var comp = _Compressor;
                if (comp == null)
                    return false;
                comp.BaseStream.Position = 0;
                comp.Write(data, offset, count);
                comp.Write(new byte[1], 0, 1); // flushes the Deflate stream

                // Replace the original payload with the compressed edition.
                offset = 0;
                count = (int)comp.BaseStream.Length;
                data = ((System.IO.MemoryStream)_Compressor.BaseStream).ToArray();
            }

            int cur = 0;
            while (cur < count) {
                if (cur != 0) {
                    f.OpCode = OpCode.Continuation;
                    f.RSV1 = f.RSV2 = f.RSV3 = false;
                }
                int toWrite = count - cur;
                toWrite = Math.Min(MaxFrameSize, toWrite);
                f.Length = toWrite;
                int headSize;
                f.Fin = endOfMessage && cur + toWrite == count;
                f.Serialise(ref _FrameHeadBuffer, out headSize);
                if (!await ctx.WriteAsync(_FrameHeadBuffer, 0, headSize, token).ConfigureAwait(false))
                    return false;
                if (!await ctx.WriteAsync(data, offset + cur, toWrite, token).ConfigureAwait(false))
                    return false;
                cur += toWrite;
            }
            return true;
        }

        /// <summary>
        /// This WebSocket client-mode connection is intended only for use with Civil Money
        /// peers. As such it is always TLS, it does not 'mask', it only uses URL '/' and the host name is
        /// fudged to avoid an unnecessary DNS round-trip for SSL certificate validation purposes.
        /// </summary>
        public static async Task<SslWebSocket> TryConnectAsync(IPEndPoint ep, string hostName, string protocol, CancellationToken token) {
            SslStream ssl;
            var c = new TcpClient();
            c.SendTimeout = 10000;
            const int connectTimeout = 5 * 1000;
         
            try {
                var connectTask = c.ConnectAsync(ep.Address, ep.Port);
                using (var waitCancel = new CancellationTokenSource()) {
                    await Task.WhenAny(connectTask, Task.Delay(connectTimeout, waitCancel.Token));
                    waitCancel.Cancel();
                }
                if (!connectTask.IsCompleted || !c.Connected) {
                    c.Dispose();
                    return null;
                }
                // Authoritative civil.money servers will not have a matching
                ssl = new SslStream(c.GetStream(), false, (sender, cert, chain, errors) => {
                    return cert.Subject.IndexOf("CN=*." + DNS.UNTRUSTED_DOMAIN + ",") > -1
                    || cert.Subject.IndexOf("CN=*." + DNS.AUTHORITATIVE_DOMAIN + ",") > -1;
                });
            } catch {
                c.Client.Dispose();
                return null;
            }
            try {
                await ssl.AuthenticateAsClientAsync(hostName, null, System.Security.Authentication.SslProtocols.Tls
                    | System.Security.Authentication.SslProtocols.Tls11 | System.Security.Authentication.SslProtocols.Tls12,
                    false);
            } catch {
                ssl.Dispose();
                c.Client.Dispose();
                return null;
            }
            var ctx = new SslWebContext(ssl, c, null, (IPEndPoint)c.Client.RemoteEndPoint);
            var s = new StringBuilder();
            s.CRLF("GET / HTTP/1.1");
            s.CRLF("Host: " + hostName);
            s.CRLF("Connection: Upgrade");
            s.CRLF("Upgrade: Websocket");
            s.CRLF("Sec-WebSocket-Version: 13");
            //var b = new byte[16];
            //CryptoFunctions.RNG(b);
            var b = Guid.NewGuid().ToByteArray();
            var key = Convert.ToBase64String(b);
            s.CRLF("Sec-WebSocket-Key: " + key);
            if (protocol != null)
                s.CRLF("Sec-WebSocket-Protocol: " + protocol);
            s.CRLF("");
            await ctx.WriteAsync(s.ToString(), token);

            s.Remove(0, s.Length);

            var response = new NamedValueList();
            using (var ms = new System.IO.MemoryStream()) {
                bool endOfHeaders = false;
                while (ssl.IsAuthenticated && c.Connected && ctx.IsConnected) {
                    try {
                        var readTask = ssl.ReadAsync(ctx.ReadBuffer, 0, ctx.ReadBuffer.Length, token);
                        var timeoutTask = Task.Delay(5 * 1000);
                        await Task.WhenAny(timeoutTask, readTask).ConfigureAwait(false);
                        if (!readTask.IsCompleted) {
                            Dbg("Timed out waiting on reply, closing");
                            ctx.Close();
                            break;
                        }
                        ctx.ReadLength = readTask.Result;
                    } catch {
                        Dbg("Server connection error");
                        ctx.Close();
                        break;
                    }
                    if (ctx.ReadLength == 0)
                        break;
                    ctx.ReadPos = 0;
                    int i = 0;
                    // Read header lines
                    for (; i < ctx.ReadLength; i++) {
                        if (ctx.ReadBuffer[i] == 10) { // should be CR LF
                            ms.Write(ctx.ReadBuffer, ctx.ReadPos, i - ctx.ReadPos - 1);
                            s.Append(Encoding.UTF8.GetString(ms.ToArray()));
                            ms.SetLength(0);
                            ctx.ReadPos = i + 1;
                            if (s.Length == 0) {
                                endOfHeaders = true;
                                break;
                            } else {
                                response.Append(s.ToString());
                                s.Remove(0, s.Length);
                            }
                        }
                    }
                    if (endOfHeaders)
                        break;
                    int remainder = i - ctx.ReadPos;
                    ms.Write(ctx.ReadBuffer, ctx.ReadPos, remainder);
                    ctx.ReadPos += remainder;
                }
            }
            await ctx.Reset();

            Debug.Assert(ctx.ReadPos == ctx.ReadLength);

            if (response.Count == 0 || !response[0].Name.StartsWith("HTTP/1.1 101 ")) {
                Dbg("WebSocket connect failed: " + (response.Count != 0 ? response[0].Name : "no data"));
                ctx.Close();
                return null;
            }

            using (var sha1 = System.Security.Cryptography.SHA1.Create())
                if (response["Sec-WebSocket-Accept"] != Convert.ToBase64String(sha1.ComputeHash(Encoding.UTF8.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")))) {
                    Dbg("WebSocket connect failed: bad key");
                    ctx.Close();
                    return null;
                }

            ctx.UpgradeToWebSocket();
            ctx.WebSocket.State = WebSocketState.Open;

            return ctx.WebSocket;
        }

        private static void Dbg(string msg, params object[] args) {
            Console.WriteLine(String.Format(msg, args));
        }

        public class WebSocketReceiveResult {
            public int Count;
            public bool EndOfMessage;
            public WebSocketMessageType MessageType;
        }

        private class Frame {
            public bool Fin;
            public bool IsMasked;
            public long Length;

            /// <summary>
            /// Ostensibly masking was added to the spec to solve an issue with proxies caching
            /// data. Masking has next to zero security value and is largely a waste of time.
            /// Unfortunately all clients are required to use it. In a non TLS context one might
            /// like use this as a nonce to ensure the same frame isn't received twice or prevent
            /// a 'replay' attack.. although any attack which isn't dumb as door nails would just
            /// set a new random mask..
            /// </summary>
            public int MaskingKey;

            public OpCode OpCode;
            public bool RSV1;
            public bool RSV2;
            public bool RSV3;

            /// <exception cref="ObjectDisposedException"/>
            public async Task ReadAsync(SslWebContext ctx, System.Threading.CancellationToken token) {
                var b = await ctx.ReadByte(token).ConfigureAwait(false);
                Fin = (b & 0x80) != 0;
                RSV1 = (b & 0x40) != 0;
                RSV2 = (b & 0x20) != 0;
                RSV3 = (b & 0x10) != 0;
                OpCode = (OpCode)(b & 0xF);
                b = await ctx.ReadByte(token).ConfigureAwait(false);
                IsMasked = (b & 0x80) != 0;
                long length = (b & 0x7f);
                if (length == 0x7e) {
                    length = await ctx.ReadByte(token).ConfigureAwait(false) << 8
                        | await ctx.ReadByte(token).ConfigureAwait(false);
                } else if (length == 0x7f) {
                    length = await ctx.ReadByte(token).ConfigureAwait(false) << 56
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 48
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 40
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 32
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 24
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 16
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 8
                         | await ctx.ReadByte(token).ConfigureAwait(false);
                }
                Length = length;
                if (IsMasked) {
                    MaskingKey = await ctx.ReadByte(token).ConfigureAwait(false) << 24
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 16
                         | await ctx.ReadByte(token).ConfigureAwait(false) << 8
                         | await ctx.ReadByte(token).ConfigureAwait(false);
                } else {
                    MaskingKey = 0;
                }
            }

            public void Serialise(ref byte[] b, out int len) {
                if (b == null)
                    b = new byte[14];
                len = 0;

                int v = 0;
                v |= Fin ? 0x80 : 0;
                v |= RSV1 ? 0x40 : 0;
                v |= RSV2 ? 0x20 : 0;
                v |= RSV3 ? 0x10 : 0;
                v |= (int)OpCode;
                b[len++] = (byte)v;
                v = IsMasked ? 0x80 : 0;
                if (Length < 126) {
                    b[len++] = (byte)(v | (int)Length);
                } else if (Length <= 0xFFFF) {
                    b[len++] = (byte)(v | 0x7e);
                    b[len++] = (byte)(Length >> 8);
                    b[len++] = (byte)(Length & 0xFF);
                } else {
                    b[len++] = (byte)(v | 0x7f);
                    b[len++] = (byte)((Length >> 56) & 0xff);
                    b[len++] = (byte)((Length >> 48) & 0xff);
                    b[len++] = (byte)((Length >> 40) & 0xff);
                    b[len++] = (byte)((Length >> 32) & 0xff);
                    b[len++] = (byte)((Length >> 24) & 0xff);
                    b[len++] = (byte)((Length >> 16) & 0xff);
                    b[len++] = (byte)((Length >> 8) & 0xff);
                    b[len++] = (byte)((Length >> 0) & 0xff);
                }
                if (IsMasked) {
                    b[len++] = (byte)((MaskingKey >> 24) & 0xff);
                    b[len++] = (byte)((MaskingKey >> 16) & 0xff);
                    b[len++] = (byte)((MaskingKey >> 8) & 0xff);
                    b[len++] = (byte)((MaskingKey >> 0) & 0xff);
                }
            }
        }

        #region IDisposable Support

        private bool _IsDisposed;

        protected virtual void Dispose(bool disposing) {
            if (!_IsDisposed) {
                _IsDisposed = true;
                if (disposing) {
                    State = WebSocketState.Aborted;

                    if (_Context != null) {
                        _Context.Dispose();
                        _Context = null;
                    }
                    if (_Compressor != null) {
                        _Compressor.Dispose();
                        _Compressor = null;
                    }
                }
            }
        }

        public void Dispose() {
            Dispose(true);
        }

        #endregion IDisposable Support
    }
}