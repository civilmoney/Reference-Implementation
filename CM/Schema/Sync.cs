#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Schema {

    /// <summary>
    /// DHT peers call SYNC to inform to responsible peers about their current version of an account
    /// and its data. Responsible peers will then either ignore it or begin the process to query
    /// other peers on the network to make sure that it has the latest copy of everything.
    /// </summary>
    public class SyncAnnounce : Message {

        public SyncAnnounce() {
        }

        public SyncAnnounce(string payload) : base(payload) {
        }

        /// <summary>
        /// Currently always 1
        /// </summary>
        public uint APIVersion { get { return Values.Get<uint>("VER"); } set { Values.Set<uint>("VER", value); } }

        /// <summary>
        /// Informs the destination of the endpoint to use to retrieve the item.
        /// </summary>
        public string MyEndPoint { get { return this["EP"]; } set { this["EP"] = value; } }

        /// <summary>
        /// Modification time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime UpdatedUtc { get { return Values.Get<DateTime>("UPD-UTC"); } set { Values.Set<DateTime>("UPD-UTC", value); } }

        /// <summary>
        /// An SHA256 hash of all Transaction updated-utc times related to the account
        /// </summary>
        public byte[] TransactionsHash { get { return Values.Get<byte[]>("TRANS-HASH"); } set { Values.Set<byte[]>("TRANS-HASH", value); } }

        /// <summary>
        /// An SHA256 hash of all Vote updated-utc times related to the account
        /// </summary>
        public byte[] VotesHash { get { return Values.Get<byte[]>("VOTES-HASH"); } set { Values.Set<byte[]>("VOTES-HASH", value); } }
    }
}