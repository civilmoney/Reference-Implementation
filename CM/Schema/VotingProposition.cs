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
    /// VotingPropositions are defined in code and served by the authoritative domain servers
    /// who also generate reports and validate people's votes stored across the network.
    /// </summary>
    public class VotingProposition {
        /// <summary>
        /// A unique identifier for the proposition.
        /// </summary>
#if JAVASCRIPT
        [Bridge.Name("id")]
#endif
        public uint ID;
        /// <summary>
        /// The date and time that this proposition was issued
        /// </summary>
        public DateTime CreatedUtc;
        /// <summary>
        /// The date and time by which votes will not be counted.
        /// </summary>
        public DateTime CloseUtc;
        /// <summary>
        /// The current vote tally for the proposition.
        /// </summary>
        public uint For;
        /// <summary>
        /// The current vote tally against the proposition.
        /// </summary>
        public uint Against;
        /// <summary>
        /// The number of voters who did not meet the minimum requirements for voting.
        /// </summary>
        public uint Ineligible;
        /// <summary>
        /// An array of all available transactions. It will be important to fully define the
        /// implications of propositions in all supported UI languages.
        /// </summary>
        public TranslatedDetails[] Translations;

        /// <summary>
        /// Proposition details for each supported UI language code.
        /// </summary>
        public class TranslatedDetails {
            /// <summary>
            /// e.g. EN-GB
            /// </summary>
            public string Code;
            /// <summary>
            /// The title for the proposition
            /// </summary>
            public string Title;
            /// <summary>
            /// A brief description of the proposal
            /// </summary>
            public string Description;
            /// <summary>
            /// The list of known potential negative impacts
            /// </summary>
            public string NegativeImpacts;
            /// <summary>
            /// The list of known potential positive impacts
            /// </summary>
            public string PositiveImpacts;
        }
    }
}
