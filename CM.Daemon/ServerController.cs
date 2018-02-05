#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Daemon {

    /// <summary>
    /// Starts/Stops/Monitors the CM.Server process.
    /// </summary>
    public class ServerController {
        private const string NETCOREVERSION = "netcoreapp2.0";
        private const string AUTHORITATIVE_DOMAIN = "update.civil.money";//"192-168-0-88-8000.untrusted-server.com:8000";
        private bool _HasCrashed;
        private bool _IsExpectingExit;
        private bool _IsRunning;
        private int _LogIndex;
        private string[] _LogLines = new string[1024];
        private System.IO.StreamWriter _LogWriter;
        private Thread _MonitorThread;
        private Process _Process;
        private ProcessStatistics _Stats;

        static readonly System.Security.Cryptography.RSAParameters _UpdateServerPublicKey = new System.Security.Cryptography.RSAParameters() {
            Modulus = Convert.FromBase64String("tSF5e4oN9Iks8D33utBCxH1zy9mE1FWQ6s0BS0AOG/qPWXNFg2leYVWyxpbHCOP9h95vqc7p4U1B8XDnly1J8MvyZShPZ9cK7+H5tfBsmePY2NhUpNrkEM1l36bHeLIIJCr2ZfyzA7efEhH2y+z2bOhmZIbX3AwmOGDxx54IzJEn05LFvY+TVKBLy0IFpFfuoUSpoorykeZcZ9lB3RCW76WNCXJEfIVA2xcT5YxTRVZjVufDBhMxWHvXr2hVB+bXuyN08SeGfAvT6ZdJtdFUQ+bqS3Il1DwaGjxK8MDbXRFbdtz5yG6F0Z0pRGd2IMa7XmfzrQAhssQoedf2keVBOQ=="),
            Exponent = new byte[] { 1, 0, 1 }
        };

        public ServerController() {
            _LogWriter = new System.IO.StreamWriter(
                    new System.IO.FileStream(System.IO.Path.Combine(AppContext.BaseDirectory, "log.txt"),
                    System.IO.FileMode.OpenOrCreate,
                    System.IO.FileAccess.ReadWrite,
                    System.IO.FileShare.ReadWrite));
            _LogWriter.BaseStream.SetLength(0);
        }

        public async Task<bool> CheckForUpdate(CancellationToken token) {
            Log("Checking for updates..");

            string versionInfo = null;

            try {
                var req = System.Net.HttpWebRequest.CreateHttp("https://" + AUTHORITATIVE_DOMAIN + "/api/get-release-info/" + NETCOREVERSION + "/server");
                var res = await req.GetResponseAsync() as System.Net.HttpWebResponse;
                if (res.StatusCode != System.Net.HttpStatusCode.OK) {
                    Log("Update check received status code {0}", res.StatusCode);
                    return false;
                }
                using (var sr = new System.IO.StreamReader(res.GetResponseStream(), System.Text.Encoding.UTF8))
                    versionInfo = sr.ReadToEnd();
            } catch (Exception ex) {
                Log("Update check failed: {0}", ex.Message);
                return false;
            }

            token.ThrowIfCancellationRequested();

            var lines = versionInfo.Split('\n');
            var queue = new List<AppFile>();
            Version currentVersion = null;
            for (int i = 0; i < lines.Length; i++) {
                var line = lines[i].Trim();
                if (line.Length == 0)
                    continue;
                try {
                    var ar = line.Split(' ');

                    var relativePath = System.Net.WebUtility.UrlDecode(ar[0]);
                    var length = long.Parse(ar[1]);
                    var signature = ar[2];

                    var pathPaths = relativePath.Split('/');
                    var version = Version.Parse(pathPaths[0]);

                    if (currentVersion == null)
                        currentVersion = version;

                    if (currentVersion != version)
                        throw new Exception("Unexpected version.");

                    var localPath = AppContext.BaseDirectory;
                    for (int x = 0; x < pathPaths.Length; x++)
                        localPath = System.IO.Path.Combine(localPath, pathPaths[x]);
                    if (!localPath.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("Unsafe path data received");
                    if (System.IO.File.Exists(localPath)
                        && VerifySignature(localPath, signature)) {
                        continue;
                    }

                    queue.Add(new AppFile() {
                        LocalPath = localPath,
                        RemoteUrl = "https://" + AUTHORITATIVE_DOMAIN + "/api/get-release-file/" + NETCOREVERSION + "/server/" + ar[0],
                        Signature = signature,
                        Length = length
                    });
                } catch (Exception ex) {
                    SendFailureNotification("CheckForUpdate line '{0}' crashed with {1}", line, ex.Message);
                    return false;
                }
            }

            if (queue.Count == 0) {
                Log("Up to date with version {0}.", currentVersion);
                return false;
            }

            token.ThrowIfCancellationRequested();

            Log("Downloading new version {0}...", currentVersion);
            bool allSucceeded = false;
            string versionFolder = System.IO.Path.Combine(AppContext.BaseDirectory, currentVersion.ToString());
            if (!System.IO.Directory.Exists(versionFolder))
                System.IO.Directory.CreateDirectory(versionFolder);
            try {
                for (int i = 0; i < queue.Count; i++) {
                    var file = queue[i];
                    var req = System.Net.HttpWebRequest.CreateHttp(file.RemoteUrl);
                    var res = await req.GetResponseAsync() as System.Net.HttpWebResponse;
                    if (res.StatusCode != System.Net.HttpStatusCode.OK) {
                        Log("Download failed {0}: {1}", res.StatusCode, file.RemoteUrl);
                        return false;
                    }
                    using (var s = System.IO.File.OpenWrite(file.LocalPath)) {
                        s.SetLength(0);
                        await res.GetResponseStream().CopyToAsync(s, 81920, token);
                        if (s.Length != file.Length)
                            throw new Exception(
                                String.Format("Unexpected file length for {0}. Got {1}, expected {2}.",
                                file.LocalPath, s.Length, file.Length));
                    }

                    if (!VerifySignature(file.LocalPath, file.Signature)) {
                        throw new Exception("Signature check failed for " + file.LocalPath);
                    }
                    token.ThrowIfCancellationRequested();
                }
                allSucceeded = true;
                return true;
            } catch (Exception ex) {
                if (!token.IsCancellationRequested)
                    SendFailureNotification("Upgrade failed with {0}", ex.Message);
                return false;
            } finally {
                if (!allSucceeded) {
                    System.IO.Directory.Delete(versionFolder, true);
                }
            }
        }

        public void Start(CancellationToken token) {
            if (_MonitorThread != null)
                throw new InvalidOperationException("The controller is already running.");

            Debug.Assert(_Process == null);
            _IsRunning = true;
            _MonitorThread = new Thread(MonitorThread);
            _MonitorThread.Start(token);
        }

        public void Stop() {
            _IsRunning = false;
            _MonitorThread?.Join();
            _MonitorThread = null;
        }

        private static bool VerifySignature(string path, string signature) {
            if (!System.IO.File.Exists(path) || String.IsNullOrWhiteSpace(signature))
                return false;
            using (var rsa = System.Security.Cryptography.RSA.Create()) {
                rsa.ImportParameters(_UpdateServerPublicKey);
                return rsa.VerifyData(System.IO.File.ReadAllBytes(path), Convert.FromBase64String(signature), System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            }
        }

        private string ExportLog(int items) {
            var s = new StringBuilder();
            int idx = _LogIndex % _LogLines.Length;
            for (int i = idx - 1; i >= 0 && items > 0; i--) {
                if (_LogLines[i] == null)
                    break;
                s.Append(_LogLines[i] + "\r\n");
                items--;
            }
            for (int i = _LogLines.Length - 1; i > idx && items > 0; i--) {
                if (_LogLines[i] == null)
                    break;
                s.Append(_LogLines[i] + "\r\n");
                items--;
            }
            return s.ToString();
        }


        private Version GetInstalledVersion() {
            var ar = System.IO.Directory.GetDirectories(AppContext.BaseDirectory);
            Version best = null;
            for (int i = 0; i < ar.Length; i++) {
                Version v;
                if (Version.TryParse(System.IO.Path.GetFileName(ar[i]), out v)
                    && (best == null || best < v))
                    best = v;
            }
            return best;
        }

        private void CleanupOldVersions() {
            var ar = System.IO.Directory.GetDirectories(AppContext.BaseDirectory);
            var versions = new List<Tuple<Version, string>>();
            for (int i = 0; i < ar.Length; i++) {
                Version v;
                if (Version.TryParse(System.IO.Path.GetFileName(ar[i]), out v))
                    versions.Add(new Tuple<Version, string>(v, ar[i]));
            }
            versions.Sort((a, b) => { return a.Item1.CompareTo(b.Item1); });
            // keep the last 4x versions
            for (int i = 0; i < versions.Count - 4; i++) {
                try {
                    System.IO.Directory.Delete(versions[i].Item2, true);
                    Log("Cleaned up old version " + System.IO.Path.GetFileName(versions[i].Item2));
                } catch {
                    Log("Unable to clean up old version " + System.IO.Path.GetFileName(versions[i].Item2));
                }
            }
        }

        private void Log(string msg, params object[] args) {
            var line = DateTime.UtcNow.ToString("s") + ": " + String.Format(msg, args);
            Console.WriteLine(line);
            _LogLines[_LogIndex % _LogLines.Length] = line;
            _LogIndex++;
            try {
                lock (_LogWriter)
                    _LogWriter.Write(line + "\r\n");
               
            } catch { }
        }

        private async void MonitorThread(object o) {
            var token = (CancellationToken)o;

            var time = new Stopwatch();
            var updateCheckInterval = TimeSpan.FromHours(1);
            // force immediate update
            var lastUpdateCheck = -updateCheckInterval;
            var lastLogFlush = TimeSpan.Zero;
            time.Start();

            Version version = GetInstalledVersion();
            int crashCount = 0;
            bool isAwaitingFixedBuild = false;

            CleanupOldVersions();

            while (_IsRunning
                && !token.IsCancellationRequested) {
                if ((time.Elapsed - lastUpdateCheck).TotalHours > 1) {
                    lastUpdateCheck = time.Elapsed;
                    try {
                        if (await CheckForUpdate(token)) {
                            // There's a new version successfully downloaded.
                            version = GetInstalledVersion();
                            if (_Process != null)
                                StopServer();
                            crashCount = 0;
                        }
                        if (token.IsCancellationRequested)
                            break;

                        CleanupOldVersions();

                    } catch { }
                }

                if (_Process == null
                    && version != null) {
                    if (_HasCrashed && !isAwaitingFixedBuild) {
                        crashCount++;
                        if (crashCount > 5) {
                            var fault = String.Format("Giving up after 5 crashes under version {0}. Awaiting fixed build.", version);
                            Log(fault);
                            SendFailureNotification(fault);
                            isAwaitingFixedBuild = true;
                        }
                    }
                    if (!isAwaitingFixedBuild)
                        StartServer(version);
                }

                if (_Stats != null)
                    _Stats.Poll(time.Elapsed);

                if ((time.Elapsed - lastLogFlush).TotalSeconds > 5) {
                    lastLogFlush = time.Elapsed;
                    try {
                        lock(_LogWriter)
                        _LogWriter.Flush();
                    } catch { }
                }

                Thread.Sleep(1000);
            }

        }

        private void Proc_ErrorDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e) {
            if (e.Data != null)
                Log(e.Data);
        }

        private void Proc_Exited(object sender, EventArgs e) {
            var proc = sender as System.Diagnostics.Process;
            if (!_IsExpectingExit) {
                Log("Host exiting unexpectedly: 0x" + proc.ExitCode.ToString("X"));
                _HasCrashed = true;
            }
            proc.ErrorDataReceived -= Proc_ErrorDataReceived;
            proc.OutputDataReceived -= Proc_OutputDataReceived;
            proc.Exited -= Proc_Exited;
            proc.Dispose();
            _Process = null;
            _Stats = null;
        }

        private void Proc_OutputDataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e) {
            if (e.Data != null)
                Log(e.Data);
        }

        /// <summary>
        /// Attempts to notify an authoritative civil.money server about some kind of nasty crash or
        /// problem going on with this peer.
        /// </summary>
        private async void SendFailureNotification(string message, params object[] args) {
            try {
                var str = String.Format(message, args);
                Console.WriteLine("Error reporting: " + str);
                str += "\r\n--- LOG ---\r\n";
                str += ExportLog(10) + "\r\n";
                var b = Encoding.UTF8.GetBytes(str);
                var req = System.Net.HttpWebRequest.CreateHttp("https://" + AUTHORITATIVE_DOMAIN + "/api/log-error/server");
                req.Method = "POST";
                req.ContentType = "text/plain; charset=utf-8";
                req.Headers[System.Net.HttpRequestHeader.ContentLength] = b.Length.ToString();
                using (var s = await req.GetRequestStreamAsync()) {
                    s.Write(b, 0, b.Length);
                }
                using (var res = await req.GetResponseAsync() as System.Net.HttpWebResponse) {
                    Log("Error reporting returned: " + res.StatusCode);
                }
            } catch (Exception ex) {
                Log("Failed to report error: {0}", ex.Message);
            }
        }

        private void StartServer(Version v) {
            Debug.Assert(v != null);

            var appPath = System.IO.Path.Combine(AppContext.BaseDirectory, v.ToString());
            appPath = System.IO.Path.Combine(appPath, "CM.Server.dll");
            var proc = _Process = new Process();
            proc.StartInfo = new ProcessStartInfo("dotnet", "\"" + appPath + "\"") {
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WorkingDirectory = AppContext.BaseDirectory
            };
            proc.ErrorDataReceived += Proc_ErrorDataReceived;
            proc.OutputDataReceived += Proc_OutputDataReceived;
            proc.Exited += Proc_Exited;
            proc.EnableRaisingEvents = true;

            if (proc.Start()) {
                Log("Server started (PID " + proc.Id + ".)");
                _HasCrashed = false;
                _IsExpectingExit = false;
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                _Stats = new ProcessStatistics(proc);
            } else {
                Log("Unable to start.");
                proc.Dispose();
                _HasCrashed = true;
                _Process = null;
            }
        }

        private void StopServer() {
            var proc = _Process;
            Debug.Assert(proc != null);

            _IsExpectingExit = true;
            proc.StandardInput.WriteLine();
            proc.WaitForExit();
            Debug.Assert(_Process == null);
            Log("Server stopped.");
        }

        private class AppFile {
            public long Length;
            public string LocalPath;
            public string RemoteUrl;
            public string Signature;
        }

        private class ProcessStatistics {
            public double CPU;
            public long RAM;
            public long RAMPeak;
            public int Threads;
            public int ThreadsPeak;
            private TimeSpan _LastCPUCheck;
            private TimeSpan _LastProcTime;
            private Process _Proc;

            public ProcessStatistics(Process proc) {
                _Proc = proc;
            }

            public void Poll(TimeSpan now) {
                _Proc.Refresh();
                RAM = GC.GetTotalMemory(false);
                if (RAMPeak < RAM)
                    RAMPeak = RAM;
                if (_LastCPUCheck != now)
                    CPU = (_Proc.TotalProcessorTime - _LastProcTime).TotalSeconds / (Environment.ProcessorCount * (now - _LastCPUCheck).TotalSeconds);
                if (CPU > 1)
                    CPU = 0; // nonsense
                _LastCPUCheck = now;
                _LastProcTime = _Proc.TotalProcessorTime;
                Threads = _Proc.Threads.Count;
                if (ThreadsPeak < Threads)
                    ThreadsPeak = Threads;
            }
        }
    }
}