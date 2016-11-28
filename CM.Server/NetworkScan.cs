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
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    /// <summary>
    /// A utility for finding all peers on the network
    /// </summary>
    internal static class NetworkScan {
        private static TimeSpan _LastEndTime;

        private static TimeSpan _LastStartTime;

        static NetworkScan() {
            Peers = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        }

        public static bool IsInProgress { get; private set; }

        /// <summary>
        /// The amount time it's taking/taken to scan the entire
        /// network.
        /// </summary>
        public static TimeSpan LastUpdateDuration {
            get {
                if (IsInProgress)
                    return Clock.Elapsed - _LastStartTime;
                else
                    return _LastEndTime - _LastStartTime;
            }
        }

        /// <summary>
        /// A dictionary of peers currently on the network and their successors
        /// </summary>
        public static System.Collections.Concurrent.ConcurrentDictionary<string, string> Peers { get; private set; }

        public static async Task Update(DistributedHashTable dht, CMSeed[] seeds, CancellationToken token) {
            if (IsInProgress)
                return;

            IsInProgress = true;
            _LastStartTime = Clock.Elapsed;

            try {
                var seen = new HashSet<string>();
                var OK = new HashSet<string>();
                var queue = new Stack<string>();
                var originalList = Peers.Keys.ToArray();
                foreach (var seed in seeds) {
                    queue.Push(seed.Domain.ToLower().Trim());
                }

                while (queue.Count > 0 && !token.IsCancellationRequested) {
                    var p = queue.Pop();
                    if (!seen.Contains(p))
                        seen.Add(p);
                    var parts = p.Split(':');
                    var host = parts[0];
                    var port = int.Parse(parts.Length == 2 ? parts[1] : DNS.DEFAULT_PORT);
                    IPAddress[] ips;
                    try {
                        ips = await System.Net.Dns.GetHostAddressesAsync(host);
                        if (ips.Length == 0)
                            continue;
                    } catch {
                        continue;
                    }
                    var ep = new IPEndPoint(ips[0], port);
                    var epString = ep.ToString();

                    Schema.PingResponse pong = null;
                    using (var conn = await dht.Connect(epString)) {
                        if (conn.IsValid) {
                            try {
                                var m = await conn.Connection.SendAndReceive("PING", new Schema.PingRequest());
                                pong = m.Cast<Schema.PingResponse>();
                            } catch {
                            }
                        }
                    }
                    // Make sure the peer knows that he has his correct IP before
                    // we'll consider it valid.
                    if (pong != null
                        && pong.MyIP == ep.Address.ToString()) {
                        Peers[epString] = pong.SuccessorEndpoint;
                        OK.Add(epString);
                        var peerSeen = (pong.Seen ?? string.Empty).Split(',');
                        for (int i = 0; i < peerSeen.Length; i++) {
                            var potential = Helpers.ParseEP(peerSeen[i]);
                            if (potential == null)
                                continue;
                            var str = potential.ToString();
                            if (!seen.Contains(str)) {
                                seen.Add(str);
                                queue.Push(str);
                            }
                        }
                    }
                }

                if (token.IsCancellationRequested)
                    return;

                // Delete any peers that are not on the OK list
                for (int i = 0; i < originalList.Length; i++) {
                    if (!OK.Contains(originalList[i])) {
                        string succ;
                        Peers.TryRemove(originalList[i], out succ);
                    }
                }
            } finally {
                _LastEndTime = Clock.Elapsed;
                IsInProgress = false;
            }
        }
    }
}