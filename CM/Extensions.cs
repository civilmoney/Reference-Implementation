#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System.Collections.Generic;
using System.Text;

namespace CM {

    /// <summary>
    /// Useful CLR extensions
    /// </summary>
    public static class Extensions {

        /// <summary>
        /// Because Environment.NewLine in unix is different.
        /// </summary>
        public static void CRLF(this StringBuilder s, string line = null) {
            s.Append(line);
            s.Append("\r\n");
        }

        /// <summary>
        /// If necessary, encloses the string in quotes and escapes quote characters.
        /// </summary>
        /// <param name="s">The string to escape.</param>
        /// <returns>A CSV value.</returns>
        public static string CsvEscape(this string s) {
            if (s == null)
                return string.Empty;
            if (s.IndexOf(',') == -1
                && s.IndexOf('\n') == -1
                && s.IndexOf('\r') == -1
                && s.IndexOf('"') == -1)
                return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Reads CSV lines and returns an array of values. Multi-line is supported.
        /// </summary>
        /// <param name="document">The csv document to parse.</param>
        /// <param name="i">The current document index.</param>
        /// <returns>An array of values or null if the end of document is reached.</returns>
        public static string[] NextCsvLine(this string document, ref int i) {
            if (i >= document.Length)
                return null;
            var s = new StringBuilder();
            bool inValue = false;
            bool inString = false;
            var ar = new List<string>();
            for (; i < document.Length; i++) {
                var c = document[i];
                if (c == '\r' && !inString) {
                    if (i + 1 >= document.Length
                        || document[i + 1] == '\n') {
                        i += 2;
                        // end of line
                        ar.Add(s.ToString());
                        s.Remove(0, s.Length);
                        return ar.ToArray();
                    }
                }
                if (!inValue) {
                    if (char.IsWhiteSpace(c) || c == ',')
                        continue;
                    inValue = true;
                    if (c == '"') {
                        inString = true;
                        continue;
                    }
                }
                if (inString && c == '"') { // handle "" escape
                    if (i + 1 >= document.Length
                        || document[i + 1] != '"') {
                        ar.Add(s.ToString());
                        s.Remove(0, s.Length);
                        inString = false;
                        inValue = false;
                        continue;
                    } else {
                        // double quote
                        i++;
                    }
                } else if (!inString) {
                    if (c == ',') {
                        ar.Add(s.ToString());
                        s.Remove(0, s.Length);
                        inValue = false;
                        continue;
                    }
                }

                s.Append(c);
            }
            ar.Add(s.ToString());
            return ar.ToArray();
        }

        /// <summary>
        /// Reads a CSV value at the specified index. Supports quote characters.
        /// </summary>
        /// <param name="line">The string to read from</param>
        /// <param name="i">The index to begin reading.</param>
        /// <returns>CSV delimited value</returns>
        public static string NextCsvValue(this string line, ref int i) {
            var s = new StringBuilder();
            bool inValue = false;
            bool inString = false;
            for (; i < line.Length; i++) {
                var c = line[i];
                if (!inValue) {
                    if (char.IsWhiteSpace(c) || c == ',')
                        continue;
                    inValue = true;
                    if (c == '"') {
                        inString = true;
                        continue;
                    }
                }
                if (inString && c == '"') { // handle "" escape
                    if (i + 1 >= line.Length
                        || line[i + 1] != '"') {
                        i++;
                        return s.ToString().Trim();
                    }
                } else if (!inString) {
                    if (c == ',') {
                        i++;
                        return s.ToString().Trim();
                    }
                }

                s.Append(c);
            }
            return s.ToString();
        }
    }
}