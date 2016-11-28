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
            // Rather not a massive json dependency..

            var keys = System.Text.RegularExpressions.Regex.Matches(jsonString, @"""(?<key>[^""]+?)"":\s*""?(?<value>.*?)""?\s*[,|}]");
            for (int i = 0; i < keys.Count; i++) {
                var m = keys[i];
                var value = m.Groups["value"].Value.Trim(new char[] { '"', ' ', '\r', '\n' });
                try {
                    switch (m.Groups["key"].Value.ToLower()) {
                        case "port": Port = int.Parse(value); break;
                        case "datafolder": DataFolder = value; break;
                        case "authoritativepfxcertificate":
                            AuthoritativePfxCertificate = value;
                            break;

                        case "authoritativepfxpassword":
                            AuthoritativePfxPassword = value;
                            break;

                        case "enableauthoritativedomainfeatures":
                            EnableAuthoritativeDomainFeatures = String.Compare(value, "true", true) == 0;
                            break;
                        case "enableport80redirect":
                            EnablePort80Redirect = String.Compare(value, "true", true) == 0;
                            break;
                        case "updateserverprivatekey":
                            UpdateServerPrivateKey = value;
                            break;
                    }
                } catch (Exception ex) {
                    throw new FormatException(
                        String.Format("JSON settings file value '{0}' is invalid.", value), ex);
                }
            }
        }

        public string AuthoritativePfxCertificate { get; set; }
        public string AuthoritativePfxPassword { get; set; }
        public string DataFolder { get; set; }

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