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

#pragma warning disable CS0162 // unreachable code where debugging constants are in effect.

namespace CM {

    public static partial class Helpers {

        public static string DateToISO8601(DateTime date) {
            if (date == DateTime.MinValue)
                return null;
            return date.ToString("s");
        }

        public static DateTime DateFromISO8601(string date) {
            DateTime d;
            DateFromISO8601(date, out d);
            return d;
        }

        static Helpers() {
            var ar = new string[] {
                // used
                "register", "history",
                "status", "api", "regions", "language",
                // potentially useful later
                "help", "faq", "support", "settings", "peers", "vote", "votes",
                "forum", "discuss", "admin", "src", "img", "css", "bin", "servers",
                "about", "privacy", "privacy-policy", "terms", "legal","disclaimer",
                "wiki", "seeds", "network",
                "download", "downloads", "secure", "info", "json", "xml", "contact",
                "config", "maintenance", "report", "reports", "translate", "apps", "events",
                "groups", "dev", "contributing", "docs", "documentation", "calendar",
                "ledger", "accounts", "blog", "announcements", "media", "videos",
                "video", "rss", "live"
            };
            ReservedPathNames = new Dictionary<string, string>();
            for (int i = 0; i < ar.Length; i++)
                ReservedPathNames.Add(ar[i], null);
        }

        /// <summary>
        /// Reserved path names which may or may not be desired
        /// at some future point in time. Basically we don't
        /// want somebody registering these as their account name
        /// since account URLs are on the civil.money root.
        /// </summary>
        public static readonly Dictionary<string, string> ReservedPathNames; // Dictionary instead of HashSet because of Bridge.NET restriction.

        private const string UnicodeRanges =
            @"\u0900-\u097F" //Hindi
           + @"\u4e00-\u9faf" //CJK
           + @"\u3400-\u4dbf" //CJK - rare
           + @"\uAC00-\uD7AF" // Hangul (Korean)
           ;

#if JAVASCRIPT
        public static bool IsISO8601(string date) {
            if (date == null) return false;
            //2016-03-08T15:00:00.000Z
            return date.Trim().Match(@"^\d{4}\-\d{2}\-\d{2}T\d{2}\:\d{2}\:\d{2}$")!=null;
        }

        public static bool DateFromISO8601(string date, out DateTime d) {
            if (!IsISO8601(date)) {
                d = DateTime.MinValue;
                return false;
            }
            // AssumeLocal is important so .NET doesn't change the DateTime representation on us.
            return DateTime.TryParse((date ?? string.Empty).Trim(), System.Globalization.CultureInfo.InvariantCulture, out d, false);
        }
         /// <summary>
        /// Returns true if the ID is valid. The actual validation should be:
        /// <para>
        /// ^[\p{Ll}|\p{Mn}][\p{Ll}|\p{Mn}|0-9|\.|\-]{2,47}$
        /// </para>
        /// </summary>
        public static bool IsIDValid(string id) {
            // Since javascript doesn't support \p{L}
            var L = ""
            // Ll - lowercase
            + @"\u0061-\u007a\u00aa\u00b5\u00ba\u00df-\u00f6\u00f8-\u00ff\u0101\u0103\u0105\u0107\u0109\u010b\u010d\u010f\u0111\u0113\u0115\u0117\u0119\u011b\u011d\u011f\u0121\u0123\u0125\u0127\u0129\u012b\u012d\u012f\u0131\u0133\u0135\u0137\u0138\u013a\u013c\u013e\u0140\u0142\u0144\u0146\u0148\u0149\u014b\u014d\u014f\u0151\u0153\u0155\u0157\u0159\u015b\u015d\u015f\u0161\u0163\u0165\u0167\u0169\u016b\u016d\u016f\u0171\u0173\u0175\u0177\u017a\u017c\u017e-\u0180\u0183\u0185\u0188\u018c\u018d\u0192\u0195\u0199-\u019b\u019e\u01a1\u01a3\u01a5\u01a8\u01aa\u01ab\u01ad\u01b0\u01b4\u01b6\u01b9\u01ba\u01bd-\u01bf\u01c6\u01c9\u01cc\u01ce\u01d0\u01d2\u01d4\u01d6\u01d8\u01da\u01dc\u01dd\u01df\u01e1\u01e3\u01e5\u01e7\u01e9\u01eb\u01ed\u01ef\u01f0\u01f3\u01f5\u01f9\u01fb\u01fd\u01ff\u0201\u0203\u0205\u0207\u0209\u020b\u020d\u020f\u0211\u0213\u0215\u0217\u0219\u021b\u021d\u021f\u0221\u0223\u0225\u0227\u0229\u022b\u022d\u022f\u0231\u0233-\u0239\u023c\u023f\u0240\u0242\u0247\u0249\u024b\u024d\u024f-\u0293\u0295-\u02af\u037b-\u037d\u0390\u03ac-\u03ce\u03d0\u03d1\u03d5-\u03d7\u03d9\u03db\u03dd\u03df\u03e1\u03e3\u03e5\u03e7\u03e9\u03eb\u03ed\u03ef-\u03f3\u03f5\u03f8\u03fb\u03fc\u0430-\u045f\u0461\u0463\u0465\u0467\u0469\u046b\u046d\u046f\u0471\u0473\u0475\u0477\u0479\u047b\u047d\u047f\u0481\u048b\u048d\u048f\u0491\u0493\u0495\u0497\u0499\u049b\u049d\u049f\u04a1\u04a3\u04a5\u04a7\u04a9\u04ab\u04ad\u04af\u04b1\u04b3\u04b5\u04b7\u04b9\u04bb\u04bd\u04bf\u04c2\u04c4\u04c6\u04c8\u04ca\u04cc\u04ce\u04cf\u04d1\u04d3\u04d5\u04d7\u04d9\u04db\u04dd\u04df\u04e1\u04e3\u04e5\u04e7\u04e9\u04eb\u04ed\u04ef\u04f1\u04f3\u04f5\u04f7\u04f9\u04fb\u04fd\u04ff\u0501\u0503\u0505\u0507\u0509\u050b\u050d\u050f\u0511\u0513\u0561-\u0587\u1d00-\u1d2b\u1d62-\u1d77\u1d79-\u1d9a\u1e01\u1e03\u1e05\u1e07\u1e09\u1e0b\u1e0d\u1e0f\u1e11\u1e13\u1e15\u1e17\u1e19\u1e1b\u1e1d\u1e1f\u1e21\u1e23\u1e25\u1e27\u1e29\u1e2b\u1e2d\u1e2f\u1e31\u1e33\u1e35\u1e37\u1e39\u1e3b\u1e3d\u1e3f\u1e41\u1e43\u1e45\u1e47\u1e49\u1e4b\u1e4d\u1e4f\u1e51\u1e53\u1e55\u1e57\u1e59\u1e5b\u1e5d\u1e5f\u1e61\u1e63\u1e65\u1e67\u1e69\u1e6b\u1e6d\u1e6f\u1e71\u1e73\u1e75\u1e77\u1e79\u1e7b\u1e7d\u1e7f\u1e81\u1e83\u1e85\u1e87\u1e89\u1e8b\u1e8d\u1e8f\u1e91\u1e93\u1e95-\u1e9b\u1ea1\u1ea3\u1ea5\u1ea7\u1ea9\u1eab\u1ead\u1eaf\u1eb1\u1eb3\u1eb5\u1eb7\u1eb9\u1ebb\u1ebd\u1ebf\u1ec1\u1ec3\u1ec5\u1ec7\u1ec9\u1ecb\u1ecd\u1ecf\u1ed1\u1ed3\u1ed5\u1ed7\u1ed9\u1edb\u1edd\u1edf\u1ee1\u1ee3\u1ee5\u1ee7\u1ee9\u1eeb\u1eed\u1eef\u1ef1\u1ef3\u1ef5\u1ef7\u1ef9\u1f00-\u1f07\u1f10-\u1f15\u1f20-\u1f27\u1f30-\u1f37\u1f40-\u1f45\u1f50-\u1f57\u1f60-\u1f67\u1f70-\u1f7d\u1f80-\u1f87\u1f90-\u1f97\u1fa0-\u1fa7\u1fb0-\u1fb4\u1fb6\u1fb7\u1fbe\u1fc2-\u1fc4\u1fc6\u1fc7\u1fd0-\u1fd3\u1fd6\u1fd7\u1fe0-\u1fe7\u1ff2-\u1ff4\u1ff6\u1ff7\u2071\u207f\u210a\u210e\u210f\u2113\u212f\u2134\u2139\u213c\u213d\u2146-\u2149\u214e\u2184\u2c30-\u2c5e\u2c61\u2c65\u2c66\u2c68\u2c6a\u2c6c\u2c74\u2c76\u2c77\u2c81\u2c83\u2c85\u2c87\u2c89\u2c8b\u2c8d\u2c8f\u2c91\u2c93\u2c95\u2c97\u2c99\u2c9b\u2c9d\u2c9f\u2ca1\u2ca3\u2ca5\u2ca7\u2ca9\u2cab\u2cad\u2caf\u2cb1\u2cb3\u2cb5\u2cb7\u2cb9\u2cbb\u2cbd\u2cbf\u2cc1\u2cc3\u2cc5\u2cc7\u2cc9\u2ccb\u2ccd\u2ccf\u2cd1\u2cd3\u2cd5\u2cd7\u2cd9\u2cdb\u2cdd\u2cdf\u2ce1\u2ce3\u2ce4\u2d00-\u2d25\ufb00-\ufb06\ufb13-\ufb17\uff41-\uff5a"
            // Lu - uppercase
            + @"\u0041-\u005a\u00c0-\u00d6\u00d8-\u00de\u0100\u0102\u0104\u0106\u0108\u010a\u010c\u010e\u0110\u0112\u0114\u0116\u0118\u011a\u011c\u011e\u0120\u0122\u0124\u0126\u0128\u012a\u012c\u012e\u0130\u0132\u0134\u0136\u0139\u013b\u013d\u013f\u0141\u0143\u0145\u0147\u014a\u014c\u014e\u0150\u0152\u0154\u0156\u0158\u015a\u015c\u015e\u0160\u0162\u0164\u0166\u0168\u016a\u016c\u016e\u0170\u0172\u0174\u0176\u0178\u0179\u017b\u017d\u0181\u0182\u0184\u0186\u0187\u0189-\u018b\u018e-\u0191\u0193\u0194\u0196-\u0198\u019c\u019d\u019f\u01a0\u01a2\u01a4\u01a6\u01a7\u01a9\u01ac\u01ae\u01af\u01b1-\u01b3\u01b5\u01b7\u01b8\u01bc\u01c4\u01c7\u01ca\u01cd\u01cf\u01d1\u01d3\u01d5\u01d7\u01d9\u01db\u01de\u01e0\u01e2\u01e4\u01e6\u01e8\u01ea\u01ec\u01ee\u01f1\u01f4\u01f6-\u01f8\u01fa\u01fc\u01fe\u0200\u0202\u0204\u0206\u0208\u020a\u020c\u020e\u0210\u0212\u0214\u0216\u0218\u021a\u021c\u021e\u0220\u0222\u0224\u0226\u0228\u022a\u022c\u022e\u0230\u0232\u023a\u023b\u023d\u023e\u0241\u0243-\u0246\u0248\u024a\u024c\u024e\u0386\u0388-\u038a\u038c\u038e\u038f\u0391-\u03a1\u03a3-\u03ab\u03d2-\u03d4\u03d8\u03da\u03dc\u03de\u03e0\u03e2\u03e4\u03e6\u03e8\u03ea\u03ec\u03ee\u03f4\u03f7\u03f9\u03fa\u03fd-\u042f\u0460\u0462\u0464\u0466\u0468\u046a\u046c\u046e\u0470\u0472\u0474\u0476\u0478\u047a\u047c\u047e\u0480\u048a\u048c\u048e\u0490\u0492\u0494\u0496\u0498\u049a\u049c\u049e\u04a0\u04a2\u04a4\u04a6\u04a8\u04aa\u04ac\u04ae\u04b0\u04b2\u04b4\u04b6\u04b8\u04ba\u04bc\u04be\u04c0\u04c1\u04c3\u04c5\u04c7\u04c9\u04cb\u04cd\u04d0\u04d2\u04d4\u04d6\u04d8\u04da\u04dc\u04de\u04e0\u04e2\u04e4\u04e6\u04e8\u04ea\u04ec\u04ee\u04f0\u04f2\u04f4\u04f6\u04f8\u04fa\u04fc\u04fe\u0500\u0502\u0504\u0506\u0508\u050a\u050c\u050e\u0510\u0512\u0531-\u0556\u10a0-\u10c5\u1e00\u1e02\u1e04\u1e06\u1e08\u1e0a\u1e0c\u1e0e\u1e10\u1e12\u1e14\u1e16\u1e18\u1e1a\u1e1c\u1e1e\u1e20\u1e22\u1e24\u1e26\u1e28\u1e2a\u1e2c\u1e2e\u1e30\u1e32\u1e34\u1e36\u1e38\u1e3a\u1e3c\u1e3e\u1e40\u1e42\u1e44\u1e46\u1e48\u1e4a\u1e4c\u1e4e\u1e50\u1e52\u1e54\u1e56\u1e58\u1e5a\u1e5c\u1e5e\u1e60\u1e62\u1e64\u1e66\u1e68\u1e6a\u1e6c\u1e6e\u1e70\u1e72\u1e74\u1e76\u1e78\u1e7a\u1e7c\u1e7e\u1e80\u1e82\u1e84\u1e86\u1e88\u1e8a\u1e8c\u1e8e\u1e90\u1e92\u1e94\u1ea0\u1ea2\u1ea4\u1ea6\u1ea8\u1eaa\u1eac\u1eae\u1eb0\u1eb2\u1eb4\u1eb6\u1eb8\u1eba\u1ebc\u1ebe\u1ec0\u1ec2\u1ec4\u1ec6\u1ec8\u1eca\u1ecc\u1ece\u1ed0\u1ed2\u1ed4\u1ed6\u1ed8\u1eda\u1edc\u1ede\u1ee0\u1ee2\u1ee4\u1ee6\u1ee8\u1eea\u1eec\u1eee\u1ef0\u1ef2\u1ef4\u1ef6\u1ef8\u1f08-\u1f0f\u1f18-\u1f1d\u1f28-\u1f2f\u1f38-\u1f3f\u1f48-\u1f4d\u1f59\u1f5b\u1f5d\u1f5f\u1f68-\u1f6f\u1fb8-\u1fbb\u1fc8-\u1fcb\u1fd8-\u1fdb\u1fe8-\u1fec\u1ff8-\u1ffb\u2102\u2107\u210b-\u210d\u2110-\u2112\u2115\u2119-\u211d\u2124\u2126\u2128\u212a-\u212d\u2130-\u2133\u213e\u213f\u2145\u2183\u2c00-\u2c2e\u2c60\u2c62-\u2c64\u2c67\u2c69\u2c6b\u2c75\u2c80\u2c82\u2c84\u2c86\u2c88\u2c8a\u2c8c\u2c8e\u2c90\u2c92\u2c94\u2c96\u2c98\u2c9a\u2c9c\u2c9e\u2ca0\u2ca2\u2ca4\u2ca6\u2ca8\u2caa\u2cac\u2cae\u2cb0\u2cb2\u2cb4\u2cb6\u2cb8\u2cba\u2cbc\u2cbe\u2cc0\u2cc2\u2cc4\u2cc6\u2cc8\u2cca\u2ccc\u2cce\u2cd0\u2cd2\u2cd4\u2cd6\u2cd8\u2cda\u2cdc\u2cde\u2ce0\u2ce2\uff21-\uff3a"
            // Lt - titlecase
            + @"\u01c5\u01c8\u01cb\u01f2\u1f88-\u1f8f\u1f98-\u1f9f\u1fa8-\u1faf\u1fbc\u1fcc\u1ffc"
            // Lm - modifiers
            + @"\u02b0-\u02c1\u02c6-\u02d1\u02e0-\u02e4\u02ee\u037a\u0559\u0640\u06e5\u06e6\u07f4\u07f5\u07fa\u0e46\u0ec6\u10fc\u17d7\u1843\u1d2c-\u1d61\u1d78\u1d9b-\u1dbf\u2090-\u2094\u2d6f\u3005\u3031-\u3035\u303b\u309d\u309e\u30fc-\u30fe\ua015\ua717-\ua71a\uff70\uff9e\uff9f"
            // Lo - letters without casing
            + @"\u01bb\u01c0-\u01c3\u0294\u05d0-\u05ea\u05f0-\u05f2\u0621-\u063a\u0641-\u064a\u066e\u066f\u0671-\u06d3\u06d5\u06ee\u06ef\u06fa-\u06fc\u06ff\u0710\u0712-\u072f\u074d-\u076d\u0780-\u07a5\u07b1\u07ca-\u07ea\u0904-\u0939\u093d\u0950\u0958-\u0961\u097b-\u097f\u0985-\u098c\u098f\u0990\u0993-\u09a8\u09aa-\u09b0\u09b2\u09b6-\u09b9\u09bd\u09ce\u09dc\u09dd\u09df-\u09e1\u09f0\u09f1\u0a05-\u0a0a\u0a0f\u0a10\u0a13-\u0a28\u0a2a-\u0a30\u0a32\u0a33\u0a35\u0a36\u0a38\u0a39\u0a59-\u0a5c\u0a5e\u0a72-\u0a74\u0a85-\u0a8d\u0a8f-\u0a91\u0a93-\u0aa8\u0aaa-\u0ab0\u0ab2\u0ab3\u0ab5-\u0ab9\u0abd\u0ad0\u0ae0\u0ae1\u0b05-\u0b0c\u0b0f\u0b10\u0b13-\u0b28\u0b2a-\u0b30\u0b32\u0b33\u0b35-\u0b39\u0b3d\u0b5c\u0b5d\u0b5f-\u0b61\u0b71\u0b83\u0b85-\u0b8a\u0b8e-\u0b90\u0b92-\u0b95\u0b99\u0b9a\u0b9c\u0b9e\u0b9f\u0ba3\u0ba4\u0ba8-\u0baa\u0bae-\u0bb9\u0c05-\u0c0c\u0c0e-\u0c10\u0c12-\u0c28\u0c2a-\u0c33\u0c35-\u0c39\u0c60\u0c61\u0c85-\u0c8c\u0c8e-\u0c90\u0c92-\u0ca8\u0caa-\u0cb3\u0cb5-\u0cb9\u0cbd\u0cde\u0ce0\u0ce1\u0d05-\u0d0c\u0d0e-\u0d10\u0d12-\u0d28\u0d2a-\u0d39\u0d60\u0d61\u0d85-\u0d96\u0d9a-\u0db1\u0db3-\u0dbb\u0dbd\u0dc0-\u0dc6\u0e01-\u0e30\u0e32\u0e33\u0e40-\u0e45\u0e81\u0e82\u0e84\u0e87\u0e88\u0e8a\u0e8d\u0e94-\u0e97\u0e99-\u0e9f\u0ea1-\u0ea3\u0ea5\u0ea7\u0eaa\u0eab\u0ead-\u0eb0\u0eb2\u0eb3\u0ebd\u0ec0-\u0ec4\u0edc\u0edd\u0f00\u0f40-\u0f47\u0f49-\u0f6a\u0f88-\u0f8b\u1000-\u1021\u1023-\u1027\u1029\u102a\u1050-\u1055\u10d0-\u10fa\u1100-\u1159\u115f-\u11a2\u11a8-\u11f9\u1200-\u1248\u124a-\u124d\u1250-\u1256\u1258\u125a-\u125d\u1260-\u1288\u128a-\u128d\u1290-\u12b0\u12b2-\u12b5\u12b8-\u12be\u12c0\u12c2-\u12c5\u12c8-\u12d6\u12d8-\u1310\u1312-\u1315\u1318-\u135a\u1380-\u138f\u13a0-\u13f4\u1401-\u166c\u166f-\u1676\u1681-\u169a\u16a0-\u16ea\u1700-\u170c\u170e-\u1711\u1720-\u1731\u1740-\u1751\u1760-\u176c\u176e-\u1770\u1780-\u17b3\u17dc\u1820-\u1842\u1844-\u1877\u1880-\u18a8\u1900-\u191c\u1950-\u196d\u1970-\u1974\u1980-\u19a9\u19c1-\u19c7\u1a00-\u1a16\u1b05-\u1b33\u1b45-\u1b4b\u2135-\u2138\u2d30-\u2d65\u2d80-\u2d96\u2da0-\u2da6\u2da8-\u2dae\u2db0-\u2db6\u2db8-\u2dbe\u2dc0-\u2dc6\u2dc8-\u2dce\u2dd0-\u2dd6\u2dd8-\u2dde\u3006\u303c\u3041-\u3096\u309f\u30a1-\u30fa\u30ff\u3105-\u312c\u3131-\u318e\u31a0-\u31b7\u31f0-\u31ff\u3400\u4db5\u4e00\u9fbb\ua000-\ua014\ua016-\ua48c\ua800\ua801\ua803-\ua805\ua807-\ua80a\ua80c-\ua822\ua840-\ua873\uac00\ud7a3\uf900-\ufa2d\ufa30-\ufa6a\ufa70-\ufad9\ufb1d\ufb1f-\ufb28\ufb2a-\ufb36\ufb38-\ufb3c\ufb3e\ufb40\ufb41\ufb43\ufb44\ufb46-\ufbb1\ufbd3-\ufd3d\ufd50-\ufd8f\ufd92-\ufdc7\ufdf0-\ufdfb\ufe70-\ufe74\ufe76-\ufefc\uff66-\uff6f\uff71-\uff9d\uffa0-\uffbe\uffc2-\uffc7\uffca-\uffcf\uffd2-\uffd7\uffda-\uffdc"
            // Mn - non spacing marks
            + @"\u0300-\u036f\u0483-\u0486\u0591-\u05bd\u05bf\u05c1\u05c2\u05c4\u05c5\u05c7\u0610-\u0615\u064b-\u065e\u0670\u06d6-\u06dc\u06df-\u06e4\u06e7\u06e8\u06ea-\u06ed\u0711\u0730-\u074a\u07a6-\u07b0\u07eb-\u07f3\u0901\u0902\u093c\u0941-\u0948\u094d\u0951-\u0954\u0962\u0963\u0981\u09bc\u09c1-\u09c4\u09cd\u09e2\u09e3\u0a01\u0a02\u0a3c\u0a41\u0a42\u0a47\u0a48\u0a4b-\u0a4d\u0a70\u0a71\u0a81\u0a82\u0abc\u0ac1-\u0ac5\u0ac7\u0ac8\u0acd\u0ae2\u0ae3\u0b01\u0b3c\u0b3f\u0b41-\u0b43\u0b4d\u0b56\u0b82\u0bc0\u0bcd\u0c3e-\u0c40\u0c46-\u0c48\u0c4a-\u0c4d\u0c55\u0c56\u0cbc\u0cbf\u0cc6\u0ccc\u0ccd\u0ce2\u0ce3\u0d41-\u0d43\u0d4d\u0dca\u0dd2-\u0dd4\u0dd6\u0e31\u0e34-\u0e3a\u0e47-\u0e4e\u0eb1\u0eb4-\u0eb9\u0ebb\u0ebc\u0ec8-\u0ecd\u0f18\u0f19\u0f35\u0f37\u0f39\u0f71-\u0f7e\u0f80-\u0f84\u0f86\u0f87\u0f90-\u0f97\u0f99-\u0fbc\u0fc6\u102d-\u1030\u1032\u1036\u1037\u1039\u1058\u1059\u135f\u1712-\u1714\u1732-\u1734\u1752\u1753\u1772\u1773\u17b7-\u17bd\u17c6\u17c9-\u17d3\u17dd\u180b-\u180d\u18a9\u1920-\u1922\u1927\u1928\u1932\u1939-\u193b\u1a17\u1a18\u1b00-\u1b03\u1b34\u1b36-\u1b3a\u1b3c\u1b42\u1b6b-\u1b73\u1dc0-\u1dca\u1dfe\u1dff\u20d0-\u20dc\u20e1\u20e5-\u20ef\u302a-\u302f\u3099\u309a\ua806\ua80b\ua825\ua826\ufb1e\ufe00-\ufe0f\ufe20-\ufe23"

            + UnicodeRanges;
            var rx = @"^[" + L + "][" + L + @"|0-9|\-]{2,47}$";
            return id != null && id.Match(rx) != null && !ReservedPathNames.ContainsKey(id);
        }
#else

        /// <summary>
        /// The ID rules are:
        /// - Starts with letter
        /// - Lowercase letters, numbers allowed.
        /// - 3-48 chars in length, but maximum of 48 UTF-8 bytes (checked after reg-exp)
        /// Further restrictions are:
        /// - Must not be an ISO31662 region code unless the account contains a valid ATTR-GOV attribute.
        /// - Must not be an application reserved name.
        /// </summary>
        private static readonly System.Text.RegularExpressions.Regex AccountIDRegex
            = new System.Text.RegularExpressions.Regex(@"^[\p{L}|\p{Mn}|" + UnicodeRanges + @"][\p{L}|\p{Mn}|" + UnicodeRanges + @"|0-9|\-]{2,47}$");

        public static bool DateFromISO8601(string date, out DateTime d) {
            if (!IsISO8601(date)) {
                d = DateTime.MinValue;
                return false;
            }
            // AssumeLocal is important so .NET doesn't change the DateTime representation on us.
            return DateTime.TryParse((date ?? string.Empty).Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeLocal, out d);
        }

        //2016-03-08T15:00:00.000Z
        private static readonly System.Text.RegularExpressions.Regex ISO8601Regex =
            new System.Text.RegularExpressions.Regex(@"^\d{4}\-\d{2}\-\d{2}T\d{2}\:\d{2}\:\d{2}$");

        public static bool IsISO8601(string date) {
            if (date == null) return false;
            return ISO8601Regex.Match(date.Trim()).Success;
        }

        /// <summary>
        /// Checks an ID for correct size and pattern.
        /// </summary>
        /// <param name="id">The ID to validate</param>
        /// <returns>True if OK, otherwise false.</returns>
        public static bool IsIDValid(string id) {
            return id != null
                && AccountIDRegex.IsMatch(id)
                && Encoding.UTF8.GetByteCount(id) <= Constants.MaxAccountIDLengthInUtf8Bytes
                && !ReservedPathNames.ContainsKey(id);
        }

#endif

        /// <summary>
        /// Splits a transaction ID into its date/payee/payer components
        /// </summary>
        /// <param name="id">The ID to parse</param>
        /// <param name="createdUtc">Pointer to receive the creation date</param>
        /// <param name="payee">Pointer to receive the payee ID</param>
        /// <param name="payer">Pointer to receive the payer ID</param>
        /// <returns>True if the ID was valid, otherwise false.</returns>
        public static bool TryParseTransactionID(string id,
            out DateTime createdUtc, out string payee, out string payer) {
            createdUtc = DateTime.MinValue;
            payee = null;
            payer = null;
            if (id == null)
                return false;
            var parts = id.Trim().Split(' ');
            if (parts.Length != 3)
                return false;
            if (!Helpers.DateFromISO8601(parts[0], out createdUtc)
                || !IsIDValid(parts[1])
                || !IsIDValid(parts[2]))
                return false;
            payee = parts[1];
            payer = parts[2];
            return true;
        }

        public static bool IsHashEqual(byte[] a, byte[] b) {
            if (a == null && b == null)
                return true;
            if ((a == null) != (b == null))
                return false;
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < b.Length; i++)
                if (b[i] != a[i])
                    return false;
            return true;
        }

        public static bool IsLocalNetworkAddress(string ep) {
            if (ep == null)
                return false;
            return ep.StartsWith("0.")
                        || ep.StartsWith("127.0.")
                        || ep.StartsWith(":")
                        || ep.StartsWith("192.168.");
        }

        public static byte[] DHT_ID(string value) {
            var b = Cryptography.MD5.ComputeHash(Encoding.UTF8.GetBytes(value.ToLower())).Take(Constants.DHTIDSize).ToArray();
            return b;
        }
        public static byte[] DHT_IDForEndpoint(string ipAddressAndPort) {
            var value = ipAddressAndPort;
            if (!Constants.Peer_DHT_ID_Uses_Port) {
                var idx = value.IndexOf(':');
                if (idx > -1)
                    value = value.Substring(0, idx);
            }
            return DHT_ID(value); 
        }
#if !JAVASCRIPT
        public static System.Net.IPEndPoint ParseEP(string s) {
            System.Net.IPAddress ip;
            int port;
            if (s == null
                || s.IndexOf(':') == -1
                || !System.Net.IPAddress.TryParse(s.Substring(0, s.IndexOf(':')), out ip)
                || !int.TryParse(s.Substring(s.IndexOf(':') + 1), out port))
                return null;
            return new System.Net.IPEndPoint(ip, port);
        }
        public static System.Numerics.BigInteger DHT_IDi(string ipAddressAndPort) {
            return FromBigEndian(DHT_IDForEndpoint(ipAddressAndPort));
        }

        public static System.Numerics.BigInteger FromBigEndian(byte[] p) {
            var b = new byte[p.Length];
            Array.Copy(p, b, b.Length);
            Array.Reverse(b);
            if (b[b.Length - 1] > 127
                // key starts with zero
                || b[b.Length - 1] == 0) {
                Array.Resize(ref b, b.Length + 1);
                b[b.Length - 1] = 0;
            }
            return new System.Numerics.BigInteger(b);
        }

        public static byte[] ToBigEndian(this System.Numerics.BigInteger i, int length) {
            var b = i.ToByteArray().Take(length).ToArray();
            if (b.Length != length)
                Array.Resize(ref b, length);
            Array.Reverse(b);
            return b;
        }

#endif

        /// <summary>
        /// Returns the copy having the highest time-stamp and whether or not
        /// the DHT network presently agrees with the record. IsTransientState
        /// is set to true if MinimumNumberOfCopies are not satisfied. In which
        /// case the item should be flagged at the presentation layer.
        /// </summary>
        /// <typeparam name="T">An IStorable Message type.</typeparam>
        /// <param name="copies">The collection of items to sort</param>
        /// <param name="latest">Pointer to receive the latest item.</param>
        /// <returns></returns>
        public static CMResult CheckConsensus<T>(List<T> copies, out T latest)
                    where T : Message, IStorable {
            latest = default(T);
            if (copies.Count == 0)
                return CMResult.E_Item_Not_Found;
            // Consensus is simply:
            // - pick the "newest",
            // - make sure that a min number of copies agree with that version
            copies.Sort((a, b) => {
                return a.UpdatedUtc.CompareTo(b.UpdatedUtc) * -1;
            });

            var best = copies[0];

            // Omit any stale results from the count
            var latestCopies = copies.Where(x => x.UpdatedUtc == best.UpdatedUtc).ToList();
            int count = latestCopies.Count;
            // For accounts we also want to factor in soft calculations
            if (best is Schema.Account) {
                var calcs = new List<Schema.AccountCalculations>();
                for (int i = 0; i < latestCopies.Count; i++) {
                    var a = latestCopies[i] as Schema.Account;
                    if (a.AccountCalculations == null)
                        a.AccountCalculations = new Schema.AccountCalculations(a);
                    calcs.Add(a.AccountCalculations);
                }
                calcs.Sort((a, b) => {
                    return a.LastTransactionUtc.GetValueOrDefault().CompareTo(b.LastTransactionUtc.GetValueOrDefault()) * -1;
                });
                // The one with the newest transaction is probably correct
                // but not necessarily if it's not synced older transactions.
                var bestCalc = calcs[0];
                var latestCalcs = calcs.Where(x => x.LastTransactionUtc.GetValueOrDefault() == bestCalc.LastTransactionUtc.GetValueOrDefault()).ToList();
                // Which ones have credits + debits that agree
                var consensusCalcs = Schema.AccountCalculations.GetConsensus(latestCalcs, out count);
                best = consensusCalcs.Account as T;
            }
            latest = best;
            latest.ConsensusCount = count;
            return latest.ConsensusOK ? CMResult.S_OK : CMResult.S_Item_Transient;
        }

        /// <summary>
        /// Gets the current depreciated value of the transaction, regardless of its status.
        /// </summary>
        /// <param name="reportingTimeUtc">Typically DateTime.UtcNow, but may be a historical point in time for history analysis.</param>
        /// <param name="transactionCreatedUtc">The creation date of the transaction.</param>
        /// <param name="amount">The original amount of the transaction.</param>
        /// <returns>A current amount with demurrage applied.</returns>
        public static decimal CalculateTransactionDepreciatedAmount(
            DateTime reportingTimeUtc,
            DateTime transactionCreatedUtc, decimal amount) {
            // A linear demurrage begins 12 months after Created UTC, over a following 12
            // month period.
            // DEPRECIATE() = MIN(1, MAX(0, 1 - ( (DAYS-SINCE-CREATION - 365) / 365 ))) * AMOUNT
            int daysSinceCreation = (int)(reportingTimeUtc - transactionCreatedUtc).TotalDays;
            return Math.Round(Math.Min(1, Math.Max(0, 1 - ((daysSinceCreation - 365) / 365.0m))) * amount, 6);
        }

        /// <summary>
        /// Gets the current depreciated value of the transaction for the payer's side
        /// according to the transaction date and status.
        /// </summary>
        /// <param name="reportingTimeUtc">Typically DateTime.UtcNow, but may be a historical point in time for history analysis.</param>
        /// <param name="transactionCreatedUtc">The creation date of the transaction.</param>
        /// <param name="amount">The original amount of the transaction.</param>
        /// <param name="payeeStatus">The current payee status.</param>
        /// <param name="payerStatus">The current payer status.</param>
        /// <returns>A depreciated amount or 0 if it should not currently account against the payer's balance.</returns>
        public static decimal CalculateTransactionDepreciatedAmountForPayer(
            DateTime reportingTimeUtc,
            DateTime transactionCreatedUtc, decimal amount,
            Schema.PayeeStatus payeeStatus,
            Schema.PayerStatus payerStatus) {
            switch (payeeStatus) {
                case Schema.PayeeStatus.NotSet: // Unaccepted payments count against payer balance until we know otherwise.
                case Schema.PayeeStatus.Accept: // OK.
                    break;

                case Schema.PayeeStatus.Refund:
                case Schema.PayeeStatus.Decline:
                    return 0; // Not accepted/declined
            }
            //
            // When payers issue a Dispute they get their money back, but the payee
            // retains their money also, unless they choose to refund amicably.
            //
            // Non-refunded Disputed transactions reflect badly on the seller as well
            // as the buyer.
            //
            switch (payerStatus) {
                case Schema.PayerStatus.NotSet:
                case Schema.PayerStatus.Dispute:
                case Schema.PayerStatus.Cancel:
                    return 0; //
                case Schema.PayerStatus.Accept:
                    break;
            }

            return CalculateTransactionDepreciatedAmount(reportingTimeUtc, transactionCreatedUtc, amount);
        }

        /// <summary>
        /// Gets the current depreciated value of the transaction for the payee's side
        /// according to the transaction date and status.
        /// </summary>
        /// <param name="reportingTimeUtc">Typically DateTime.UtcNow, but may be a historical point in time for history analysis.</param>
        /// <param name="transactionCreatedUtc">The creation date of the transaction.</param>
        /// <param name="amount">The original amount of the transaction.</param>
        /// <param name="payeeStatus">The current payee status.</param>
        /// <returns>A depreciated amount or 0 if it should not currently account against the payee's balance.</returns>
        public static decimal CalculateTransactionDepreciatedAmountForPayee(
            DateTime reportingTimeUtc,
            DateTime transactionCreatedUtc, decimal amount,
            Schema.PayeeStatus payeeStatus) {
            switch (payeeStatus) {
                case Schema.PayeeStatus.Accept:
                    break; // OK
                case Schema.PayeeStatus.NotSet:
                case Schema.PayeeStatus.Refund:
                case Schema.PayeeStatus.Decline:
                    return 0; // Not accepted/declined
            }

            // When payers issue a Dispute they get their money back, but the payee
            // retains their money also, unless they choose to refund amicably.
            //
            // Non-refunded Disputed transactions reflect badly on the seller as well
            // as the buyer.
            return CalculateTransactionDepreciatedAmount(reportingTimeUtc, transactionCreatedUtc, amount);
        }

        /// <summary>
        /// Because of demurrage over a 12 month period after the first year on all
        /// transactions, we always consider your balance to be based on a two
        /// year period. So the calculation for balance is simply
        /// 'BasicYearlyAllowance x 2 + RecentCredits - RecentDebits'
        /// where credits and debits both have their demurrage depreciated amounts
        /// summed.
        /// </summary>
        /// <param name="recentCredits"></param>
        /// <param name="recentDebits"></param>
        /// <returns></returns>
        public static decimal CalculateAccountBalance(decimal recentCredits, decimal recentDebits) {
            return Constants.BasicYearlyAllowance * 2 + recentCredits - recentDebits;
        }

        /// <summary>
        /// Returns true if the specified old and new PayeeStatus values are valid. 
        /// </summary>
        /// <param name="oldStatus">The old or previous PayeeStatus</param>
        /// <param name="newStatus">The new or current PayeeStatus</param>
        public static bool IsPayeeStatusChangeAllowed(Schema.PayeeStatus oldStatus, Schema.PayeeStatus newStatus) {

            if (oldStatus == newStatus)
                return true; // no change

            switch (newStatus) {
                case Schema.PayeeStatus.NotSet:
                    return oldStatus == Schema.PayeeStatus.NotSet;
                case Schema.PayeeStatus.Accept:
                    return oldStatus == Schema.PayeeStatus.NotSet
                        || oldStatus == Schema.PayeeStatus.Decline;
                case Schema.PayeeStatus.Decline:
                    return oldStatus == Schema.PayeeStatus.NotSet;
                case Schema.PayeeStatus.Refund:
                    return oldStatus == Schema.PayeeStatus.Accept;
            }

            throw new NotImplementedException("Unrecognised PayeeStatus " + newStatus);
        }

        /// <summary>
        /// Returns true if the specified old and new PayerStatus values are valid. 
        /// </summary>
        /// <param name="oldStatus">The old or previous PayerStatus</param>
        /// <param name="newStatus">The new or current PayerStatus</param>
        public static bool IsPayerStatusChangeAllowed(Schema.PayerStatus oldStatus, Schema.PayerStatus newStatus) {

            if (oldStatus == newStatus  // no change
                && newStatus != Schema.PayerStatus.NotSet // disallowed, fall through to switch below
                )
                return true;

            switch (newStatus) {
                case Schema.PayerStatus.NotSet:
                    // Only a payer is allowed to initialise a transaction and payers must always be
                    // in "Accept" during creation. Therefore NotSet is always disallowed. It is only
                    // defined for completeness/programming convenience.
                    return false;
                case Schema.PayerStatus.Accept: return oldStatus == Schema.PayerStatus.NotSet;  // Default/Null -> Accept
                case Schema.PayerStatus.Dispute: return oldStatus == Schema.PayerStatus.Accept; // Accept -> Dispute
                case Schema.PayerStatus.Cancel: return oldStatus == Schema.PayerStatus.Accept; // Accept -> Cancel
            }

            throw new NotImplementedException("Unrecognised PayerStatus " + newStatus);
        }
    }
}