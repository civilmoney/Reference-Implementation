using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CM.Server {
    partial class AuthoritativeDomainReporter {

        
        //
        // Telemetry logging is a temporary feature during the pilot phase.
        //
        // We're only interested in very coarse grain and not even particularly 
        // accurate data here (we'll ignore missed counts due to thread concurrency.)
        // The purpose of telemetry is to answer:
        //
        // - Is there a particular HTTP page being hammered.
        // - Is there a particularly popular referrer.
        // - Which languages/locales are using Civil Money the most.
        //
        // We intentionally do not track any uniquely identifying characteristics. Data is
        // cleared hourly and sent off as a batch to xxxx.civil.money/api/telem-report
        //

        TelemetryReport _Telem;
        System.Text.RegularExpressions.Regex _BotRegex = new System.Text.RegularExpressions.Regex(
            @"bot\b|spider|crawl|wget|slurp", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        public void LogTelemetry(SslWebContext context) {
            var agent = context.RequestHeaders["User-Agent"];
            var lang = context.RequestHeaders["Accept-Language"]; // Most bots don't send this
            if (!String.IsNullOrWhiteSpace(agent)
                && !String.IsNullOrWhiteSpace(lang)
                && !_BotRegex.IsMatch(agent))
                _Telem.Log(context.Url.AbsolutePath, context.RequestHeaders["Referer"], lang);
        }


        class TelemetryReport {
            private const string TELEMETRY_DOMAIN = "update.civil.money";

            DateTime _LastFlush = DateTime.UtcNow;
            System.Collections.Concurrent.ConcurrentDictionary<string, int> _Paths = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            System.Collections.Concurrent.ConcurrentDictionary<string, int> _Referrers = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            System.Collections.Concurrent.ConcurrentDictionary<string, int> _Languages = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();
            static readonly char[] _LangDelimiters = new char[] { ',', ';' };

            /// <summary>
            /// Logs pertinent HTTP statistics (path, referrer, language) from users.
            /// </summary>
            public void Log(string path, string referrer, string lang) {

                if (String.Equals(lang, "zh-cn", StringComparison.OrdinalIgnoreCase)) {
                    // Ignore referrer spam
                    return;
                }

                if (!String.IsNullOrWhiteSpace(path)) {
                    path = path.Trim();
                    int v;
                    _Paths.TryGetValue(path, out v);
                    _Paths[path] = ++v;
                }
                if (!String.IsNullOrWhiteSpace(referrer)) {
                    int v;
                    referrer = referrer.Trim();
                    _Referrers.TryGetValue(referrer, out v);
                    _Referrers[referrer] = ++v;
                }
                if (!String.IsNullOrWhiteSpace(lang)) {
                    // Accept-Language: en-AU, en-US; q=0.7, en; q=0.3
                    // We only care about the first/primary one.
                    int idx = lang.IndexOfAny(_LangDelimiters);
                    if (idx > -1)
                        lang = lang.Substring(0, idx);
                    lang = lang.Trim().ToLower();
                    int v;
                    _Languages.TryGetValue(lang, out v);
                    _Languages[lang] = ++v;
                }

                // If data is getting quite full, signal for an immediate flush
                if (_Paths.Count
                    + _Referrers.Count
                    + _Languages.Count > 1000)
                    _LastFlush = DateTime.MinValue;
            }

            /// <summary>
            /// Clears HTTP statistics and submits the info to a central
            /// server for offline analysis.
            /// </summary>
            public async Task Flush(Log log) {
                if ((DateTime.UtcNow - _LastFlush).TotalHours < 1)
                    return;
                _LastFlush = DateTime.UtcNow;
                var paths = _Paths.ToArray();
                var referrers = _Referrers.ToArray();
                var langs = _Languages.ToArray();

                if (langs.Length
                    + referrers.Length
                    + langs.Length == 0)
                    return;

                // We don't actually care if we drop/lose statistics during upload.
                _Paths.Clear();
                _Referrers.Clear();
                _Languages.Clear();

                //
                // The data we submit to xxxx.civil.money/api/log-telem/http
                // is in the format of:
                //
                // paths:
                // <path>
                // <count>
                // ...
                // referrers:
                // <referer>
                // <count>
                // ..
                // languages:
                // <lang>
                // <count>
                // ..
         
                try {
                    // The payloads will never be very large during the pilot
                    // phase. When this becomes a memory hog, it's time to
                    // remove the telemetry reporter entirely. 
                    var s = new System.Text.StringBuilder();
                    s.CRLF("paths:");
                    foreach (var kp in paths) {
                        s.CRLF(kp.Key);
                        s.CRLF(kp.Value.ToString());
                    }
                    s.CRLF("referrers:");
                    foreach (var kp in referrers) {
                        s.CRLF(kp.Key);
                        s.CRLF(kp.Value.ToString());
                    }
                    s.CRLF("languages:");
                    foreach (var kp in langs) {
                       s.CRLF(kp.Key);
                       s.CRLF(kp.Value.ToString());
                    }
                    var b = System.Text.Encoding.UTF8.GetBytes(s.ToString());
                    var req = System.Net.HttpWebRequest.CreateHttp("https://" + TELEMETRY_DOMAIN + "/api/log-telem/http");
                    req.Method = "POST";
                    req.ContentType = "text/plain; charset=utf-8";
                    req.Headers[System.Net.HttpRequestHeader.ContentLength] = b.Length.ToString();
                    using (var st = await req.GetRequestStreamAsync()) {
                        st.Write(b, 0, b.Length);
                    }
                    using (var res = await req.GetResponseAsync() as System.Net.HttpWebResponse) {
                        if (res.StatusCode != System.Net.HttpStatusCode.OK)
                            log.Write(this, LogLevel.WARN, "Telemetry submission failed with status code {0}", res.StatusCode);
                    }
                } catch (Exception ex) {
                    log.Write(this, LogLevel.WARN, "Telemetry submission failed: {0}", ex.Message);
                }
            }
        }
    }
}
