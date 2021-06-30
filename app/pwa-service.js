//  <mimeMap fileExtension=".webmanifest" mimeType="application/manifest+json" />

const staticCacheName = "civilmoney-pwa-build3";
// const dynamicCacheName = "civilmoney-pwa-dynamic";
const filesToCache = [
    "/common/civilmoney-common.js",
    "/common/civilmoney-model.js",
    "/common/civilmoney-dom.js",
    "/common/unspsc.js",
    "/favicon.ico",
    "/app/",
    "/app/pwa.min.css",
    "/app/pwa.js",
    "/app/pwa.webmanifest",
    "/app/text/en.js",
    "/app/qrcode.min.js",
    "/app/qr-scanner.umd.min.js",
    "/app/qr-scanner-worker.min.js",
    "https://fonts.gstatic.com/s/robotoslab/v13/BngMUXZYTXPIvIBgJJSb6ufA5qW54A.woff2",
    "https://fonts.gstatic.com/s/robotoslab/v13/BngMUXZYTXPIvIBgJJSb6ufJ5qW54A.woff2",
    "https://fonts.gstatic.com/s/robotoslab/v13/BngMUXZYTXPIvIBgJJSb6ufO5qW54A.woff2",
    "https://fonts.gstatic.com/s/robotoslab/v13/BngMUXZYTXPIvIBgJJSb6ufC5qW54A.woff2",
    "https://fonts.gstatic.com/s/robotoslab/v13/BngMUXZYTXPIvIBgJJSb6ufD5qW54A.woff2",
    "https://fonts.gstatic.com/s/robotoslab/v13/BngMUXZYTXPIvIBgJJSb6ufN5qU.woff2",
    "https://fonts.gstatic.com/s/robotoslab/v13/BngMUXZYTXPIvIBgJJSb6ufB5qW54A.woff2"
];

self.addEventListener("activate", async (e) => {
    // Purge obsolete cache
    const keys = await caches.keys();
    keys.map((key) => {
        if (key !== staticCacheName
            && key.indexOf("pwa-build") > -1) {
            caches.delete(key);
        }
    });
});

self.addEventListener("install", async (e) => {
    const cache = await caches.open(staticCacheName);
    cache.addAll(filesToCache);
});

self.addEventListener("fetch", (e) => {
    const req = e.request;
    const url = new URL(req.url);
    if (url.origin === location.origin
        || url.origin === "fonts.gstatic.com") {
        e.respondWith(staticReply(req));
    } else {
        //e.respondWith(dynamicReply(req));
        e.respondWith(fetch(req));
    }
});

// No real use for dynamic caching.
// async function dynamicReply(req) {
//     // Prefers network when available
//     const cache = await caches.open(dynamicCacheName);
//     let res = null;
//     try {
//         res = await fetch(req);
//         cache.put(req, res.clone());
//     } catch (err) {
//         res = await cache.match(req);
//     }
//     return res;
// }

async function staticReply(req) {
    const cached = await caches.match(req);
    return cached || fetch(req);
}

