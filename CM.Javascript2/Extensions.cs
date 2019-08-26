using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CM.JS;
using SichboUI;
using static Retyped.dom;

namespace CM.Schema {
    static class Extensions {
        public static string ToLocalisedName(this RecentReputation rep) {
            var label = rep == RecentReputation.Good ? SR.LABEL_REPUTATION_GOOD
              : rep == RecentReputation.Overspent ? SR.LABEL_REPUTATION_OVERSPENT
              : CM.JS.SR.LABEL_REPUTATION_BAD;
            return label;
        }
        public static string GetLocalisedDescription(this CMResult res) {
            return res.ToString();
        }
        public static HTMLElement AmountReputation(this Node el, decimal num) {
            return el.Amount("",
                (num - (num % 1)).ToString("N0").Replace(",", SR.CHAR_THOUSAND_SEPERATOR),
                (num % 1).ToString(SR.CHAR_DECIMAL + "0"));
        }
        public static HTMLElement Amount(this Node el, decimal num, string prefix = "", bool roundTo2DP = false, string status = "") {
            var amount = Math.Abs(num);
            var neg = num < 0 ? " - " : "";
            var decimalComponent = (amount % 1);
            var decimalComponentStr = decimalComponent.ToString(roundTo2DP ? "0.00" : "0.000000")
                .Substring(2);
            decimalComponentStr = decimalComponentStr.TrimEnd(new char[] { '0' });
            while (decimalComponentStr.Length < 2)
                decimalComponentStr += "0";
            return el.Amount(prefix,
                neg + (amount - decimalComponent).ToString("N0").Replace(",", SR.CHAR_THOUSAND_SEPERATOR),
                SR.CHAR_DECIMAL + decimalComponentStr,
                status);
        }
        public static HTMLElement Amount(this Node el, string prefix = "", string num = "", string dec = "", string status = "") {
            var sp = el.Span("amount");
            sp.Span(text: prefix);
            sp.Span(text: num);
            if (String.IsNullOrEmpty(status)) {
                sp.Span(text: dec);
            } else {
                sp.Span().innerHTML = $"<span>{dec}</span><span>{status}</span>";
            }
            return sp;
        }
        /// <summary>
        /// includeAge  = false rationale:
        /// A new account must be treated by society just as equally as an established one. 
        /// The goal of //c is to level the playing field between the 'haves' and the 'have-nots'.
        /// </summary>
        public static void AppendAccountInfo(this HTMLElement div, Account a, bool includeAge = false) {

            // Check governing authority keys if required
            var gaName = ISO31662.GetName(a.ID);
            if (gaName != null) {

                var check = new AsyncRequest<bool>();
                a.CheckIsValidGoverningAuthority(check, JSCryptoFunctions.Identity);
                if (!check.Item) {
                    div.Div("warning", text: SR.LABEL_STATUS_GOVERNINGAUTHORITY_CHECK_FAILED);
                    return;
                } else {
                    div.Div("authority", text: String.Format(SR.TITLE_GOVERNINGAUTHORITY_FOR_BLANK, gaName));
                }

            } else {

                var calc = a.AccountCalculations;
                var age = (System.DateTime.UtcNow - a.CreatedUtc);
                HTMLElement bal = null;
                if (calc != null
                    && calc.RecentCredits != null
                    && calc.RecentDebits != null
                    && calc.RecentReputation != null) {

                    decimal rr = 0;
                    RecentReputation rep;
                    Helpers.CalculateRecentReputation(calc.RecentCredits.Value, calc.RecentDebits.Value, out rr, out rep);

                    div.Div("rep").AmountReputation(rr);

                    var glyph = rep == RecentReputation.Good ? Glyphs.CircleTick
                      : rep == RecentReputation.Overspent ? Glyphs.CircleError
                      : Glyphs.Warning;

                    div.Div("standing").Div(
                        style: glyph.CSS(rep == RecentReputation.Good ? Colors.C1 : "#cc0000")
                        + "background-size: 1em;background-position:left center; padding-left: 1.5em;",
                        text: rep.ToLocalisedName());

                    bal = new HTMLDivElement() {
                        className = "bal"
                    };
                    var balance = Helpers.CalculateAccountBalance(calc.RecentCredits.Value, calc.RecentDebits.Value);
                    bal.Div().Amount(balance, prefix: Constants.Symbol, roundTo2DP: true);
                    bal.Div(style: "font-size:14px;", text: ("USD " + (balance < 0 ? "-" : "") + " $" + Math.Abs(balance * Constants.USDExchange).ToString("N0")));
                }

                if (includeAge) {
                    div.Div("age", text: age.TotalDays / 365 > 1 ? string.Format(SR.LABEL_YEARS_OLD, ((int)age.TotalDays / 365).ToString("N0"))
                                  : string.Format(SR.LABEL_DAYS_OLD, ((int)age.TotalDays).ToString("N0")));
                }

                if (!String.IsNullOrEmpty(a.Values[AccountAttributes.IncomeEligibility_Key])) {
                    div.Div("income", text: a.GetIncomeEligibilityLocalised());
                }

                var region = ISO31662.GetName(a.Iso31662Region);
                if (region != null) {
                    div.Div("region", text: region);
                }

                div.Div("skills", text: a.GetSkillsSummary());
                if (bal != null)
                    div.appendChild(bal);
            }

        }
    }
    partial class Account {

        public string GetIncomeEligibilityLocalised() {
            var eligibility = Values[AccountAttributes.IncomeEligibility_Key];
            switch (eligibility) {
                case AccountAttributes.IncomeEligibility_Working: eligibility = SR.LABEL_INCOME_ELIGIBILITY_WORKING; break;
                case AccountAttributes.IncomeEligibility_LookingForWork: eligibility = SR.LABEL_INCOME_ELIGIBILITY_LOOKING_FOR_WORK; break;
                case AccountAttributes.IncomeEligibility_HealthProblem: eligibility = SR.LABEL_INCOME_ELIGIBILITY_HEALTH_PROBLEM; break;
                case AccountAttributes.IncomeEligibility_Retired: eligibility = SR.LABEL_INCOME_ELIGIBILITY_RETIRED; break;
                default: eligibility = SR.LABEL_VALUE_NOT_SET; break;
            }
            return eligibility;
        }

        public string GetOneLineAttributeSummaryHtml() {
            var s = new StringBuilder();
            var age = (System.DateTime.UtcNow - CreatedUtc);
            s.Append("<span>");
            s.Append(
                age.TotalDays / 365 > 1 ? string.Format(SR.LABEL_YEARS_OLD, ((int)age.TotalDays / 365))
                : string.Format(SR.LABEL_DAYS_OLD, ((int)age.TotalDays)));
            s.Append("</span>");

            var eligibility = GetIncomeEligibilityLocalised();
            if (eligibility != SR.LABEL_VALUE_NOT_SET) {
                s.Append("<span>&bull;</span><span>" + eligibility + "</span>");
            }
            bool separate = s.Length > 0;
            for (int i = 0; i < Values.Count; i++) {
                var v = Values[i];
                if (String.Equals(v.Name, "ATTR-SKILL", StringComparison.OrdinalIgnoreCase)
                    && !String.IsNullOrEmpty(v.Value)) {
                    AccountAttributes.SkillCsv skill = new AccountAttributes.SkillCsv(v.Value);
                    if (!String.IsNullOrEmpty(skill.Value)) {
                        if (separate) {
                            s.Append("<span>&bull;</span>");
                            separate = false;
                        }
                        s.Append("<span>");
                        s.Append(skill.Value.HtmlEncode());
                        switch (skill.Level) {
                            case AccountAttributes.SkillLevel.Amateur: break;
                            case AccountAttributes.SkillLevel.Certified:
                                s.Append(" (" + SR.LABEL_SKILL_LEVEL_CERTIFIED + ")");
                                break;

                            case AccountAttributes.SkillLevel.Experienced:
                                s.Append(" (" + SR.LABEL_SKILL_LEVEL_EXPERIENCED + ")");
                                break;

                            case AccountAttributes.SkillLevel.Qualified:
                                s.Append(" (" + SR.LABEL_SKILL_LEVEL_QUALIFIED + ")");
                                break;
                        }
                        s.Append("</span> ");
                    }
                }
            }
            return s.ToString();
        }

        /// <summary>
        /// Javascript Extension method -- pulls skills and levels and produces a
        /// line delimited list.
        /// </summary>
        /// <returns></returns>
        public string GetSkillsSummary() {
            var s = new StringBuilder();
            for (int i = 0; i < Values.Count; i++) {
                var v = Values[i];
                if (String.Equals(v.Name, "ATTR-SKILL", StringComparison.OrdinalIgnoreCase)
                    && !String.IsNullOrEmpty(v.Value)) {
                    AccountAttributes.SkillCsv skill = new AccountAttributes.SkillCsv(v.Value);
                    if (!String.IsNullOrEmpty(skill.Value)) {
                        if (s.Length > 0)
                            s.Append("\n");
                        s.Append(skill.Value);
                        switch (skill.Level) {
                            case AccountAttributes.SkillLevel.Amateur: break;
                            case AccountAttributes.SkillLevel.Certified:
                                s.Append(" (" + SR.LABEL_SKILL_LEVEL_CERTIFIED.ToLower() + ")");
                                break;

                            case AccountAttributes.SkillLevel.Experienced:
                                s.Append(" (" + SR.LABEL_SKILL_LEVEL_EXPERIENCED.ToLower() + ")");
                                break;

                            case AccountAttributes.SkillLevel.Qualified:
                                s.Append(" (" + SR.LABEL_SKILL_LEVEL_QUALIFIED.ToLower() + ")");
                                break;
                        }
                    }
                }
            }
            return s.ToString();
        }
    }
}