#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM.Schema {

    public class PingRequest : Message {

        /// <summary>
        /// When specified, informs the peer that I'm a DHT peer also, and this
        /// is my public end-point. Peers must reject this field if the end-point
        /// IP address does not match the incoming request.
        /// </summary>
        public string EndPoint { get { return this["EP"]; } set { this["EP"] = value; } }
    }

    public class PingResponse : Message {

        /// <summary>
        /// Provides the caller with their external IP address
        /// </summary>
        public string YourIP { get { return this["YOUR-IP"]; } set { this["YOUR-IP"] = value; } }

        /// <summary>
        /// The IP that the server thinks its public address is. This can help clients
        /// correlate seed host names with IP addresses, as well as detect network address
        /// translation issues.
        /// </summary>
        public string MyIP { get { return this["MY-IP"]; } set { this["MY-IP"] = value; } }

        /// <summary>
        /// The endpoint of the peer's successor
        /// </summary>
        public string SuccessorEndpoint { get { return this["SUCC"]; } set { this["SUCC"] = value; } }

        /// <summary>
        /// The endpoint of the peer's predecessor
        /// </summary>
        public string PredecessorEndpoint { get { return this["PRED"]; } set { this["PRED"] = value; } }

        /// <summary>
        /// Comma delimited list of top 10 seen endpoints
        /// </summary>
        public string Seen { get { return this["SEEN"]; } set { this["SEEN"] = value; } }
    }
}