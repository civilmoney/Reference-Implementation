﻿#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    /// <summary>
    /// This a minimal HTTPS/SSL-only web server implementation designed for speed and bare-bones functionality.
    /// </summary>
    public class SslWebServer {
        private ConcurrentDictionary<SslWebContext, string> _ConnectedClients;
        private TcpListener _Listener;
        private TcpListener _Port80Listener;
        private Log _Log;
        private int _Port;
        private X509Certificate2 _ServerCert;
        private AttackMitigation _AttackPrevention;

        public SslWebServer(X509Certificate2 sslCertificate, int port, Log log, AttackMitigation prevention) {
            _Port = port;
            _Log = log;
            _ConnectedClients = new ConcurrentDictionary<SslWebContext, string>();
            _ServerCert = sslCertificate;
            _AttackPrevention = prevention;
        }

        public Func<SslWebContext,Task> HandleHttpRequest;

        public event Action<SslWebSocket> HandleWebSocket;

        public int ConnectionCount { get { return _ConnectedClients.Count; } }

        public string DemandWebSocketOrigin { get; set; }

        public string DemandWebSocketProtocol { get; set; }

        public bool IsListening { get; private set; }

        public async void Start() {
            if (IsListening)
                return;

            _Listener = new TcpListener(new IPEndPoint(IPAddress.Any, _Port));
            // Workaround for linux re-bind bug
            _Listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _Log.Write(this, LogLevel.INFO, "HTTPS listener started.");
            _Listener.Start();
            IsListening = true;
            while (IsListening) {
                try {
                    var client = await _Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    var address = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                    if (!_AttackPrevention.ShouldDropTcpConnection(address)) {
#pragma warning disable CS4014
                        Task.Run(() => HandleClient(client));
#pragma warning restore CS4014
                    } else {
                        Debug.WriteLine("IP rejected " + address);
                        client.Dispose();
                    }
                } catch (Exception ex) {
                    _Log.Write(this, LogLevel.WARN, "TCP error: " + ex.Message);
                }
            }
        }
        

        public async void StartPort80Redirect() {
            _Port80Listener = new TcpListener(new IPEndPoint(IPAddress.Any, 80));
            // Workaround for linux re-bind bug
            _Port80Listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _Log.Write(this, LogLevel.INFO, "HTTP Port 80 redirect started.");
            _Port80Listener.Start();
            while (IsListening) {
                try {
                    var client = await _Port80Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    var address = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
                    if (!_AttackPrevention.ShouldDropTcpConnection(address)) {
#pragma warning disable CS4014
                        Task.Run(() => RedirectClient(client));
#pragma warning restore CS4014
                    } else {
                        Debug.WriteLine("IP rejected " + address);
                        client.Dispose();
                    }
                } catch (Exception ex) {
                    _Log.Write(this, LogLevel.WARN, "TCP error: " + ex.Message);
                }
            }
        }

        private async Task RedirectClient(TcpClient c) {
            try {
                using (var stream = c.GetStream()) {
                    var b = new byte[1024];
                    int read;
                    string path = null;
                    string protocol = null;
                    bool newLine = false;
                    int total = 0;
                    var s = new StringBuilder();
                    while ((read = await stream.ReadAsync(b, 0, b.Length)) != 0) {
                        for (int i = 0; i < read; i++) {
                            // We don't care what the payload is. Just look for header terminator,
                            // within a reasonable volume of data.
                            if (b[i] == '\n') {
                                if (newLine && path != null) {
                                    // End of headers, we're done.
                                    b = Encoding.ASCII.GetBytes(
                                        String.Format("{0} 301 Moved Permanently\r\nLocation: https://civil.money{1}\r\nContent-Length: 0\r\n",
                                        protocol, path));
                                    stream.Write(b, 0, b.Length);
                                    return;
                                }
                                if (path == null) {
                                    var line = s.ToString();
                                    if (!line.EndsWith("HTTP/1.1") 
                                        && !line.EndsWith("HTTP/1.0"))
                                        return; // protocol violation
                                    protocol = "HTTP/1." + line[line.Length - 1]; // 1.1 | 1.0
                                    int idx = line.IndexOf(' ');
                                    // method = line.Substring(0, idx);
                                    path = line.Substring(idx + 1, line.Length - (8 + idx + 2));
                                    if (!path.StartsWith("/"))
                                        path = "/"; // re-base
                                }
                                //Debug.WriteLine(s);
                                s.Remove(0, s.Length);
                                newLine = true;
                            } else if (b[i] != '\r') {
                                newLine = false;
                                s.Append((char)b[i]);
                            }
                        }
                        total += read;
                        if (total > 0x10000) {
                            return; // client is sending junk, disconnect
                        }
                    }
                }
            } finally {
                c.Client.Dispose();
            }
        }

        public void Stop() {
            IsListening = false;
            _Log.Write(this, LogLevel.INFO, "HTTPS listener stopping.");
            var wait = new List<Task>();
            var conns = _ConnectedClients.ToArray();
            for (int i = 0; i < conns.Length; i++) {
                var s = conns[i].Key;
                try {
                    if (s.WebSocket != null) {
                        wait.Add(s.WebSocket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, null, new CancellationTokenSource(500).Token));
                    } else
                        s.Dispose();
                } catch { }
                string foo;
                _ConnectedClients.TryRemove(s, out foo);
            }
            if (!Task.WaitAll(wait.ToArray(), 5000)) {
                _Log.Write(this, LogLevel.INFO, "Timed out waiting on all WebSocket closures.");
            }
            if (_ConnectedClients.Count > 0) {
                _Log.Write(this, LogLevel.WARN, "{0} lingering connections after stop.", _ConnectedClients.Count);
            }
            _Listener.Stop();
            if (_Port80Listener != null) {
                _Log.Write(this, LogLevel.INFO, "HTTP Port 80 redirect stopping.");
                _Port80Listener.Stop();
            }
        }

        internal void OnContextClosed(SslWebContext c) {
            string foo;
            _ConnectedClients.TryRemove(c, out foo);
        }

        private async Task HandleClient(TcpClient c) {

            c.SendTimeout = 10000;

            var stream = c.GetStream();

            SslStream ssl = null;

            try {
                ssl = new SslStream(stream,
                   true,
                   OnRemoteCertificateValidation,
                   OnLocalCertificateValidation,
                   EncryptionPolicy.RequireEncryption);

                await ssl.AuthenticateAsServerAsync(_ServerCert, false,
                                                    System.Security.Authentication.SslProtocols.Tls
                                                    | System.Security.Authentication.SslProtocols.Tls11
                                                    | System.Security.Authentication.SslProtocols.Tls12,
                                                    false
                                                   ).ConfigureAwait(false);
            } catch (Exception ex) {
                _Log.Write(this, LogLevel.WARN, "SSL error: " + ex.Message);
                if (ssl != null)
                    ssl.Dispose();
                stream.Dispose();
                return;
            }

            if (!IsListening) {
                stream.Dispose();
                return;
            }

            var line = new StringBuilder();
            var ms = new System.IO.MemoryStream();
            var cancel = new CancellationTokenSource();
            var ctx = new SslWebContext(ssl, c, this, (IPEndPoint)c.Client.RemoteEndPoint);
            _ConnectedClients[ctx] = null;

            while (ssl.IsAuthenticated && c.Connected && ctx.IsConnected) {
                try {
                    var readTask = ssl.ReadAsync(ctx.ReadBuffer, 0, ctx.ReadBuffer.Length, cancel.Token);
                    var timeoutTask = Task.Delay(30 * 1000);
                    await Task.WhenAny(timeoutTask, readTask).ConfigureAwait(false);
                    if (!readTask.IsCompleted) {
                        Debug.WriteLine("Idle connection closing");
                        cancel.Cancel();
                        break;
                    }
                    ctx.ReadLength = readTask.Result;
                } catch {
                    Debug.WriteLine("Client connection error");
                    cancel.Cancel();
                    break;
                }

                if (ctx.ReadLength == 0)
                    break;

                ctx.ReadPos = 0;
                int i = 0;
                for (; i < ctx.ReadLength; i++) {
                    if (ctx.ReadBuffer[i] == 10) { // should be CR LF
                        ms.Write(ctx.ReadBuffer, ctx.ReadPos, i - ctx.ReadPos - 1);
                        line.Append(Encoding.UTF8.GetString(ms.ToArray()));
                        ms.SetLength(0);
                        if (line.Length == 0) {
                            // End of headers

                            if (String.Compare(ctx.RequestHeaders["upgrade"], "websocket", true) == 0) {
                                if (!_AttackPrevention.ShouldDropWebSocketConnection(ctx.RemoteEndPoint.Address)) {
                                    ctx.UpgradeToWebSocket();
                                } else {
                                    Debug.WriteLine("WebSocket rejected " + ctx.RemoteEndPoint.Address);
                                    ctx.Dispose();
                                    break;
                                }
                            }

                            if (ctx.WebSocket != null) {
                                ctx.ReadPos += 2;// skip \r\n
                                Debug.Assert(ctx.ReadPos == ctx.ReadLength);
                                if (await ctx.WebSocket.TryNegotiateAsync(DemandWebSocketProtocol, DemandWebSocketOrigin, cancel.Token)
                                    .ConfigureAwait(false)) {
                                    if (HandleWebSocket != null)
                                        HandleWebSocket?.Invoke(ctx.WebSocket);
                                    else
                                        ctx.Close();
                                    return;
                                }
                            } else {
                                if (HandleHttpRequest != null 
                                    && !String.IsNullOrEmpty(ctx.FirstLine))
                                    await HandleHttpRequest?.Invoke(ctx);
                            }

                            // Next request begins
                            await ctx.Reset().ConfigureAwait(false);
                        } else {
                            // Append header
                            ctx.AppendLine(line.ToString());
                            line.Remove(0, line.Length);
                        }
                        ctx.ReadPos = i + 1;
                    }
                }

                int remainder = i - ctx.ReadPos;
                ms.Write(ctx.ReadBuffer, ctx.ReadPos, remainder);
                ctx.ReadPos += remainder;
            }

            ctx.Close();
#if !DESKTOPCLR
            c.Dispose();
#else
            c.Close();
#endif
        }

        private X509Certificate OnLocalCertificateValidation(object sender, string host, X509CertificateCollection col, X509Certificate cert, string[] acceptableIssuers) {
            return col.Count > 0 ? col[0] : null;
        }

        private bool OnRemoteCertificateValidation(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors) {
            return true;
        }
    }
}