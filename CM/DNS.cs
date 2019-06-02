#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM {

    public class DNS {
        public const string AUTHORITATIVE_DOMAIN = "civil.money";
        public const string DEFAULT_PORT = "443";
        public const string UNTRUSTED_DOMAIN = "untrusted-server.com";
        public static readonly string[] Nameservers = new string[] {
            "ns1.civil.money", "ns2.civil.money"
        };

        /// <summary>
        /// Converts an IP address or domain name (and optional port) into a
        /// *.untrusted.civil.com domain name.
        ///
        /// The name server for untrusted.civil.com simply resolves the
        /// IP/host in the first fragment to its original address.
        /// </summary>
        public static string EndpointToUntrustedDomain(string ep, bool preservePort) {
            var parts = ep.Split(':');

            if (parts[0].IndexOf(UNTRUSTED_DOMAIN) > -1)
                return parts[0] + (preservePort ? ":" + (parts.Length == 2 ? parts[1] : DEFAULT_PORT) : ""); // already converted.

            // aaa.bbb.ccc.ddd:port ->  aaa-bbb-ccc-ddd-port.untrusted.civil.com
            return parts[0].Replace(".", "-") + "-" + (parts.Length == 2 ? parts[1] : DEFAULT_PORT) + "." + UNTRUSTED_DOMAIN
                + (preservePort ? ":" + (parts.Length == 2 ? parts[1] : DEFAULT_PORT) : "");
        }

        /// <summary>
        /// Converts a *.untrusted.civil.com domain back into its original IP/host:port form.
        /// </summary>
        public static string UntrustedDomainToEndpoint(string domainName) {
            if (String.Equals(domainName, UNTRUSTED_DOMAIN, StringComparison.OrdinalIgnoreCase))
                return UNTRUSTED_DOMAIN + ":" + DEFAULT_PORT;
            var dotted = "." + UNTRUSTED_DOMAIN;

            if (!domainName.ToLower().EndsWith(dotted.ToLower())) // IgnoreCase is critical but no EndsWith(..,StringComparison.OrdinalIgnoreCase) for bridge.net
                throw new ArgumentException("Invalid untrusted domain name.");
            domainName = domainName.ToLower().Replace(dotted, "");
            // aaa-bbb-ccc-ddd-port -> aaa.bbb.ccc.ddd:port
            var parts = domainName.Split('-');
            byte b0, b1, b2, b3;
            if ((parts.Length == 4 || parts.Length == 5)
                 && byte.TryParse(parts[0], out b0)
                 && byte.TryParse(parts[1], out b1)
                 && byte.TryParse(parts[2], out b2)
                 && byte.TryParse(parts[3], out b3)
                ) {
                ushort port = 80;
                if (parts.Length == 5
                    && !ushort.TryParse(parts[4], out port))
                    throw new ArgumentException("Invalid untrusted domain name.");
                return b0 + "." + b1 + "." + b2 + "." + b3 + ":" + port;
            } else {
                // host-name-com-8000 -> host.name.com:8000
                // or
                // host-name-com -> host.name.com:443
                // if the last segment is a number, assume it's a port
                ushort port;
                if (ushort.TryParse(parts[parts.Length - 1], out port)) {
                    return String.Join(".", parts, 0, parts.Length - 1) + ":" + port;
                } else {
                    return String.Join(".", parts, 0, parts.Length) + ":" + DEFAULT_PORT;
                }
            }
        }
    }
}