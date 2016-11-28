#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM.Schema {

    public class FindResponsiblePeerRequest : Message {

        /// <summary>
        /// Distributed Hash Table storage key in decimal
        /// </summary>
        public byte[] DHTID { get { return Values.Get<byte[]>("DHT-ID"); } set { Values.Set<byte[]>("DHT-ID", value); } }

        public string HopList { get { return Values.Get<string>("HOPS"); } set { Values.Set<string>("HOPS", value); } }
        public uint MaxHopCount { get { return Values.Get<uint>("MAX-HOPS"); } set { Values.Set<uint>("MAX-HOPS", value); } }
    }

    public class FindResponsiblePeerResponse : Message {
        public string HopList { get { return Values.Get<string>("HOPS"); } set { Values.Set<string>("HOPS", value); } }

        /// <summary>
        /// The proposed end-point that is responsible for the requested key
        /// </summary>
        public string PeerEndpoint { get { return this["PEER"]; } set { this["PEER"] = value; } }
    }
}