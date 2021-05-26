const SITE_DOMAIN = "civilmoney.pages.dev";
const COMMUNITY_ACCOUNT = "0000000000000000";

function error(message, code) {
    return new Response(message, { status: code })
}
function replyWithJson(object, status) {
    return new Response(JSON.stringify(object), {
        headers: {
            "content-type": "application/json;charset=utf-8"
        },
        status: status
    });
}
function replyWithHtml(html, status) {
    return new Response(html, {
        headers: {
            "content-type": "text/html;charset=utf-8"
        },
        status: status
    });
}
function isAccountNumberValid(accountNumber) {
    return (accountNumber || "").match(/^[\d]{16}$/) !== null;
}
function isUNSPSCValid(unspscCode) {
    return (unspscCode || "").match(/^[\d]{8}$/) !== null;
}
function formatUTCDate(yyyymmddhhmm) {
    try {
        let formatted = yyyymmddhhmm.substr(0, 4)
            + "-" + yyyymmddhhmm.substr(4, 2)
            + "-" + yyyymmddhhmm.substr(6, 2)
            + " " + yyyymmddhhmm.substr(8, 2)
            + ":" + yyyymmddhhmm.substr(10, 2);
        return formatted;
    } catch (err) {
        throw "Invalid UTC yyyymmddhhmm.";
    }
}
function parseUTCDate(yyyymmddhhmm) {
    try {
        let formatted = formatUTCDate(yyyymmddhhmm) + " UTC";
        return new Date(formatted);
    } catch (err) {
        throw "Invalid UTC yyyymmddhhmm.";
    }
}
function twoYearsFrom(yyyymmddhhmm) {
    let d = parseUTCDate(yyyymmddhhmm);
    d.setFullYear(d.getFullYear() + 2);
    return d;
}
function sumDigitsGroupedIntoFours(bignumber) {
    if ((bignumber.length / 4) % 1 != 0) {
        throw `Number '${bignumber}' is invalid because it cannot be grouped into fours.`;
    }
    let sum = 0;
    while (bignumber.length > 0) {
        sum += parseInt(bignumber.substr(0, 4), 10);
        bignumber = bignumber.substr(4);
    }
    return sum;
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

addEventListener('fetch', event => {
    const { request } = event
    //let asset = await getAssetFromKV(event)
    const response = handleRequest(request).catch(handleError)
    event.respondWith(response)
})

function handleError(error) {
    console.error('Uncaught error:', error)

    const { stack } = error
    return new Response(stack || error, {
        status: 500,
        headers: {
            'Content-Type': 'text/plain;charset=UTF-8'
        }
    })
}

async function getTemplate(pagePath) {
    const response = await fetch("http://" + SITE_DOMAIN + "/" + (IS_TEST ? "test-" : "") + pagePath);
    return await response.text();
}

async function handleRequest(request) {

    const { city, region, country } = request.cf || {}
    const ip = request.headers.get('cf-connecting-ip');
    const ipCountry = city + ", " + region + ", " + country;
    const { pathname } = new URL(request.url);

    let url = pathname;
    switch (url) {
        case "/":
        case "/about": return getHomepage(request);
        case "/favicon.ico":
        case "/robots.txt": return new Response(null, { status: 204 });
    }

    url = decodeURI(url);
    if (url.startsWith("/")) {
        url = url.substr(1);
    }
    if (url.endsWith("/")) {
        url = url.substr(0, url.length - 1);
    }

    const asJson = (request.headers.get("Accept") || "").match("text/html") === null;
    const tx = new TransactionUrl(url);
    switch (tx.type) {
        //civil.money/407 -740
        case "branch": return getBranch(asJson, tx.toBranch);
        //civil.money/407 -740/6412535550641217
        case "account": return getAccount(asJson, tx.toBranch, tx.toNumber);
        case "old-transaction": return getTransaction(asJson, tx.toBranch, tx.toNumber, tx.fromBranch, tx.fromNumber, tx.yyyymmddhhmm, null, null);
        case "new-transaction":
            {
                if (request.method.toLowerCase() == "put") {
                    return putTransaction(ip, ipCountry, tx.toBranch, tx.toNumber, tx.fromBranch, tx.fromNumber, tx.yyyymmddhhmm, tx.unspsc, tx.amount);
                } else {
                    return getTransaction(asJson, tx.toBranch, tx.toNumber, tx.fromBranch, tx.fromNumber, tx.yyyymmddhhmm, tx.unspsc, tx.amount);
                }
            }
    }

    if (url.endsWith(".css") || url.endsWith(".js")) {
        let rewrite = new URL(request.url);
        rewrite.hostname = SITE_DOMAIN;
        return fetch(rewrite.toString());
    }

    return new Response('Not Found', { status: 404 })
}

class CMObject {
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

class Branch extends CMObject {

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
            this.transactions = [];
            this.accounts = [];
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
        let recent = [];
        let accounts = {};
        let seen = {};
        while (1) {
            for (var i = 0; i < res.keys.length; i++) {
                const item = res.keys[i];
                const line = (item.name.substr(item.name.lastIndexOf(':') + 1))
                const csv = line.split(',');
                const tx = new Transaction(csv[0], csv[1], csv[2], csv[3], csv[4], csv[5], csv[6], false);

                if (!tx.isValid || tx.isExpired() || seen.hasOwnProperty(line)) {
                    continue;
                }
                seen[line] = null;
                recent.push(line);

                if (tx.toBranch.id === this.id && tx.toNumber !== COMMUNITY_ACCOUNT) {
                    tax += tx.amount * 0.1;
                }

                if (tx.toBranch.id === this.id && tx.toNumber === COMMUNITY_ACCOUNT) {
                    credits += tx.amount;
                }

                if (tx.fromBranch.id === this.id && tx.fromNumber === COMMUNITY_ACCOUNT) {
                    debits += tx.amount;
                }

                if (tx.toBranch.id === this.id && tx.toNumber !== COMMUNITY_ACCOUNT) {
                    if (!accounts.hasOwnProperty(tx.toNumber)) {
                        accounts[tx.toNumber] = { count: 0, credits: 0, debits: 0, last: "" };
                    }
                    accounts[tx.toNumber].count++;
                    accounts[tx.toNumber].credits += tx.amount;
                    accounts[tx.toNumber].last = tx.yyyymmddhhmm;
                }

                if (tx.fromBranch.id === this.id && tx.fromNumber !== COMMUNITY_ACCOUNT) {
                    if (!accounts.hasOwnProperty(tx.fromNumber)) {
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
        this.transactions = recent;
        this.accounts = accounts;

        await LEDGER.put(`Cache:${this.id}:`, JSON.stringify(this));
    }

    async loadDetails() {
        const cachedString = await LEDGER.get(`Cache:${this.id}:`);
        if (cachedString === null) {
            await this.updateCache();
            return;
        }
        const cache = JSON.parse(cachedString);
        this.totalAccounts = cache.totalAccounts;
        this.revenue = cache.revenue;
        this.credits = cache.credits;
        this.debits = cache.debits;
        this.transactions = cache.transactions;
        this.accounts = cache.accounts;
    }
}


class TransactionUrl {
    constructor(url) {
        const REGEX_TO_FROM_DATE_UNSPSC_AMOUNT = /(\(?[+-]?\d+ [+-]?\d+\)?)[ ]*[\t/,|]?[ ]*(\d{4}\s*\d{4}\s*\d{4}\s*\d{4})?[ ]*[\t/,|]?[ ]*(\(?[+-]?\d+ [+-]?\d+\)?)?[ ]*[\t/,|]?[ ]*(\d{4}\s*\d{4}\s*\d{4}\s*\d{4})?[ ]*[\t/,|]?[ ]*(\d{4}[ -.]?\d{2}[ -.]?\d{1,2}[\s@]*\d{1,2}[.:]?\d{2})?[ ]*[\t/,|]?[ ]*(\d{8})?[ ]*[\t/,|]?[ ]*(?:[-+])?(\d+)?/i
        try {
            this.type = null;
            this.toBranch = null;
            this.toNumber = null;
            this.fromBranch = null;
            this.fromNumber = null;
            this.yyyymmddhhmm = null;
            this.unspsc = null;
            this.amount = null;
            const ar = url.match(REGEX_TO_FROM_DATE_UNSPSC_AMOUNT);
            if (ar === null) {
                return;
            }

            // check for spurious numbers (not all numbers matched properly)
            for (var i = 1; i < ar.length; i++) {
                if (ar[i] !== undefined && ar[i - 1] === undefined) {
                    return;
                }
            }

            if (ar.length === 8
                && ar[6] !== undefined
                && ar[7] !== undefined) {
                this.amount = ar[7];
                this.unspsc = ar[6];
            }
            if (ar.length >= 6
                && ar[5] !== undefined
            ) {
                this.yyyymmddhhmm = ar[5].replace(/[^\d]/g, "");
            }
            if (ar.length >= 5
                && ar[4] !== undefined
                && ar[3] !== undefined) {
                this.fromNumber = ar[4].replace(/[^\d]/g, "");
                this.fromBranch = ar[3].replace(/[()+]/g, "");
            }
            if (ar.length >= 3
                && ar[2] !== undefined) {
                this.toNumber = ar[2].replace(/[^\d]/g, "");
            }
            if (ar.length >= 2
                && ar[1] !== undefined) {
                this.toBranch = ar[1].replace(/[()+]/g, "");
            }

            this.type = this.amount !== null && this.yyyymmddhhmm !== null && this.toNumber !== null && this.toNumber !== null ? "new-transaction"
                : this.yyyymmddhhmm !== null && this.toNumber !== null && this.toNumber !== null ? "old-transaction"
                    : this.toNumber !== null && this.fromBranch === null ? "account"
                        : this.toBranch !== null && this.fromBranch === null ? "branch"
                            : null;
        } catch (err) {
            throw `Invalid url ${url}`;
        }
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
        if (this.unspsc === null || this.amount === null)
            return s;
        s += " UNSPSC: " + this.unspsc + " //c " + this.amount;
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
        if (this.unspsc === null || this.amount === null)
            return s;
        s += "," + this.unspsc + "," + this.amount;
        return s;
    }
}


class Transaction extends CMObject {

    constructor(toBranch, toNumber,
        fromBranch, fromNumber,
        yyyymmddhhmm, unspsc, amount, isLoadDesired) {
        super();
        let field = "";
        try {

            field = "yyyymmddhhmm";
            if ((yyyymmddhhmm || "").match(/^[\d]{12}$/) === null) {
                throw "Must be 12 digits.";
            }
            this.yyyymmddhhmm = yyyymmddhhmm;

            field = "toBranch";
            this.toBranch = new Branch(toBranch);
            this.toBranch.throwIfErrant();

            field = "toNumber";
            this.toNumber = toNumber.replace(/\s/g, "");
            if (!isAccountNumberValid(this.toNumber)) {
                throw "Must be 16 digits.";
            }

            field = "fromBranch";
            this.fromBranch = new Branch(fromBranch);
            this.fromBranch.throwIfErrant();

            field = "fromNumber";
            this.fromNumber = fromNumber.replace(/\s/g, "");
            if (!isAccountNumberValid(this.fromNumber)) {
                throw "Must be 16 digits.";
            }


            field = "account";
            if (this.toBranch.id + "," + this.toNumber === this.fromBranch.id + "," + this.fromNumber) {
                throw "The from/to accounts must be different.";
            }

            if (isLoadDesired) {

            } else {
                field = "unspsc";
                this.unspsc = unspsc.replace(/\s/g, "");
                if (!isUNSPSCValid(this.unspsc)) {
                    throw "Must be 8 digits.";
                }

                field = "amount";
                this.amount = parseInt(amount, 10);
            }

        } catch (err) {
            this.setError(`Invalid Transaction ${field}. ${(err || "")}`);
        }
    }

    uid() {
        return this.toBranch.id
            + "," + this.toNumber
            + "," + this.fromBranch.id
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

        if (this._isLoaded) {
            return this._isAdded;
        }

        const res = await LEDGER.getWithMetadata(this.uid());
        this._isAdded = res.value !== null;

        if (!this._isAdded) {
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

        this._debugValue = res.value;
        if (res.metadata !== null) {
            this._utc = res.metadata.utc;
            this._ip = res.metadata.ip;
            this._ipCountry = res.metadata.ipCountry;
        }
        return true;
    }

    async tryAdd(ip, ipCountry) {
        try {
            let unixEpocExpiry = twoYearsFrom(this.yyyymmddhhmm).getTime() / 1000;
            const meta = { utc: new Date(), ip: ip, ipCountry: ipCountry };

            // Master copy "by UID"
            await LEDGER.put(this.uid(), this.data(), {
                expiration: unixEpocExpiry,
                metadata: meta
            });

            // Seller Account copy 
            // CC:-55 -559:A:2410994419756559:-55 -559,2410994419756559,446 -635,6412535550641217,202104151300,90101503,775
            await LEDGER.put("CC:" + this.toBranch.id + ":A:" + this.toNumber + ":" + this.csvLine(), null, {
                expiration: unixEpocExpiry,
                metadata: meta
            });

            // Buyer Account copy 
            // CC:446 -635:A:6412535550641217:-55 -559,2410994419756559,446 -635,6412535550641217,202104151300,90101503,775
            await LEDGER.put("CC:" + this.fromBranch.id + ":A:" + this.fromNumber + ":" + this.csvLine(), null, {
                expiration: unixEpocExpiry,
                metadata: meta
            });

            await LEDGER.delete("Cache:" + this.toBranch.id + ":", null);
            await LEDGER.delete("Cache:" + this.toBranch.id + ":A:" + this.toNumber, null);
            await LEDGER.delete("Cache:" + this.fromBranch.id + ":", null);
            await LEDGER.delete("Cache:" + this.fromBranch.id + ":A:" + this.fromNumber, null);

            this._isAdded = true;
            this._ip = ip;
            this._ipCountry = ipCountry;
            this._utc = meta.utc;

            return true;
        } catch (err) {
            this.setError("There was a problem saving data. " + err);
            return false;
        }
    }
}

class AccountSummary extends CMObject {

    constructor(branch, accountNumber) {
        super();
        if (!isAccountNumberValid(accountNumber)) {
            this.setError("Account number '" + accountNumber + "' must be 16 digits.");
            return;
        }
        if (!branch.isValid) {
            this.setError(branch.error);
            return;
        }
        this.branch = branch;
        this.number = accountNumber;
    }


    async updateCache() {
        // List all account activity
        let res = await LEDGER.list({ prefix: `CC:${this.branch.id}:A:${this.number}:` });
        let credits = 0;
        let debits = 0;
        let recent = [];
        let peers = {};
        let seen = {};
        while (1) {
            for (var i = 0; i < res.keys.length; i++) {
                const item = res.keys[i];
                const line = (item.name.substr(item.name.lastIndexOf(':') + 1))
                const csv = line.split(',');
                const tx = new Transaction(csv[0], csv[1], csv[2], csv[3], csv[4], csv[5], csv[6], false);

                if (!tx.isValid || tx.isExpired() || seen.hasOwnProperty(line)) {
                    continue;
                }
                seen[line] = null;
                recent.push(line);

                if (tx.toBranch.id === this.branch.id && tx.toNumber === this.number) {
                    credits += tx.amount;
                }

                if (tx.fromBranch.id === this.branch.id && tx.fromNumber === this.number) {
                    debits += tx.amount;
                }

                if (tx.toBranch.id === this.branch.id && tx.toNumber === this.number) {
                    const otherAccount = `(${tx.fromBranch.id}) ${tx.fromNumber}`;
                    if (!peers.hasOwnProperty(otherAccount)) {
                        peers[otherAccount] = { count: 0, sent: 0, received: 0, last: "" };
                    }
                    peers[otherAccount].count++;
                    peers[otherAccount].received += tx.amount;
                    peers[otherAccount].last = tx.yyyymmddhhmm;
                }

                if (tx.fromBranch.id === this.branch.id && tx.fromNumber === this.number) {
                    const otherAccount = `(${tx.toBranch.id}) ${tx.toNumber}`;
                    if (!peers.hasOwnProperty(otherAccount)) {
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

        await LEDGER.put(`Cache:${this.branch.id}:A:${this.number}:`, JSON.stringify(this));
    }

    async loadDetails() {
        const cachedString = await LEDGER.get(`Cache:${this.branch.id}:A:${this.number}:`);
        if (cachedString === null) {
            await this.updateCache();
            return;
        }
        const cache = JSON.parse(cachedString);
        this.totalPeers = cache.totalPeers;
        this.credits = cache.credits;
        this.debits = cache.debits;
        this.transactions = cache.transactions;
        this.peers = cache.peers;
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
    let defaults = {
        latitude: latitude,
        longitude: longitude,
        locationName: city + ", " + region
    };
    html = html.replace(" DEFAULTS = {}", " DEFAULTS = " + JSON.stringify(defaults, null, 2));
    const referer = (request.headers.get("Referer") || "");
    if (referer.length > 0
        && referer.lastIndexOf("civil.money") == -1
        && referer.lastIndexOf("civilmoney.org") == -1
        && referer.lastIndexOf("civilmoney.com") == -1) {
        const ip = request.headers.get('cf-connecting-ip');
        await SendTelemetry(`CM ${city}, ${region}, ${country} ${ip} via:\n${referer}`);
    }
    return replyWithHtml(html, 200);
}

async function getBranch(asJson, branchID) {
    try {
        const branch = new Branch(branchID);
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
        return error(err, 400);
    }
}

async function getAccount(asJson, branchID, accountID) {
    try {
        const branch = new Branch(branchID);
        accountID = (accountID || "").replace(/\s/g, "");
        let account = new AccountSummary(branch, accountID);
        if (account.isValid) {
            await account.loadDetails();
        }
        if (asJson) {
            return replyWithJson(account, account.isValid ? 200 : 400);
        } else {
            let html = await getTemplate("account.html");
            html = html.replace(" DETAILS = {}", " DETAILS = " + JSON.stringify(account, null, 2));
            html = html.replace(/#ACCOUNT_NUMBER#/gi, formatAccountNumber(account.branch.id, account.number));
            return replyWithHtml(html, account.isValid ? 200 : 400);
        }
    } catch (err) {
        return error(err, 400);
    }
}

async function getTransaction(asJson, toBranch, toNumber,
    fromBranch, fromNumber,
    yyyymmddhhmm, unspsc, amount) {
    /* 
    200 OK
    218 This is fine
    400 Bad Request
    */
    const isLoadDesired = unspsc === null && amount === null;
    let t = new Transaction(toBranch, toNumber,
        fromBranch, fromNumber,
        yyyymmddhhmm, unspsc, amount, isLoadDesired);

    let isAdded = false;

    if (t.isValid) {
        isAdded = await t.isAdded(isLoadDesired);
        t._isAdded = isAdded;
    }

    if (asJson) {
        return replyWithJson(t, t.isValid ? (isAdded ? 200 : 218) : 400);
    } else {
        let html = await getTemplate("transaction.html");
        html = html.replace(" DETAILS = {}", " DETAILS = " + JSON.stringify(t, null, 2));
        return replyWithHtml(html, t.isValid ? (isAdded ? 200 : 218) : 400);
    }
}

async function putTransaction(ip, ipCountry, toBranch, toNumber,
    fromBranch, fromNumber,
    yyyymmddhhmm, unspsc, amount) {
    /* 
    201 Created
    200 OK
    400 Bad Request
    500 Some error
    */
    let t = new Transaction(toBranch, toNumber,
        fromBranch, fromNumber,
        yyyymmddhhmm, unspsc, amount, false);
    let status = 0;
    if (t.isValid) {
        let isAlreadyAdded = await t.isAdded();
        if (!isAlreadyAdded) {
            if (!await t.tryAdd(ip, ipCountry)) {
                status = 500;
            } else {
                status = 201;
                await SendTelemetry(`CM https://civil.money/${toBranch},${toNumber} //c ${amount}`);
            }
        } else {
            status = 200;
        }
    } else {
        status = 400;
    }

    return replyWithJson(t, status);
}
