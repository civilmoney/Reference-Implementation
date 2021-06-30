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
