#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace CM.Server {
    /// <summary>
    /// Used by the SslWebServer to mitigate dumb-as-bricks attack vectors such as connection flooding.
    /// </summary>
    /// <remarks>
    /// Variables are hard-coded because we may need to be able to tweak these and deploy the
    /// configuration changes to all peers remotely with version updates.
    /// </remarks>
    public class AttackMitigation {

        public int MaxIPConnectionsPerMinute;
        public int MaxIPWebSocketConnectionsPerMinute;
        public Log Log;

        readonly ConcurrentDictionary<IPAddress, IPStat> _Stats = new ConcurrentDictionary<IPAddress, IPStat>();

        internal class IPStat {
            public IPAddress Address;
            public int ConnectionCount;
            public int WebSocketCount;
            public TimeSpan WindowStart;
            public bool HasLogged;

            public IPStat(IPAddress address) {
                Address = address;
                Reset();
            }
            
            public void Reset() {
                ConnectionCount = 0;
                WebSocketCount = 0;
                WindowStart = Clock.Elapsed;
                HasLogged = false;
            }

            public void LogOnce(Log log, string msg) {
                if (!HasLogged) {
                    HasLogged = true;
                    log?.Write(this, LogLevel.INFO, "[" + Address + "] " + msg);
                }
            }
        }
        IPStat FindOrCreateIPStat(IPAddress address) {
            IPStat st;
            if (!_Stats.TryGetValue(address, out st)) {
                st = new IPStat(address);
                _Stats[address] = st;
            }
            return st;
        }
        public bool ShouldDropTcpConnection(IPAddress address) {
            var st = FindOrCreateIPStat(address);
            if ((Clock.Elapsed - st.WindowStart).TotalMinutes < 1) {
                System.Threading.Interlocked.Increment(ref st.ConnectionCount);
            } else {
                st.Reset();
            }
            if (st.ConnectionCount <= MaxIPConnectionsPerMinute)
                return false;
            st.LogOnce(Log, "IP connection banned");
            return true;
        }

        public bool ShouldDropWebSocketConnection(IPAddress address) {
            var st = FindOrCreateIPStat(address);
            if ((Clock.Elapsed - st.WindowStart).TotalMinutes < 1) {
                System.Threading.Interlocked.Increment(ref st.ConnectionCount);
            } else {
                st.ConnectionCount = 0;
                st.WindowStart = Clock.Elapsed;
            }
            if (st.ConnectionCount <= MaxIPConnectionsPerMinute)
                return false;
            st.LogOnce(Log, "WebSocket banned");
            return true;
        }
    }
}
