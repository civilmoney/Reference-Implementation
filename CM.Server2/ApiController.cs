using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CM.Server {

    [ResponseCache(NoStore = true, Duration = 0)]
    [Produces("application/json")]
    public class ApiController : ControllerBase {
        private const int CACHE_DURATION = 60 * 60 * 24 * 30;
        private const string MimeTextPlain = "text/plain; charset=utf-8";
        private static readonly char[] UnsafePathChars = new char[] { '/', '\\' };
        private Server _Server;

        static ApiController() {
        }

        public ApiController(IServer server) {
            _Server = server as Server;
        }

        public static bool EnableJavascriptAppDebugMode { get; set; }

        public static string ResourceUrl(string path) {
            path = path.Substring(1);
            if (!EnableJavascriptAppDebugMode && !path.EndsWith("webworkers.js"))
                path = path.Replace(".js", ".min.js");
            return "/" + path + "?" + System.IO.File.GetLastWriteTime(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "wwwroot", path)).Ticks;
        }

        // GET api/get-propositions
        [HttpGet("api/get-propositions")]
        public IActionResult GetPropositions() {
            return Ok(AuthoritativeDomainReporter.Propositions);
        }

        // GET api/get-random?length=int
        [HttpGet("api/get-random")]
        public IActionResult GetRandom([FromQuery] uint length) {
            if (length <= 4096 && length > 0) {
                var b = System.Buffers.ArrayPool<byte>.Shared.Rent((int)length);
                CryptoFunctions.RNG(b);
                string res = Convert.ToBase64String(b, 0, (int)length);
                System.Buffers.ArrayPool<byte>.Shared.Return(b);
                return Content(res, MimeTextPlain);
            } else {
                return BadRequest("Length out of expected range.");
            }
        }

        [HttpGet("api/releases/{releaseFile}")]
        public IActionResult GetReleaseDaemon([FromRoute]string releaseFile) {

            if (String.IsNullOrEmpty(releaseFile) || releaseFile.IndexOfAny(UnsafePathChars) > -1) {
                return BadRequest();
            }

            var baseFolder = Path.Combine(ServerConfiguration.BaseDirectory, "releases");
            var path = Path.Combine(baseFolder, releaseFile);
            if (!path.StartsWith(baseFolder) || !System.IO.File.Exists(path)) {
                _Server.Log.Write(this, LogLevel.WARN, "Bad file path requested: {0}", HttpContext.Request.Path.Value);
                return BadRequest();
            }
            return File(System.IO.File.OpenRead(path), "application/octet-stream");
        }

        // GET api/get-release-file/netcoreapp{version}/{app name}/{version}/path/to/file.dll
        // -> binary
        [HttpGet("api/get-release-file/{netcoreAppVersion}/{application}/{versionString}/{*filePath}")]
        public IActionResult GetReleaseFile([FromRoute]string netcoreAppVersion, [FromRoute] string application, [FromRoute] string versionString, [FromRoute] string filePath) {
            if (_Server.UpdateServerSigningKey.D == null) {
                return NotFound("This server does not support updates.");
            }
            if (String.IsNullOrEmpty(netcoreAppVersion)
                || String.IsNullOrEmpty(application)
                || application.IndexOfAny(UnsafePathChars) > -1
                || netcoreAppVersion.IndexOfAny(UnsafePathChars) > -1) {
                return BadRequest();
            }

            var file = filePath.Split('/');

            switch (application) {
                case "server": {
                        var version = Version.Parse(versionString); // Validation
                        var baseFolder = Path.Combine(ServerConfiguration.BaseDirectory, "releases");
                        var localPath = baseFolder;
                        localPath = Path.Combine(localPath, netcoreAppVersion);
                        localPath = Path.Combine(localPath, application);
                        localPath = Path.Combine(localPath, version.ToString());

                        foreach (var part in file)
                            localPath = Path.Combine(localPath, part);
                        if (!localPath.StartsWith(baseFolder)
                            || !System.IO.File.Exists(localPath)) {
                            _Server.Log.Write(this, LogLevel.WARN, "Bad file path requested: {0}/{1}/{2}",
                                application,
                                versionString,
                                String.Join("/", file));
                            return BadRequest();
                        }
                        return File(System.IO.File.OpenRead(localPath), "application/octet-stream");
                    }
            }

            return NotFound();
        }

        // GET api/get-release-info/netcoreapp{netcoreAppVersion}/{application}
        // -> 1.0.0/{file} {size} {sha256-base64}
        //    1.0.0/some%20resource/{file} {size} {sha256-base64}
        [HttpGet("api/get-release-info/{netcoreAppVersion}/{application}")]
        public IActionResult GetReleaseInfo([FromRoute]string netcoreAppVersion, [FromRoute] string application) {
            if (_Server.UpdateServerSigningKey.D == null) {
                return NotFound("This server does not support updates.");
            }
            if (String.IsNullOrEmpty(netcoreAppVersion)
                || String.IsNullOrEmpty(application)
                || application.IndexOfAny(UnsafePathChars) > -1
                || netcoreAppVersion.IndexOfAny(UnsafePathChars) > -1) {
                return BadRequest();
            }

            // Releases are kept in https://civil.money/releases/netcoreappX.X/application-name/1.x.x
            var folder = Path.Combine(ServerConfiguration.BaseDirectory, "releases");
            folder = Path.Combine(folder, netcoreAppVersion);
            folder = Path.Combine(folder, application);
            if (!System.IO.Directory.Exists(folder))
                return NotFound();
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
                return NotFound();
            }

            versions.Sort();
            var version = versions[versions.Count - 1];

            var versionFolder = Path.Combine(folder, version.ToString());
            

            Debug.Assert(System.IO.Directory.Exists(versionFolder));

            var files = System.IO.Directory.GetFiles(versionFolder, "*", SearchOption.AllDirectories);
            var s = new StringBuilder();
            using (var rsa = System.Security.Cryptography.RSA.Create()) {
                rsa.ImportParameters(_Server.UpdateServerSigningKey);
                for (int i = 0; i < files.Length; i++) {
                    var info = new System.IO.FileInfo(files[i]);
                    Debug.Assert(info.Exists);
                    // We want "1.0.0/CM.Server.dll"
                    var relativePath = files[i].Substring(folder.Length).Substring(1);

                    string signature = null;
                    using (var stream = info.OpenRead()) {
                        signature = Convert.ToBase64String(rsa.SignData(stream, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1));
                    }

                    s.CRLF(String.Format("{0} {1} {2}", relativePath.Replace("\\", "/").Replace(" ", "%20"), info.Length.ToString(), signature));
                }
            }
            return Content(s.ToString(), MimeTextPlain);
        }

        //GET api/get-revenue/CA-NS
        [HttpGet("api/get-revenue/{region}")]
        public IActionResult GetRevenue([FromRoute]string region) {
            if (ISO31662.GetName(region) == null) {
                return NotFound();
            }

            var ar = _Server.Reporter.GetTaxSummaryItems(region, DateTime.UtcNow.Date.AddYears(-2), DateTime.UtcNow.Date.AddDays(1));
            decimal total = 0;
            int count = 0;
            for (int i = 0; i < ar.Items.Count; i++) {
                if ((ar.Items[i].Flags & AuthoritativeDomainReporter.TaxRevenueFlags.Ineligible) != 0)
                    continue;
                total += ar.Items[i].Revenue;
                count++;
            }
            return Ok(new {
                count,
                revenue = total,
                lastUpdatedUtc = Helpers.DateToISO8601(ar.GeneratedUtc)
            });
        }

        // GET api/get-revenue-data/CA-NS?from={from}&to={to}&startat=0&max=1000
        [HttpGet("api/get-revenue-data/{region}")]
        public IActionResult GetRevenueData([FromRoute]string region,
            [FromQuery] DateTime from, [FromQuery] DateTime to,
            [FromQuery] int startat, [FromQuery] int max) {
            if (ISO31662.GetName(region) == null) {
                return NotFound();
            }

            // input validation
            if (ISO31662.GetName(region) == null) {
                return NotFound();
            }

            if (to <= from) {
                return BadRequest("Invalid 'to' date value. Must be greater than from.");
            }

            var ar = _Server.Reporter.GetTaxSummaryItems(region, from, to).Items;

            if (max <= 0)
                max = 1000;
            var s = new StringBuilder();
            for (int i = startat; i < Math.Min(ar.Count, startat + max); i++) {
                var t = ar[i];
                s.Append(t.Date.ToString("s") + " " + t.ID + " " + t.Revenue + " " + ((t.Flags & AuthoritativeDomainReporter.TaxRevenueFlags.Ineligible) != 0 ? "NT" : "OK") + "\r\n");
            }
            return Content(s.ToString(), MimeTextPlain);
        }

        // GET api/get-vote-data?proposition-id=X
        [HttpGet("api/get-vote-data")]
        public IActionResult GetVoteData([FromQuery(Name = "proposition-id")] uint propositionId) {
            if (propositionId == 0)
                return BadRequest();

            var files = new List<string>(Directory.GetFiles(_Server.Reporter._FolderCompiledData, "prop" + propositionId + "-votes-report-*.zip"));
            if (files.Count == 0) {
                return NotFound("Results are not yet available.");
            }
            files.Sort();
            var fileName = files[files.Count - 1];
            return File(fileName, "application/octet-stream");
        }

        // POST api/log-error/{app name}
        [HttpPost("api/log-error/{application}")]
        public IActionResult LogAppError([FromRoute] string application, [FromForm] string error) {
            if (String.IsNullOrEmpty(error))
                return BadRequest();

            var sender = HttpContext.Connection.RemoteIpAddress;
            var file = Path.Combine(ServerConfiguration.BaseDirectory, "remote-errors.txt");
            try {
                _Server.Log.Write(this, LogLevel.INFO, "Remote error received from " + sender + " (" + error.Length + " chars)");
                System.IO.File.AppendAllText(file,
                    DateTime.Now.ToString("s") + " [" + sender + ", " + application + "]: "
                    + error
                    + "\r\n=================\r\n");
            } catch (Exception ex) {
                _Server.Log.Write(this, LogLevel.INFO, "Remote error from " + sender + " failed to log: " + ex.Message);
            }

            return Ok();
        }

        // POST api/log-telem/{type name}
        [HttpPost("api/log-telem/{typeName}")]
        public IActionResult LogTelemetry([FromRoute] string typeName, [FromForm] string report) {
            if (typeName != "http" || String.IsNullOrEmpty(report))
                return BadRequest();

            var sender = HttpContext.Connection.RemoteIpAddress;
            // We'll log telemetry by UTC day
            var file = Path.Combine(ServerConfiguration.BaseDirectory, "telemetry");
            if (!Directory.Exists(file))
                Directory.CreateDirectory(file);
            file = Path.Combine(file, typeName + "-telem-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".txt");
            try {
                _Server.Log.Write(this, LogLevel.INFO, "Remote telemetry received from " + sender + " (" + report.Length + " chars)");
                System.IO.File.AppendAllText(file,
                    "==== " + DateTime.Now.ToString("s") + " [" + sender + "] ====\r\n"
                    + report
                    + "\r\n=================\r\n");
            } catch (Exception ex) {
                _Server.Log.Write(this, LogLevel.INFO, "Remote telemetry from " + sender + " failed to log: " + ex.Message.ToString());
            }
            return Ok();
        }
    }
}