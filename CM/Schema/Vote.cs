#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Text;

namespace CM.Schema {

    /// <summary>
    /// Describes a user vote for a proposal by the CM steering group.
    /// </summary>
    public class Vote : Message, IStorable {
        public Vote() : base() {
        }

        public Vote(string payload) : base(payload) {
        }

        #region IStorable

        public int ConsensusCount { get; set; }

        public bool ConsensusOK { get { return ConsensusCount >= Constants.MinimumNumberOfCopies; } }

        /// <summary>
        /// Votes are stored in path /VOTES/{Proposition ID}/{Account ID}
        /// </summary>
        public string Path {
            get {
                return "VOTES/" + PropositionID + "/" + VoterID;
            }
        }
        /// <summary>
        /// Votes are indexed in accounts under ACCNT/{VoterID}/VOTES/{PropositionID}
        /// </summary>
        public string AccountPath {
            get {
                return "ACCNT/" + VoterID + "/VOTES/" + PropositionID;
            }
        }

        #endregion IStorable

        /// <summary>
        /// Currently always 1
        /// </summary>
        public uint APIVersion { get { return Values.Get<uint>("VER"); } set { Values.Set<uint>("VER", value); } }

        /// <summary>
        /// Creation time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime CreatedUtc { get { return Values.Get<DateTime>("UTC"); } set { Values.Set<DateTime>("UTC", value); } }

        /// <summary>
        /// Modification time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime UpdatedUtc { get { return Values.Get<DateTime>("UPD-UTC"); } set { Values.Set<DateTime>("UPD-UTC", value); } }

        /// <summary>
        /// The user placing the vote.
        /// </summary>
        public string VoterID { get { return this["VTR-ID"]; } set { this["VTR-ID"] = value; } }

        /// <summary>
        /// The ID of the proposition being voted on.
        /// </summary>
        public uint PropositionID { get { return Values.Get<uint>("PROP"); } set { Values.Set<uint>("PROP", value); } }

        /// <summary>
        /// The user's selected vote on the proposal. By design propositions must be binary (for or against.)
        /// This is to take potentially unfair "ranked order" voting procedures off the table.
        /// </summary>
        public bool Value { get { return Values.Get<bool>("VOTE"); } set { Values.Set<bool>("VOTE", value); } }

        /// <summary>
        /// The user's RSA signature.
        /// </summary>
        public byte[] Signature { get { return Values.Get<byte[]>("SIG"); } set { Values.Set<byte[]>("SIG", value); } }

        /// <summary>
        /// Vote signing data consists of all attribute values (with the exception of SIG itself.)
        /// </summary>
        /// <returns></returns>
        public byte[] GetSigningData() {
            var data = new List<byte>();
            for (int i = 0; i < Values.Count; i++) {
                var v = Values[i];
                if (String.Compare(v.Name, "SIG", StringComparison.OrdinalIgnoreCase) == 0)
                    continue;
                data.AddRange(Encoding.UTF8.GetBytes(Values[i].Value));
            }
            return data.ToArray();
        }
    }
}