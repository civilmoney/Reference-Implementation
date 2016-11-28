#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Schema {

    /// <summary>
    /// Defines supported account attribute names and values
    /// </summary>
    public class AccountAttributes {
        public const string GoverningAuthority_Key = "ATTR-GOV";
        public const string IncomeEligibility_HealthProblem = "HLTH";
        public const string IncomeEligibility_Key = "ATTR-ELIG";
        public const string IncomeEligibility_LookingForWork = "UNEMP";
        public const string IncomeEligibility_Retired = "AGED";
        public const string IncomeEligibility_Working = "WORK";
        public const string PushNotification_Key = "ATTR-PUSH";
        public const string SkillOrService_Key = "ATTR-SKILL";

        /// <summary>
        /// Skill levels provide further context around
        /// a person's qualifications, community involvement
        /// and income eligibility
        /// </summary>
        public enum SkillLevel {
            Amateur = 0,
            Qualified = 1,
            Experienced = 2,
            Certified = 3
        }

        public class PushNotificationCsv {
            public string HttpUrl;
            public string Label;

            public PushNotificationCsv() {
            }

            public PushNotificationCsv(string csvdata) {
                int i = 0;
                Label = csvdata.NextCsvValue(ref i);
                HttpUrl = csvdata.NextCsvValue(ref i);
            }

            public override string ToString() {
                if (String.IsNullOrWhiteSpace(HttpUrl))
                    return String.Empty;
                return Label.CsvEscape() + "," + HttpUrl.CsvEscape();
            }
        }

        /// <summary>
        /// Describes a skill CSV value
        /// </summary>
        public class SkillCsv {
            public SkillLevel Level;

            public string Value;

            public SkillCsv() {
            }

            public SkillCsv(string csvdata) {
                int i = 0;
                var level = csvdata.NextCsvValue(ref i);
                int v;
                int.TryParse(level, out v);
                Level = (SkillLevel)v;
                Value = csvdata.NextCsvValue(ref i);
            }

            public override string ToString() {
                if (String.IsNullOrWhiteSpace(Value))
                    return String.Empty;
                return ((int)Level) + "," + Value.CsvEscape();
            }
        }
    }
}