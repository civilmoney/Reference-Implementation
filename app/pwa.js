const ui = new Element("div", "ui");
const GLOBAL_LEDGER_DOMAIN = "civil.money";

let currentPage = null;

window.indexedDB = window.indexedDB || window.mozIndexedDB || window.webkitIndexedDB || window.msIndexedDB;
window.IDBTransaction = window.IDBTransaction || window.webkitIDBTransaction || window.msIDBTransaction;
window.IDBKeyRange = window.IDBKeyRange || window.webkitIDBKeyRange || window.msIDBKeyRange

class LedgerEntry {
    constructor(key, value, metadata, expiration) {
        this.key = key;
        this.value = value;
        this.metadata = metadata;
        this.expiration = expiration;
    }
}
class LedgerListRequest {
    constructor() {
        this.prefix = null;
        this.cursor = null;
    }
}
class LedgerListResult {
    constructor() {
        this.cursor = {
            iCursor: null,

        };
        this.list_complete = false;
        this.keys = [];
    }
}
// Roughly emmulates the Cloudflare KV store for reusability.
const LEDGER = {
    m_db: null,
    initialise: async () => {
        return new Promise((resolve, reject) => {
            if (LEDGER.m_db !== null) {
                resolve();
                return;
            }
            var request = window.indexedDB.open("civilmoney-kv", 1);
            request.onerror = (e) => {
                reject(RS.PHRASE_UnableToAccessStorageOnThisDevice + " - " + e.target.errorCode);
            };
            request.onsuccess = (e) => {
                LEDGER.m_db = e.target.result;
                resolve();
            };
            request.onupgradeneeded = (e) => {
                const upgrade = e.target.result;
                upgrade.createObjectStore("entries", { keyPath: "key" });
            }
        });
    },
    put: async (key, value, expirationMetaData) => {
        await LEDGER.initialise();
        return new Promise((resolve, reject) => {
            const transaction = LEDGER.m_db.transaction(["entries"], "readwrite");
            const objectStore = transaction.objectStore("entries");
            const entry = new LedgerEntry(key, value, expirationMetaData ? expirationMetaData.metadata : null, expirationMetaData ? expirationMetaData.expiration : 0);
            const objectRequest = objectStore.put(entry); // Overwrite if exists
            objectRequest.onsuccess = (e) => {
                resolve();
            };
            objectRequest.onerror = function (e) {
                reject("put failed: " + e.target.errorCode);
            };
        });
    },
    getWithMetadata: async (key) => {
        await LEDGER.initialise();
        return new Promise((resolve, reject) => {
            const transaction = LEDGER.m_db.transaction(["entries"], "readonly");
            const objectStore = transaction.objectStore("entries");
            const objectRequest = objectStore.get(key);
            objectRequest.onsuccess = (e) => {
                resolve(objectRequest.result || { value: null });
            };
            objectRequest.onerror = function (e) {
                reject("getWithMetadata failed: " + e.target.errorCode);
            };
        });
    },
    get: async (key) => {
        return (await LEDGER.getWithMetadata(key)).value;
    },
    delete: async (key) => {
        await LEDGER.initialise();
        return new Promise((resolve, reject) => {
            const transaction = LEDGER.m_db.transaction(["entries"], "readwrite");
            const objectStore = transaction.objectStore("entries");
            const objectRequest = objectStore.delete(key);
            objectRequest.onsuccess = (e) => {
                resolve();
            };
            objectRequest.onerror = function (e) {
                reject("delete failed: " + e.target.errorCode);
            };
        });
    },
    list: async (ledgerListRequest) => {
        await LEDGER.initialise();
        return new Promise((resolve, reject) => {
            let res = new LedgerListResult();
            res.cursor = ledgerListRequest.cursor || { iCursor: null, originalPrefix: ledgerListRequest.prefix };

            const onResult = (e) => {
                const cursor = e.target.result;
                if (cursor) {
                    if (cursor.key.startsWith(res.cursor.originalPrefix)) {
                        res.keys.push({ name: cursor.key, expiration: cursor.value.expiration, metadata: cursor.value.metadata });
                    }
                    if (res.keys.length === 2) {
                        res.cursor.iCursor = cursor;
                        resolve(res);
                        res = null;
                    } else {
                        cursor.continue();
                    }
                } else {
                    res.list_complete = true;
                    resolve(res);
                    res = null;
                }
            };
            const onError = (e) => { reject("list failed: " + e.target.errorCode); };

            const isContinuation = res.cursor.iCursor !== null;
            if (!isContinuation) {
                const transaction = LEDGER.m_db.transaction(["entries"], "readonly");
                const objectStore = transaction.objectStore("entries");
                res.cursor.iCursor = objectStore.openCursor();
                res.cursor.iCursor.onsuccess = onResult
                res.cursor.iCursor.onerror = onError;
            } else {
                res.cursor.iCursor.request.onsuccess = onResult;
                res.cursor.iCursor.request.onerror = onError;
                try {
                    res.cursor.iCursor.continue();
                } catch {
                    res.list_complete = true;
                    resolve(res);
                }
            }


        });

    }
};


class LocalAccount {
    constructor() {
        this.branch = "";
        this.number = "";
        this.label = null;
        this.lastUsed = 0;
        this.memorableSentence = "";
    }
}

class TopNav extends Element {
    constructor(isButtonForBack = true) {
        super("div", "top-nav");
        this._onButton = null;
        this._button = this.element("a", isButtonForBack ? "back" : "menu");
        this._button.dom.ariaLabel = isButtonForBack ? RS.LABEL_Back : RS.ARIA_OpenMenu;
        this._button.dom.title = isButtonForBack ? RS.LABEL_Back : RS.ARIA_OpenMenu;
        this._button.dom.href = "javascript:;";
        this._button.dom.addEventListener("click", () => {
            this._onButton();
        });
        let right = this.div();
        this._title = right.h1();
        this._intro = right.div();
    }
    get title() {
        return this._title.dom.textContent;
    }
    set title(value) {
        if (value !== null && value !== undefined) {
            this._title.dom.textContent = value;
        } else {
            this._title.dom.textContent = "";
        }
    }
    get intro() {
        return this._intro.dom.textContent;
    }
    set intro(value) {
        if (value !== null && value !== undefined) {
            this._intro.dom.textContent = value;
        } else {
            this._intro.dom.textContent = "";
        }
    }
    get onButton() {
        return this._onButton;
    }
    set onButton(value) {
        this._onButton = value;
    }
}

class AmountElement extends Element {
    constructor(value, editable = false) {
        super("div", "amount");
        this._value = value;
        this._onValueChanged = null;
        const amounts = this.div("amounts");
        let row = amounts.div("time");
        row.span("symbol", "//c");
        this._valueEl = row.span("val", formatDecimals(value));
        this._valueEl.dom.ariaLabel = RS.ARIA_AmountAsCivilizedTime;
        this._valueEl.dom.title = RS.ARIA_AmountAsCivilizedTime;

        const sums = amounts.div("sums");
        let sum = sums.div("as-hours");
        sum.span("", "Hours ");
        this._sumHrsEl = sum.span("hrs", " ");
        this._sumHrsEl.dom.ariaLabel = RS.ARIA_AmountAsHours;
        this._sumHrsEl.dom.title = RS.ARIA_AmountAsHours;


        sum = sums.div("as-usd");
        sum.span("", "USD$ ");
        this._sumUsdEl = sum.span("usd", " ");
        this._sumUsdEl.dom.ariaLabel = RS.ARIA_AmountAsUSD;
        this._sumUsdEl.dom.title = RS.ARIA_AmountAsUSD;


        this.updateSummary();

        this._valueEl.dom.addEventListener("change", () => { this.tryParseValue(); this.raiseValueChanged(); });
        this._valueEl.dom.addEventListener("keyup", () => { this.tryParseValue(); this.raiseValueChanged(); });

        this._sumHrsEl.dom.addEventListener("change", () => { this.tryParseHrs(); this.raiseValueChanged(); });
        this._sumHrsEl.dom.addEventListener("keyup", () => { this.tryParseHrs(); this.raiseValueChanged(); });

        this._sumUsdEl.dom.addEventListener("change", () => { this.tryParseUsd(); this.raiseValueChanged(); });
        this._sumUsdEl.dom.addEventListener("keyup", () => { this.tryParseUsd(); this.raiseValueChanged(); });

        this.isEditable = editable;
        if (editable && value === 0) {
            this._valueEl.dom.textContent = "";
        }
        this._feedbackLabel = this.div("feedback");
    }
    get isEditable() {
        return this._isEditable;
    }
    set isEditable(val) {
        this._isEditable = val;
        if (val) {
            this.dom.classList.add("editable");
            this._valueEl.dom.contentEditable = true;
            this._sumHrsEl.dom.contentEditable = true;
            this._sumUsdEl.dom.contentEditable = true;
            this._valueEl.dom.inputMode = "decimal";
            this._sumHrsEl.dom.inputMode = "decimal";
            this._sumUsdEl.dom.inputMode = "decimal";

        } else {
            this.dom.classList.remove("editable");
            this._valueEl.dom.contentEditable = false;
            this._sumHrsEl.dom.contentEditable = false;
            this._sumUsdEl.dom.contentEditable = false;
        }
    }
    get value() {
        return this._value;
    }
    set value(val) {
        this._value = val;
        this._valueEl.dom.textContent = this._isEditable && val === 0 ? " " : formatDecimals(val);
        this.updateSummary();
    }
    updateSummary() {
        this._sumHrsEl.dom.textContent = this._value === 0 ? " " : formatDecimals(this._value / 3600);
        this._sumUsdEl.dom.textContent = this._value === 0 ? " " : formatDecimals(this._value / 3600 * 50);
        this._sumHrsEl.dom.classList.remove("error");
        this._sumUsdEl.dom.classList.remove("error");
    }
    get error() {
        return this._valueEl.dom.classList.contains("error") ? this._feedbackLabel.dom.textContent : "";
    }
    set error(value) {
        if (value === null || value === "") {
            this.dom.classList.remove("error");
            this._feedbackLabel.dom.textContent = "";
        } else {
            this._valueEl.dom.classList.remove("looks-good");
            this._feedbackLabel.dom.textContent = value;
            this.dom.classList.add("error")
        }
    }
    get hasError() {
        return this.dom.classList.contains("error");
    }
    get friendlyDescription() {
        return RS.PHRASE_BLANKTimeOrBLANKHoursOrBLANKUSD.replace("{0}", formatDecimals(this._value))
            .replace("{1}", this._sumHrsEl.dom.textContent)
            .replace("{2}", this._sumUsdEl.dom.textContent);
    }
    tryParseHrs() {
        if (!this._isEditable) {
            return;
        }
        let numbers = this._sumHrsEl.dom.textContent.trim();
        let val = parseFloat(numbers);
        if (isNaN(val)) {
            this._sumHrsEl.dom.classList.add("error");
            return;
        }
        this._sumHrsEl.dom.classList.remove("error");
        this._value = parseInt(val * 3600);
        this._valueEl.dom.textContent = formatDecimals(this._value);
        this._sumUsdEl.dom.textContent = formatDecimals(val * 50);
        this._sumUsdEl.dom.classList.remove("error");
        this.error = null;
    }
    tryParseUsd() {
        if (!this._isEditable) {
            return;
        }
        let numbers = this._sumUsdEl.dom.textContent.trim();
        let val = parseFloat(numbers);
        if (isNaN(val)) {
            this._sumUsdEl.dom.classList.add("error");
            return;
        }
        this._sumUsdEl.dom.classList.remove("error");
        this._value = parseInt(val / 50 * 3600);
        this._valueEl.dom.textContent = formatDecimals(this._value);
        this._sumHrsEl.dom.textContent = formatDecimals(val / 50);
        this._sumHrsEl.dom.classList.remove("error");
        this.error = null;
    }
    tryParseValue() {
        if (!this._isEditable) {
            return;
        }
        let numbers = this._valueEl.dom.textContent.replace(/[^\d]/gi, "");
        let val = parseInt(numbers, 10);
        if (isNaN(val)) {
            this._valueEl.dom.classList.add("error");
            return;
        }
        this.error = null;
        let sel = window.getSelection();
        let idx = sel.focusOffset;
        let preFormatted = this._valueEl.dom.textContent;
        this.value = val;
        let postFormatted = sel.focusNode.textContent;

        switch (postFormatted.length - preFormatted.length) {
            case 1: {
                // comma inserted
                idx++;
            } break;
            case -1: {
                // comma removed
                idx--;
            } break;
            case 0: {
                // no change
            } break;
            default: {
                // pasted
                idx = postFormatted.length;
            } break;
        }

        let r = document.createRange();
        r.setStart(this._valueEl.dom.firstChild, Math.max(0, idx));
        r.collapse(true);
        sel.removeAllRanges();
        sel.addRange(r);

    }
    onValueChanged(delegate) {
        this._onValueChanged = delegate;
    }
    raiseValueChanged() {
        if (this._onValueChanged) {
            this._onValueChanged(this);
        }
    }
}

class SignaturePanel extends Element {
    constructor() {
        super("div", "signature-panel");
        this.div("label", "Signature");
        const signatureBox = new Element("canvas");
        signatureBox.dom.style.touchAction = "none";
        signatureBox.dom.style.width = "300px";
        signatureBox.dom.style.height = "80px";

        const row = this.div("panel");
        row.add(signatureBox);

        this.onHasSignature = null;
        this.onNoSignature = null;
        this.isEmpty = true;

        row.button("x", () => {
            this.clear();
        });

        this.dc = signatureBox.dom.getContext("2d");

        let points = [];
        let isDrawing = false;

        const getRelativePoint = (e) => {
            const clientRect = signatureBox.dom.getBoundingClientRect();
            const clientX = e.clientX - clientRect.left;
            const clientY = e.clientY - clientRect.top;
            return { x: clientX, y: clientY };
        };
        const flush = () => {
            if (points.length < 6) {
                const p = points[0];
                this.dc.beginPath();
                this.dc.arc(p.x, p.y, this.dc.lineWidth / 2, 0, Math.PI * 2, true);
                this.dc.closePath();
                this.dc.fill();
                //points = [];
                return
            }
            this.dc.beginPath();
            this.dc.moveTo(points[0].x, points[0].y);
            for (i = 1; i < points.length - 2; i++) {
                var c = (points[i].x + points[i + 1].x) / 2,
                    d = (points[i].y + points[i + 1].y) / 2;
                this.dc.quadraticCurveTo(points[i].x, points[i].y, c, d);
            }
            this.dc.quadraticCurveTo(points[i].x, points[i].y, points[i + 1].x, points[i + 1].y);
            this.dc.stroke();
            //points = [];
            points = points.splice(points.length - 3, 3);
        };
        signatureBox.dom.addEventListener("pointerdown", (e) => {
            points.push(getRelativePoint(e));
            isDrawing = true;
            signatureBox.dom.setPointerCapture(e.pointerId);
        });
        signatureBox.dom.addEventListener("pointerup", (e) => {
            isDrawing = false;
            signatureBox.dom.releasePointerCapture(e.pointerId);
            if (points.length > 0) {
                flush();
            }
            points = [];
            this.isEmpty = false;
            if (this.onHasSignature !== null) {
                this.onHasSignature();
            }
        });
        signatureBox.dom.addEventListener("pointermove", (e) => {
            if (!isDrawing) {
                return;
            }
            points.push(getRelativePoint(e));
            flush();
        });
    }
    init() {

        this.clear();
    }
    clear() {
        this.isEmpty = true;
        this.dc.canvas.width = 300;
        this.dc.canvas.height = 80;
        //this.dc.clearRect(0, 0, this.dc.canvas.width, this.dc.canvas.height);
        this.dc.fillStyle = "#fff";
        this.dc.fillRect(0, 0, this.dc.canvas.width, this.dc.canvas.height);
        //
        //this.dc.fillStyle = "#000";
        //this.dc.fillRect(10, this.dc.canvas.height - 22, this.dc.canvas.width - 20, 2);

        this.dc.fillStyle = "#000";
        this.dc.strokeStyle = "#000";
        this.dc.lineWidth = 5;
        this.dc.lineCap = "round";
        this.dc.lineJoin = "round";
        if (this.onNoSignature !== null) {
            this.onNoSignature();
        }
    }
    toBase64() {
        return this.dc.canvas.toDataURL('image/webp', 70);
    }
}

class QRCodeAnimation extends Element {
    constructor(longData) {
        super("div", "qr-anim");
        const chunkSize = 100;
        this._inner = this.div();
        this._h2 = this.h2();
        this._slowDownButton = this.button("Slow down", (b) => {
            this._slow = !this._slow;
            b.dom.textContent = this._slow ? "Speed up" : "Slow down";
        });

        this.SVGs = [];
        let strings = [];
        for (let i = 0; i < longData.length; i += chunkSize) {
            strings.push(longData.substr(i, Math.min(chunkSize, longData.length - i)));
        }
        for (let i = 0; i < strings.length; i++) {
            strings[i] = (i + 1) + "/" + strings.length + ":" + strings[i];
        }
        for (let i = 0; i < strings.length; i++) {
            var qrcode = new QRCode({
                content: strings[i],
                container: "svg-viewbox",
                color: "#000000",
                background: "#ffffff",
                ecl: "M",
                join: true
            });
            this.SVGs.push(qrcode.svg());
        }
        if (strings.length === 1) {
            this._slowDownButton.remove();
        }
        this._slow = false;
        this.start();
    }
    start() {
        if (this.SVGs.length === 1) {
            this._inner.dom.innerHTML = this.SVGs[0];
            this._h2.remove();
        } else {
            let cursor = 0;
            this._countDown = this._slow ? 5 : 2;
            this._interval = setInterval(() => {
                this._h2.dom.textContent = (cursor + 1) + "/" + this.SVGs.length;//+ " ( " + this._countDown + (this._slow ? " slower" : "") + "... )"
                this._inner.dom.innerHTML = this.SVGs[cursor];
                this._countDown--;
                if (this._countDown === 0) {
                    cursor++;
                    if (cursor === this.SVGs.length) {
                        cursor = 0;
                    }
                    this._countDown = this._slow ? 5 : 2;
                }
            }, 100);
        }
    }
}

class QRScanDialog extends Element {
    constructor(title, onResult) {
        super("div", "scan-dialog qr");
        this._inner = this.div();
        const header = this._inner.h2(null, title);
        this._video = new Element("video");

        this._region = this._inner.div("vid-region");
        this._region.add(this._video);

        const buttons = this._inner.div("buttons");
        const retry = buttons.button(RS.LABEL_Retry, () => {
            header.dom.classList.remove("error");
            header.dom.textContent = title;
            retry.dom.style.display = "none";
            this._qrScanner.start();
        }, true, RS.LABEL_Retry);
        retry.dom.style.display = "none";
        buttons.button(RS.LABEL_Cancel, () => {
            this._qrScanner.destroy();
            this.remove();
        });
        this._parts = [];
        this._qrScanner = new QrScanner(this._video.dom, result => {
            const multipart = result.match(/^(\d+)\/(\d+):/);
            if (multipart) {
                const thisPart = parseInt(multipart[1]);
                const totalParts = parseInt(multipart[2]);
                if (this._parts.length !== totalParts) {
                    this._parts = Array(totalParts).fill(null);
                }

                this._parts[thisPart - 1] = result.substr(result.indexOf(':') + 1)
                let s = "";

                let got = 0;
                for (let i = 0; i < this._parts.length; i++) {
                    if (this._parts[i] !== null) {
                        s += this._parts[i];
                        got++;
                    }
                }
                if (got !== this._parts.length) {
                    header.dom.textContent = title + " (got " + got + " of " + this._parts.length + ")";
                    return;
                }
                if (s.startsWith("https://civil.money/")) {
                    onResult(s);
                    this._qrScanner.destroy();
                    this.remove();
                }
            } else {
                if (result.startsWith("https://civil.money/")) {
                    onResult(result);
                    this._qrScanner.destroy();
                    this.remove();
                }
            }
        }, error => {
            // header.dom.textContent = error;
            // header.dom.classList.add("error");
            // retry.dom.style.display = "block";
        });
        this._region.div().dom.appendChild(this._qrScanner.$canvas);
    }
    tryStart() {
        this._qrScanner.start();
    }
}


class PhotoDialog extends Element {
    constructor(title, onResult) {
        super("div", "scan-dialog");
        this._inner = this.div();
        const header = this._inner.h2(null, title);
        this._video = new Element("video");
        const vidRow = this._inner.div();
        vidRow.add(this._video);
        const photoRow = this._inner.div();
        const buttons = this._inner.div("buttons");
        let takeButton = null;
        let reTakeButton = null;
        let okButton = null;
        let base64 = null;
        photoRow.dom.style.display = "none";

        okButton = buttons.button("OK", () => {
            onResult(base64);
            this.stop();
            this.remove();
        }, true);
        okButton.dom.style.display = "none";

        reTakeButton = buttons.button("Re-take", () => {
            photoRow.dom.style.display = "none";
            vidRow.dom.style.display = "block";
            okButton.dom.style.display = "none";
            takeButton.dom.style.display = "block";
            reTakeButton.dom.style.display = "none";
        });
        reTakeButton.dom.style.display = "none";

        takeButton = buttons.button("Take", () => {
            const canvas = document.createElement("canvas"),
                context2D = canvas.getContext("2d");
            canvas.width = this._video.dom.videoWidth;
            canvas.height = this._video.dom.videoHeight;
            context2D.drawImage(this._video.dom, 0, 0, canvas.width, canvas.height);
            base64 = canvas.toDataURL('image/webp', 70);
            photoRow.dom.innerHTML = `<img src="${base64}"/>`;
            photoRow.dom.style.display = "block";
            vidRow.dom.style.display = "none";
            okButton.dom.style.display = "block";
            takeButton.dom.style.display = "none";
            reTakeButton.dom.style.display = "block";

        }, true);

        buttons.button("Cancel", () => {
            this.stop();
            this.remove();
        });

        navigator.mediaDevices.getUserMedia({
            // video: true
            video: {
                width: { min: 640, ideal: 1280, max: 1920 },
                height: { min: 480, ideal: 720, max: 1080 }
            }
        }).then((stream) => {
            this._video.dom.srcObject = stream;
            this._video.dom.play();
        }).catch((err) => {
            vidRow.dom.innerHTML = "Please enable access to your camera. " + err.name + " " + err.error;
        });

    }
    stop() {
        if (this._video.dom.srcObject) {
            this._video.dom.srcObject.getTracks().forEach(track => track.stop());
        }
    }
}


class VoiceDialog extends Element {
    constructor(title, onResult) {
        super("div", "scan-dialog");
        this._inner = this.div();
        const header = this._inner.h2(null);
        header.dom.innerHTML = title;

        const vidRow = this._inner.div();

        const buttons = this._inner.div("buttons");
        let okButton = null;
        let base64 = null;
        this._isRecording = false;

        okButton = buttons.button("OK", () => {
            onResult(base64);
            this.stop();
            this.remove();
        }, true);
        okButton.dom.style.display = "none";

        const processChunks = () => {
            if (this._recordedChunks.length === 0) {
                return;
            }
            const blob = new Blob(this._recordedChunks, {
                type: "audio/webm"
            });
            var reader = new FileReader();
            reader.onloadend = function () {
                base64 = reader.result;
                okButton.dom.style.display = "block";
                vidRow.dom.innerHTML = `<video controls><source src="${base64}" type="audio/webm" /></video>`;
            }
            reader.readAsDataURL(blob);
            this._recordedChunks = [];
        };

        buttons.button("Record", (b) => {
            if (this._isRecording) {
                this._isRecording = false;
                b.dom.textContent = "Re-Record";
                this._recorder.stop();
                b.dom.classList.remove("default");
            } else {
                this._isRecording = true;
                b.dom.textContent = "Stop";
                this._recordedChunks = [];
                this._recorder.start();
                okButton.dom.style.display = "none";
            }
        }, true);

        buttons.button("Cancel", () => {
            this.stop();
            this.remove();
        });
        this._recorder = null;
        this._stream = null;
        this._recordedChunks = [];
        navigator.mediaDevices.getUserMedia({
            audio: true
        }).then((stream) => {
            this._stream = stream;
            this._recorder = new MediaRecorder(stream, { mimeType: "audio/webm; codecs=opus" });
            this._recorder.ondataavailable = (e) => {
                if (e.data.size > 0) {
                    this._recordedChunks.push(e.data);
                    if (!this._isRecording) {
                        processChunks();
                    }
                }
            };
        }).catch((err) => {
            vidRow.dom.innerHTML = "Please enable access to your camera. " + err.name + " " + err.error;
        });

    }
    stop() {
        if (this._recorder.state === "recording") {
            this._recorder.stop();
        }
        if (this._stream) {
            this._stream.getTracks().forEach(track => track.stop());
        }
    }
}




function setPage(page) {
    if (currentPage !== null) {
        currentPage.remove();
    }
    closeAccountMenu();
    currentPage = page;
    ui.add(page);
}

function isCurrentPage(className) {
    if (currentPage === null) {
        return false;
    }
    return currentPage.dom.classList.contains(className);
}

function closeAccountMenu() {
    const menu = document.querySelector(".flyout-menu");
    if (menu) {
        menu.parentElement.removeChild(menu);
    }
}

async function openAccountMenu() {
    const menu = new Element("div", "flyout-menu");

    let onRemove = null;
    let forceClose = false;
    onRemove = (e) => {
        if (!forceClose && menu.dom.contains(e.target)) {
            return;
        }
        menu.remove();
        document.body.removeEventListener("click", onRemove);
    };

    menu.div("close").a(RS.ARIA_ExitMenu, (e) => { forceClose = true; onRemove(e); });

    const myAccounts = await getMyAccounts();

    if (myAccounts.length > 0) {
        const currentAccount = myAccounts[0];
        if (currentAccount.label) {
            menu.h2(null, currentAccount.label);
            menu.h3(null, formatAccountNumber(currentAccount.branch, currentAccount.number));
        } else {
            menu.h2(null, formatAccountNumber(currentAccount.branch, currentAccount.number));
        }
        let isCurrent = isCurrentPage("account-summary");
        menu.div((isCurrent ? "current" : "") + " summary").a(RS.LABEL_Summary, showAccountSummary);
        isCurrent = isCurrentPage("account-ledger");
        menu.div((isCurrent ? "current" : "") + " ledger").a(RS.LABEL_Ledger, () => showAccountLedger(null));
        isCurrent = isCurrentPage("account-rename");
        menu.div((isCurrent ? "current" : "") + " relabel").a(RS.LABEL_NicknameAccount, showRenameAccount);
        isCurrent = isCurrentPage("account-remove");
        menu.div((isCurrent ? "current" : "") + " remove").a(RS.LABEL_RemoveAccount, showRemoveAccount);
        menu.h2(null, "Other accounts");
    }

    for (let i = 1; i < myAccounts.length; i++) {
        const acc = myAccounts[i];
        const a = menu.div("other-account").a("", setCurrentAccount);
        a.account = acc;
        if (acc.label) {
            a.dom.innerHTML = HTMLEncode(acc.label) + "<br/><small>" + formatAccountNumber(acc.branch, acc.number) + "</small>";
        } else {
            a.dom.textContent = formatAccountNumber(acc.branch, acc.number);
        }
    }

    menu.div("add").a(RS.LABEL_AddAccount, showAddAccount);
    menu.div("help").a(RS.LABEL_Help, () => {
        window.location = "https://civil.money";
    });

    document.body.appendChild(menu.dom);

    setTimeout(() => {
        document.body.addEventListener("click", onRemove);
    }, 100);

}

async function setCurrentAccount(srcLinkElementOrLocalAccount) {
    const acc = srcLinkElementOrLocalAccount.account || srcLinkElementOrLocalAccount;
    let ar = await getMyAccounts();
    for (let i = 0; i < ar.length; i++) {
        if (ar[i].branch === acc.branch && ar[i].number === acc.number) {
            ar[i].lastUsed = new Date().getTime();
            break;
        }
    }
    await LEDGER.put("my-account-numbers", ar);

    showAccountSummary();
}

async function getMyAccounts() {
    let myAccounts = (await LEDGER.get("my-account-numbers")) || [];
    // Basic sanity check incase someone's fiddled with the cache.
    myAccounts = myAccounts.filter(x => x.branch !== null && x.number !== null);
    myAccounts.sort((a, b) => {
        return a.lastUsed === b.lastUsed ? 0
            : a.lastUsed > b.lastUsed ? -1
                : 1;
    });
    return myAccounts;
}

function showProofOfTimePage(currentAccount, transactionUrl, signature) {

    transactionUrl.fromBranch = currentAccount.branch;
    transactionUrl.fromNumber = currentAccount.number;
    transactionUrl.proofOfTime = signature;

    const page = new Element("div", "proof-of-time");
    const nav = new TopNav(true);
    nav.dom.classList.add("headless");
    nav.onButton = () => {
        showPaymentPage(currentAccount, transactionUrl);
    };
    //nav.title = RS.LABEL_SendAPayment;
    const thisAccountNumberFormatted = formatAccountNumber(currentAccount.branch, currentAccount.number);
    nav.intro = thisAccountNumberFormatted;
    page.add(nav);

    const content = page.div("content");

    content.div("proof", RS.LABEL_ProofOfTime);

    content.add(new QRCodeAnimation(`https://civil.money/${transactionUrl.toCsvString()}`));
    const errorFeedback = content.div("error");

    content.div("buttons pay-amount").button(RS.LABEL_Done, async () => {

        const error = await tryCommitTransactionUrlToLedger(transactionUrl);
        if (!error) {
            showAccountSummary();
        } else {
            errorFeedback.dom.textContent = error;
        }

    }, true, RS.ARIA_CommitThisPaymentToYourLedger);

    content.div("buttons share").button(RS.LABEL_ExportToAPaymentFile, () => {

        const csv = transactionUrl.toCsvString();
        const id = "CivilMoney " + RS.LABEL_Payment + " " + transactionUrl.uid();
        var bl = null;
        try {
            bl = new File([csv], id + ".txt", {
                type: "text/plain",
            });
        } catch (err) {
            bl = new Blob([csv], {
                type: "text/plain",
            });
        }
        // 
        // if ("share" in navigator && navigator.canShare && navigator.canShare({ files: [bl] })) {
        //     navigator.share({
        //         files: [bl],
        //         title: 'CivilMoney Payment',
        //         text: id
        //     }).catch((e) => {
        //         var a = document.createElement("a");
        //         a.href = URL.createObjectURL(bl);
        //         a.download = id + ".csv";
        //         a.click();
        //     });
        // } else {
        var a = document.createElement("a");
        a.href = URL.createObjectURL(bl);
        a.download = id + ".txt";
        a.click();
        // }
    }, true, RS.ARIA_SendThisPaymentAsAnEmailOrSomething);
    setPage(page);
}

function showPaymentPage(currentAccount, transactionUrl) {
    const page = new Element("div", "receive-payment");
    const nav = new TopNav(true);
    nav.dom.classList.add("headless");
    nav.onButton = showAccountSummary;
    nav.title = RS.LABEL_SendAPayment;
    const thisAccountNumberFormatted = formatAccountNumber(currentAccount.branch, currentAccount.number);
    nav.intro = thisAccountNumberFormatted;
    page.add(nav);

    const content = page.div("content");

    content.div("to", RS.LABEL_To + ": " + formatAccountNumber(transactionUrl.toBranch, transactionUrl.toNumber));
    const isUnrecognised = true;
    if (isUnrecognised) {
        content.div("unrecognized-hint", RS.PHRASE_ThisPayeeIsNotInYourLedger);
    }
    const amount = new AmountElement(transactionUrl.amount);
    content.add(amount);
    content.div("date", formatUTCDate(transactionUrl.yyyymmddhhmm) + " UTC");
    const unspsc = content.div("unspsc");
    unspsc.dom.innerHTML = "UNSPSC(s)<br/>" + formatFriendlyUNSPSCs(transactionUrl.unspscItems);
    unspsc.dom.title = RS.LABEL_UNSPSCFull;
    unspsc.dom.ariaLabel = RS.LABEL_UNSPSCFull;
    if (transactionUrl.memo !== null && transactionUrl.memo !== undefined) {
        content.div("memo", transactionUrl.memo);
    }


    const sig = new SignaturePanel();
    content.add(sig);

    const payButton = content.div("buttons pay-amount").button(null, () => {
        showProofOfTimePage(currentAccount, transactionUrl, sig.toBase64());
    }, true, RS.LABEL_Pay + " " + amount.friendlyDescription);
    payButton.span("pay", RS.LABEL_Pay);
    payButton.span("symbol", "//c");
    payButton.span("value", formatDecimals(transactionUrl.amount));
    payButton.dom.setAttribute("disabled", "");
    sig.onHasSignature = () => {
        payButton.dom.removeAttribute("disabled");
    };
    sig.onNoSignature = () => {
        payButton.dom.setAttribute("disabled", "");
    };
    setPage(page);
    sig.init();
}
function makeAccountUrl(account, path) {
    return "/" + account.branch + "," + account.number + path;
}
async function showAccountSummary() {
    let myAccounts = await getMyAccounts();
    if (myAccounts.length === 0) {
        // nothing to show
        showAddAccount();
        return;
    }

    const currentAccount = myAccounts[0];


    const page = new Element("div", "account-summary");
    const nav = new TopNav(false);
    nav.onButton = openAccountMenu;
    nav.title = currentAccount.label || RS.LABEL_MyAccount;
    pushAppPath(makeAccountUrl(currentAccount, "/"), nav.title + " | " + RS.LABEL_Summary);

    const thisAccountNumberFormatted = formatAccountNumber(currentAccount.branch, currentAccount.number);
    nav.intro = thisAccountNumberFormatted;
    page.add(nav);
    const content = page.div("content");
    const sum = new LedgerAccountSummary(currentAccount.branch, currentAccount.number);
    await sum.updateCache();
    await sum.loadDetails();
    const balance = UNIVERSAL_BASIC_INCOME_2YR + sum.credits - sum.debits;
    content.add(new AmountElement(balance));


    let payHint = null;
    content.div("pay").button(null, () => {
        payHint.dom.classList.remove("error");
        payHint.dom.innerHTML = RS.PHRASE_TapToPaySomeone;
        const scan = new QRScanDialog(RS.PHRASE_LookingForARecipientQRCode, (res) => {
            try {
                const t = new TransactionUrl(res);
                if ((t.fromBranch !== "*" && t.fromBranch !== currentAccount.branch)
                    || (t.fromNumber !== "*" && t.fromNumber !== currentAccount.number)) {
                    payHint.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${HTMLEncode(RS.PHRASE_TheBranchOrAccountNumberIsNotBLANK.replace("{0}", thisAccountNumberFormatted))}<br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                    payHint.dom.classList.add("error");
                } else if (t.toBranch === currentAccount.branch
                    && t.toNumber === currentAccount.number) {
                    payHint.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${RS.PHRASE_TheRecipientAndPayerCantBeTheSameAccount}<br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                    payHint.dom.classList.add("error");
                } else {
                    showPaymentPage(currentAccount, t);
                }

            } catch (err) {
                payHint.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                payHint.dom.classList.add("error");
            }
            console.log(res);
        });
        document.body.appendChild(scan.dom);
        scan.tryStart();
    }, true, RS.PHRASE_TapToPaySomeonesQR);

    payHint = content.div("pay-hint").h2(null, RS.PHRASE_TapToPaySomeone);
    content.div("buttons accept").button(RS.LABEL_AcceptAPayment, () => beginAcceptPayment(), true, RS.ARIA_AcceptAPayment);
    content.div("buttons ledger").button(RS.LABEL_Ledger, () => showAccountLedger(null), false, RS.ARIA_Ledger);
    content.div("buttons backup").button(RS.LABEL_BackupLedger, backupAccount, false, RS.ARIA_BackupLedger);
    content.div("buttons restore").button(RS.LABEL_RestoreLedger, restoreAccount, false, RS.ARIA_RestoreLedger);

    // content.div("buttons").button("Test", () => {
    //     const dlg = new VoiceDialog("Say something", (base64) => {
    //         console.log(base64);
    //     })
    //     document.body.appendChild(dlg.dom);
    // }, false, "Review debits and credits.");

    setPage(page);


}

async function backupAccount() {
    let myAccounts = await getMyAccounts();
    if (myAccounts.length === 0) {
        // nothing to show
        showAddAccount();
        return;
    }
    const currentAccount = myAccounts[0];
    const file = new LedgerCSVFile(currentAccount.memorableSentence, currentAccount.branch, currentAccount.number, currentAccount.label);
    let res = await LEDGER.list({ prefix: `CC:${currentAccount.branch}:A:${currentAccount.number}:` });
    let seen = {};
    while (1) {
        for (let i = 0; i < res.keys.length; i++) {
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
            seen[line] = "";
            const t = new TransactionUrl(tx.csvLine());
            const proof = await LEDGER.getWithMetadata(t.uid() + ":proof");
            if (proof.metadata && proof.metadata.memo) {
                t.memo = proof.metadata.memo;
            }
            if (proof.value) {
                t.proofOfTime = proof.value;
            }
            if (item.metadata.taxRevenueStatus !== TaxRevenueStatus.NotSubmitted) {
                t.isSubmitted = true;
            }
            file.appendTransaction(t);
        }

        if (res.list_complete) {
            break;
        } else {
            res = await LEDGER.list({ "cursor": res.cursor });
        }
    }

    const bl = file.toCsvBlob();
    var a = document.createElement("a");
    a.href = URL.createObjectURL(bl);
    a.download = file.fileName();
    a.click();
}

async function restoreAccount() {

    const f = document.createElement("input");
    f.type = "file";
    f.multiple = true;
    f.style.display = "none";
    f.onchange = async (e) => {
        var ar = e.currentTarget.files;
        if (ar.length === 0)
            return;
        const rawTransactions = [];
        let importLabel = null;
        let importPhrase = null;
        let importNumber = null;
        for (var i = 0; i < ar.length; i++) {
            const reader = new CSVReader(await new Response(ar[i]).text());
            let line = null;
            while ((line = reader.nextCsvLine()) !== null) {
                line[0] = line[0].trim();
                if (line[0].startsWith('#')
                    && line.length > 1
                    && line[1].trim().length > 0) {

                    line[0] = line[0].toLocaleLowerCase();
                    if (line[0].indexOf("name") > -1) {
                        importLabel = line[1].trim();
                    } else if (line[0].indexOf("phrase") > -1) {
                        importPhrase = line[1].trim();
                    } else if (line[0].indexOf("number") > -1) {
                        importNumber = line[1].trim();
                    }
                    continue;
                }
                // We're looking "(branch) number,...)
                if (!line[0].startsWith('(')) {
                    continue;
                }
                line = normaliseCsvArray(line);
                const t = new TransactionUrl();
                t.parseArrayOfValues(line);
                rawTransactions.push(t);
            }
        }
        showImportPayments(rawTransactions, importNumber, importLabel, importPhrase);
    };

    currentPage.dom.appendChild(f);
    f.click();
}

async function tryCommitTransactionUrlToLedger(transactionUrl) {
    const t = new LedgerTransaction(transactionUrl.toBranch, transactionUrl.toNumber,
        transactionUrl.fromBranch, transactionUrl.fromNumber,
        transactionUrl.yyyymmddhhmm, transactionUrl.unspscItems, transactionUrl.amount, false);
    if (!await t.isAdded(true)) {
        const taxRevenueStatus = transactionUrl.isSubmitted ? TaxRevenueStatus.Submitted : TaxRevenueStatus.NotSubmitted;
        if (await t.tryAdd("-", "-", taxRevenueStatus)
            && await t.tryAddProofOfTime(transactionUrl.proofOfTime, transactionUrl.memo)) {
            return null;
        } else {
            return t.error;
        }
    }
    return null;
}

function formatFriendlyUNSPSCs(unspscItems, total) {
    let s = "";
    for (let i = 0; i < unspscItems.length; i++) {
        const item = unspscItems[i];
        if (i > 0) {
            s += "<br/>";
        }
        s += `<b>${item.unspsc}</b> ${unspsc_data[item.unspsc].replace(/\|/g, " → ")}`;
        if (item.amount !== 0) {
            s += " //c " + formatDecimals(item.amount);
        }
    }
    return s;
}

async function getNickNameForAccount(branchNumber, number) {
    let ar = await getMyAccounts();
    ar = ar.filter((x) => { return x.branch === branchNumber && x.number === number; });
    if (ar.length === 0 || !ar[0].label) {
        return RS.LABEL_Branch + " " + branchNumber + " " + RS.LABEL_AccountNumber + " " + number;
    } else {
        return ar[0].label;
    }
}

function showCaptureProofDialog(transactionUrl) {
    const page = new Element("div", "receive-payment");
    const nav = new TopNav();
    nav.onButton = () => {
        beginReceivePayment(transactionUrl);
    };
    nav.title = RS.LABEL_AcceptAPayment;
    nav.intro = formatAccountNumber(transactionUrl.toBranch, transactionUrl.toNumber);
    page.add(nav);
    const content = page.div("content");
    content.add(new AmountElement(transactionUrl.amount));
    content.div("date", formatUTCDate(transactionUrl.yyyymmddhhmm) + " UTC");
    content.div("unspsc").dom.innerHTML = "UNSPSC(s) " + formatFriendlyUNSPSCs(transactionUrl.unspscItems);
    content.div("unspsc").dom.title = RS.LABEL_UNSPSCFull;
    content.div("unspsc").dom.ariaLabel = RS.LABEL_UNSPSCFull;

    if (transactionUrl.memo !== null && transactionUrl.memo !== undefined) {
        content.div("memo", transactionUrl.memo);
    }
    content.div().h2(null, RS.LABEL_Payer);
    content.div("payer", formatAccountNumber(transactionUrl.fromBranch, transactionUrl.fromNumber));
    content.div().h2(null, RS.LABEL_CaptureProofOfTime);
    const sig = new SignaturePanel();
    content.add(sig);
    const otherProof = content.div();
    const errorFeedback = content.div("error");
    transactionUrl.proofOfTime = null;

    const tryCommitTransaction = async () => {

        const error = await tryCommitTransactionUrlToLedger(transactionUrl);
        if (!error) {
            showAccountSummary();
        } else {
            errorFeedback.dom.textContent = error;
        }
    }

    content.div("buttons capture").button(RS.LABEL_TakeARelaventPhotoInstead, () => {

        const dlg = new PhotoDialog(
            RS.PHRASE_RelaventPhotoOfPayerOrResourceAccountNumberBLANK
                .replace("{0}", formatAccountNumber(transactionUrl.fromBranch, transactionUrl.fromNumber))
            , async (base64) => {
                transactionUrl.proofOfTime = base64;
                otherProof.dom.innerHTML = `<img src="${base64}"/>`;
                commit.dom.removeAttribute("disabled");
            })
        document.body.appendChild(dlg.dom);

    }, false, RS.LABEL_CaptureProofOfTime);
    content.div("buttons capture").button(RS.LABEL_TakeAVoiceRecording, async () => {
        const date = parseUTCDate(transactionUrl.yyyymmddhhmm);

        // This is <your name>, I'm paying {1} time to {2} on {3} at {4}.
        const statement = RS.PHRASE_PayerBLANKPleaseSayImPayingToBLANKOnBLANKAtBLANKBLANKTimeHTML
            .replace("{0}", formatAccountNumber(transactionUrl.fromBranch, transactionUrl.fromNumber))
            .replace("{1}", transactionUrl.amount)
            .replace("{2}", await getNickNameForAccount(transactionUrl.toBranch, transactionUrl.toNumber))
            .replace("{3}", date.toDateString())
            .replace("{4}", date.getHours().toString().padStart(2, '0') + ":" + date.getMinutes().toString().padStart(2, '0'));

        const dlg = new VoiceDialog(statement, async (base64) => {
            transactionUrl.proofOfTime = base64;
            otherProof.dom.innerHTML = `<video controls><source src="${base64}" type="audio/webm"/></video>`;
            commit.dom.removeAttribute("disabled");
        })
        document.body.appendChild(dlg.dom);
    }, false, RS.LABEL_CaptureProofOfTime);

    let commit = content.div("buttons capture").button(RS.LABEL_Finalize, async () => {
        if (transactionUrl.proofOfTime === null) {
            if (sig.isEmpty) {
                errorFeedback.dom.textContent = RS.PHRASE_AtLeastOneKindOfProofOfTimeIsRequired;
                return;
            }
            errorFeedback.dom.textContent = "";
            transactionUrl.proofOfTime = sig.toBase64();
        }
        await tryCommitTransaction();
    }, true, RS.ARIA_Finalize);
    commit.dom.setAttribute("disabled", "");

    sig.onHasSignature = () => {
        commit.dom.removeAttribute("disabled");
    };
    sig.onHasNoSignature = () => {
        commit.dom.setAttribute("disabled", "");
    };

    setPage(page);

    sig.init();
}

function beginReceivePayment(transactionUrl) {
    const page = new Element("div", "receive-payment");
    const nav = new TopNav();
    nav.onButton = () => {
        beginAcceptPayment(transactionUrl);
    };
    nav.title = RS.LABEL_AcceptAPayment;
    nav.intro = formatAccountNumber(transactionUrl.toBranch, transactionUrl.toNumber);
    page.add(nav);

    pushAppPath("/" + transactionUrl.toCsvString(), "//c " + transactionUrl.amount + " " + formatUTCDate(transactionUrl.yyyymmddhhmm));

    const content = page.div("content");
    content.add(new AmountElement(transactionUrl.amount));
    content.div("date", formatUTCDate(transactionUrl.yyyymmddhhmm) + " UTC");
    const unspsc = content.div("unspsc");
    unspsc.dom.innerHTML = "UNSPSC(s)<br/>" + formatFriendlyUNSPSCs(transactionUrl.unspscItems);
    unspsc.dom.title = RS.LABEL_UNSPSCFull;
    unspsc.dom.ariaLabel = RS.LABEL_UNSPSCFull;

    if (transactionUrl.memo !== null && transactionUrl.memo !== undefined) {
        content.div("memo", transactionUrl.memo);
    }

    const qr = new QRCodeAnimation(`https://civil.money/${transactionUrl.toCsvString()}`);
    content.add(qr);
    const hintRow = content.div("hint", RS.PHRASE_PayMeQRCodeHint);

    const feedback = content.div();
    let payer = null;
    let capture = null;
    let share = null;
    let payerFeedback = null;

    const scanAppRow = content.div("buttons scan-app");
    scanAppRow.button(RS.LABEL_ScanAPayersApp, (b) => {
        const scan = new QRScanDialog(RS.PHRASE_LookingForPayersQRSequence, (res) => {
            try {
                const t = new TransactionUrl(res);
                if (t.toBranch !== transactionUrl.toBranch
                    || t.toNumber !== transactionUrl.toNumber
                    // || t.yyyymmddhhmm !== transactionUrl.yyyymmddhhmm
                    || t.amount !== transactionUrl.amount
                ) {
                    feedback.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${RS.PHRASE_TransactionDetailsDontMatch}<br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                    feedback.dom.classList.add("error");
                } else if (t.proofOfTime === null
                    || t.proofOfTime === undefined
                    || t.proofOfTime.length === 0) {
                    feedback.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${RS.PHRASE_ProofOfTimeIsMissing}<br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                    feedback.dom.classList.add("error");
                } else if (t.fromBranch === "*"
                    || t.fromNumber === "*") {
                    feedback.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${RS.PHRASE_PayerAccountDetailsMissing}<br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                    feedback.dom.classList.add("error");
                } else if (t.fromBranch === t.toBranch
                    && t.fromNumber === t.toNumber) {
                    feedback.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${RS.PHRASE_TheRecipientAndPayerCantBeTheSameAccount}<br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                    feedback.dom.classList.add("error");
                } else {
                    payer.value = formatAccountNumber(t.fromBranch, t.fromNumber);
                    b.remove();
                    capture.remove();
                    share.remove();
                    qr.remove();
                    payerFeedback.remove();
                    hintRow.remove();
                    scanAppRow.remove();
                    if (t.proofOfTime.startsWith("data:image/")) {
                        content.div().dom.innerHTML = `<img src="${t.proofOfTime}"/>`;
                    } else if (t.proofOfTime.startsWith("data:audio/")) {
                        content.div().dom.innerHTML = `<video controls><source src="${t.proofOfTime}" type="audio/webm" /></video>`;
                    } else {
                        content.div().dom.textContent = t.proofOfTime;
                    }
                    content.div("buttons").button(RS.LABEL_Finalize, async () => {
                        const error = await tryCommitTransactionUrlToLedger(t);
                        if (!error) {
                            showAccountSummary();
                        } else {
                            feedback.dom.textContent = error;
                            feedback.dom.classList.add("error");
                        }
                    }, true, RS.ARIA_Finalize);
                }

            } catch (err) {
                feedback.dom.innerHTML = `<b>${HTMLEncode(RS.PHRASE_InvalidQREncountered)} :(</b><br/>${err}<br/>${HTMLEncode(RS.LABEL_DataReceived)}: ${HTMLEncode(res)}`;
                feedback.dom.classList.add("error");
            }

        });

        document.body.appendChild(scan.dom);
        scan.tryStart();

    }, true, RS.LABEL_ScanAPayersApp);

    const preFillFrom = transactionUrl.fromBranch && transactionUrl.fromBranch !== "*"
        && transactionUrl.fromNumber && transactionUrl.fromNumber !== "*" ? formatAccountNumber(transactionUrl.fromBranch, transactionUrl.fromNumber)
        : "";
    payer = content.div().field(FieldType.Text, RS.LABEL_Payer, preFillFrom, null, EMPTY_ACCOUNT_NUMBER_EXAMPLE);

    payerFeedback = content.div();

    capture = content.div("buttons capture").button(RS.LABEL_CaptureProofOfTime, () => {
        let isPayerValid = false;
        let test = null;
        try {
            // Let TransactionUrl validate branch/number for us
            // (it will go into the toXXX fields with a resolved account url type.)
            test = new TransactionUrl(payer.value);
            isPayerValid = (test.type === "account");
        } catch (e) { }
        if (!isPayerValid) {
            payerFeedback.dom.innerHTML = `<b>${RS.PHRASE_AnInvalidPayerAccountNumberEntered}</b><br/>${RS.PHRASE_AccountNumberExampleHTML}`;
            payerFeedback.dom.classList.add("error");
            return;
        }
        transactionUrl.fromBranch = test.toBranch;
        transactionUrl.fromNumber = test.toNumber;
        showCaptureProofDialog(transactionUrl);

    }, false, RS.LABEL_CaptureProofOfTime);

    let linkInfo = null;

    share = content.div("buttons share").button(RS.LABEL_SharePaymentLink, () => {
        linkInfo.dom.textContent = RS.LABEL_CivilMoneyPaymentLink + ": https://civil.money/" + transactionUrl.toCsvString();
        if ("share" in navigator) {
            navigator.share({
                title: RS.LABEL_CivilMoneyPaymentLink,
                url: "https://civil.money/" + transactionUrl.toCsvString()
            });
        }
    }, false, RS.LABEL_SharePaymentLink);

    linkInfo = content.div("buttons");
    setPage(page);
}

async function beginAcceptPayment(editTransactionUrlOrNull = null) {
    let myAccounts = await getMyAccounts();
    if (myAccounts.length === 0) {
        // nothing to show
        showAddAccount();
        return;
    }

    if (editTransactionUrlOrNull === undefined) {
        editTransactionUrlOrNull = null;
    }

    const currentAccount = myAccounts[0];



    const page = new Element("div", "accept-payment");
    const nav = new TopNav();
    nav.onButton = showAccountSummary;
    nav.title = (currentAccount.label ? currentAccount.label + " | " : "") + RS.LABEL_AcceptAPayment;

    pushAppPath(makeAccountUrl(currentAccount, "/accept"), nav.title);

    nav.intro = formatAccountNumber(currentAccount.branch, currentAccount.number);
    page.add(nav);

    const content = page.div("content");
    const itemsContainer = content.div();
    let items = (editTransactionUrlOrNull !== null ? editTransactionUrlOrNull.unspscItems : []);
    if (items.length === 0) {
        items.push({ unspsc: "", amount: 0 });
    }


    const onUnspscValueChanged = (sender) => {
        let unspscResults = sender.unspscResults;
        const vals = sender.value.trim().toLowerCase().split(' ').filter(x => x.trim().length > 0);
        const getResult = (keyString, value) => {
            const res = { "score": 0, "key": keyString, "value": value };
            value = value.toLowerCase();
            for (let i = 0; i < vals.length; i++) {
                const term = vals[i];
                const idx = value.indexOf(term);
                if (term === keyString) {
                    res.score += 1000;
                } else if (idx > -1) {
                    res.score += value.length - idx; // nearer first
                } else {
                    res.hit = false;
                    return res;
                }
            }
            res.hit = res.score > 0;
            return res;
        }
        const results = [];
        Object.keys(unspsc_data).forEach((k) => {
            let res = getResult(k.toString(), unspsc_data[k]);
            if (res.hit) {
                results.push(res);
            }
        })
        results.sort((a, b) => { return a.score > b.score; });
        unspscResults.dom.innerHTML = "";
        if (results.length === 0) {
            unspscResults.dom.classList.remove("popup");
            unspscResults.dom.innerHTML = sender.value.trim().length === 0 ? "" : RS.LABEL_NoUNSPSCSuggestions;
        } else {
            const onUnspcResultClick = (event) => {
                sender.value = event.target.id;
                sender.error = null;
                unspscResults.dom.innerHTML = event.target.textContent;
                unspscResults.dom.classList.remove("popup");
            };
            unspscResults.dom.classList.add("popup");
            unspscResults.div("close").button("X", () => {
                unspscResults.dom.innerHTML = "";
                unspscResults.dom.classList.remove("popup");
                
            }, true, RS.ARIA_ExitMenu);
            for (let i = 0; i < 20 && i < results.length; i++) {
                const res = results[i];
                let a = new Element("a", null, res.key + ": " + res.value.replace(/\|/g, " → "));
                a.dom.href = "javascript:;";
                a.dom.id = res.key;
                a.dom.addEventListener("click", onUnspcResultClick);
                a.dom.addEventListener("mouseup", onUnspcResultClick);
                unspscResults.div().add(a);
            }
        }

    };

    const validateAndSumAmounts = () => {
        const rows = content.dom.querySelectorAll('.line-item');
        const totals = {
            isValid: false,
            total: 0,
            items: []
        };
        for (let i = 0; i < rows.length; i++) {
            const unspsc = rows[i].unspsc;
            const amount = rows[i].amount;
            unspsc.error = null;
            amount.error = null;
            if (!(unspsc.value in unspsc_data)) {
                unspsc.error = RS.LABEL_InvalidUNSPSC;
            }
            const amountValue = parseInt(amount.value);
            if (isNaN(amountValue) || amountValue < 1) {
                amount.error = RS.LABEL_AmountMustBeGreaterThanZero;
            }
            if (unspsc.hasError || amount.hasError) {
                totals.isValid = false;
                return totals;
            }
            totals.items.push({ unspsc: unspsc.value, amount: amountValue });
            totals.total += amountValue;
            totals.isValid = true;
        }

        return totals;
    };

    const addLineItem = (item) => {
        const itemRow = itemsContainer.div("line-item");
        const unspcCell = itemRow.div("unspsc");
        itemRow.dom.unspsc = unspcCell.field(FieldType.Text, RS.LABEL_UNSPSCShort, item.unspsc);
        itemRow.dom.unspsc.unspscResults = unspcCell.div("unspsc-results");
        itemRow.dom.unspsc.onValueChanged((sender) => {
            sender.error = null;
            onUnspscValueChanged(sender);
        });
        

        const amounts = itemRow.div("item-amount");

        itemRow.dom.amount = new AmountElement(item.amount, true);
        itemRow.dom.amount.dom.prepend(new Element("label", null, RS.LABEL_Amount).dom);
        itemRow.dom.amount.onValueChanged((sender) => {
            sender.error = null;
        });
        amounts.add(itemRow.dom.amount);
        itemRow.div("remove").button("X", () => {
            itemRow.remove();
        }, false, RS.LABEL_RemoveItem)
    }

    for (let i = 0; i < items.length; i++) {
        addLineItem(items[i]);
    }

    content.div("add-item").button("+ Item", () => {
        addLineItem({ unspsc: "", amount: 0 });
    });



    const memo = content.div().field(FieldType.Text, RS.LABEL_Memo, (editTransactionUrlOrNull !== null ? editTransactionUrlOrNull.memo : null));


    content.div("buttons").button(RS.LABEL_Continue, () => {
        const totals = validateAndSumAmounts();
        if (!totals.isValid) {
            return;
        }
        const t = new TransactionUrl(
            currentAccount.branch
            + "," + currentAccount.number
            + ",*,*"
            + "," + dateToUtcYYYYMMDDHHMM(new Date())
            + "," + unspscItemsToString(totals.items)
            + "," + totals.total
            + ",\"" + memo.value + "\"");

        beginReceivePayment(t);

    }, true, RS.LABEL_Continue);

    content.div("buttons share").button(RS.LABEL_ImportSomebodysPayment, () => {
        const f = document.createElement("input");
        f.type = "file";
        f.multiple = true;
        f.style.display = "none";
        f.onchange = async (e) => {
            var ar = e.currentTarget.files;
            if (ar.length === 0)
                return;
            const rawTransactions = [];
            for (var i = 0; i < ar.length; i++) {
                const data = await new Response(ar[i]).text();
                rawTransactions.push(data);
            }
            showImportPayments(rawTransactions);
        };
        content.dom.appendChild(f);
        f.click();
    }, false, RS.ARIA_ImportSomebodysPayment);

    setPage(page);
}

async function showImportPayments(stringOrTransactionUrlsArray, importNumber, importAccountLabel, importPhrase) {
    let currentAccount = null;
    let myAccounts = await getMyAccounts();
    let isAccountNew = false;
    if (importNumber) {
        let t = new TransactionUrl(importNumber);
        if (t.type !== "account") {
            if (myAccounts.length === 0) {
                // nothing to show
                alert(RS.PHRASE_TheImportedAccountNumberIsInvalid);
                showAddAccount();
                return;
            }
        }
        // does the account already exist
        currentAccount = myAccounts.find(x => x.branch === t.toBranch && x.number === t.toNumber);
        if (!currentAccount) {
            currentAccount = new LocalAccount();
            currentAccount.branch = t.toBranch;
            currentAccount.number = t.toNumber;
            currentAccount.label = importAccountLabel;
            currentAccount.memorableSentence = importPhrase;
            currentAccount.lastUsed = new Date().getTime();
            isAccountNew = true;
        }
    } else {

        if (myAccounts.length === 0) {
            // nothing to show
            alert(RS.PHRASE_TheImportedAccountNumberIsInvalid);
            showAddAccount();
            return;
        }

        currentAccount = myAccounts[0];
    }



    const page = new Element("div", "account-import");
    const nav = new TopNav(false);
    nav.onButton = openAccountMenu;
    nav.title = (currentAccount.label ? currentAccount.label + " | " : "") + RS.LABEL_Import;
    pushAppPath(makeAccountUrl(currentAccount, "/import"), nav.title);

    nav.intro = RS.PHRASE_ImportTransactions;
    page.add(nav);
    const content = page.div("content");
    if (importAccountLabel) {
        content.h2("", importAccountLabel);
    }
    const transactions = [];
    const summary = content.div("hint");
    const items = content.div("table");

    for (let i = 0; i < stringOrTransactionUrlsArray.length; i++) {
        let tx = null;
        if (typeof stringOrTransactionUrlsArray[i] === 'string') {
            tx = new TransactionUrl(stringOrTransactionUrlsArray[i]);
        } else {
            tx = stringOrTransactionUrlsArray[i];
        }

        const row = items.div("row expanded");
        if (tx.type === "new-transaction") {
            const isCredit = tx.toBranch === currentAccount.branch && tx.toNumber === currentAccount.number;
            const isDebit = tx.fromBranch === currentAccount.branch && tx.fromNumber === currentAccount.number;
            const otherAccount = isDebit ?
                formatAccountNumber(tx.toBranch, tx.toNumber) : formatAccountNumber(tx.fromBranch, tx.fromNumber);

            row.dom.setAttribute("uid", tx.uid());
            let div = row.div();
            div.span("date", formatUTCDate(tx.yyyymmddhhmm));
            if (isCredit || isDebit) {
                div.span("account", otherAccount);
                div.span("amount", (isDebit ? "-" : "+") + formatDecimals(tx.amount));
            } else {
                // Unrelated transaction, but we can import it anyway.
                div.span("account", formatAccountNumber(tx.fromBranch, tx.fromNumber) + " -> " + formatAccountNumber(tx.toBranch, tx.toNumber));
                div.span("amount", formatDecimals(tx.amount));
                row.div("error", RS.PHRASE_UnrecognisedTransactionData);
            }


            proofStringToElement(row.div("proof").dom, tx.proofOfTime);
            row.div("unspsc").dom.innerHTML = formatFriendlyUNSPSCs(tx.unspscItems, tx.amount);
            row.div("memo", tx.memo);

            const existing = await LEDGER.getWithMetadata(tx.uid());
            const existingProof = await LEDGER.getWithMetadata(tx.uid() + ":proof");
            if (!existing.value || !existingProof.value) {
                transactions.push(tx);
            } else if (!isAccountNew) {
                row.div("error", RS.LABEL_AlreadyImported);
            }

        } else {

            row.div("error", RS.PHRASE_InvalidTransactionData + ": " + rawTransactionsArray[i]);
        }
    }
    if (!isAccountNew) {
        summary.dom.textContent = RS.PHRASE_BLANKTransactionsCanBeImported.replace("{0}", formatDecimals(transactions.length));
    }
    const feedback = content.div("error");

    if (transactions.length > 0 || isAccountNew) {
        content.div("buttons").button(RS.LABEL_Continue, async () => {
            for (let i = 0; i < transactions.length; i++) {
                const error = await tryCommitTransactionUrlToLedger(transactions[i]);
                if (error) {
                    feedback.dom.textContent = error;
                    return;
                }
            }

            let ar = await getMyAccounts();
            let exists = false;
            for (let i = 0; i < ar.length; i++) {
                if (ar[i].branch === currentAccount.branch && ar[i].number === currentAccount.number) {
                    if (importAccountLabel) {
                        ar[i].label = importAccountLabel;
                    }
                    exists = true;
                }
            }
            if (!exists) {
                ar.push(currentAccount);
            }
            await LEDGER.put("my-account-numbers", ar);

            showAccountSummary();

        }, true, RS.LABEL_Continue);
    }

    setPage(page);
}
function proofStringToElement(containerElement, proofString) {

    if (proofString.startsWith("data:image/")) {
        const img = document.createElement("img");
        img.src = proofString;
        containerElement.appendChild(img);
    } else if (proofString.startsWith("data:audio/")
        || proofString.startsWith("data:video/")) {
        const video = document.createElement("video");
        video.classList.add(proofString.startsWith("data:audio/") ? "audio" : "video");
        const src = document.createElement("source");
        src.src = proofString;
        video.appendChild(src);
        video.controls = true;
        containerElement.appendChild(video);
    }
}
async function showAccountLedger(errantItemsToHighlight = null) {
    let myAccounts = await getMyAccounts();
    if (myAccounts.length === 0) {
        // nothing to show
        showAddAccount();
        return;
    }

    const currentAccount = myAccounts[0];


    const page = new Element("div", "account-ledger");
    const nav = new TopNav(false);
    nav.onButton = openAccountMenu;
    nav.title = (currentAccount.label ? currentAccount.label + " | " : "") + RS.LABEL_Ledger;
    pushAppPath(makeAccountUrl(currentAccount, "/ledger"), nav.title);

    nav.intro = formatAccountNumber(currentAccount.branch, currentAccount.number);
    page.add(nav);
    const content = page.div("content");

    const sum = new LedgerAccountSummary(currentAccount.branch, currentAccount.number);
    await sum.updateCache();
    await sum.loadDetails();
    const balance = UNIVERSAL_BASIC_INCOME_2YR + sum.credits - sum.debits;
    content.add(new AmountElement(balance));
    const breakdown = content.div("sum-breakdown");
    let col = breakdown.div("earned");
    col.div("", RS.LABEL_Earned);
    col.add(new AmountElement(sum.credits));
    col = breakdown.div("spent");
    col.div("", RS.LABEL_Spent);
    col.add(new AmountElement(sum.debits));
    const unsubmittedInfo = content.div();
    content.div("hint").dom.innerHTML = RS.PHRASE_TimeSpentOnPositiveThingsAreConsideredValuableHTML;


    // Hopefully this GCs on page removal
    page.intersectionObserver = new IntersectionObserver(async (entries) => {
        for (let i = 0; i < entries.length; i++) {
            if (entries[i].intersectionRatio <= 0) {
                continue;
            }
            const el = entries[i].target;
            if (el.classList.contains("unloaded")) {
                el.classList.remove("unloaded");
                const proof = await LEDGER.getWithMetadata(el.getAttribute("uid") + ":proof");
                if (proof.value) {
                    el.classList.add("has-proof");
                    if (proof.metadata && proof.metadata.memo) {
                        el.querySelector('.memo').textContent = proof.metadata.memo;
                    }
                    const proofEl = el.querySelector('.proof');
                    proofStringToElement(proofEl, proof.value);
                }
            }
        }
    });
    const toggleProof = (e) => {
        e.target.classList.toggle("expanded");
    };

    let unsubmittedCount = 0;

    const onRemoveItemClick = async (e) => {
        const row = e.row;
        const tx = e.tx;
        if (confirm(RS.PHRASE_ConfirmPaymentDelete)) {
            row.remove();
            await tx.tryAdd("-", "-", TaxRevenueStatus.Submitted, true);
        }
    };

    const table = content.div("table");
    for (let i = 0; i < sum.transactions.length; i++) {
        const line = sum.transactions[i];
        const csv = line.split(',');
        const tx = new LedgerTransaction(csv[0], csv[1], csv[2], csv[3], csv[4], unspscItemsFromString(csv[5]), csv[6], false);
        if (!tx.isValid) {
            throw tx.error;
        }
        if (errantItemsToHighlight !== null
            && !(tx.uid() in errantItemsToHighlight)) {
            continue;
        }
        // csv[6] = empty memo
        // csv[7] = empty proof of time
        const isSubmitted = csv[8] === "Y";
        if (!isSubmitted) {
            unsubmittedCount++;
        }
        const isDebit = tx.fromBranch === currentAccount.branch && tx.fromNumber === currentAccount.number;
        const otherAccount = isDebit ?
            formatAccountNumber(tx.toBranch, tx.toNumber) : formatAccountNumber(tx.fromBranch, tx.fromNumber);
        let row = table.div("row unloaded");
        row.dom.setAttribute("uid", tx.uid());
        let div = row.div();
        div.span("date", formatUTCDate(tx.yyyymmddhhmm));
        div.span("account", otherAccount);
        div.span("amount", (isDebit ? "-" : "+") + formatDecimals(tx.amount));
       

        row.div("proof");
        const unspscItems = unspscItemsFromString(tx.unspsc);
        row.div("unspsc").dom.innerHTML = formatFriendlyUNSPSCs(unspscItems, tx.amount);
        row.div("memo");
        const del = row.div("delete").button(RS.LABEL_RemoveItem, onRemoveItemClick);
        del.tx = tx;
        del.row = row;

        page.intersectionObserver.observe(row.dom);
        row.dom.addEventListener("click", toggleProof);
        if (errantItemsToHighlight && (tx.uid() in errantItemsToHighlight)) {
            row.div("error", errantItemsToHighlight[tx.uid()]);
        }
    }

    if (unsubmittedCount > 0) {
        unsubmittedInfo.div("hint", RS.PHRASE_BLANKTransactionsHaventBeenSubmittedToYourLocalBranchYet.replace("{0}", formatDecimals(unsubmittedCount)));
        unsubmittedInfo.div("sync").button(
            RS.LABEL_SubmitToBranchLedger, () => {
                beginSubmitToBranch();
                unsubmittedInfo.remove();
            }, true,
            RS.ARIA_SubmitToBranchLedger);
    }

    setPage(page);
}

async function beginSubmitToBranch() {
    let myAccounts = await getMyAccounts();
    if (myAccounts.length === 0) {
        // nothing to show
        showAddAccount();
        return;
    }
    const status = new Element("div", "sync-status");
    const msg = status.div("msg", "Uploading...");
    const prog = status.div("prog");

    document.body.append(status.dom);
    const currentAccount = myAccounts[0];
    const sum = new LedgerAccountSummary(currentAccount.branch, currentAccount.number);
    await sum.updateCache();
    await sum.loadDetails();
    const errors = [];
    for (let i = 0; i < sum.transactions.length; i++) {
        const line = sum.transactions[i];
        const csv = line.split(',');
        const isSubmitted = csv[8] === "Y";
        prog.dom.style.width = (i / sum.transactions.length) * 100 + "%";
        if (isSubmitted) {
            continue;
        }
        const tx = new LedgerTransaction(csv[0], csv[1], csv[2], csv[3], csv[4], unspscItemsFromString(csv[5]), csv[6], false);
        if (!tx.isValid) {
            errors.push({ transaction: tx.uid(), error: tx.error });
            continue;
        }
        let res = await fetch(`https://${GLOBAL_LEDGER_DOMAIN}/${line}`, {
            method: 'GET',
            accept: 'application/json'
        });

        if (res.status === 218) {
            const proof = await LEDGER.getWithMetadata(tx.uid() + ":proof");
            if (!proof.value) {
                errors.push({ transaction: tx.uid(), error: RS.LABEL_ProofOfTimeIsMissing });
                continue;
            }
            // upload with proof of time
            res = await fetch(`https://${GLOBAL_LEDGER_DOMAIN}/${line}`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'text/plain;charset=utf-8'
                },
                body: proof.value
            });
            if (res.status === 201
                || res.status === 200) {
                await tx.tryAdd("-", "-", TaxRevenueStatus.Submitted);
            } else {
                errors.push({ transaction: tx.uid(), error: RS.LABEL_BranchLedgerError + ": " + res.statusText });
            }
        } else if (res.status === 200) {
            // already added
            await tx.tryAdd("-", "-", TaxRevenueStatus.Submitted);
        } else {
            const reply = await res.json();
            errors.push({ transaction: tx.uid(), error: RS.LABEL_BranchLedgerError + ": " + res.statusText + " " + reply.error });
        }


    }
    if (errors.length === 0) {
        status.remove();
    } else {
        msg.dom.textContent = RS.LABEL_BLANKProblemsEncountered.replace("{0}", errors.length);
        status.button(RS.LABEL_ReviewProblems, () => {
            showAccountLedger(errors);
            status.remove();
        })
    }
}
function showAddAccount() {
    const page = new Element("div", "add-account");
    const nav = new TopNav();
    nav.onButton = showSplash;
    nav.title = RS.LABEL_AddAccount;
    nav.intro = RS.PHRASE_AddAccountIntro;
    page.add(nav);
    pushAppPath("/add-account", RS.LABEL_AddAccount);


    const content = page.div("content");

    content.div("friendly-heading location", RS.LABEL_BranchLatitudeLongitude);

    const geoError = content.div("error");
    let row = content.div("lat-lon");
    let lat = null, lon = null;

    lat = row.field(FieldType.Text, RS.LABEL_Latitude);
    lon = row.field(FieldType.Text, RS.LABEL_Longitude);

    if ('geolocation' in navigator) {
        content.div("use-loc").a(RS.LABEL_TryToUseMyLocation, () => {
            navigator.geolocation.getCurrentPosition((loc) => {
                lat.value = parseInt(loc.coords.latitude * 10) / 10;
                lon.value = parseInt(loc.coords.longitude * 10) / 10;
            }, () => {
                geoError.dom.textContent = RS.PHRASE_UnableToRetreiveYourLocation;
            });
        });
    }

    content.div("friendly-heading memorable-sentence", RS.LABEL_MyShortMemorableSentence);
    const numberPreview = content.div("number-preview");
    numberPreview.dom.textContent = EMPTY_ACCOUNT_NUMBER_EXAMPLE;

    row = content.div("sentence");
    const phrase = row.field(FieldType.TextArea, null);
    const numberError = content.div("error");

    row = content.div("buttons");
    const continueButton = row.button(RS.LABEL_Continue, async () => {

        const url = new TransactionUrl(numberPreview.dom.textContent);
        if (url.type !== "account") {
            throw "Invalid account number derived. This shouldn't happen.";
        }

        let myAccounts = await getMyAccounts();

        if (!myAccounts.find((v) => {
            if (v.branch === url.toBranch && v.number === url.toNumber) {
                // Make current
                v.lastUsed = new Date().getTime();
                return true;
            }
            return false;
        })) {
            const acc = new LocalAccount();
            acc.branch = url.toBranch;
            acc.number = url.toNumber;
            acc.memorableSentence = phrase.value;
            acc.lastUsed = new Date().getTime();
            acc.label = null;
            myAccounts.push(acc);
        }
        await LEDGER.put("my-account-numbers", myAccounts);
        showAccountSummary();
    }, true, RS.LABEL_Continue);

    continueButton.dom.style.visibility = "hidden";

    const regenerateAccount = () => {
        let latitudeInDecimals = 0.0;
        let longitudeInDecimals = 0.0;
        let phraseText = "";
        let field = "";
        let ok = true;
        try {
            if (phrase.value.length > 0) {
                phraseText = phrase.value.toLowerCase().replace(/\s/g, "");
                if (gatherUsefulNumbersFromUtf8(stringToUtf8(phraseText)).length < 16) {
                    numberError.dom.textContent = RS.PHRASE_MoreWordsOrLettersRequired;
                    ok = false;
                } else {
                    numberError.dom.textContent = "";
                }
            } else {
                ok = false;
            }
        } catch (err) {
            ok = false;
            numberError.dom.textContent = err;
        }

        try {
            geoError.dom.textContent = "";
            if (lat.value.length > 0 || phraseText.length > 0) {
                field = RS.LABEL_Latitude;
                latitudeInDecimals = parseFloat(lat.value.replace("+", "").trim());
                if (latitudeInDecimals < -80 || latitudeInDecimals > 80 || isNaN(latitudeInDecimals)) {
                    geoError.dom.textContent = RS.PHRASE_LatitudeInvalid;
                    ok = false;
                }
            } else {
                ok = false;
            }
            if (lon.value.length > 0 || phraseText.length > 0) {
                field = RS.LABEL_Longitude;
                longitudeInDecimals = parseFloat(lon.value.replace("+", "").trim());
                if (longitudeInDecimals < -180 || longitudeInDecimals > 180 || isNaN(longitudeInDecimals)) {
                    geoError.dom.textContent = RS.PHRASE_LongitudeInvalid;
                    ok = false;
                }
            } else {
                ok = false;
            }
        } catch (err) {
            ok = false;
            geoError.dom.textContent = field + " " + err;
        }

        if (ok) {
            numberError.dom.textContent = "";
            geoError.dom.textContent = "";

            var utf8 = stringToUtf8(phraseText);
            var useful = gatherUsefulNumbersFromUtf8(utf8);
            if (useful.length >= 16) {
                var allnumbers = "";
                for (var i = 0; i < useful.length; i++) {
                    allnumbers += useful[i];
                }
                numberPreview.dom.textContent = formatAccountNumber(parseInt((latitudeInDecimals * 10)) + " " + parseInt((longitudeInDecimals * 10)), allnumbers);
                continueButton.dom.style.visibility = "visible";

            } else {
                numberPreview.dom.textContent = EMPTY_ACCOUNT_NUMBER_EXAMPLE;
                continueButton.dom.style.visibility = "hidden";

            }

        } else {
            numberPreview.dom.textContent = EMPTY_ACCOUNT_NUMBER_EXAMPLE;
            continueButton.dom.style.visibility = "hidden";

        }

    };
    phrase.dom.addEventListener("keyup", regenerateAccount);
    phrase.dom.addEventListener("change", regenerateAccount);
    lon.dom.addEventListener("keyup", regenerateAccount);
    lon.dom.addEventListener("change", regenerateAccount);
    lat.dom.addEventListener("keyup", regenerateAccount);
    lat.dom.addEventListener("change", regenerateAccount);

    content.div("use-loc").a("Import account details from a *.csv backup instead", restoreAccount);

    setPage(page);
}

function showSplash() {
    const splash = new Element("div", "splash");
    const h1 = splash.h1();
    h1.b("//Civilized");
    h1.text(" Money");
    splash.div("p", RS.PHRASE_SplashIntro);
    splash.div("p").button(RS.LABEL_Continue, showAccountSummary, true, RS.ARIA_TapToGetStarted);
    setPage(splash);
}

async function showRenameAccount() {

    let myAccounts = await getMyAccounts();
    if (myAccounts.length === 0) {
        // nothing to show
        showAddAccount();
        return;
    }
    const currentAccount = myAccounts[0];
    pushAppPath(makeAccountUrl(currentAccount, "/rename"), RS.LABEL_NicknameAccount);

    const page = new Element("div", "account-rename");
    const nav = new TopNav(false);
    nav.onButton = openAccountMenu;
    nav.title = RS.LABEL_NicknameAccount;
    nav.intro = formatAccountNumber(currentAccount.branch, currentAccount.number);
    page.add(nav);
    const content = page.div("content");

    const name = content.div().field(FieldType.Text, RS.LABEL_NicknameAccount, currentAccount.label);
    content.div("buttons").button(RS.LABEL_Continue, async () => {
        let ar = await getMyAccounts();
        for (let i = 0; i < ar.length; i++) {
            if (ar[i].branch === currentAccount.branch && ar[i].number === currentAccount.number) {
                ar[i].label = name.value;
            }
        }
        await LEDGER.put("my-account-numbers", ar);
        showAccountSummary();

    }, true, RS.LABEL_Continue);

    setPage(page);
}

async function showRemoveAccount() {

    let myAccounts = await getMyAccounts();
    if (myAccounts.length === 0) {
        // nothing to show
        showAddAccount();
        return;
    }
    const currentAccount = myAccounts[0];
    pushAppPath(makeAccountUrl(currentAccount, "/remove"), RS.LABEL_RemoveAccount);

    const page = new Element("div", "account-remove");
    const nav = new TopNav(false);
    nav.onButton = openAccountMenu;
    nav.title = RS.LABEL_RemoveAccount;
    const formattedNumber = formatAccountNumber(currentAccount.branch, currentAccount.number);
    nav.intro = formattedNumber;
    page.add(nav);
    const content = page.div("content");

    content.div(null, RS.PHRASE_AreYouSureYouWantToRemoveAccountBLANKFromThisDevice.replace("{0}", formattedNumber));

    content.div("buttons").button(RS.LABEL_Continue, async () => {
        let ar = await getMyAccounts();
        for (let i = 0; i < ar.length; i++) {
            if (ar[i].branch === currentAccount.branch && ar[i].number === currentAccount.number) {
                ar.splice(i, 1);
                i--;
            }
        }
        await LEDGER.put("my-account-numbers", ar);
        showAccountSummary();

    }, true, RS.LABEL_Continue);

    setPage(page);
}
function pushAppPath(path, title) {

    if (!path.toLowerCase().startsWith("/app")) {
        path = "/app" + path;
    }
    if (decodeURI(window.location.pathname) !== path) {
        window.history.pushState(
            {},
            title || path,
            window.location.origin + path
        );
    }
    document.title = title || path;
}
async function handleNavigation() {
    let path = decodeURI(window.location.pathname.toLowerCase());
    if (path.startsWith("/app")) {
        path = path.substr(4);
    }

    let myAccounts = await getMyAccounts();
    if (path.length <= 1) {

        if (myAccounts.length !== 0) {
            await showAccountSummary();
        } else {
            showSplash();
        }
    } else if (path === "/add-account") {
        await showAddAccount();
    } else {

        if (path.startsWith("/")) {
            path = path.substr(1);
        }

        let idx = path.indexOf("/");

        let transaction = new TransactionUrl(idx > -1 ? path.substr(0, idx) : path);
        switch (transaction.type) {
            // account sub page
            case "account": {
                if (myAccounts[0].branch !== transaction.toBranch || myAccounts[0].number !== transaction.toNumber) {
                    await setCurrentAccount({ branch: transaction.toBranch, number: transaction.toNumber });
                }
                if (idx > -1) {
                    path = path.substr(idx);
                }
                switch (path) {
                    case "/ledger": await showAccountLedger(); return;
                    case "/rename": await showRenameAccount(); return;
                    case "/remove": await showRemoveAccount(); return;
                    default: await showAccountSummary(); return;
                }
            }
            case "new-transaction": {
                if (myAccounts[0].branch === transaction.toBranch && myAccounts[0].number === transaction.toNumber) {
                    await beginAcceptPayment(transaction);
                } else {
                    await showPaymentPage(myAccounts[0], transaction);
                }
            } break;
        }
    }
}

window.addEventListener('load', async (e) => {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker
            .register('/app/pwa-service.js');
    }
    document.body.appendChild(ui.dom);
    handleNavigation();
});

window.addEventListener('popstate', handleNavigation);

