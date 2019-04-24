using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CM.AttackClient {
    /// <summary>
    /// A .NETCore Civil Money client implementation. 
    /// </summary>
    public class PeerConnection : IDisposable {
        System.Net.IPEndPoint _Endpoint;
        CM.Server.Connection _Conn;
        public PeerConnection(System.Net.IPEndPoint ipEndPoint) {
            _Endpoint = ipEndPoint;
        }

        public async Task<bool> ConnectAsync() {
            var sock = await CM.Server.SslWebSocket.TryConnectAsync(System.Net.IPAddress.Any, _Endpoint,
                 DNS.EndpointToUntrustedDomain(_Endpoint.ToString(), false),
                    Constants.WebSocketProtocol, CancellationToken.None);
            if (sock != null) {
                _Conn = new Server.Connection(sock);
                _Conn.ProcessOutboundAsync(OnClosedRemotely);
            }
            return _Conn != null;
        }
        void OnClosedRemotely(Server.Connection conn) {
            Debug.Assert(_Conn == conn);
        }
        public CM.Server.Connection Connection {
            get { return _Conn; }
        }
        public override string ToString() {
            return _Endpoint.ToString() + (_Conn != null ? " (connected)" : " (closed)");
        }

        public void Dispose() {
            _Conn?.Dispose();
        }
    }
}
