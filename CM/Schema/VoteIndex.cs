#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CM.Schema {
    /// <summary>
    /// A VoteIndex is the single-line data returned during a LIST query. As well, each DHT
    /// peer keeps these in account containers, which allows for faster queries.
    /// </summary>
    public class VoteIndex {
        public uint PropositionID;
        public string VoterID;
        public bool Value;
        public DateTime CreatedUtc;
        public DateTime UpdatedUtc;
       
        public VoteIndex() { }
        public VoteIndex(Vote v) {
            PropositionID = v.PropositionID;
            VoterID = v.VoterID;
            Value = v.Value;
            CreatedUtc = v.CreatedUtc;
            UpdatedUtc = v.UpdatedUtc;
           
        }
        public VoteIndex(string data) {
            Parse(data);
        }

        public void Parse(string data) {
            var ar = data.Split(' ');
            PropositionID = uint.Parse(ar[0]);
            VoterID = ar[1];
            Value = ar[2] == "1";
            CreatedUtc = Helpers.DateFromISO8601(ar[3]);
            UpdatedUtc = Helpers.DateFromISO8601(ar[4]);
        }
        /// <summary>
        /// The format of a VoteIndex string is:
        /// {PropositionID} + " " + {VoterID} + " " + {Value} + " " + {CreatedUtc} + " " + {UpdatedUtc}
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return PropositionID
                + " " + VoterID
                + " " + (Value ? "1" : "0")
                + " " + Helpers.DateToISO8601(CreatedUtc)
                + " " + Helpers.DateToISO8601(UpdatedUtc);
        }
    }
}
