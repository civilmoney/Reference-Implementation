using Bridge.Html5;
using CM.Javascript;
using System;
using System.Text;

/*
 * Various extension methods which are useful for this Bridge.NET/javascript project.
 */

namespace CM {

    internal static class BridgeHacks {

        private static int _LabelCounter;

        public static HTMLAnchorElement A(this Node el, string html, string href, string className = null) {
            return el.AppendChild(new HTMLAnchorElement() {
                InnerHTML = html ?? String.Empty,
                Href = href,
                OnClick = (e) => {
                    e.PreventDefault();
                    e.StopPropagation();
                    App.Identity.Navigate(((HTMLAnchorElement)e.CurrentTarget).Href);
                },
                ClassName = className ?? String.Empty
            }) as HTMLAnchorElement;
        }

        public static HTMLAnchorElement A(this Node el, string html, Action<MouseEvent<HTMLAnchorElement>> onClick, string className = null) {
            return el.AppendChild(new HTMLAnchorElement() {
                InnerHTML = html ?? String.Empty,
                Href = "javascript:;",
                ClassName = className ?? String.Empty,
                OnClick = onClick
            }) as HTMLAnchorElement;
        }

        public static void AddClass(this HTMLElement el, string name) {
            if (el.ClassList != null) {
                if (!el.ClassList.Contains(name))
                    el.ClassList.Add(name);
            } else {
                // ie9
                var cur = el.ClassName ?? string.Empty;
                if (cur.IndexOf(name) > -1)
                    return;
                el.ClassName = cur + " " + name;
            }
        }

        public static HTMLElement Amount(this Node el, decimal num, string prefix = "", bool roundTo2DP = false) {
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
                SR.CHAR_DECIMAL + decimalComponentStr); //.ToString(SR.CHAR_DECIMAL + new string('0', Math.Min(2, decimalComponent.ToString().Length)))
        }

        public static HTMLElement Amount(this Node el, string prefix = "", string num = "", string dec = "") {
            var sp = el.Span(null, "amount");
            sp.Span(prefix);
            sp.Span(num);
            sp.Span(dec);
            return sp;
        }

        public static HTMLElement AmountReputation(this Node el, decimal num) {
            return el.Amount("",
                (num - (num % 1)).ToString("N0").Replace(",", SR.CHAR_THOUSAND_SEPERATOR),
                (num % 1).ToString(SR.CHAR_DECIMAL + "0"));
        }

        public static HTMLButtonElement Button(this Node el, string html) {
            var e = el.AppendChild(new HTMLButtonElement() {
                InnerHTML = html ?? String.Empty
            }) as HTMLButtonElement;
            return e;
        }

        public static HTMLButtonElement Button(this Node el, string html, Action<MouseEvent<HTMLButtonElement>> onClick) {
            var e = el.AppendChild(new HTMLButtonElement() {
                InnerHTML = html ?? String.Empty,
                OnClick = onClick
            }) as HTMLButtonElement;

            return e;
        }

        public static HTMLButtonElement Button(this Node el, string html, string hashUrl) {
            var e = el.AppendChild(new HTMLButtonElement() {
                InnerHTML = html ?? String.Empty,
                OnClick = (args) => {
                    App.Identity.Navigate(hashUrl);
                }
            }) as HTMLButtonElement;

            return e;
        }

        public static HTMLInputElement CheckBox(this Node el, string label) {
            var d = el.Span(null, "check");
            var id = "ch" + (_LabelCounter++);
            var ch = d.AppendChild(new HTMLInputElement() {
                Type = InputType.Checkbox,
                Id = id
            }) as HTMLInputElement;
            var styled = d.AppendChild(new HTMLLabelElement() { HtmlFor = id, TabIndex = 0 });
            ch["check-styled"] = styled;
            styled.AddEventListener(EventType.KeyPress,
                (e) => {
                    if (e.Target != styled)
                        return;
                    var ev = (KeyboardEvent)e;
                    if (ev.KeyCode == 13 || ev.KeyCode == 32) {
                        e.PreventDefault();
                        e.StopPropagation();
                        ch.Checked = !ch.Checked;
                        if (ch.OnChange != null)
                            ch.OnChange(null);
                    }
                });
            d.AppendChild(new HTMLLabelElement() {
                HtmlFor = id,
                InnerHTML = label
            });
            return ch;
        }

        public static void Clear(this HTMLElement el) {
            while (el.ChildElementCount > 0)
                el.RemoveChild(el.FirstChild);
        }

        public static bool ContainsClass(this HTMLElement el, string name) {
            if (el.ClassList != null) {
                return el.ClassList.Contains(name);
            } else {
                // ie9
                var cur = el.ClassName ?? string.Empty;
                return cur.IndexOf(name) != -1;
            }
        }

        public static HTMLDivElement Div(this Node el, string className = null, string html = null) {
            return el.AppendChild(new HTMLDivElement() {
                ClassName = className ?? String.Empty,
                InnerHTML = html ?? String.Empty
            }) as HTMLDivElement;
        }

        public static string GetLocalisedDescription(this CMResult res) {
            return res.ToString();
        }

        public static HTMLElement H1(this Node el, string html) {
            return el.AppendChild(new HTMLHeadingElement(HeadingType.H1) {
                InnerHTML = html ?? String.Empty
            }) as HTMLElement;
        }

        public static HTMLElement H2(this Node el, string html, string className = null) {
            return el.AppendChild(new HTMLHeadingElement(HeadingType.H2) {
                InnerHTML = html ?? String.Empty,
                ClassName = className ?? String.Empty
            }) as HTMLElement;
        }

        public static HTMLElement H3(this Node el, string html) {
            return el.AppendChild(new HTMLHeadingElement(HeadingType.H3) {
                InnerHTML = html ?? String.Empty
            }) as HTMLElement;
        }

        public static HTMLElement H4(this Node el, string html) {
            return el.AppendChild(new HTMLHeadingElement(HeadingType.H4) {
                InnerHTML = html ?? String.Empty
            }) as HTMLElement;
        }

        public static HTMLElement OnEnterKey(this HTMLElement el, Action a) {
            el.AddEventListener(EventType.KeyPress, (e) => {
                if (e.Target != el)
                    return;
                var ev = (KeyboardEvent)e;
                if (ev.KeyCode == 13) {
                    e.PreventDefault();
                    e.StopPropagation();
                    a();
                }
            });
            return el;
        }

        public static HTMLElement OnEnterKeySetFocus(this HTMLElement el, HTMLElement target) {
            el.AddEventListener(EventType.KeyPress, (e) => {
                if (e.Target != el)
                    return;
                var ev = (KeyboardEvent)e;
                if (ev.KeyCode == 13) {
                    e.PreventDefault();
                    e.StopPropagation();
                    if (target["check-styled"] != null) {
                        ((HTMLElement)target["check-styled"]).Focus();
                    } else {
                        target.Focus();
                    }
                }
            });
            return el;
        }

        public static HTMLInputElement Password(this Node el) {
            return el.AppendChild(new HTMLInputElement() {
                Type = InputType.Password
            }) as HTMLInputElement;
        }

        public static Point Position(this HTMLElement el) {
            var y = 0;
            var x = 0;
            var tmp = el;
            while (tmp != null) {
                y += tmp.OffsetTop;
                x += tmp.OffsetLeft;
                tmp = tmp.OffsetParent;
            }
            return new Point() { X = x, Y = y };
        }

        public static HTMLInputElement RadioButton(this Node el, string group, string label) {
            var d = el.Span(null, "check");
            var id = "ch" + (_LabelCounter++);
            var ch = d.AppendChild(new HTMLInputElement() {
                Type = InputType.Radio,
                Id = id,
                Name = group
            }) as HTMLInputElement;

            var styled = d.AppendChild(new HTMLLabelElement() { HtmlFor = id, TabIndex = 0 });
            ch["check-styled"] = styled;
            styled.AddEventListener(EventType.KeyPress,
                (e) => {
                    var ev = (KeyboardEvent)e;
                    if (ev.KeyCode == 13 || ev.KeyCode == 32) {
                        e.PreventDefault();
                        e.StopPropagation();
                        ch.Checked = !ch.Checked;
                        if (ch.OnChange != null)
                            ch.OnChange(null);
                    }
                });
            d.AppendChild(new HTMLLabelElement() {
                HtmlFor = id,
                InnerHTML = label
            });
            return ch;
        }

        public static void RemoveClass(this HTMLElement el, string name) {
            if (el.ClassList != null) {
                el.ClassList.Remove(name);
            } else {
                // ie9
                var cur = el.ClassName ?? string.Empty;
                if (cur.IndexOf(name) == -1)
                    return;
                el.ClassName = cur.Replace(name, "");
            }
        }

        public static void RemoveEx(this HTMLElement el) {
            Bridge.Script.Write(@"
if(typeof el.remove == 'function')
    el.remove();
else
");
            if (el.ParentElement != null)
                el.ParentElement.RemoveChild(el);
        }

        public static HTMLElement Reputation(this Node el, RecentReputation rep, bool showglyph = true, bool showlabel = true) {
            var glyph = rep == RecentReputation.Good ? CM.Javascript.Assets.SVG.CircleTick
                    : rep == RecentReputation.Overspent ? CM.Javascript.Assets.SVG.CircleError
                    : CM.Javascript.Assets.SVG.Warning;
            var colour = rep == RecentReputation.Good ? "#288600" : "#cc0000";

            var html = "";
            if (showglyph)
                html = glyph.ToString(16, 16, colour);
            if (showlabel)
                html += rep.ToLocalisedName();
            return el.Span(html);
        }

        public static void ScrollTo(this HTMLElement el) {
            var p = el.Position();
            Window.Scroll(p.X, p.Y);
        }

        public static HTMLSelectElement Select(this Node el) {
            var e = el.AppendChild(new HTMLSelectElement()) as HTMLSelectElement;
            return e;
        }

        public static HTMLElement Span(this Node el, string html = null, string className = null) {
            return el.AppendChild(new HTMLSpanElement() {
                InnerHTML = html ?? String.Empty,
                ClassName = className ?? String.Empty
            }) as HTMLElement;
        }

        public static bool StartsWith(this string src, string s, StringComparison comp) {
            if (comp == StringComparison.OrdinalIgnoreCase) {
                src = src.ToLower();
                s = s.ToLower();
            }
            return src.StartsWith(s);
        }
        public static HTMLInputElement TextBox(this Node el, string value) {
            var t = el.AppendChild(new HTMLInputElement() {
                Type = InputType.Text,
                Value = value ?? String.Empty,
            }) as HTMLInputElement;
            t.SetAttribute("autocorrect", "off");
            t.SetAttribute("autocapitalize", "off");
            t.SetAttribute("spellcheck", "false");
            //spellcheck="false"
            return t;
        }

        public static string ToLocalisedName(this RecentReputation rep) {
            var label = rep == RecentReputation.Good ? SR.LABEL_REPUTATION_GOOD
              : rep == RecentReputation.Overspent ? SR.LABEL_REPUTATION_OVERSPENT
              : CM.Javascript.SR.LABEL_REPUTATION_BAD;
            return label;
        }
        public struct Point {
            public int X;
            public int Y;
        }
    }
}

namespace CM.Schema {

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
                        s.Append(Page.HtmlEncode(skill.Value));
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
namespace System.Text {

    public static class Encoding {

        public static class UTF8 {

            public static int GetByteCount(string s) {
                return GetBytes(s).Length;
            }

            public static byte[] GetBytes(string s) {
                var arr = new byte[0];
                Bridge.Script.Write(@"
        var utf8 = unescape(encodeURIComponent(s));
        for (var i = 0; i < utf8.length; i++) {
            arr.push(utf8.charCodeAt(i));
        }
");

                return arr;
            }

            public static byte[] GetPreamble() {
                return new byte[] { 239, 187, 191 };
            }
            public static string GetString(byte[] b, int offset, int count) {
                if (b == null || b.Length == 0 || count == 0)
                    return null;
                if (b.Length < offset + count)
                    throw new ArgumentOutOfRangeException();
                var s = new System.Text.StringBuilder();
                for (int i = offset; i < offset + count; i++) {
                    switch (b[i] >> 4) {
                        case 0:
                        case 1:
                        case 2:
                        case 3:
                        case 4:
                        case 5:
                        case 6:
                        case 7:
                            s.Append(String.FromCharCode(b[i]));
                            break;

                        case 12:
                        case 13:
                            s.Append(String.FromCharCode((b[i] & 0x1f) << 6 | b[i + 1] & 0x3f));
                            i++;
                            break;

                        case 14:
                            s.Append(String.FromCharCode((b[i] & 0x0F) << 12 | b[i + 1] & 0x3f << 6 | b[i + 2] & 0x3f));
                            i += 2;
                            break;
                    }
                }
                return s.ToString();
            }
        }
    }
}