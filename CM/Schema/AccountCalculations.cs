#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;

namespace CM.Schema {

    /// <summary>
    /// Describes optional CALC-XXX attributes exposed by Account messages.
    /// </summary>
    public class AccountCalculations {
        internal readonly Account Account;

        public AccountCalculations(Account account) {
            if (account == null) throw new ArgumentNullException("account");
            Account = account;
        }

        /// <summary>
        /// The date of the last transaction this account was involved in.
        /// This can help corroborate DHT peer account statements.
        /// </summary>
        public DateTime? LastTransactionUtc {
            get {
                if (String.IsNullOrWhiteSpace(Account.Values["CALC-LAST-TRANS"]))
                    return null;
                return Account.Values.Get<DateTime>("CALC-LAST-TRANS");
            }
            set {
                if (value != null)
                    Account.Values.Set<DateTime>("CALC-LAST-TRANS", value.Value);
                else
                    Account.Values.RemoveAll("CALC-LAST-TRANS");
            }
        }

        /// <summary>
        /// Recent credits in the past 12 months.
        /// </summary>
        public decimal? RecentCredits {
            get {
                if (String.IsNullOrWhiteSpace(Account.Values["CALC-CREDITS"]))
                    return null;
                return Account.Values.Get<decimal>("CALC-CREDITS");
            }
            set {
                if (value != null)
                    Account.Values.Set<decimal>("CALC-CREDITS", value.Value);
                else
                    Account.Values.RemoveAll("CALC-CREDITS");
            }
        }

        /// <summary>
        /// Recent debits in the past 12 months.
        /// </summary>
        public decimal? RecentDebits {
            get {
                if (String.IsNullOrWhiteSpace(Account.Values["CALC-DEBITS"]))
                    return null;
                return Account.Values.Get<decimal>("CALC-DEBITS");
            }
            set {
                if (value != null)
                    Account.Values.Set<decimal>("CALC-DEBITS", value.Value);
                else
                    Account.Values.RemoveAll("CALC-DEBITS");
            }
        }

        /// <summary>
        ///  MIN(1, (BASIC-YEARLY-ALLOWANCE + RECENT-CREDITS) / ( RECENT-DEBITS + BASIC-YEARLY-ALLOWANCE * 2 )) * 100
        /// </summary>
        public decimal? RecentReputation {
            get {
                if (String.IsNullOrWhiteSpace(Account.Values["CALC-REP"]))
                    return null;
                return Account.Values.Get<decimal>("CALC-REP");
            }
            set {
                if (value != null)
                    Account.Values.Set<decimal>("CALC-REP", value.Value);
                else
                    Account.Values.RemoveAll("CALC-REP");
            }
        }
        /// <summary>
        /// True if the account has at least 1 transaction every month 
        /// for the last 12 months with multiple parties. This is to deter automated vote stuffing.
        /// </summary>
        public bool IsEligibleForVoting {
            get {
                return Account.Values.Get<bool>("CAN-VOTE");
            }
            set {
                Account.Values.Set<bool>("CAN-VOTE", value);
            }
        }
        /// <summary>
        /// Given a list of untrusted AccountCalculations from various servers, come to a consensus
        /// regarding credits and debits on the account.
        /// </summary>
        /// <param name="pool">The pool of untrusted calculations.</param>
        /// <param name="bestCount">Pointer to receive the number of servers that agreed with the calculation.</param>
        /// <returns>The calculation that most servers agree with or null.</returns>
        public static AccountCalculations GetConsensus(List<AccountCalculations> pool, out int bestCount) {
            bestCount = 0;
            if (pool.Count == 0)
                return null;
            var counts = new Dictionary<string, int>();
            AccountCalculations best = null;

            for (int i = 0; i < pool.Count; i++) {
                var c = pool[i];
                var key = c.RecentDebits.GetValueOrDefault() + "_" + c.RecentCredits.GetValueOrDefault() + "_" + c.IsEligibleForVoting;
                int count;
                counts.TryGetValue(key, out count);
                count++;
                counts[key] = count;
                if (bestCount < count) {
                    best = c;
                    bestCount = count;
                }
            }
            return best;
        }
    }
}