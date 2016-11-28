#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM {

    /// <summary>
    /// API objects such as Account, Transaction and Vote implement this
    /// for a unified consensus algorithm
    /// </summary>
    public interface IStorable {

        /// <summary>
        /// All storable objects use UpdatedUtc as its version
        /// indicator.
        /// </summary>
        DateTime UpdatedUtc { get; }

        /// <summary>
        /// True when ConsensusCount is &gt;= the minimum number of copies required.
        /// </summary>
        bool ConsensusOK { get; }

        /// <summary>
        /// Set by the CheckConsensus helper. The number of copies
        /// that agree with the item. 
        /// </summary>
        int ConsensusCount { get; set; }

        /// <summary>
        /// Gets the deterministic storage path of the object
        /// </summary>
        string Path { get; }
    }
}