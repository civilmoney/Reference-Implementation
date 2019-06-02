using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CM.Server {
    /// <summary>
    /// Obtains the up-to-date *.civil.money TLS certificate from a remote key vault.
    /// </summary>
    internal static class CertificateRenew {

        private static TimeSpan _LastCheck;

        private enum CertificateFormat {
            Pfx,
            Pem
        }

        public static async void TryUpdate(Server server) {
            var config = server.Configuration;
            var settings = config.CertificateRenew;
            var current = server.CurrentCertificate;
            if (settings == null
                || String.IsNullOrEmpty(settings.Domain)
                || String.IsNullOrEmpty(settings.Secret)
                || String.IsNullOrEmpty(settings.Server)
                || String.IsNullOrEmpty(config.AuthoritativePfxCertificate)
                || (current != null && current.NotAfter > DateTime.UtcNow.AddDays(14))
                || (_LastCheck.Ticks != 0 && (Clock.Elapsed - _LastCheck).TotalDays < 1))
                return;

            _LastCheck = Clock.Elapsed;

            try {
                var res = await GetCertificateAsync(new GetCertificateRequest() {
                    Server = settings.Server,
                    Format = CertificateFormat.Pfx,
                    Domain = settings.Domain,
                    Secret = settings.Secret,
                });
                if (res.Success) {
                    var remote = new X509Certificate2(res.Certificate, config.AuthoritativePfxPassword);
                    if ((current == null || remote.NotAfter > current.NotAfter)
                        && remote.Subject.IndexOf(settings.Domain, StringComparison.OrdinalIgnoreCase) > -1
                        && remote.HasPrivateKey) {
                        // Looks sane.
                        System.IO.File.WriteAllBytes(config.AuthoritativePfxCertificate, res.Certificate);
                        server.CurrentCertificate = remote;
                        server.Log.Write(server, LogLevel.INFO, "Certificate has been updated and applied successfully.");
                    }
                } else {
                    // The remote update server doesn't have a better certificate yet. Try again tomorrow.
                }
            } catch (Exception ex) {
                server.Log.Write(server, LogLevel.FAULT, "Certificate auto-update failed with " + ex.Message);
            }
        }

        private static async Task<GetCertificateResponse> GetCertificateAsync(GetCertificateRequest req) {
            string server = req.Server;
            if (!server.StartsWith("http"))
                server = "https://" + server;
            HttpWebRequest r = System.Net.WebRequest.CreateHttp(server + "/api/get-certificate");
            r.Method = "POST";
            var json = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(req));
            r.ContentLength = json.Length;
            r.ContentType = "application/json";
            using (var s = await r.GetRequestStreamAsync()) {
                s.Write(json, 0, json.Length);
            }
            var res = await r.GetResponseAsync();
            using (var s = res.GetResponseStream()) {
                using (var reader = new System.IO.StreamReader(s, Encoding.UTF8)) {
                    var response = await reader.ReadToEndAsync();
                    return Newtonsoft.Json.JsonConvert.DeserializeObject<GetCertificateResponse>(response);
                }
            }
        }

        private class GetCertificateRequest {
            public string Domain;
            public CertificateFormat Format;
            public string Secret;
            public string Server;
        }

        private class GetCertificateResponse {
            public byte[] Certificate;
            public DateTime ExpiresUtc;
            public string Feedback;
            public DateTime LastUpdatedUtc;
            public bool Success;
        }
    }
}