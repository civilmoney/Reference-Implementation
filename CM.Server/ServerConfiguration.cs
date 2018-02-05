#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Server {

    public class ServerConfiguration {

        public ServerConfiguration() {
        }

        public ServerConfiguration(string jsonString) {
            try {
                var dic = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.Dictionary<string, ServerConfiguration>>(jsonString);
                var tmp = dic["Settings"];
                var props = typeof(ServerConfiguration).GetProperties();
                foreach (var p in props)
                    p.SetValue(this, p.GetValue(tmp));
            } catch (Exception ex) {
                throw new FormatException(
                   "JSON settings file is invalid.", ex);
            }
        }

        public string AuthoritativePfxCertificate { get; set; }
        public string AuthoritativePfxPassword { get; set; }
        public string DataFolder { get; set; }
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
        /// <summary>
        /// Typically set on civil.money domain servers.
        /// </summary>
        public bool EnablePort80Redirect { get; set; }
        /// <summary>
        /// Used only by https://update.civil.money to provide an authenticode of sorts
        /// for the CM.Daemon to verify using the well known Civil Money public key. 
        /// </summary>
        public string UpdateServerPrivateKey { get; set; }
    }
}