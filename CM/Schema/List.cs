#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Schema {

    /// <summary>
    /// Describes a DHT peer search request. The search path is specified as the argument of the CMD LIST line.
    /// </summary>
    public class ListRequest : Message {

        /// <summary>
        /// Currently always 1
        /// </summary>
        public uint APIVersion { get { return Values.Get<uint>("VER"); } set { Values.Set<uint>("VER", value); } }

        /// <summary>
        /// The item Updated date to start from
        /// </summary>
        public DateTime UpdatedUtcFromInclusive { get { return Values.Get<DateTime>("UTC-FROM"); } set { Values.Set<DateTime>("UTC-FROM", value); } }

        /// <summary>
        /// The item Updated date to stop at
        /// </summary>
        public DateTime UpdatedUtcToExclusive { get { return Values.Get<DateTime>("UTC-TO"); } set { Values.Set<DateTime>("UTC-TO", value); } }

        /// <summary>
        /// The maximum number of results to return. The default value is 1000 when not specified.
        /// </summary>
        public uint Max { get { return Values.Get<uint>("MAX"); } set { Values.Set<uint>("MAX", value); } }

        /// <summary>
        /// The starting index.
        /// </summary>
        public uint StartAt { get { return Values.Get<uint>("START"); } set { Values.Set<uint>("START", value); } }

        /// <summary>
        /// For Accounts: [UTC | UPD-UTC | ID] [ASC | DESC]
        /// For Transactions: [UTC | UPD-UTC | PYR-ID | PYE-ID | AMNT] [ASC | DESC]
        /// For Votes: [UTC | UPD-UTC | VTR-ID] [ASC | DESC]
        /// </summary>
        public string Sort { get { return this["SORT"]; } set { this["SORT"] = value; } }
    }

    /// <summary>
    /// Describes a DHT peer search reply
    /// </summary>
    public class ListResponse : Message {

        /// <summary>
        /// The starting index for this result
        /// </summary>
        public uint StartAt { get { return Values.Get<uint>("START"); } set { Values.Set<uint>("START", value); } }

        /// <summary>
        /// The number of items in this result
        /// </summary>
        public uint Count { get { return Values.Get<uint>("COUNT"); } set { Values.Set<uint>("COUNT", value); } }

        /// <summary>
        /// The total number of records available
        /// </summary>
        public uint Total { get { return Values.Get<uint>("TOTAL"); } set { Values.Set<uint>("TOTAL", value); } }
    }
}