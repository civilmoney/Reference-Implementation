//
// Language: ES6
//
// Common CivilMoney helpers used in the Browser main site, the Progressive Web App
// as well as the cloudflare worker.
//

var RX_BRANCH = /\(?[+-]?\d{1,4}\s+[+-]?\d{1,4}\)?/i
var BRANCH_ERROR_EXAMPLE = "Branch should be in the format of 'Latitude*10 Longitude*10'.<br/>Example: NY, NY USA is <b>40.7</b>30610,<b>-73.9</b>35242. Civil Branch would be 407 -739.";
var ACCOUNT_ERROR_EXAMPLE = "Account should be in the format of (Latitude*10 Longitude*10) 0000 0000 0000 0000.<br/>Example:<br/>NY, NY USA is <b>40.7</b>30610,<b>-73.9</b>35242. Civil Branch would be 407 -739. An account number would be (407 -739) 1111 2222 3333 4444."

var UNIVERSAL_BASIC_INCOME_2YR = 4320000 * 2;
var EMPTY_ACCOUNT_NUMBER_EXAMPLE = "(±000 ±000) 0000 0000 0000 0000";
var COMMUNITY_ACCOUNT = "0000000000000000";

function stringToUtf8(string) {
    var utf8 = unescape(encodeURIComponent(string));
    var ar = [];
    for (var i = 0; i < utf8.length; i++) {
        ar.push(utf8.charCodeAt(i));
    }
    return ar;
}
function gatherUsefulNumbersFromUtf8(utf8) {
    var ar = [];
    for (var i = 0; i < utf8.length; i++) {
        var str = utf8[i].toString();
        var num = str.replace(/^[10]+/g, "");
        if (num.length === 0) {
            num = str.substr(str.length - 1);
        }
        ar.push(parseInt(num, 10));
    }
    return ar;
}
function removeWhiteSpace(str) {
    return str.replace(/\s/g, "");
}
function isAccountNumberValid(accountNumber) {
    return (accountNumber || "").match(/^[\d]{16}$/) !== null;
}

function isUNSPSCValid(unspscCode) {
    try {
        unspscItemsFromString(unspscCode);
        return unspscCode.trim().length >= 8;
    } catch (e) {
        return false;
    }
}
function branchFromString(unsanitised) {
    return unsanitised.replace(/[()+]/g, "");
}
function branchIDToLatitudeLongitude(id) {
    const ar = id.split(" ");
    if (ar[0].match(/^-?[\d]{1,4}$/) === null) {
        throw "Latitude should be a positive or negative integer (latitude * 10, example: -61.9277 = -619)";
    }
    if (ar[1].match(/^-?[\d]{1,4}$/) === null) {
        throw "Longitude should be a positive or negative integer (longitude * 10, example: -45.5321 = -455)";
    }
    return {
        latitude: parseInt(ar[0], 10) / 10.0,
        longitude: parseInt(ar[1], 10) / 10.0
    };
}
function formatDecimals(number) {
    var left = parseInt(number).toString();
    var s = "";
    for (var i = left.length; i > 0; i -= 3) {
        s = left.substr(Math.max(0, i - 3), 3 + (i < 3 ? i - 3 : 0)) + (s.length > 0 ? "," + s : "");
    }
    var decimal = parseInt((number % 1) * 100).toString();
    if (decimal !== "0") {
        s += ".";
        if (decimal.length === 1) {
            s += "0";
        }
        s += decimal;
    }
    return s;
}
function formatUTCDate(yyyymmddhhmm, seperator = " ") {
    try {
        var formatted = yyyymmddhhmm.substr(0, 4)
            + "-" + yyyymmddhhmm.substr(4, 2)
            + "-" + yyyymmddhhmm.substr(6, 2)
            + seperator + yyyymmddhhmm.substr(8, 2)
            + ":" + yyyymmddhhmm.substr(10, 2);
        return formatted;
    } catch (err) {
        throw "Invalid UTC yyyymmddhhmm.";
    }
}
function parseUTCDate(yyyymmddhhmm) {
    try {
        var formatted = formatUTCDate(yyyymmddhhmm) + " UTC";
        return new Date(formatted);
    } catch (err) {
        throw "Invalid UTC yyyymmddhhmm.";
    }
}
function twoYearsFrom(yyyymmddhhmm) {
    var d = parseUTCDate(yyyymmddhhmm);
    d.setFullYear(d.getFullYear() + 2);
    return d;
}
function isUTCDateValid(yyyymmddhhmm) {
    if ((yyyymmddhhmm || "").length !== "yyyymmddhhmm".length) {
        return false;
    }
    try {
        parseUTCDate(yyyymmddhhmm);
        return true;
    } catch (err) {
        return false;
    }
}
function makeTwoNumbers(str) {
    str = removeWhiteSpace(str);
    return str.length === 1 ? "0" + str : str;
}
function dateToUtcYYYYMMDDHHMM(date) {
    return date.getUTCFullYear().toString()
        + makeTwoNumbers((date.getUTCMonth() + 1).toString())
        + makeTwoNumbers(date.getUTCDate().toString())
        + makeTwoNumbers(date.getUTCHours().toString())
        + makeTwoNumbers(date.getUTCMinutes().toString());
}

function formatAccountNumber(branch, allnumbers) {
    var number = "";
    if (branch !== null) {
        number += "(";
        number += branch;
        number += ") ";
    }
    for (var i = 0; i < allnumbers.length && i < 16; i++) {
        if (i !== 0 && (i % 4) === 0) {
            number += " ";
        }
        number += allnumbers[i];
    }
    return number;
}


class UnspscItem {
    constructor() {
        this.unspsc = 0;
        this.amount = 0;
    }
}
function unspscItemsFromString(data) {
    //(?<code>\d{8}): (?<amount>\d+) ;?
    return data.split(';').map((v) => {
        var codeAmount = v.split(':');
        if (codeAmount[0].trim().length !== 8) {
            throw `Invalid UNSPSC '${codeAmount[0]}'`;
        }
        return { unspsc: parseInt(codeAmount[0], 10), amount: (codeAmount.length === 2 ? parseInt(codeAmount[1], 10) : 0) };
    });
}
function unspscItemsToString(unspscItemArray) {
    var s = "";
    if (unspscItemArray) {
        s = unspscItemArray.map((v) => { return v.unspsc + (v.amount !== 0 ? ":" + v.amount : ""); }).join(';');
    }
    return s;
}

// Just breaks apart "(branch) number" into two cells
function normaliseCsvArray(src) {
    var ar = [];
    for (var i = 0; i < src.length; i++) {
        var rx = src[i].match(/(\(?[+-]?\d+ [+-]?\d+\)?)[ ]*[\t/,|]?[ ]*(\d{4}\s*\d{4}\s*\d{4}\s*\d{4})/);
        if (rx !== null && rx.length === 3) {
            ar.push(rx[1].replace(/\(\)/g, ""));
            ar.push(rx[2]);
        } else {
            ar.push(src[i]);
        }
    }
    return ar;
}


// All CivilMoney transactions boil down to a simple line of text that can
// be denoted as a URI as well as a CSV.
class TransactionUrl {

    constructor(url) {

        this.type = null;
        this.toBranch = null;
        this.toNumber = null;
        this.fromBranch = null;
        this.fromNumber = null;
        this.yyyymmddhhmm = null;
        this.unspscItems = []; // UnspscItem
        this.amount = null;
        this.memo = null;
        this.proofOfTime = null;
        this.isSubmitted = false;

        if (url) {
            this.parseUrlOrThrow(url);
        }
    }

    parseUrlOrThrow(url) {

        // Not robust enough
        // const REGEX_TO_FROM_DATE_UNSPSC_AMOUNT = /(\(?[+-]?\d+ [+-]?\d+\)?)[ ]*[\t/,|]?[ ]*(\d{4}\s*\d{4}\s*\d{4}\s*\d{4})?[ ]*[\t/,|]?[ ]*(\(?[+-]?\d+ [+-]?\d+\)?|\(?\*\)?)?[ ]*[\t/,|]?[ ]*(\d{4}\s*\d{4}\s*\d{4}\s*\d{4}|\*)?[ ]*[\t/,|]?[ ]*(\d{4}[ -.]?\d{2}[ -.]?\d{1,2}[\s@]*\d{1,2}[.:]?\d{2})?[ ]*[\t/,|]?[ ]*((?:\d{8}(?:\s*\:\s*\d+)?;?\s*)+)?[ ]*[\t/,|]?[ ]*(?:[-+])?(\d+)?[ ]*[\t/,|]?([^"][^\t/,|]*?|[ ]*"[^"]+?")?[ ]*[\t/,|]?([^"][^\t/,|]*?|[ ]*"[^"]+?")?/i
        try {

            if (url.indexOf("https://civil.money/") === 0) {
                url = url.substr("https://civil.money/".length);
            }

            var csv = new CSVReader(url);
            var ar = csv.nextCsvLine();
            if (ar === null) {
                return;
            }
            ar = normaliseCsvArray(ar);

            this.parseArrayOfValues(ar);

        } catch (err) {
            throw `Invalid url ${url}`;
        }
    }

    parseArrayOfValues(ar) {
        if (ar.length >= 10
            && ar[9] !== undefined) { 
            this.isSubmitted = ar[9].match(/^1|y|yes|true$/i) !== null;
        }

        if (ar.length >= 9
            && ar[8] !== undefined) {
            this.proofOfTime = ar[8].replace(/^"|"$/g, '');
        }

        if (ar.length >= 8
            && ar[7] !== undefined) {
            this.memo = ar[7].replace(/^"|"$/g, '');
        }

        if (ar.length >= 7
            && ar[5] !== undefined
            && ar[6] !== undefined) {
            this.amount = ar[6];
            this.unspscItems = unspscItemsFromString(ar[5]);
        }
        if (ar.length >= 5
            && ar[4] !== undefined
        ) {
            this.yyyymmddhhmm = ar[4].replace(/[^\d]/g, "");
        }
        if (ar.length >= 4
            && ar[3] !== undefined
            && ar[2] !== undefined) {
            this.fromNumber = ar[3].replace(/[^\d]/g, "");
            this.fromBranch = ar[2].replace(/[()+]/g, "");
            if (this.fromBranch === "*") {
                this.fromNumber = "*";
            }
        }
        if (ar.length >= 2
            && ar[1] !== undefined) {
            this.toNumber = ar[1].replace(/[^\d]/g, "");
        }
        if (ar.length >= 1
            && ar[0] !== undefined) {
            this.toBranch = ar[0].replace(/[()+]/g, "");
        }

        this.type = this.amount !== null && this.yyyymmddhhmm !== null && this.toNumber !== null && this.toNumber !== null ? "new-transaction"
            : this.yyyymmddhhmm !== null && this.toNumber !== null && this.toNumber !== null ? "old-transaction"
                : this.toNumber !== null && this.fromBranch === null ? "account"
                    : this.toBranch !== null && this.fromBranch === null ? "branch"
                        : null;
    }

    uid() {
        return this.toBranch
            + "," + this.toNumber
            + "," + this.fromBranch
            + "," + this.fromNumber
            + "," + this.yyyymmddhhmm;
    }
    toFriendlyString() {
        var s = "";
        if (this.type === "new-transaction" || this.type === "old-transaction") {
            s += "To: ";
        } else if (this.type === "branch") {
            s += "Branch: ";
        } else if (this.type === "account") {
            s += "Account: ";
        }
        if (this.toBranch === null)
            return s;
        if (this.toNumber !== null) {
            s += formatAccountNumber(this.toBranch, this.toNumber);
        } else {
            s += this.toBranch;
        }
        if (this.fromBranch === null
            || this.fromNumber === null
            || this.yyyymmddhhmm === null)
            return s;
        s += " From: ";
        s += " " + formatAccountNumber(this.fromBranch, this.fromNumber);
        s += " Date: " + formatUTCDate(this.yyyymmddhhmm);
        if (this.unspscItems.length === 0 || this.amount === null)
            return s;
        s += " UNSPSC(s): " + unspscItemsToString(this.unspscItems) + " //c " + this.amount;
        return s;
    }
    toCsvString() {
        var s = "";
        if (this.toBranch === null)
            return s;
        s += this.toBranch;
        if (this.toNumber === null)
            return s;
        s += "," + this.toNumber;
        if (this.fromBranch === null
            || this.fromNumber === null
            || this.yyyymmddhhmm === null)
            return s;
        s += "," + this.fromBranch + "," + this.fromNumber;
        s += "," + this.yyyymmddhhmm;
        if (this.unspscItems.length === 0 || this.amount === null)
            return s;
        s += "," + unspscItemsToString(this.unspscItems) + "," + this.amount;
        const hasMemoOrProof = (this.memo !== undefined && this.memo !== null && this.memo.trim().length > 0)
            || (this.proofOfTime !== undefined && this.proofOfTime !== null && this.proofOfTime.trim().length > 0);
        if (hasMemoOrProof) {
            s += ",\"" + (this.memo || "") + "\"";
            s += ",\"" + (this.proofOfTime || "") + "\"";
            if (this.isSubmitted === true) {
                s += ",Y";
            }
        }

        return s;
    }
}


class CSVReader {
    constructor(data) {
        this._data = data;
        this._i = 0;
    }
    nextCsvLine() {
        var document = this._data;
        if (this._i >= document.length)
            return null;
        var s = "";
        var inValue = false;
        var inString = false;
        var wasLastQuoted = false;
        var isDelimiter = (c) => {
            return c === ',' || c === '\t' || c === '|'
        };
        var isWhiteSpace = (c) => {
            return c === ' ';
        };
        var ar = [];
        for (; this._i < document.length; this._i++) {
            var c = document[this._i];
            if ((c === '\r' || c === '\n') && !inString) {
                if (c === '\n'
                    || this._i + 1 >= document.length
                    || document[this._i + 1] === '\n') {
                    this._i += 2;
                    // end of line
                    ar.push(s);
                    s = "";
                    return ar;
                }
            }
            if (!inValue) {
                if (isWhiteSpace(c))
                    continue;
                if (isDelimiter(c) && wasLastQuoted) {
                    wasLastQuoted = false;
                    continue;
                }

                inValue = true;
                if (c === '"') {
                    inString = true;
                    continue;
                }
            }
            if (inString && c === '"') { // handle "" escape
                if (this._i + 1 >= document.length
                    || document[this._i + 1] !== '"') {
                    ar.push(s);
                    s = "";
                    inString = false;
                    inValue = false;
                    wasLastQuoted = true;
                    continue;
                } else {
                    // double quote
                    this._i++;
                }
            } else if (!inString) {
                if (isDelimiter(c)) {
                    ar.push(s);
                    s = "";
                    inValue = false;
                    wasLastQuoted = false;
                    continue;
                }
            }

            s += c;
        }
        ar.push(s);
        return ar;
    }
}


class LedgerCSVFile {
    constructor(memorableSentence, branchNumber, number, label) {
        this.memorableSentence = memorableSentence;
        this.branchNumber = branchNumber;
        this.number = number;
        this.label = label;
        this.items = [];
        this.usefulUnspscs = {};
    }
    appendTransaction(transactionUrl) {
        this.items.push(transactionUrl);
        for (var i = 0; i < transactionUrl.unspscItems; i++) {
            var code = transactionUrl.unspscItems[i].unspsc;
            if (!(code in this.usefulUnspscs)) {
                this.usefulUnspscs[code] = 1;
            } else {
                this.usefulUnspscs[code]++;
            }
        }
    }
    fileName() {

        return `CivilMoney Ledger ${this.label} (${this.branchNumber}) ${this.number}.csv`
    }
    toCsvBlob() {

        var csv = `# My memorable phrase:,"${this.memorableSentence || ""}"`
            + `\r\n# Number:,${formatAccountNumber(this.branchNumber, this.number)}`
            + `\r\n# Name:,"${this.label || ""}"`
            + "\r\n# My useful United Nations Standard Products and Services Codes (UNSPSC):,"
            + "\r\n# "
            + "\r\n# "
            + "\r\n# Notes:"
            + "\r\n# - Delete rows from your ledger after 2 years."
            + "\r\n# - Submit transactions periodically to your community branch or global ledger and mark the Submitted column with a 'Y' (yes) after you do."
            + "\r\n# ,,,,,,,,Universal Basic Income,Balance"
            + `\r\nTo,From,YYYY-MM-DD @ HH:MM (UTC),UNSPSCs,Amount,Memo,Proof of Time,Submitted,${UNIVERSAL_BASIC_INCOME_2YR},USD$`;
        var startRow = 12;

        for (var i = 0; i < this.items.length; i++) {
            var t = this.items[i];
            csv += `\r\n${formatAccountNumber(t.toBranch, t.toNumber)},${formatAccountNumber(t.fromBranch, t.fromNumber)},${formatUTCDate(t.yyyymmddhhmm, '@')}," ${unspscItemsToString(t.unspscItems)} ",${t.amount},"${(t.memo || "").replace(/"/g, "\"\"")}","${(t.proofOfTime || "").replace(/"/g, "\"\"")}"`;
            csv += `,${(t.isSubmitted?"Y":"")}`;
            var row = startRow + i;
            csv += `,=E${row} + I${row - 1}`;
            csv += `,"=ROUND(I${row}/3600 *50,2)"`;
           

        }

        var bl = null;
        try {
            bl = new File([csv], this.fileName(), {
                type: "application/octet-stream",
            });
        } catch (err) {
            bl = new Blob([csv], {
                type: "application/octet-stream",
            });
        }
        return bl;
    }
}