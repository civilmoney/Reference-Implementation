#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Server {

    /// <summary>
    /// As specified in settings.json
    /// </summary>
    public class ServerConfiguration {
        public static string BaseDirectory { get; }
   
        public static ServerConfiguration Load() {
            string appConfig = System.IO.Path.Combine(BaseDirectory, "settings.json");
            if (!System.IO.File.Exists(appConfig))
                throw new InvalidOperationException("Expecting configuration file: " + appConfig);
            var json = System.IO.File.ReadAllText(appConfig);

            return new ServerConfiguration(json);
        }

        static ServerConfiguration() {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            BaseDirectory = dir.Parent.FullName;
        }

        public ServerConfiguration() {
        }

        public ServerConfiguration(string jsonString) {
            try {
                var dic = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, ServerConfiguration>>(jsonString);
                var tmp = dic["Settings"];
                var props = typeof(ServerConfiguration).GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance);
                foreach (var p in props) {
                    p.SetValue(this, p.GetValue(tmp));
                }
            } catch (Exception ex) {
                throw new FormatException(
                   "JSON settings file is invalid.", ex);
            }
        }

        public string AuthoritativePfxCertificate { get; set; }
        public string AuthoritativePfxPassword { get; set; }
        public string DataFolder { get; set; }
        public string PermittedForwardingProxyIP { get; set; } = "127.0.0.1";
        /// <summary>
        /// Additional user-defined seeds
        /// </summary>
        public string Seeds { get; set; }
        /// <summary>
        /// Enabling this is has no real implications for the network, other than the server will do
        /// unnecessary extra labour, compiling data that never gets used.
        /// </summary>
        public bool EnableAuthoritativeDomainFeatures { get; set; }

        public int Port { get; set; }
        public string IP { get; set; }
        /// <summary>
        /// Typically set on civil.money domain servers.
        /// </summary>
        public bool EnablePort80Redirect { get; set; }
        /// <summary>
        /// Used only by https://update.civil.money to provide an authenticode of sorts
        /// for the CM.Daemon to verify using the well known Civil Money public key. 
        /// </summary>
        public string UpdateServerPrivateKey { get; set; }
        /// <summary>
        /// Used by civil.money servers to automate LetsEncrypt renewal.
        /// </summary>
        public CertificateRenewSettings CertificateRenew { get; set; }
    }

    /// <summary>
    /// Used by civil.money servers to automate LetsEncrypt renewal.
    /// </summary>
    public class CertificateRenewSettings {
        public string Server { get; set; }
        public string Domain { get; set; }
        public string Secret { get; set; }
    }
}