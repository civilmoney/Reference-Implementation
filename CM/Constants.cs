#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM {
    public class CMSeed {
        public CMSeed(string domain, string ep) {
            Domain = domain;
            EndPoint = ep;
        }
        public string Domain;
        public string EndPoint;
    }

    public class Constants {

        public const string WebSocketTransport = "wss";
        public const int APIVersion = 1;
        public const int MaxAccountIDLengthInUtf8Bytes = 48;

        /// <summary>
        /// The minimum sane date time applies to any kind of object. It will
        /// never be dated before the Civil Money system even existed.
        /// </summary>
#if JAVASCRIPT
        public static readonly DateTime MinimumSaneDateTime = new DateTime(2016, 1, 1, 0, 0, 0);
#else
        public static readonly DateTime MinimumSaneDateTime = new DateTime(2016, 1, 1, 0, 0, 0, DateTimeKind.Utc);
#endif
        public const int MaxHopCount = 30;
        public const int MaxAllowedTimestampErrorInMinutes = 10;
        public static readonly byte[] GoverningAuthorityRSAPublicKey = Convert.FromBase64String("6DiS3klqFkjWEvz6qHqH8XTvcwL+4kBG2X58nuokoYOnEGLnSUc6fWK+5ZiwdvrPeKbfMZoZ5LSw+jZe08/dS3NpWcf0KEdrNPFvSVnTMAzMVep65AIHeImOkLUOejGOZm49orYP1HTZVfBzs3ULxJ3ibBQcnCk8YRZKrao02B0=");
        public const decimal TaxRate = 0.1m;
        public const int BasicYearlyAllowance = 600;
        public const decimal USDExchange = 50;
        public static readonly byte[] StandardExponent65537 = new byte[] { 1, 0, 1 };
        public const int MessageReplyTimeoutMs = 5 * 1000;
        public const int DHTIDSize = 8;
        public const int NumberOfCopies = 5;
        public const int MinimumNumberOfCopies = 2;
        public static readonly char[] NewLineChars = new char[] { '\r', '\n' };
        public const decimal MinimumTransactionAmount = 0.000001m; // we'll accept up to 6dp

        public const string Symbol = "//c";
        public const string TrustedSite = "https://civil.money";

        // DHT (Distributed Dash Table) object paths, per the API.

        public const string PATH_ACCNT = "ACCNT";
        public const string PATH_TRANS = "TRANS";
        public const string PATH_REGIONS = "REGIONS";
        public const string PATH_VOTES = "VOTES";

#if DEBUG
        public const string WebSocketProtocol = "test.civil.money";
        public static readonly CMSeed[] Seeds = new CMSeed[] {
            new CMSeed("127-0-0-1."+DNS.UNTRUSTED_DOMAIN+":8000", "127.0.0.1:8000"),
            new CMSeed("127-0-0-1."+DNS.UNTRUSTED_DOMAIN+":8001", "127.0.0.1:8001")
        };
        /// <summary>
        /// True for debug mode only. It is useful to be able to run multiple servers 
        /// off 1x machine.
        /// </summary>
        public const bool Peer_DHT_ID_Uses_Port = true;
#else
        public const string WebSocketProtocol = "v1.civil.money";

        // The reason for the IPs is that the client and design is primarily an IP-based
        // protocol, however we need the domain names for web browser SSL to work.
        public static readonly CMSeed[] Seeds = new CMSeed[] {
            new CMSeed("seed1.civil.money:443", "154.127.60.153:443"), // ZA
            new CMSeed("seed2.civil.money:443", "191.96.4.225:443"), // BR
            new CMSeed("seed3.civil.money:443", "104.244.153.122:443"), // US
            new CMSeed("seed4.civil.money:443", "185.58.225.189:443"), // UK
            new CMSeed("seed5.civil.money:443", "89.40.127.213:443"), // DE
            new CMSeed("seed6.civil.money:443", "89.46.74.86:443") // IT
        };

        /// <summary>
        /// During production, peer DHT IDs are only based on IP address
        /// alone. This is to prevent a peers running multiple servers 
        /// from the same address, which can increase their chance of
        /// a consensus attack. 
        /// </summary>
        public const bool Peer_DHT_ID_Uses_Port = false;
#endif

    }
}
