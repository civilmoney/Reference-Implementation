#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

#if DEBUG
#define DEBUG_JS
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {

    /// <summary>
    /// This is the crux of a Civil Money server. It hosts the Distributed Hash Table client, an
    /// HTTPS/TLS web server, a SynchronisationManager, and a LinearHashTable data storage system.
    ///
    /// Optionally it also spins up an AuthoritiveDomainReporter and handles API functions. There are
    /// no security implications in enabling the functions on non-authoritative servers however only
    /// the *.civil.money seeds are currently intended to use these.
    /// </summary>
    public class Server : IDisposable {
        /// <summary>
        /// *.untrusted-server.com wildcard SSL is purposefully disclosed to the public at large. See API docs.
        /// </summary>
        public static readonly string UNTRUSTED_PASSWORD = "12345678";

        internal static UntrustedNameServer DNSServer;
        internal DistributedHashTable DHT;
        internal AuthoritativeDomainReporter Reporter;
        internal Storage Storage;
        internal SynchronisationManager SyncManager;
        private static readonly System.Reflection.Assembly _ThisAssembly;
        private static readonly char[] UnsafePathChars = new char[] { '/', '\\' };
        private static Dictionary<string, WWWResource> _ResourceCache = new Dictionary<string, WWWResource>();
        private static DateTime _ResourcesDate = DateTime.UtcNow;
        private System.Collections.Concurrent.ConcurrentDictionary<Connection, string> _ActiveConnections;
        private RequestHandler _Handler;
        private bool _IsRunning;
        private SslWebServer _Listener;
        private CancellationTokenSource _ShutdownToken;
        private TimeSpan _LastMaintenanceLoopTime;

        /// <summary>
        /// When this gets high, then something is wrong/hung.
        /// </summary>
        internal TimeSpan LastMaintenanceLoop {
            get { return Clock.Elapsed - _LastMaintenanceLoopTime; }
        }

        System.Security.Cryptography.RSAParameters _UpdateServerSigningKey;
        static readonly byte[] _UpdateServerPublicKey = Convert.FromBase64String("tSF5e4oN9Iks8D33utBCxH1zy9mE1FWQ6s0BS0AOG/qPWXNFg2leYVWyxpbHCOP9h95vqc7p4U1B8XDnly1J8MvyZShPZ9cK7+H5tfBsmePY2NhUpNrkEM1l36bHeLIIJCr2ZfyzA7efEhH2y+z2bOhmZIbX3AwmOGDxx54IzJEn05LFvY+TVKBLy0IFpFfuoUSpoorykeZcZ9lB3RCW76WNCXJEfIVA2xcT5YxTRVZjVufDBhMxWHvXr2hVB+bXuyN08SeGfAvT6ZdJtdFUQ+bqS3Il1DwaGjxK8MDbXRFbdtz5yG6F0Z0pRGd2IMa7XmfzrQAhssQoedf2keVBOQ==");

        static Server() {
            _ThisAssembly = typeof(Server).GetTypeInfo().Assembly;
        }

        public Server() {
            Configuration = new ServerConfiguration();
            _ActiveConnections = new System.Collections.Concurrent.ConcurrentDictionary<Connection, string>();
            _Handler = new RequestHandler(this);
            Log = new Log(this);
        }

        public ServerConfiguration Configuration { get; set; }

        public Log Log { get; private set; }
        public Func<string> OnHostLogExportRequested { get; set; }
        public Action OnRestartRequested { get; set; }
        public string PublicIP { get; private set; }

        public static void DebugDump(string title, string data) {
            try {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Program.BaseDirectory, "debug.txt"),
                    DateTime.UtcNow.ToString("s") + ": " + title
                    + "\r\n-----\r\n" + data + "\r\n-----\r\n\r\n");
                Console.WriteLine("Debug dumped: " + title);
            } catch { }
        }

        /// <summary>
        /// The *.untrusted-server.com wild-card certificate is INTENTIONALLY publicly disclosed. We
        /// never trust the data coming from these anonymous servers but we do need TLS transport to
        /// work correctly in web browsers and mobile devices. SSL/TLS is not used for data secrecy
        /// in Civil Money (there is none) but only for browser compliance and having some sort of
        /// minimal protection around html/JavaScript source code integrity. This would be a
        /// non-issue for signed 'native' mobile implementations, but even mobile platforms are
        /// beginning to demand TLS for all network connectivity.
        /// </summary>
        public static byte[] GetUntrustedCertificate() {
            var res = "CM.Server.untrusted-server-pass12345678.pfx";
            var names = _ThisAssembly.GetManifestResourceNames();

            using (var ms = _ThisAssembly.GetManifestResourceStream(res)) {
                var b = new byte[ms.Length];
                ms.Read(b, 0, b.Length);
                return b;
            }
        }

        public void Dispose() {
            Reporter?.Dispose();
            SyncManager?.Dispose();
            Storage?.Dispose();
        }

        public void Start() {
            if (_Listener != null)
                return;
            _ShutdownToken = new CancellationTokenSource();
            Log.Write(this, LogLevel.INFO, "Starting..");
            if (String.IsNullOrWhiteSpace(Configuration.DataFolder)) {
                Log.Write(this, LogLevel.INFO, "Storage DataFolder cannot be empty.");
                throw new ArgumentException("Storage DataFolder cannot be empty.");
            }
            if (Configuration.Port <= 0 || Configuration.Port > short.MaxValue) {
                Log.Write(this, LogLevel.INFO, "Invalid server port specified.");
                throw new ArgumentException("Invalid server port specified.");
            }

            System.Security.Cryptography.X509Certificates.X509Certificate2 cert;
            if (String.IsNullOrEmpty(Configuration.AuthoritativePfxCertificate)) {
                cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                   GetUntrustedCertificate(), UNTRUSTED_PASSWORD);
            } else {
                var file = Configuration.AuthoritativePfxCertificate;
                if (file.IndexOf('/') == -1 && file.IndexOf('\\') == -1)
                    file = System.IO.Path.Combine(Program.BaseDirectory, file);
                cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
                   file, Configuration.AuthoritativePfxPassword);
            }

#if !DESKTOPCLR
            using (var chain = new X509Chain()) {
                bool chainRes = chain.Build(cert);
                Debug.WriteLine("Certificate chain: {0}, OK: {1}", chain.ChainElements.Count, chainRes);
                for (int i = 0; i < chain.ChainElements.Count; i++)
                    Debug.WriteLine("{0}: {1}", i, chain.ChainElements[i].Certificate?.Subject);
            }
#endif

            if (!String.IsNullOrWhiteSpace(Configuration.UpdateServerPrivateKey)) {
                _UpdateServerSigningKey = CryptoFunctions.RSAParametersFromKey(_UpdateServerPublicKey,
                    Convert.FromBase64String(Configuration.UpdateServerPrivateKey));
                Log.Write(this, LogLevel.INFO, "Update server signing key applied.");
            }

            DHT = new DistributedHashTable(Log);
            DHT.ServicePort = Configuration.Port;

            var localFolder = Configuration.DataFolder;
            if (localFolder.IndexOf(":") == -1
                && !localFolder.StartsWith("/"))
                localFolder = System.IO.Path.Combine(Program.BaseDirectory, localFolder);
            Log.Write(this, LogLevel.INFO, "Data folder: " + localFolder);
            Storage = new Storage(localFolder, DHT, Log);
            SyncManager = new SynchronisationManager(localFolder, Storage, DHT, Log);
            if (Configuration.EnableAuthoritativeDomainFeatures) {
                if (DNSServer == null) {
                    DNSServer = new UntrustedNameServer(Log);
                    DNSServer.Start();
                }
                Reporter = new AuthoritativeDomainReporter(localFolder, DHT, Storage, Log);
            }
            var prevention = new AttackMitigation();
            prevention.Log = Log;
#if DEBUG
            // These features are disabled during debug since many servers all run 
            // off the same IP.
            prevention.MaxIPConnectionsPerMinute = int.MaxValue;
            prevention.MaxIPWebSocketConnectionsPerMinute = int.MaxValue;
#else
            if (Configuration.EnableAuthoritativeDomainFeatures) {
                // For authoritative web servers allow 1 visitor per IP/second:
                // HTML Page + CSS + JavaScript x2 + WebSocket
                prevention.MaxIPConnectionsPerMinute = 5 * 60;
                prevention.MaxIPWebSocketConnectionsPerMinute = 60;
            } else {
                // For regular peers, allow 1 TCP connection per second
                // and one WebSocket every 2 seconds
                prevention.MaxIPConnectionsPerMinute = 60;
                prevention.MaxIPWebSocketConnectionsPerMinute = 30;
            }
#endif

            // Originally this was using the .NET HttpListener, but unfortunately
            // Mono's implementation turned out to be less than stable and missing
            // critical SSL and secure WebSocket capabilities. And then anyway I
            // changed to .NET Core runtime instead of mono in the end.
            _Listener = new SslWebServer(cert, Configuration.Port, Log, prevention);
            _Listener.DemandWebSocketProtocol = Constants.WebSocketProtocol;
            _Listener.HandleHttpRequest = _Listener_HandleHttpRequest;
            _Listener.HandleWebSocket = _Listener_HandleWebSocket;
            _Listener.Start();
            if (Configuration.EnablePort80Redirect)
                _Listener.StartPort80Redirect();
            _IsRunning = true;
            MaintenanceLoop();
            WatchdogLoop();
            SyncManager.Start();
            Log.Write(this, LogLevel.INFO, "Running.");
        }



        public void Stop() {
            _IsRunning = false;
            _ShutdownToken?.Cancel();
            _Listener.Stop();
            _Listener = null;
            if (Storage != null)
                Storage.Dispose();
            if (SyncManager != null)
                SyncManager.Stop();
        }

        private static WWWResource GetWWWResource(string name) {
            WWWResource r;
            while (!Monitor.TryEnter(_ResourceCache, 100))
                System.Threading.Thread.Sleep(1);
            try {
                if (_ResourceCache.TryGetValue(name, out r))
                    return r;
                try {
                    var names = _ThisAssembly.GetManifestResourceNames();
                    var res = "CM.Server." + name;
                    foreach (var n in names) {
                        // we want case-insensitive for resource names..
                        if (String.Compare(n, res, true) == 0)
                            using (var ms = _ThisAssembly.GetManifestResourceStream(n)) {
                                var b = new byte[ms.Length];
                                ms.Read(b, 0, b.Length);
                                ms.Dispose();
                                r = new WWWResource();
                                r.MIME = name.EndsWith(".js") ? "application/javascript; charset=utf-8"
                                    : name.EndsWith(".woff") ? "font/x-woff"
                                    : name.EndsWith(".ico") ? "image/x-icon"
                                    : name.EndsWith(".css") ? "text/css"
                                    : name.EndsWith(".svg") ? "image/svg+xml"
                                    : "text/html; charset=utf-8";

                                b = RemoveBOM(b);

                                r.Data = b;
                                if (name.EndsWith(".htm")) {
                                    var html = Encoding.UTF8.GetString(r.Data);
                                    // integrity
                                    string[] resources = new string[] {
                                        "cm.css", "webworkers.js", "cm.js",
                                        "cm.min.css", "cm.min.js"
                                    };
                                    using (var sha = System.Security.Cryptography.SHA384.Create()) {
                                        for (int i = 0; i < resources.Length; i++) {
                                            var att = "\"/" + resources[i] + "\"";
                                            if (html.IndexOf(att) > -1) {
                                                var hash = sha.ComputeHash(GetWWWResource(resources[i]).Data);
                                                html = html.Replace(att, att + " integrity=\"sha384-" + Convert.ToBase64String(hash) + "\"");
                                            }
                                        }
                                    }
                                    r.Data = Encoding.UTF8.GetBytes(html);
                                }
                            }
                    }
                } catch {
                    r = null;
                }
                _ResourceCache[name] = r;
            } finally {
                Monitor.Exit(_ResourceCache);
            }
            return r;
        }

        /// <summary>
        /// For live debugging only -- finds the CM.Javascript/www project folder and serves up
        /// a file. For production we always have resources embedded in the .dll to reduce
        /// deployment complexity.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        private static WWWResource GetWWWResourceDevelopment(string name) {
            try {
                if (name.IndexOf("..") > -1 || name.IndexOf("\\") > -1 || name.IndexOf("/") > -1)
                    throw new ArgumentException(); // just in case this gets left in somehow

                var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
                // Essentially: ../../../../CM.Javascript/www
                var path = Path.Combine(Path.Combine(dir.Parent.Parent.Parent.Parent.FullName, "CM.Javascript"), "www");
                var file = Path.Combine(path, name);
                if (!System.IO.File.Exists(file))
                    return null;
                var r = new WWWResource();
                r.MIME = name.EndsWith(".js") ? "application/javascript; charset=utf-8"
                    : name.EndsWith(".woff") ? "font/x-woff"
                    : name.EndsWith(".ico") ? "image/x-icon"
                    : name.EndsWith(".svg") ? "image/svg+xml"
                    : name.EndsWith(".css") ? "text/css"
                    : "text/html; charset=utf-8";

                r.Data = RemoveBOM(System.IO.File.ReadAllBytes(file));
                if (name.EndsWith(".htm")) {
                    var html = Encoding.UTF8.GetString(r.Data);
                    // integrity
                    string[] resources = new string[] {
                        "cm.css", "webworkers.js", "cm.js",
                        "cm.min.css", "cm.min.js"
                    };
                    using (var sha = System.Security.Cryptography.SHA384.Create()) {
                        for (int i = 0; i < resources.Length; i++) {
                            var att = "\"/" + resources[i] + "\"";
                            if (html.IndexOf(att) > -1) {
                                var hash = sha.ComputeHash(RemoveBOM(System.IO.File.ReadAllBytes(Path.Combine(path, resources[i]))));
                                html = html.Replace(att, att + " integrity=\"sha384-" + Convert.ToBase64String(hash) + "\"");
                            }
                        }
                    }
                    r.Data = Encoding.UTF8.GetBytes(html);
                }
                return r;
            } catch { }
            return null;
        }

        private static byte[] RemoveBOM(byte[] b) {
            if (b.Length >= 3 // Skip the UTF8 BOM, there are no byte order marks in HTTP
                && b[0] == 239
                && b[1] == 187
                && b[2] == 191) {
                b = b.Skip(3).ToArray();
            }
            return b;
        }

        private async Task _Listener_HandleHttpRequest(SslWebContext context) {
            try {
#if DEBUG
                Log.Write(this, LogLevel.INFO, "HTTP [" + context.RequestHeaders["host"] + "]: " + context.FirstLine);
#endif
                var path = context.Url.AbsolutePath;
                var args = path.Substring(1).Split('/');


                bool isAPIRequest = args[0] == "api" && args.Length > 1;

                if (isAPIRequest) {
                    if (Reporter != null) {
                        await HandleApiQuery(context).ConfigureAwait(false);
                        return;
                    }
                } else {
                    var file = args[0];
                    bool shouldSendSecurityHeaders = false;
                    if (!file.EndsWith(".js")
                       && !file.EndsWith(".ico")
                       && !file.EndsWith(".svg")
                       && !file.EndsWith(".woff")
                       && !file.EndsWith(".css")) {

                        // This is a landing page request. The path may be anything from a user account,
                        // to a voting screen. It is up to JavaScript to present the correct content.

                        shouldSendSecurityHeaders = true;
#if DEBUG_JS
                        file = "debug.htm";
#else
                        file = "default.htm";
#endif
                        // If we are an authoritative https://civil.money server, log some basic
                        // telemetry during the pilot phase, so we can gauge when/if the project
                        // becomes popular.
                        Reporter?.LogTelemetry(context);
                    }

#if DEBUG_JS
                    // For development testing - send files directly from disk
                    var res = GetWWWResourceDevelopment(file);
#else
                    // For production, our resources are always embedded in the .dll
                    var res = GetWWWResource(file);
#endif

                    if (res != null) {
#if !DEBUG_JS
                        DateTime modifiedSince;
                        if (DateTime.TryParse(context.RequestHeaders["If-Modified-Since"] ?? String.Empty,
                            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out modifiedSince)) {
                            if ((_ResourcesDate - modifiedSince).TotalSeconds < 1) {
                                await context.ReplyAsync(HttpStatusCode.NotModified).ConfigureAwait(false);
                                return;
                            }
                        }
#endif
                        var data = res.Data;
                        var headers = new Dictionary<string, string>  {
                                { "Content-Type", res.MIME},
                                { "Last-Modified", _ResourcesDate.ToString("ddd, d MMM yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + " GMT" },
                            };
                        if (shouldSendSecurityHeaders) {
                            headers.Add("X-Frame-Options", "DENY");
                            headers.Add("X-XSS-Protection", "1; mode=block");
                            headers.Add("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-eval' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; connect-src 'self' " + Constants.WebSocketTransport + "://*.untrusted-server.com:* " + Constants.WebSocketTransport + "://*.civil.money:* " + Constants.WebSocketTransport + "://civil.money:* https://*.civil.money:* https://civil.money:*;");
                        }

                        // Shouldn't really use compression over TLS as it leaks entropy bits, but we have no 
                        // secrets under CM and so don't care if the connection is decrypted..
                        var encoding = context.RequestHeaders["Accept-Encoding"] ?? String.Empty;
                        if (encoding.IndexOf("gzip") > -1) {
                            if (res.DataGZip == null) {
                                using (var ms = new System.IO.MemoryStream()) {
                                    using (var g = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Compress, true)) {
                                        g.Write(data, 0, data.Length);
                                        g.Flush();
                                    }
                                    res.DataGZip = ms.ToArray();
                                }
                            }
                            data = res.DataGZip;
                            headers["Content-Encoding"] = "gzip";
                        } else if (encoding.IndexOf("deflate") > -1) {
                            if (res.DataDeflate == null) {
                                using (var ms = new System.IO.MemoryStream()) {
                                    using (var g = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true)) {
                                        g.Write(data, 0, data.Length);
                                        g.Flush();
                                    }
                                    res.DataDeflate = ms.ToArray();
                                }
                            }
                            data = res.DataDeflate;
                            headers["Content-Encoding"] = "deflate";
                        }
                        headers["Content-Length"] = data.Length.ToString();
                        if (context.Method == "HEAD")
                            data = null;

                        await context.ReplyAsync(HttpStatusCode.OK, headers, data, CancellationToken.None).ConfigureAwait(false);

                        return;
                    }
                }

                await context.ReplyAsync(HttpStatusCode.NotFound).ConfigureAwait(false);
            } catch (Exception ex) {
                Log.Write(this, LogLevel.INFO, "HTTP client glitch: {0}", ex);
            }
        }

        private async void _Listener_HandleWebSocket(SslWebSocket webSocketContext) {
            Connection conn = null;
            try {
                conn = new Connection(webSocketContext, _Handler.ProcessRequest);
                _ActiveConnections[conn] = null;
                await conn.ProcessInboundAsync().ConfigureAwait(false);
            } catch (IOException) {
            } catch (Exception ex) {
                Log.Write(this, LogLevel.WARN, "HandleSocket: " + ex.Message);
            } finally {
                if (conn != null) {
                    string status;
                    _ActiveConnections.TryRemove(conn, out status);
                }
            }
        }

        private async Task HandleApiAptRepo(SslWebContext context) {
            // For an APT repo just need to serve a couple of signed files as generated
            // by reprepro.
            var path = context.Url.AbsolutePath;
            path = path.Substring("/api/get-repo/".Length);
            if (path.IndexOf("..") > -1) {
                // unsafe
                await context.ReplyAsync(HttpStatusCode.NotFound, null, "Not found").ConfigureAwait(false);
                return;
            }
            path = path.Replace('/', Path.DirectorySeparatorChar);

            var baseFolder = Path.Combine(Path.Combine(Program.BaseDirectory, "releases"), "repository");
            var localPath = Path.Combine(baseFolder, path);
            if (System.IO.File.Exists(localPath)) {
                using (var fs = new System.IO.FileStream(localPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read)) {
                    await context.ReplyAsync(HttpStatusCode.OK,
                        new Dictionary<string, string> { }, fs)
                        .ConfigureAwait(false);
                }
            } else {

                await context.ReplyAsync(HttpStatusCode.NotFound, null, "Not found")
                                            .ConfigureAwait(false);
            }
        }

        private async Task HandleApiGetReleaseFile(SslWebContext context, string netCoreVersion, string application, string versionString, IEnumerable<string> file) {
            switch (application) {
                case "server": {
                        var version = Version.Parse(versionString); // Validation
                        var baseFolder = Path.Combine(Program.BaseDirectory, "releases");
                        var localPath = baseFolder;
                        localPath = Path.Combine(localPath, netCoreVersion);
                        localPath = Path.Combine(localPath, application);
                        localPath = Path.Combine(localPath, version.ToString());
                        foreach (var part in file)
                            localPath = Path.Combine(localPath, part);
                        if (!localPath.StartsWith(baseFolder)) {
                            Log.Write(this, LogLevel.WARN, "Bad file path requested: {0}/{1}/{2}",
                                application,
                                versionString,
                                String.Join("/", file));
                            await context.ReplyAsync(HttpStatusCode.BadRequest)
                                .ConfigureAwait(false);
                            return;
                        }
                        using (var fs = new System.IO.FileStream(localPath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read)) {
                            await context.ReplyAsync(HttpStatusCode.OK, new Dictionary<string, string> {
                                    { "Content-Type", "application/octet-stream" }
                                }, fs)
                                .ConfigureAwait(false);
                        }
                    }
                    return;
            }
            await context.ReplyAsync(HttpStatusCode.NotFound)
                .ConfigureAwait(false);
        }


        private async Task HandleApiGetReleaseInfo(SslWebContext context, string netcoreAppVersion, string application) {
            if (application.IndexOfAny(UnsafePathChars) > -1) {
                await context.ReplyAsync(HttpStatusCode.NotFound)
                    .ConfigureAwait(false);
                return;
            }
            // Releases are kept in https://civil.money/releases/netcoreappX.X/application-name/1.x.x
            var folder = Path.Combine(Program.BaseDirectory, "releases");
            folder = Path.Combine(folder, netcoreAppVersion);
            folder = Path.Combine(folder, application);
            var versionFolders = Directory.GetDirectories(folder);
            var versions = new List<Version>(versionFolders.Length);
            for (int i = 0; i < versionFolders.Length; i++) {
                Version v;
                // During uploads, we'll temporarily name folders in such a way that Version.Parse
                // will fail, and only commit the upload (rename the folder to 1.x.x) once files have
                // all been checked.
                if (!Version.TryParse(Path.GetFileName(versionFolders[i]), out v))
                    continue;
                versions.Add(v);
            }

            if (versions.Count == 0) {
                await context.ReplyAsync(HttpStatusCode.NotFound)
                    .ConfigureAwait(false);
                return;
            }

            versions.Sort();
            var version = versions[versions.Count - 1];

            var versionFolder = Path.Combine(folder, version.ToString());

            Debug.Assert(System.IO.Directory.Exists(versionFolder));

            var files = System.IO.Directory.GetFileSystemEntries(versionFolder);
            var s = new StringBuilder();
            for (int i = 0; i < files.Length; i++) {
                var info = new System.IO.FileInfo(files[i]);
                Debug.Assert(info.Exists);
                // We want "1.0.0/CM.Server.dll"
                var relativePath = files[i].Substring(folder.Length).Substring(1);
                //string sha256 = null;
                //using (var stream = info.OpenRead())
                //using (var sha = System.Security.Cryptography.SHA256.Create()) {
                //    sha256 = Convert.ToBase64String(sha.ComputeHash(stream));
                //}
                string signature = null;
                using (var stream = info.OpenRead())
                using (var rsa = System.Security.Cryptography.RSA.Create()) {
                    rsa.ImportParameters(_UpdateServerSigningKey);
                    signature = Convert.ToBase64String(rsa.SignData(stream, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1));
                }

                s.CRLF(String.Format("{0} {1} {2}", relativePath.Replace("\\", "/").Replace(" ", "%20"), info.Length.ToString(), signature));
            }

            await context.ReplyAsync(HttpStatusCode.OK, null, s.ToString())
                .ConfigureAwait(false);
        }

        private async Task HandleApiLogError(SslWebContext context, string application) {
            if (context.ContentLength == 0) {
                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected an error body")
                    .ConfigureAwait(false);
                return;
            }
            var sender = context.RemoteEndPoint.Address;
            var file = Path.Combine(Program.BaseDirectory, "remote-errors.txt");
            try {
                Log.Write(this, LogLevel.INFO, "Remote error received from " + sender + " (" + context.ContentLength + ")");
                var report = await context.ReadRequestAsUtf8String()
                    .ConfigureAwait(false);
                System.IO.File.AppendAllText(file,
                    DateTime.Now.ToString("s") + " [" + sender + ", " + application + "]: "
                    + report
                    + "\r\n=================\r\n");

                await context.ReplyAsync(HttpStatusCode.OK)
                    .ConfigureAwait(false);
            } catch (Exception ex) {
                Log.Write(this, LogLevel.INFO, "Remote error from " + sender + " failed to log: " + ex.ToString() + "\r\n" + context.RequestHeaders.ToContentString());
            }
        }

        private async Task HandleApiLogTelem(SslWebContext context, string typeName) {

            if (typeName != "http") { // sanitation/validation
                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Invalid telemetry type")
                   .ConfigureAwait(false);
                return;
            }
            if (context.ContentLength == 0) {
                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected an error body")
                    .ConfigureAwait(false);
                return;
            }
            var sender = context.RemoteEndPoint.Address;

            // We'll log telemetry by UTC day
            var file = Path.Combine(Program.BaseDirectory, "telemetry");
            if (!Directory.Exists(file))
                Directory.CreateDirectory(file);
            file = Path.Combine(file, typeName + "-telem-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".txt");
            try {
                Log.Write(this, LogLevel.INFO, "Remote telemetry received from " + sender + " (" + context.ContentLength + ")");
                var report = await context.ReadRequestAsUtf8String()
                    .ConfigureAwait(false);
                System.IO.File.AppendAllText(file,
                    "==== " + DateTime.Now.ToString("s") + " [" + sender + "] ====\r\n"
                    + report
                    + "\r\n=================\r\n");

                await context.ReplyAsync(HttpStatusCode.OK)
                    .ConfigureAwait(false);
            } catch (Exception ex) {
                Log.Write(this, LogLevel.INFO, "Remote telemetry from " + sender + " failed to log: " + ex.ToString() + "\r\n" + context.RequestHeaders.ToContentString());
            }
        }

        /// <summary>
        /// Responds to https://civil.money/api/* calls.
        /// </summary>
        private async Task HandleApiQuery(SslWebContext context) {
            // /api/{command}/{whatever}?querystring
            try {
                var parts = context.Url.AbsolutePath.Substring(1).Split('/');
                if (parts.Length > 1) {
                    switch (parts[1]) {
                        // GET api/get-revenue/CA-NS
                        // -> { "count":"0", "revenue":"0", "lastUpdatedUtc":"date" }
                        case "get-revenue":
                            if (parts.Length < 3)
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected region in path")
                                    .ConfigureAwait(false);
                            else
                                await Reporter.HandleApiGetTaxRevenue(context, parts[2])
                                    .ConfigureAwait(false);
                            return;
                        // GET api/get-revenue-data/CA-NS?from={from}&to={to}&startat=0&max=1000
                        // -> {Updated Utc} {Created Utc} {Payee} {Payer} {Revenue} {NT|OK}\r\n
                        case "get-revenue-data":
                            if (parts.Length < 3)
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected region in path")
                                    .ConfigureAwait(false);
                            else
                                await Reporter.HandleApiGetTaxData(context, parts[2])
                                    .ConfigureAwait(false);
                            return;
                        // GET api/get-release-info/netcoreapp{version}/{app name}
                        // -> 1.0.0/{file} {size} {sha256-base64}
                        //    1.0.0/some%20resource/{file} {size} {sha256-base64}
                        case "get-release-info":
                            if (_UpdateServerSigningKey.D == null)
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "This server does not support updates.")
                                    .ConfigureAwait(false);
                            else if (parts.Length < 3)
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected application name")
                                    .ConfigureAwait(false);
                            else
                                await HandleApiGetReleaseInfo(context, parts[2], parts[3])
                                    .ConfigureAwait(false);
                            return;
                        // GET api/get-repo/*
                        case "get-repo":
                            await HandleApiAptRepo(context)
                                    .ConfigureAwait(false);
                            return;
                        // GET api/get-release-file/netcoreapp{version}/{app name}/{version}/path/to/file.dll
                        // -> binary
                        case "get-release-file":
                            if (parts.Length < 5)
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected application name/version/file.")
                                    .ConfigureAwait(false);
                            else
                                await HandleApiGetReleaseFile(context, parts[2], parts[3], parts[4], parts.Skip(5))
                                    .ConfigureAwait(false);
                            return;
                        // POST api/log-error/{app name}
                        case "log-error":
                            if (context.Method != "POST")
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected POST")
                                    .ConfigureAwait(false);
                            else if (parts.Length < 3)
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected application name")
                                    .ConfigureAwait(false);
                            else
                                await HandleApiLogError(context, parts[2])
                                    .ConfigureAwait(false);
                            return;
                        // POST api/log-telem/{type name}
                        case "log-telem":
                            if (context.Method != "POST")
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected POST")
                                    .ConfigureAwait(false);
                            else if (parts.Length < 3)
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected telemetry type name")
                                    .ConfigureAwait(false);
                            else
                                await HandleApiLogTelem(context, parts[2])
                                    .ConfigureAwait(false);
                            return;
                        // GET api/get-propositions
                        // -> json of VotingProposition[]
                        case "get-propositions":
                            await Reporter.HandleApiGetVotingPropositions(context)
                                    .ConfigureAwait(false);
                            return;
                        // GET api/get-propositions
                        // -> json of VotingProposition[]
                        case "get-vote-data":
                            await Reporter.HandleApiGetVoteData(context)
                                    .ConfigureAwait(false);
                            return;
                        // GET api/get-random?length=int
                        case "get-random":
                            uint len;
                            if (uint.TryParse(context.QueryString["length"], out len)
                                && len < 4096
                                && len > 0) {
                                var b = new byte[len];
                                CryptoFunctions.RNG(b);
                                await context.ReplyAsync(HttpStatusCode.OK, null, Convert.ToBase64String(b)).ConfigureAwait(false);
                            } else {
                                await context.ReplyAsync(HttpStatusCode.BadRequest, null, "Expected a length parameter between 1 and 4096.").ConfigureAwait(false);
                            }
                            break;
                    }
                }

                await context.ReplyAsync(HttpStatusCode.NotFound).ConfigureAwait(false);
            } catch (Exception ex) {
                Log.Write(this, LogLevel.FAULT, ex.ToString());
                try {
                    await context.ReplyAsync(HttpStatusCode.InternalServerError).ConfigureAwait(false);
                } catch { }
            }
        }

        async void WatchdogLoop() {
            bool hasWarnedOfNoPredecessor = false;
            while (_IsRunning) {
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
        Task _CurrentMaintenanceTask;

        List<CMSeed> BuildSeedsList() {
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

        private async void MaintenanceLoop() {
            var lastSeedCheck = TimeSpan.Zero;
            var lastPingRandomNode = TimeSpan.Zero;
            _LastMaintenanceLoopTime = Clock.Elapsed;
            string lastError = null;
            const int taskTimeout = 60 * 1000;


            while (_IsRunning) {
                try {

                    if (DHT.Seen.Count == 0
                        && (lastSeedCheck == TimeSpan.Zero || (Clock.Elapsed - lastSeedCheck).TotalSeconds > 30)) {
                        lastSeedCheck = Clock.Elapsed;
                        await ResolveInitialSeedServers(BuildSeedsList()).ConfigureAwait(false);
                    }

                    _CurrentMaintenanceTask = DHT.Poll(); //await DHT.Poll().ConfigureAwait(false);
                    using (var waitCancel = new CancellationTokenSource()) {
                        await Task.WhenAny(_CurrentMaintenanceTask, Task.Delay(taskTimeout, waitCancel.Token));
                        waitCancel.Cancel();
                    }
                    if (!_CurrentMaintenanceTask.IsCompleted) {
                        Log.Write(this, LogLevel.WARN, "DHT Poll timed out.");
                    }

                    //await DHT.TryUpdateOne().ConfigureAwait(false);
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
                        //await DHT.PingRandomNode().ConfigureAwait(false);
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
            Log.Write(this, LogLevel.INFO, "MaintenanceLoop stopped.");
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

        private class WWWResource {
            public byte[] Data;
            public byte[] DataDeflate;
            public byte[] DataGZip;
            public string MIME;
        }
    }
}