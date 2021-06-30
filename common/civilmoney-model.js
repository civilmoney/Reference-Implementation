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

                if (!tx.isValid || tx.isExpired() || (line in seen) || item.metadata.deleted) {
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
    async tryAdd(ip, ipCountry, taxRevenueStatus, deleted=false) {
        try {
            let unixEpocExpiry = twoYearsFrom(this.yyyymmddhhmm).getTime() / 1000;
            const meta = {
                utc: new Date(),
                ip: ip,
                ipCountry: ipCountry,
                taxRevenueStatus: taxRevenueStatus,
                "deleted": deleted
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

                if (!tx.isValid || tx.isExpired() || (line in seen) || item.metadata.deleted) {
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
