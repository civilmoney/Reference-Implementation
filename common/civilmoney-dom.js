//
// Language: ES6
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
            const ar = className.split(' ');
            ar.forEach((c) => {
                if (c.length > 0) {
                    this._el.classList.add(c);
                }
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
        const el = new Element(nodeType, className, text);
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
    b(text = null) {
        return this.element("b", null, text);
    }
    text(text) {
        return this.element("span", null, text);
    }
    a(text, onclick) {
        const a = this.element("a", null, text);
        a.dom.href = "javascript:;";
        a.dom.addEventListener("click", () => {
            onclick(a);
        });
        return a;
    }
    link(text, url) {
        const a = this.element("a", null, text);
        a.dom.href = url;
        return a;
    }
    button(text, onclick, isDefault = false, aria = null) {
        const b = this.element("button", null, text);
        if (aria !== undefined && aria !== null) {
            b.dom.ariaLabel = aria;
            b.dom.title = aria;
        }
        b.dom.addEventListener("click", () => {
            onclick(b);
        });
        if (isDefault) {
            b.dom.classList.add("default");
        }
        return b;
    }
    field(type, labelText, value, dropDownOptions = null, placeholder = null) {
        const f = new Field(type, labelText, value, dropDownOptions, placeholder);
        this._el.appendChild(f.dom);
        return f;
    }
}
const FieldType = Object.freeze({ Text: Symbol(), Integer: Symbol(), Toggle: Symbol(), DropDown: Symbol(), TextArea: Symbol() });
let s_FieldUID = 0;
const s_Encode = document.createElement("div");
function HTMLEncode(text) {
    s_Encode.textContent = text;
    return s_Encode.innerHTML;
}
class Field extends Element {
    constructor(type, labelText, value, dropDownOptions, placeholder) {
        super("div", "field");

        const fieldID = "_f" + (++s_FieldUID);
        // Private fields:
        this._label = this.element("label", null, labelText);
        this._input = null; // Element
        this._onValueChanged = null;

        switch (type) {
            case FieldType.Text:
                this._input = this.element("input");
                this._input.dom.type = "text";

                break;
            case FieldType.TextArea:
                this._input = this.element("textarea");
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
                    const op = document.createElement("option");
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
        if (placeholder) {
            this._input.dom.placeholder = placeholder;
        }
        this.value = value;
        this._feedbackLabel = this.element("div", "feedback", "");

        this._input.dom.addEventListener("keyup", () => {
            if (this._onValueChanged !== null) {
                this._onValueChanged(this);
            }
        });
        this._input.dom.addEventListener("change", () => {
            if (this._onValueChanged !== null) {
                this._onValueChanged(this);
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
        return this.dom.classList.contains("error");
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
    const http = new XMLHttpRequest();
    let res = {};
    const errors = {
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
        const json = JSON.stringify(data);
        http.setRequestHeader("Content-Type", "application/json");
        http.send(json);
    } else {
        http.send();
    }
}