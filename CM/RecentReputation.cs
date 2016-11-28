#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM {

    public enum RecentReputation {
        Good,
        Overspent,
        Bad
    }

    partial class Helpers {

        public static void CalculateRecentReputation(decimal recentCredits, decimal recentDebits, out decimal val, out RecentReputation status) {
            // MIN(1, MAX(0, (BASIC-YEARLY-INCOME * 2 + CREDITS - DEBITS) / (BASIC-YEARLY-INCOME * 4))) * 100
            val = Math.Round(Math.Max(0, Math.Min(1, (Constants.BasicYearlyAllowance * 2 + recentCredits - recentDebits) / (Constants.BasicYearlyAllowance * 4))) * 100, 1);
            // 1 year of basic income is 25%
            status = val >= 25 ? RecentReputation.Good
                // Anyone below 25 % is spending more than their annual allowance
                : val > 0 ? RecentReputation.Overspent
                // Anyone at zero has spent 2 years or more of income and should probably be
                // declined unless there is a compelling reason.
                : RecentReputation.Bad;
        }
    }
}