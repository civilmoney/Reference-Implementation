// Generated file - DO NOT MODIFY


// ============ common/civilmoney-common.js ============

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
            + "\r\n# * Delete rows from your ledger after 2 years."
            + "\r\n# ** Submit transactions periodically to your community branch or global ledger and mark the Submitted column with a 'Y' (yes) after you do."
            + "\r\n# ,,,,,,,Universal Basic Income,Balance"
            + `\r\nTo,From,*YYYY-MM-DD @ HH:MM (UTC),UNSPSCs,Amount,Memo,Proof of Time,*Submitted,${UNIVERSAL_BASIC_INCOME_2YR},USD$`;
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

// ============ common/civilmoney-model.js ============

//
// Language: ES6
//
// The CivilMoney common classes used by the Progressive Web App as well as the
// cloudflare webworker.
//
//

class LedgerObject {
    constructor() {
        this.isValid = true;

    }
    setError(message) {
        this.isValid = false;
        this.error = message;
    }
    throwIfErrant() {
        if (!this.isValid) {
            throw this.error;
        }
    }
}

class LedgerBranch extends LedgerObject {
    constructor(branchID) {
        super();
        let field = "";
        try {

            field = "ID";
            this.id = null;
            let ar = branchID.split(" ");

            field = "latitude";
            if (ar[0].match(/^-?[\d]{1,4}$/) === null) {
                throw "Latitude should be a positive or negative integer (latitude * 10, example: -61.9277 = -619)";
            }

            this.latitude = parseInt(ar[0], 10) / 10.0;
            field = "longitude";
            if (ar[0].match(/^-?[\d]{1,4}$/) === null) {
                throw "Longitude should be a positive or negative integer (longitude * 10, example: -45.5321 = -455)";
            }

            this.longitude = parseInt(ar[1], 10) / 10.0;

            this.id = ar[0] + " " + ar[1];
            this.totalAccounts = 0;
            this.revenue = 0;
            this.credits = 0;
            this.debits = 0;
            this.pendingRevenue = 0;
            this.pendingCredits = 0;
            this.pendingDebits = 0;
            this.transactions = [];
            this.accounts = [];
            this.cachedUtc = 0;
        } catch (err) {
            this.setError("Invalid Branch " + field + ". " + (err || ""));
        }
    }
    async updateCache() {
        // List all account activity
        let res = await LEDGER.list({ prefix: `CC:${this.id}:A:` });
        let tax = 0;
        let credits = 0;
        let debits = 0;

        let pendingTax = 0;
        let pendingCredits = 0;
        let pendingDebits = 0;

        let recent = [];
        let accounts = {};
        let seen = {};
        while (1) {
            for (var i = 0; i < res.keys.length; i++) {
                const item = res.keys[i];
                let fourthColon = 0;
                for (let x = 0; x < 4; x++) {
                    fourthColon = item.name.indexOf(':', fourthColon) + 1;
                }
                const line = item.name.substr(fourthColon);
                const csv = line.split(',');
                const tx = new LedgerTransaction(csv[0], csv[1], csv[2], csv[3], csv[4], unspscItemsFromString(csv[5]), csv[6], false);

                if (!tx.isValid || tx.isExpired() || (line in seen)) {
                    continue;
                }
                seen[line] = null;
                recent.push(line);

                const isVerified = item.metadata.taxRevenueStatus === TaxRevenueStatus.OK ? 1 : 0;
                const isPending = item.metadata.taxRevenueStatus === TaxRevenueStatus.Submitted ? 1 : 0;


                if (tx.toBranch === this.id && tx.toNumber !== COMMUNITY_ACCOUNT) {
                    tax += tx.amount * 0.1 * isVerified;
                    pendingTax += tx.amount * 0.1 * isPending;
                }

                if (tx.toBranch === this.id && tx.toNumber === COMMUNITY_ACCOUNT) {
                    credits += tx.amount * isVerified;
                    pendingCredits += tx.amount * isPending;
                }

                if (tx.fromBranch === this.id && tx.fromNumber === COMMUNITY_ACCOUNT) {
                    debits += tx.amount * isVerified;
                    pendingDebits += tx.amount * isPending;
                }


                if (tx.toBranch === this.id && tx.toNumber !== COMMUNITY_ACCOUNT) {
                    if (!(tx.toNumber in accounts)) {
                        accounts[tx.toNumber] = { count: 0, credits: 0, debits: 0, last: "" };
                    }
                    accounts[tx.toNumber].count++;
                    accounts[tx.toNumber].credits += tx.amount;
                    accounts[tx.toNumber].last = tx.yyyymmddhhmm;
                }

                if (tx.fromBranch === this.id && tx.fromNumber !== COMMUNITY_ACCOUNT) {
                    if (!(tx.fromNumber in accounts)) {
                        accounts[tx.fromNumber] = { count: 0, credits: 0, debits: 0, last: "" };
                    }
                    accounts[tx.fromNumber].count++;
                    accounts[tx.fromNumber].debits += tx.amount;
                    accounts[tx.fromNumber].last = tx.yyyymmddhhmm;
                }
            }

            if (res.list_complete) {
                break;
            } else {
                res = await LEDGER.list({ "cursor": res.cursor });
            }
        }

        let count = 0;
        for (const kp in accounts) {
            count++;
        }

        this.totalAccounts = count;
        this.revenue = tax;
        this.credits = credits;
        this.debits = debits;
        this.pendingRevenue = pendingTax;
        this.pendingCredits = pendingCredits;
        this.pendingDebits = pendingDebits;
        this.transactions = recent;
        this.accounts = accounts;
        this.cachedUtc = new Date().getTime() / 1000;
        await LEDGER.put(`Cache:${this.id}:`, JSON.stringify(this));
    }
    async loadDetails() {
        const cachedString = await LEDGER.get(`Cache:${this.id}:`);
        if (cachedString === null) {
            await this.updateCache();
            return;
        }
        const cache = JSON.parse(cachedString);
        const unixSecondsNow = new Date().getTime() / 1000;
        const cacheExpirationSeconds = 60 * 5;
        if (!cache.cachedUtc || (unixSecondsNow - cache.cachedUtc) > cacheExpirationSeconds) {
            await this.updateCache();
            return;
        }
        
        this.totalAccounts = cache.totalAccounts;
        this.revenue = cache.revenue;
        this.credits = cache.credits;
        this.debits = cache.debits;
        this.pendingRevenue = cache.pendingRevenue;
        this.pendingCredits = cache.pendingCredits;
        this.pendingDebits = cache.pendingDebits;
        this.transactions = cache.transactions;
        this.accounts = cache.accounts;
    }
}

//
// These status codes are kept secret by branch ledgers because it's important 
// not to involve society at large in the tax revenue generation process nor 
// incentivise people to reject one another's personal ledgers on account of taxation
// status, nor steer people's reporting behaviour in any way. Focus is always on 'benefit of the doubt',
// forgiveness, giving, and an implicit building of community acceptance of one another
// and in the case of dishonesty, a gradual change toward honesty by those participants
// as they eventually learn that being dishonest simply isn't a necessary part of their
// personal survival strategy anymore.
//
// Revenue status may also be revised randomly at any time by the system or overridden by 
// a branch overseer and is never set in stone.
// 
const TaxRevenueStatus = Object.freeze({

    NotSubmitted: 0,
    Submitted: 1,
    OK: 2,

    Ignored_UnrealisticTransaction: -1,
    Ignored_MissingProofOfTime: -2,
    Ignored_InadequateProofOfTime: -3,
    Ignored_RejectedByAI: -4,

});

class LedgerTransaction extends LedgerObject {
    constructor(toBranch, toNumber,
        fromBranch, fromNumber,
        yyyymmddhhmm, unspscItems, amount, isLoadOnly) {
        super();
        let field = "";
        try {

            field = "yyyymmddhhmm";
            if ((yyyymmddhhmm || "").match(/^[\d]{12}$/) === null) {
                throw "Must be 12 digits.";
            }
            this.yyyymmddhhmm = yyyymmddhhmm;

            field = "toBranch";
            let b = new LedgerBranch(toBranch);
            b.throwIfErrant();
            this.toBranch = b.id;

            field = "toNumber";
            this.toNumber = toNumber.replace(/\s/g, "");
            if (!isAccountNumberValid(this.toNumber)) {
                throw "Must be 16 digits.";
            }

            field = "fromBranch";
            b = new LedgerBranch(fromBranch);
            b.throwIfErrant();
            this.fromBranch = b.id;

            field = "fromNumber";
            this.fromNumber = fromNumber.replace(/\s/g, "");
            if (!isAccountNumberValid(this.fromNumber)) {
                throw "Must be 16 digits.";
            }


            field = "account";
            if (this.toBranch + "," + this.toNumber === this.fromBranch + "," + this.fromNumber) {
                throw "The from/to accounts must be different.";
            }

            if (isLoadOnly) {
                // unspsc and amount are unspecified.
            } else {
                field = "unspscItems";
                this.unspsc = unspscItemsToString(unspscItems);

                field = "amount";
                this.amount = parseInt(amount, 10);
            }

        } catch (err) {
            this.setError(`Invalid Transaction ${field}. ${(err || "")}`);
        }
    }
    uid() {
        return this.toBranch
            + "," + this.toNumber
            + "," + this.fromBranch
            + "," + this.fromNumber
            + "," + this.yyyymmddhhmm;
    }
    data() {
        return this.unspsc + "," + this.amount;
    }
    csvLine() {
        return this.uid() + "," + this.data();
    }
    isExpired() {
        return twoYearsFrom(this.yyyymmddhhmm) < new Date();
    }
    async isAdded(isLoadDesired) {

        const res = await LEDGER.getWithMetadata(this.uid());

        this._taxRevenueStatus = res.value !== null && res.metadata !== null ? res.metadata.taxRevenueStatus : 0;

        if (!this._taxRevenueStatus) {
            return false;
        }

        const unspsc = res.value.split(',')[0];
        const amount = res.value.split(',')[1];

        if (isLoadDesired) {
            this.unspsc = unspsc;
            this.amount = parseInt(amount, 10);
        } else {
            if (res.value !== this.data()) {
                // data mismatch
                this.setError(`The URL is incorrect. The actual transaction is UNSPSC ${unspsc} Amount ${amount}.`, 404);
                return false;
            }
        }

        if (res.metadata !== null) {
            this._utc = res.metadata.utc;
            this._ip = res.metadata.ip;
            this._ipCountry = res.metadata.ipCountry;
        }

        return true;
    }
    // Adds privately held/shared info (memo and proof.)
    async tryAddProofOfTime(proofOfTime, memo) {
        try {
            const unixEpocExpiry = twoYearsFrom(this.yyyymmddhhmm).getTime() / 1000;
            await LEDGER.put(this.uid() + ":proof", proofOfTime, {
                expiration: unixEpocExpiry,
                metadata: { utc: new Date(), memo: memo || "" }
            });
            return true;
        } catch (err) {
            this.setError("There was a problem saving data. " + err);
            return false;
        }
    }
    async tryGetProofOfTime() {
        const res = await LEDGER.getWithMetadata(this.uid() + ":proof");
        return res.value;
    }
    // Adds public/global ledger information.
    // `taxRevenueStatus` is set to 0 for client private ledgers
    // until they sync/upload it to the global ledger. The global
    // ledger sets proper values as records are validated.
    async tryAdd(ip, ipCountry, taxRevenueStatus) {
        try {
            let unixEpocExpiry = twoYearsFrom(this.yyyymmddhhmm).getTime() / 1000;
            const meta = {
                utc: new Date(),
                ip: ip,
                ipCountry: ipCountry,
                taxRevenueStatus: taxRevenueStatus
            };

            // Master copy "by UID"
            await LEDGER.put(this.uid(), this.data(), {
                expiration: unixEpocExpiry,
                metadata: meta
            });

            // Seller Account copy 
            // CC:-55 -559:A:2410994419756559:-55 -559,2410994419756559,446 -635,6412535550641217,202104151300,90101503:775,775
            await LEDGER.put("CC:" + this.toBranch + ":A:" + this.toNumber + ":" + this.csvLine(), null, {
                expiration: unixEpocExpiry,
                metadata: meta
            });

            // Buyer Account copy 
            // CC:446 -635:A:6412535550641217:-55 -559,2410994419756559,446 -635,6412535550641217,202104151300,90101503:775,775
            await LEDGER.put("CC:" + this.fromBranch + ":A:" + this.fromNumber + ":" + this.csvLine(), null, {
                expiration: unixEpocExpiry,
                metadata: meta
            });

            this._isAdded = true;
            this._ip = ip;
            this._ipCountry = ipCountry;
            this._utc = meta.utc;
            this._taxRevenueStatus = meta.taxRevenueStatus;
            return true;
        } catch (err) {
            this.setError("There was a problem saving data. " + err);
            return false;
        }
    }
}

class LedgerAccountSummary extends LedgerObject {
    constructor(branchID, accountNumber) {
        super();
        if (!isAccountNumberValid(accountNumber)) {
            this.setError("Account number '" + accountNumber + "' must be 16 digits.");
            return;
        }
        const b = new LedgerBranch(branchID);
        if (!b.isValid) {
            this.setError(b.error);
            return;
        }
        this.branch = branchID;
        this.number = accountNumber;
        this.cachedUtc = 0;

    }
    async updateCache() {
        // List all account activity
        let res = await LEDGER.list({ prefix: `CC:${this.branch}:A:${this.number}:` });
        let credits = 0;
        let debits = 0;
        let recent = [];
        let peers = {};
        let seen = {};
        while (1) {
            for (let i = 0; i < res.keys.length; i++) {
                const item = res.keys[i];
                let fourthColon = 0;
                for (let x = 0; x < 4; x++) {
                    fourthColon = item.name.indexOf(':', fourthColon) + 1;
                }
                let line = item.name.substr(fourthColon);
                const csv = line.split(',');
                const tx = new LedgerTransaction(csv[0], csv[1], csv[2], csv[3], csv[4], unspscItemsFromString(csv[5]), csv[6], false);

                if (!tx.isValid || tx.isExpired() || (line in seen)) {
                    continue;
                }
                seen[line] = null;

                // memo (omitted), poof (omitted), submitted (Y/N)
                line += ",,"

                // Revenue status is always either 'submitted' or not when returning
                // from the global ledger, or in private ledger apps.
                if (item.metadata.taxRevenueStatus < 0 || item.metadata.taxRevenueStatus >= 1) {
                    line += "Y";
                } else {
                    line += "N";
                }
                recent.push(line);

                if (tx.toBranch === this.branch && tx.toNumber === this.number) {
                    credits += tx.amount;
                }

                if (tx.fromBranch === this.branch && tx.fromNumber === this.number) {
                    debits += tx.amount;
                }

                if (tx.toBranch === this.branch && tx.toNumber === this.number) {
                    const otherAccount = `(${tx.fromBranch}) ${tx.fromNumber}`;
                    if (!(otherAccount in peers)) {
                        peers[otherAccount] = { count: 0, sent: 0, received: 0, last: "" };
                    }
                    peers[otherAccount].count++;
                    peers[otherAccount].received += tx.amount;
                    peers[otherAccount].last = tx.yyyymmddhhmm;
                }

                if (tx.fromBranch === this.branch && tx.fromNumber === this.number) {
                    const otherAccount = `(${tx.toBranch}) ${tx.toNumber}`;
                    if (!(otherAccount in peers)) {
                        peers[otherAccount] = { count: 0, sent: 0, received: 0, last: "" };
                    }
                    peers[otherAccount].count++;
                    peers[otherAccount].sent += tx.amount;
                    peers[otherAccount].last = tx.yyyymmddhhmm;
                }
            }

            if (res.list_complete) {
                break;
            } else {
                res = await LEDGER.list({ "cursor": res.cursor });
            }
        }

        let count = 0;
        for (const kp in peers) {
            count++;
        }

        this.totalPeers = count;
        this.credits = credits;
        this.debits = debits;
        this.transactions = recent;
        this.peers = peers;
        this.cachedUtc = new Date().getTime() / 1000;
        await LEDGER.put(`Cache:${this.branch}:A:${this.number}:`, JSON.stringify(this));
    }
    async loadDetails() {
        const cachedString = await LEDGER.get(`Cache:${this.branch}:A:${this.number}:`);
        if (cachedString === null) {
            await this.updateCache();
            return;
        }

        const cache = JSON.parse(cachedString);

        const unixSecondsNow = new Date().getTime() / 1000;
        const cacheExpirationSeconds = 60 * 5;
        if (!cache.cachedUtc || (unixSecondsNow - cache.cachedUtc) > cacheExpirationSeconds) {
            await this.updateCache();
            return;
        }

        this.cachedUtc = cache.cachedUtc;
        this.totalPeers = cache.totalPeers;
        this.credits = cache.credits;
        this.debits = cache.debits;
        this.transactions = cache.transactions;
        this.peers = cache.peers;
    }
}


// ============ webworker/ledger.js ============

//
// Language: ES6
//
// The CivilMoney global ledger API and static website handler.
//
// Environment Variables:
// IS_TEST = true
// TELEMETRY_URL = whatever
// SITE_DOMAIN = "dev.civil.money:8443" | "civilmoney.pages.dev"


function error(asJson, message, code) {
    if (asJson) {
        return replyWithJson({ isValid: false, "error": message }, code);
    } else {
        return new Response(message, { status: code })
    }
}

function replyWithJson(object, status) {
    return new Response(JSON.stringify(object), {
        headers: {
            "Access-Control-Allow-Origin": "*",
            "Content-Type": "application/json;charset=utf-8"
        },
        status: status
    });
}

function replyWithHtml(html, status) {
    return new Response(html, {
        headers: {
            "Access-Control-Allow-Origin": "*",
            "Content-Type": "text/html;charset=utf-8"
        },
        status: status
    });
}

addEventListener("fetch", event => {
    const { request } = event
    const asJson = (request.headers.get("Accept") || "").match("text/html") === null;

    const response = handleRequest(request, asJson).catch((error) => {
        return error(asJson, error.stack || error, 500);
    });

    event.respondWith(response);
})

async function getTemplate(pagePath) {
    const response = await fetch(`https://${SITE_DOMAIN}/html-${(IS_TEST === "true" ? "test" : "live")}/${pagePath}`);
    return await response.text();
}

async function handleRequest(request, asJson) {

    const { city, region, country } = request.cf || {}
    const ip = request.headers.get("cf-connecting-ip");
    const ipCountry = city + ", " + region + ", " + country;
    const { pathname, hostname, protocol } = new URL(request.url);
    // Ensure no www and always https
    const siteHostName = IS_TEST === "true" ? "test.civil.money" : "civil.money";

    if ((hostname !== siteHostName
        || protocol !== "https:")
        && hostname.indexOf("workers.dev") === -1) {
        let rewrite = new URL(request.url);
        rewrite.hostname = siteHostName;
        rewrite.protocol = "https:"
        return new Response(null, {
            status: 301, headers: {
                "Location": rewrite.toString(),
            }
        });
    }

    let url = pathname;
    switch (url) {
        case "/":
        case "/about": return getHomepage(request);
        case "/robots.txt": return new Response(null, { status: 204 });
    }
   
    if (url.endsWith(".css")
        || url.endsWith(".js")
        || url.endsWith(".png")
        || url.endsWith(".ico")
        || url.endsWith(".webmanifest")
        || url.startsWith("/app")) {
        let rewrite = new URL(request.url);
        if (SITE_DOMAIN.indexOf(':') > -1) {
            rewrite.hostname = SITE_DOMAIN.split(':')[0];
            rewrite.port = SITE_DOMAIN.split(':')[1];
        } else {
            rewrite.hostname = SITE_DOMAIN;
        }
        if (url === "/app") {
            url += "/";
        }
        rewrite.pathname = url.replace(/-cachebust\d+/gi, "");
        return fetch(rewrite.toString());
    }

    url = decodeURI(url);
    if (url.startsWith("/")) {
        url = url.substr(1);
    }
    if (url.endsWith("/")) {
        url = url.substr(0, url.length - 1);
    }
    try {
        const tx = new TransactionUrl(url);
        switch (tx.type) {
            //civil.money/407 -740
            case "branch": return getBranch(asJson, tx.toBranch);
            //civil.money/407 -740/6412535550641217
            case "account": return getAccount(asJson, tx.toBranch, tx.toNumber);
            case "old-transaction": return getTransaction(asJson, tx.toBranch, tx.toNumber, tx.fromBranch, tx.fromNumber, tx.yyyymmddhhmm, null, null);
            case "new-transaction":
                {
                    if (request.method.toLowerCase() === "post") {
                        return putTransaction(ip, ipCountry, tx.toBranch, tx.toNumber, tx.fromBranch, tx.fromNumber, tx.yyyymmddhhmm, tx.unspscItems, tx.amount, request);
                    } else {
                        return getTransaction(asJson, tx.toBranch, tx.toNumber, tx.fromBranch, tx.fromNumber, tx.yyyymmddhhmm, tx.unspscItems, tx.amount);
                    }
                }
        }

        return error(asJson, "Not found", 404);
    } catch (e) {
        return error(asJson, e.message, 500);
    }
}

async function SendTelemetry(msg) {
    try {
        if (TELEMETRY_URL !== undefined) {
            await fetch(TELEMETRY_URL + encodeURIComponent(msg));
        }
    } catch { }
}
async function getHomepage(request) {
    const { city, region, country, timezone: timeZone, latitude, longitude } = request.cf || {}
    let html = await getTemplate("index.html");
    const defaults = {
        latitude: latitude,
        longitude: longitude,
        locationName: city + ", " + region
    };
    html = html.replace(" DEFAULTS = {}", " DEFAULTS = " + JSON.stringify(defaults, null, 2));
    const referer = (request.headers.get("Referer") || "");
    if (referer.length > 0
        && referer.indexOf("civil.money") === -1
        && referer.indexOf("civilmoney.org") === -1
        && referer.indexOf("civilmoney.com") === -1) {
        const ip = request.headers.get('cf-connecting-ip');
        await SendTelemetry(`CM ${city}, ${region}, ${country} ${ip} via:\n${referer}`);
    }
    return replyWithHtml(html, 200);
}

async function getBranch(asJson, branchID) {
    try {
        const branch = new LedgerBranch(branchID);
        if (branch.isValid) {
            await branch.loadDetails();
        }
        if (asJson) {
            return replyWithJson(branch, branch.isValid ? 200 : 400);
        } else {
            let html = await getTemplate("branch.html");
            html = html.replace(" DETAILS = {}", " DETAILS = " + JSON.stringify(branch, null, 2));
            html = html.replace(/#BRANCH_NUMBER#/gi, `${branch.id}`);
            return replyWithHtml(html, branch.isValid ? 200 : 400);
        }
    } catch (err) {
        return error(asJson, err, 400);
    }
}

async function getAccount(asJson, branchID, accountID) {
    try {
        const account = new LedgerAccountSummary(branchID, accountID);
        if (account.isValid) {
            await account.loadDetails();
        }
        if (asJson) {
            return replyWithJson(account, account.isValid ? 200 : 400);
        } else {
            let html = await getTemplate("account.html");
            html = html.replace(" DETAILS = {}", " DETAILS = " + JSON.stringify(account, null, 2));
            html = html.replace(/#ACCOUNT_NUMBER#/gi, formatAccountNumber(account.branch, account.number));
            return replyWithHtml(html, account.isValid ? 200 : 400);
        }
    } catch (err) {
        return error(asJson, err, 400);
    }
}

async function getTransaction(asJson, toBranch, toNumber,
    fromBranch, fromNumber,
    yyyymmddhhmm, unspsc, amount) {
    try {
        /* 
        200 OK
        218 This is fine
        400 Bad Request
        */
        const isLoadDesired = unspsc === null && amount === null;
        const t = new LedgerTransaction(toBranch, toNumber,
            fromBranch, fromNumber,
            yyyymmddhhmm, unspsc, amount, isLoadDesired);

        let isAdded = await t.isAdded(isLoadDesired);

        // See notes on TaxRevenueStatus.
        if (isAdded) {
            t._taxRevenueStatus = TaxRevenueStatus.Submitted;
            t._ip = null;
            t._ipCountry = null;
        }

        if (asJson) {
            return replyWithJson(t, t.isValid ? (isAdded ? 200 : 218) : 400);
        } else {
            let html = await getTemplate("transaction.html");
            html = html.replace(" DETAILS = {}", " DETAILS = " + JSON.stringify(t, null, 2));
            return replyWithHtml(html, t.isValid ? (isAdded ? 200 : 218) : 400);
        }
    } catch (e) {
        return error(asJson, e.message, 500);
    }
}

async function putTransaction(ip, ipCountry, toBranch, toNumber,
    fromBranch, fromNumber,
    yyyymmddhhmm, unspsc, amount, request) {
    /* 
    201 Created
    200 OK
    400 Bad Request
    500 Some error
    */
    const t = new LedgerTransaction(toBranch, toNumber,
        fromBranch, fromNumber,
        yyyymmddhhmm, unspsc, amount, false);
    let status = 0;
    if (t.isValid) {
        let isAlreadyAdded = await t.isAdded();
        if (!isAlreadyAdded) {
            const taxRevenueStatus = TaxRevenueStatus.Submitted;
            if (!await t.tryAdd(ip, ipCountry, taxRevenueStatus)) {
                status = 500;
            } else {
                status = 201;
                await SendTelemetry(`CM https://civil.money/${toBranch},${toNumber} //c ${amount}`);
            }
        } else {
            status = 200;
        }
        const proofOfTime = await request.text();
        if (proofOfTime && (status === 200 || status === 201)) {
            // is there a proof of time already?
            const existingProof = await t.tryGetProofOfTime();
            if (existingProof !== proofOfTime) {
                const memo = ip + " " + ipCountry;
                await t.tryAddProofOfTime(proofOfTime, memo);
            }
        }
    } else {
        status = 400;
    }
    if (status === 200 || status === 201) {
        // See notes on TaxRevenueStatus.
        t._taxRevenueStatus = TaxRevenueStatus.Submitted;
    } else {
        t._taxRevenueStatus = 0;
    }
    t._ip = null;
    t._ipCountry = null;
    return replyWithJson(t, status);
}
