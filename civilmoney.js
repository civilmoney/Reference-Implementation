
var RX_BRANCH = /\(?[+-]?\d{1,4}\s+[+-]?\d{1,4}\)?/i
var BRANCH_ERROR_EXAMPLE = "Branch should be in the format of 'Latitude*10 Longitude*10'.<br/>Example: NY, NY USA is <b>40.7</b>30610,<b>-73.9</b>35242. Civil Branch would be 407 -739.";
var ACCOUNT_ERROR_EXAMPLE = "Account should be in the format of (Latitude*10 Longitude*10) 0000 0000 0000 0000.<br/>Example:<br/>NY, NY USA is <b>40.7</b>30610,<b>-73.9</b>35242. Civil Branch would be 407 -739. An account number would be (407 -739) 1111 2222 3333 4444."

var UNIVERSAL_BASIC_INCOME_2YR = 4320000 * 2;


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
function isCVCValid(cvc) {
    return (cvc || "").match(/^\d+$/) !== null;
}
function isUNSPSCValid(unspscCode) {
    return (unspscCode || "").match(/^[\d]{8}$/) !== null;
}
function branchFromString(unsanitised) {
    return unsanitised.replace(/[()+]/g, "");
}
function formatDecimals(number) {
    return parseInt(number) + "." + parseInt((number % 1) * 100);
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
//
// DOM helpers
//


//
// Base class for managing DOM elements and UI pages
//
class Element {
    constructor(nodeType, className = null, text = null) {

        // Private fields:
        this._el = document.createElement(nodeType);  // The element's DOM node.

        if (className !== null) {
            let ar = className.split(' ');
            ar.forEach((c) => {
                this._el.classList.add(c);
            });
        }

        if (text !== null) {
            this._el.textContent = text;
        }
    }

    get dom() {
        return this._el;
    }
    add(element) {
        this._el.appendChild(element.dom);
    }
    remove() {
        this._el.remove();
    }
    element(nodeType, className = null, text = null) {
        let el = new Element(nodeType, className, text);
        this._el.appendChild(el.dom);
        return el;
    }
    div(className = null, text = null) {
        return this.element("div", className, text);
    }
    span(className = null, text = null) {
        return this.element("span", className, text);
    }
    h1(className = null, text = null) {
        return this.element("h1", className, text);
    }
    h2(className = null, text = null) {
        return this.element("h2", className, text);
    }
    h3(className = null, text = null) {
        return this.element("h3", className, text);
    }
    text(text) {
        return this.element("span", null, text);
    }
    a(text, onclick) {
        let a = this.element("a", null, text);
        a.dom.href = "javascript:;";
        a.dom.addEventListener("click", () => {
            onclick(a);
        });
        return a;
    }
    link(text, url) {
        let a = this.element("a", null, text);
        a.dom.href = url;
        return a;
    }
    button(text, onclick, isDefault = false) {
        let b = this.element("button", null, text);
        b.dom.addEventListener("click", () => {
            onclick(b);
        });
        if (isDefault) {
            b.dom.classList.add("default");
        }
        return b;
    }
    field(type, labelText, value, dropDownOptions) {
        let f = new Field(type, labelText, value, dropDownOptions);
        this._el.appendChild(f.dom);
        return f;
    }
}
const FieldType = Object.freeze({ Text: Symbol(), Integer: Symbol(), Toggle: Symbol(), DropDown: Symbol() });
let s_FieldUID = 0;
let s_Encode = document.createElement("div");
function HTMLEncode(text) {
    s_Encode.textContent = text;
    return s_Encode.innerHTML;
}
class Field extends Element {
    constructor(type, labelText, value, dropDownOptions) {
        super("div", "field");

        let fieldID = "_f" + (++s_FieldUID);
        // Private fields:
        this._label = this.element("label", null, labelText);
        this._input = null; // Element
        this._onValueChanged = null;

        switch (type) {
            case FieldType.Text:
                this._input = this.element("input");
                this._input.dom.type = "input";

                break;
            case FieldType.Integer:
                this._input = this.element("input");
                this._input.dom.type = "number";
                break;
            case FieldType.Toggle:
                this._input = this.element("input");
                this._input.dom.type = "checkbox";
                this._input.dom.value = "1";
                this._input.dom.checked = value === "1" || value === true || value === "true";
                this._input.dom.remove();
                // Order matters for CSS rule
                this.dom.className = "onoff";
                this.dom.insertBefore(this._input.dom, this._label.dom);
                this._label.dom.setAttribute("tabindex", "0");
                break;
            case FieldType.DropDown:
                this._input = this.element("select");
                dropDownOptions.forEach((opt) => {
                    let op = document.createElement("option");
                    op.text = opt.text;
                    op.value = opt.value;
                    this._input.dom.appendChild(op);
                });
                this._input.dom.value = value;
                break;
            default:
                throw `Field type ${type} not implemented`;
        }
        this._input.dom.id = fieldID;
        this._label.dom.setAttribute("for", this._input.dom.id);

        this.value = value;
        this._feedbackLabel = this.element("div", "feedback", "");

        this._input.dom.addEventListener("keyup", () => {
            if (this._onValueChanged !== null) {
                this._onValueChanged();
            }
        });
        this._input.dom.addEventListener("change", () => {
            if (this._onValueChanged !== null) {
                this._onValueChanged();
            }
        });
    }

    get value() {
        return this._input.dom.value;
    }
    set value(value) {
        if (value !== null && value !== undefined) {
            this._input.dom.value = value;
        } else {
            this._input.dom.value = "";
        }
    }
    get error() {
        return this.dom.classList.contains("error") ? this._feedbackLabel.dom.textContent : "";
    }
    set error(value) {
        if (value === null || value === "") {
            this.dom.classList.remove("error");
            this._feedbackLabel.dom.textContent = "";
        } else {
            this.dom.classList.remove("looks-good");
            this._feedbackLabel.dom.textContent = value;
            this.dom.classList.add("error")
        }
    }
    get hasError() {
        return dom.classList.contains("error");
    }
    focus() {
        this._input.dom.focus();
        this.dom.scrollIntoView(true);
    }
    onValueChanged(delegate) {
        this._onValueChanged = delegate;
    }
    get looksGoodMessage() {
        return this.dom.classList.contains("looks-good") ? this._feedbackLabel.dom.textContent : "";
    }
    set looksGoodMessage(value) {

        if (value === null || value === "") {
            this.dom.classList.remove("looks-good");
            this._feedbackLabel.dom.textContent = "";
        } else {
            this.dom.classList.remove("error");
            this._feedbackLabel.dom.textContent = value !== " " ? value : "";
            this.dom.classList.add("looks-good")
        }
    }
    get isChecked() {
        return this._input.dom.checked;
    }
    set isChecked(value) {
        this._input.dom.checked = value;
    }
}

function sendAndReceive(method, path, data, onresult) {
    var http = new XMLHttpRequest();
    var res = {};
    var errors = {
        "connection": "There was a problem connecting to the server. If your internet is fine, please try again later.",
        "permission": "There was a permission related problem performing this request.",
        "input": "One or more fields were invalid.",
        "unknown": "A problem occurred on the server."
    };
    http.onload = function (e) {
        if (http.status >= 200 && http.status < 300) {
            res = JSON.parse(http.responseText);
        } else {
            if (http.status === 0) {
                res.Error = errors["connection"];
            } else {

                if (http.responseText && http.responseText[0] == "{") {
                    res = JSON.parse(http.responseText);
                } else {
                    res.Error = http.status === 400 ? errors["input"]
                        : http.status === 401 ? errors["permission"]
                            : errors["unknown"];
                }
            }
        }
        onresult(res);
    };
    http.onerror = function (e) {
        res.Error = http.status === 0 ? errors["connection"] : errors["unknown"];
        onresult(res);
    };
    http.open(method, path);
    if (data) {
        var json = JSON.stringify(data);
        http.setRequestHeader("Content-Type", "application/json");
        http.send(json);
    } else {
        http.send();
    }
}