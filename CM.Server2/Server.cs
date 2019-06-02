using CM.Schema;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    public interface IServer {

        Task ProcessHttpRequest(Microsoft.AspNetCore.Http.HttpContext context, Func<Task> next);
    }

    public class Server : IServer {
        /// <summary>
        /// *.untrusted-server.com wildcard SSL is purposefully disclosed to the public at large. See API docs.
        /// </summary>
        public static readonly string UNTRUSTED_PASSWORD = "12345678";

        internal System.Security.Cryptography.RSAParameters UpdateServerSigningKey;
        private static readonly byte[] _UpdateServerPublicKey = Convert.FromBase64String("tSF5e4oN9Iks8D33utBCxH1zy9mE1FWQ6s0BS0AOG/qPWXNFg2leYVWyxpbHCOP9h95vqc7p4U1B8XDnly1J8MvyZShPZ9cK7+H5tfBsmePY2NhUpNrkEM1l36bHeLIIJCr2ZfyzA7efEhH2y+z2bOhmZIbX3AwmOGDxx54IzJEn05LFvY+TVKBLy0IFpFfuoUSpoorykeZcZ9lB3RCW76WNCXJEfIVA2xcT5YxTRVZjVufDBhMxWHvXr2hVB+bXuyN08SeGfAvT6ZdJtdFUQ+bqS3Il1DwaGjxK8MDbXRFbdtz5yG6F0Z0pRGd2IMa7XmfzrQAhssQoedf2keVBOQ==");
        private static UntrustedNameServer s_DNSServer;
        private Dictionary<string, ProcessRequestDelegate> _Actions;
        private ConcurrentDictionary<Connection, int> _ActiveConnections = new ConcurrentDictionary<Connection, int>();
        private Task _CurrentMaintenanceTask;
        private TimeSpan _LastMaintenanceLoopTime;
        private CancellationTokenSource _ShutdownToken;
        private X509Certificate2 _UntrustedCertificate;

        public Server() {
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
            Log = new Log();
        }

        public string PublicIP { get; private set; }
        internal ServerConfiguration Configuration { get; private set; }
        internal X509Certificate2 CurrentCertificate { get; set; }
        internal DistributedHashTable DHT { get; private set; }

        /// <summary>
        /// Use to track whether or not we are even reachable. We don't want to pester the network
        /// perpetually if nobody can reach us.
        /// </summary>
        internal DateTime LastInboundPing { get; private set; }
        /// <summary>
        /// When this gets high, then something is wrong/hung.
        /// </summary>
        internal TimeSpan LastMaintenanceLoop {
            get { return Clock.Elapsed - _LastMaintenanceLoopTime; }
        }

        internal Log Log { get; private set; }
        internal AuthoritativeDomainReporter Reporter { get; private set; }
        internal Storage Storage { get; private set; }
        internal SynchronisationManager SyncManager { get; private set; }
        public static byte[] GetUntrustedCertificate() {
            var res = "CM.Server.untrusted-server-pass12345678.pfx";
            var ass = System.Reflection.Assembly.GetExecutingAssembly();
            using (var ms = ass.GetManifestResourceStream(res)) {
                var b = new byte[ms.Length];
                ms.Read(b, 0, b.Length);
                return b;
            }
        }

        public async Task ProcessHttpRequest(Microsoft.AspNetCore.Http.HttpContext context, Func<Task> next) {
            try {
                
                if (context.WebSockets.IsWebSocketRequest) {
                    var sock = await context.WebSockets.AcceptWebSocketAsync(Constants.WebSocketProtocol).ConfigureAwait(false);
                    await ProcessWebsocketAsync(sock, context).ConfigureAwait(false);
                } else {
                    if (!context.Request.IsHttps) {
                        context.Response.Redirect("https://" + context.Request.Host.Host + context.Request.Path.Value, permanent: true);
                        return;
                    }

                    Reporter?.LogTelemetry(context);

                    var headers = context.Response.Headers;
                    headers.Add("X-Frame-Options", "DENY");
                    headers.Add("X-XSS-Protection", "1; mode=block");
                    headers.Add("Content-Security-Policy", "default-src 'self'; img-src 'self' data:; script-src 'self' 'unsafe-eval' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; font-src 'self'; connect-src 'self' " + Constants.WebSocketTransport + "://*.untrusted-server.com:* " + Constants.WebSocketTransport + "://*.civil.money:* " + Constants.WebSocketTransport + "://civil.money:* https://*.civil.money:* https://civil.money:*;");

                    await next().ConfigureAwait(false);
                }
            } catch (Microsoft.AspNetCore.Connections.ConnectionResetException) { }
        }

        public void Run(ServerConfiguration config) {
            Configuration = config ?? throw new ArgumentNullException(nameof(config));

            Log.Write(this, LogLevel.INFO, "Starting..");

            if (String.IsNullOrWhiteSpace(config.DataFolder)) {
                Log.Write(this, LogLevel.INFO, "Storage DataFolder cannot be empty.");
                throw new ArgumentException("Storage DataFolder cannot be empty.");
            }

            if (config.Port <= 0 || config.Port > short.MaxValue) {
                Log.Write(this, LogLevel.INFO, "Invalid server port specified.");
                throw new ArgumentException("Invalid server port specified.");
            }

            if (!String.IsNullOrWhiteSpace(config.UpdateServerPrivateKey)) {
                UpdateServerSigningKey = CryptoFunctions.RSAParametersFromKey(_UpdateServerPublicKey,
                    Convert.FromBase64String(config.UpdateServerPrivateKey));
                Log.Write(this, LogLevel.INFO, "Update server signing key applied.");
            }

            IPAddress.TryParse(config.IP ?? "0.0.0.0", out var ip);

            if (Configuration.EnableAuthoritativeDomainFeatures) {
                if (s_DNSServer == null) {
                    s_DNSServer = new UntrustedNameServer(Log, ip);
                    s_DNSServer.Start();
                }
            }

            DHT = new DistributedHashTable(Log);
            DHT.ServicePort = config.Port;
            DHT.ServiceIP = ip;

            var localFolder = config.DataFolder;
            if (localFolder.IndexOf(":") == -1
                && !localFolder.StartsWith("/"))
                localFolder = System.IO.Path.Combine(ServerConfiguration.BaseDirectory, localFolder);

            Log.Write(this, LogLevel.INFO, "Data folder: " + localFolder);

            Storage = new Storage(localFolder, DHT, Log);
            SyncManager = new SynchronisationManager(localFolder, Storage, DHT, Log);
            if (Configuration.EnableAuthoritativeDomainFeatures) {
                Reporter = new AuthoritativeDomainReporter(localFolder, DHT, Storage, Log);
            }

            _UntrustedCertificate = new X509Certificate2(GetUntrustedCertificate(), UNTRUSTED_PASSWORD);

            X509Certificate2 cert = null;
            if (String.IsNullOrEmpty(Configuration.AuthoritativePfxCertificate)) {
                cert = _UntrustedCertificate;
            } else {
                var file = Configuration.AuthoritativePfxCertificate;
                if (file.IndexOf('/') == -1 && file.IndexOf('\\') == -1)
                    file = System.IO.Path.Combine(ServerConfiguration.BaseDirectory, file);
                if (File.Exists(file))
                    cert = new X509Certificate2(file, Configuration.AuthoritativePfxPassword);
            }

            CurrentCertificate = cert;
            if (CurrentCertificate == null) {
                Log.Write(this, LogLevel.WARN, "Starting with no immediate AuthoritativePfxCertificate. It should update in a moment.");
            }

            _ShutdownToken = new CancellationTokenSource();
            MaintenanceLoop();
            WatchdogLoop();
            SyncManager.Start();

            Log.Write(this, LogLevel.INFO, "Running.");

            WebHost.CreateDefaultBuilder()
                  .ConfigureLogging((Microsoft.Extensions.Logging.ILoggingBuilder logBuilder) => {
                          logBuilder.AddFilter(FilterLog);
                     })
                    .UseKestrel(options => {
                        options.AddServerHeader = false;

                        if (config.EnablePort80Redirect) {
                            options.Listen(new IPEndPoint(ip, 80));
                        }
                        options.Listen(new IPEndPoint(ip, config.Port),
                             listenOpens => {
                                 listenOpens.UseHttps(new HttpsConnectionAdapterOptions() {
                                     ServerCertificateSelector = OnSelectCertificate
                                 });
                             });
                    })
                    .ConfigureServices(x => {
                        x.Add(new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof(IServer), this));
                    })
               .UseStartup<Startup>()
               .Build()
               .Run();
        }

        static bool FilterLog(string category, Microsoft.Extensions.Logging.LogLevel logLevel) {
            return (int)logLevel >= (int)Microsoft.Extensions.Logging.LogLevel.Warning;
        }

        private List<CMSeed> BuildSeedsList() {
            var ar = new List<CMSeed>(Constants.Seeds);
            if (!String.IsNullOrWhiteSpace(Configuration.Seeds)) {
                var additionalSeeds = Configuration.Seeds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var entry in additionalSeeds) {
                    if (ar.FirstOrDefault(x => String.Equals(x.Domain, entry, StringComparison.OrdinalIgnoreCase)) == null)
                        ar.Add(new CMSeed(entry, null));
                }
            }

            return ar;
        }

        private async Task CommitItem(Connection conn, Message m) {
            await conn.Reply(m, await Storage.Commit(m.Request.FirstArgument, sendPushNotifications: true)
                .ConfigureAwait(false))
                 .ConfigureAwait(false);
        }

        private async Task FindResponsiblePeer(Connection conn, Message m) {
            var req = m.Cast<FindResponsiblePeerRequest>();
            var res = await DHT.FindResponsiblePeer(req)
                 .ConfigureAwait(false);
            await conn.Reply(m, res.Response.Code, res)
                 .ConfigureAwait(false);
        }

        private async Task GetItem(Connection conn, Message m) {
            var path = m.Request.AllArguments;
            if (String.IsNullOrWhiteSpace(path)) {
                await conn.Reply(m, CMResult.E_Invalid_Request);
                return;
            }
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = null;
            int querIdx = path.IndexOf('?');
            if (querIdx > -1) {
                query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(path.Substring(querIdx + 1));
                path = path.Substring(0, querIdx);
            }

            var status = Storage.Get(path, out var item);
            if (query != null) {
                DateTime calcDate;
                if (item is Account
                    && Helpers.DateFromISO8601(query["calculations-date"], out calcDate)) {
                    var a = item as Account;
                    Storage.FillAccountCalculations(a, calcDate);
                }
            }

            var res = item as Message;

            await conn.Reply(m, status, res).ConfigureAwait(false);
        }

        private async Task ListItems(Connection conn, Message m) {
            var res = Storage.List(m.Cast<ListRequest>(), out var list);
            await conn.Reply(m, res, list).ConfigureAwait(false);
        }

        private async void MaintenanceLoop() {
            var lastSeedCheck = TimeSpan.Zero;
            var lastPingRandomNode = TimeSpan.Zero;
            _LastMaintenanceLoopTime = Clock.Elapsed;
            string lastError = null;
            const int taskTimeout = 60 * 1000;
            // Wait for Kestrel server to start up
            await Task.Delay(5000).ConfigureAwait(false);

            while (true) {
                try {
                    if (DHT.Seen.Count == 0
                        && (lastSeedCheck == TimeSpan.Zero || (Clock.Elapsed - lastSeedCheck).TotalSeconds > 30)) {
                        lastSeedCheck = Clock.Elapsed;
                        await ResolveInitialSeedServers(BuildSeedsList()).ConfigureAwait(false);
                    }

                    _CurrentMaintenanceTask = DHT.Poll();
                    using (var waitCancel = new CancellationTokenSource()) {
                        await Task.WhenAny(_CurrentMaintenanceTask, Task.Delay(taskTimeout, waitCancel.Token));
                        waitCancel.Cancel();
                    }

                    if (!_CurrentMaintenanceTask.IsCompleted) {
                        Log.Write(this, LogLevel.WARN, "DHT Poll timed out.");
                    }

                    _CurrentMaintenanceTask = DHT.TryUpdateOne();
                    using (var waitCancel = new CancellationTokenSource()) {
                        await Task.WhenAny(_CurrentMaintenanceTask, Task.Delay(taskTimeout, waitCancel.Token));
                        waitCancel.Cancel();
                    }

                    if (!_CurrentMaintenanceTask.IsCompleted) {
                        Log.Write(this, LogLevel.WARN, "DHT TryUpdateOne timed out.");
                    }

                    if ((Clock.Elapsed - lastPingRandomNode).TotalMinutes >= 1) {
                        lastPingRandomNode = Clock.Elapsed;

                        _CurrentMaintenanceTask = DHT.PingRandomNode();
                        using (var waitCancel = new CancellationTokenSource()) {
                            await Task.WhenAny(_CurrentMaintenanceTask, Task.Delay(taskTimeout, waitCancel.Token));
                            waitCancel.Cancel();
                        }

                        if (!_CurrentMaintenanceTask.IsCompleted) {
                            Log.Write(this, LogLevel.WARN, "DHT PingRandomNode timed out.");
                        }
                    }

                    _CurrentMaintenanceTask = null;

                    DHT.PerformConnectionHousekeeping();

                    if (PublicIP != DHT.MyIP)
                        PublicIP = DHT.MyIP;

                    if (Storage != null)
                        Storage.PerformHouseKeeping();

                    if (Reporter != null)
                        Reporter.Poll(_ShutdownToken.Token);

                    lastError = null;
                    _LastMaintenanceLoopTime = Clock.Elapsed;
                } catch (Exception ex) {
                    var error = ex.ToString();
                    if (error != lastError) {
                        lastError = error;
                        Log.Write(this, LogLevel.WARN, "MaintenanceLoop error: " + lastError);
                    }
                }
                int sleep = DHT.ArePeersFullyPopulated ? 5000 : 2000;
                await Task.Delay(sleep).ConfigureAwait(false);
            }
        }

        private async Task OnProcessRequest(Connection conn, Message m) {
            try {
                ProcessRequestDelegate del;
                if (_Actions.TryGetValue(m.Request.Action, out del))
                    await del(conn, m).ConfigureAwait(false);
                else {
                    await conn.Reply(m, CMResult.E_Invalid_Action);
                }
            } catch (Exception ex) {
                Log.Write(LogSource.HTTP, LogLevel.FAULT, "ProcessRequest failed {0}. Details in debug.txt", ex.Message);
                System.Diagnostics.Debug.WriteLine("ProcessRequest", ex.ToString() + "\r\n----------- MESSAGE -----------\r\n" + m.RawContent + "\r\n\r\n");
            }
        }

        private X509Certificate2 OnSelectCertificate(Microsoft.AspNetCore.Connections.ConnectionContext e, string host) {
            return host != null && host.EndsWith(".untrusted-server.com", StringComparison.OrdinalIgnoreCase) ? _UntrustedCertificate
                : CurrentCertificate;
        }

        private async Task Ping(Connection conn, Message m) {
            var ping = m.Cast<PingRequest>();

            // Make sure we can reply without any I/O errors before
            // doing anything further with this caller.
            if (await conn.Reply(m, CMResult.S_OK, new PingResponse() {
                YourIP = conn.RemoteEndpoint.Address.ToString(),
                MyIP = DHT.MyIP,
                PredecessorEndpoint = DHT.Predecessor,
                SuccessorEndpoint = DHT.Successor,
                Seen = DHT.GetSeenList(),
            })) {
                LastInboundPing = DateTime.UtcNow;

                // If the ping contains an end-point then the caller is trying
                // to participate in the DHT. Validate its reported IP
                // and consider it as a predecessor.
                var ep = ping.EndPoint;
                if (ep != null) {
                    string ip = conn.RemoteEndpoint.Address.ToString();
                    if (ep.StartsWith(ip + ":")) {
                        DHT.TryAddToSeen(ep);
                        DHT.UpdatePredecessor(ep);
                    }
                }
            }
        }

        private async Task ProcessWebsocketAsync(WebSocket webSocket, Microsoft.AspNetCore.Http.HttpContext context) {
            Connection conn = null;
            try {
                conn = new Connection(this, webSocket, context, OnProcessRequest);
                _ActiveConnections[conn] = 0;
                await conn.ProcessInboundAsync()
                    .ConfigureAwait(false);
            } catch (IOException) {
            } catch (Exception ex) {
                Log.Write(LogSource.HTTP, LogLevel.WARN, "HandleSocket: " + ex.Message);
            } finally {
                if (conn != null) {
                    _ActiveConnections.TryRemove(conn, out _);
                }
            }
        }

        private async Task PutItem(Connection conn, Message m) {
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

            var res = await Storage.Put(item);
            await conn.Reply(m, res.Code, null, res.Token);
        }

        private async Task QueryCommit(Connection conn, Message m) {
            DateTime d;
            var path = m.Request.AllArguments;
            var res = Storage.QueryCommitStatus(path, out d);
            await conn.Reply(m, res, null, (d != DateTime.MinValue ? Helpers.DateToISO8601(d) : null));
        }

        private async Task ResolveInitialSeedServers(List<CMSeed> ar) {
            for (int i = 0; i < ar.Count; i++) {
                var tmp = ar[i].Domain.Split(':');
                var host = tmp[0];
                var port = tmp.Length > 1 ? tmp[1] : (Constants.WebSocketTransport == "wss" ? "443" : "80");
                try {
                    var ip = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                    for (int x = 0; x < ip.Length; x++) {
                        if (ip[x].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                            DHT.TryAddToSeen(ip[x] + ":" + port);
                            break;
                        }
                    }
                } catch { }
            }
            if (DHT.Seen.Count == 0) {
                Log.Write(this, LogLevel.WARN, "No seeds could be resolved.");
            }
        }

        private async Task Subscribe(Connection conn, Message m) {
            Storage.AddSubscription(conn, m.Request.FirstArgument);
            await conn.Reply(m, CMResult.S_OK);
        }

        private async Task Sync(Connection conn, Message m) {
            SyncManager.OnSyncAnnounceReceived(m.Cast<SyncAnnounce>(), conn.RemoteEndpoint.Address.ToString());
            await conn.Reply(m, CMResult.S_OK);
        }

        private async void WatchdogLoop() {
            bool hasWarnedOfNoPredecessor = false;
            while (true) {
                CertificateRenew.TryUpdate(this);

                if (LastMaintenanceLoop.TotalMinutes > 60) {
                    Log.Write(this, LogLevel.WARN, "MaintenanceLoop has been stalled for {0}.", LastMaintenanceLoop);
                }
                await Task.Delay(60000).ConfigureAwait(false);
                if (DHT.Predecessor == null && !hasWarnedOfNoPredecessor) {
                    Log.Write(this, LogLevel.WARN,
                        "### NETWORK PROBLEM ### No predecessor has been established after a minute of running. Please check your NAT/firewall/etc for TCP port {0} access.",
                        this.Configuration.Port);
                    hasWarnedOfNoPredecessor = true;
                }
            }
        }
    }
}