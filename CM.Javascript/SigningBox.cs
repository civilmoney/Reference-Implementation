using Bridge.Html5;
using CM.Schema;
using System;

namespace CM.Javascript {

    /// <summary>
    /// A common control for specifying your secret pass phrase or selecting a withheld private key file.
    /// </summary>
    internal class SigningBox {
        public Action OnPasswordEnterKey;
        public Action<bool> OnPasswordReadyStateChanged;
        private Account _Account;
        private HTMLDivElement _KeyWitheldRow;
        private HTMLInputElement _Password;
        private HTMLDivElement _PasswordRow;
        private RSAParameters _PrivateKey;
        private HTMLDivElement _NoAccountEnteredYet;
        private HTMLInputElement _SecurityReminder;

        public SigningBox(HTMLElement form) {
            var row = form.Div("row signing-box");
            row.H3("Signature");
            var reminder = row.Div("reminder", SR.LABEL_CIVIL_MONEY_SECURITY_REMINDER);
            var confirm = row.Div("confirm");
            _SecurityReminder = confirm.CheckBox(SR.HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS);

            _NoAccountEnteredYet = row.Div("", SR.LABEL_PLEASE_ENTER_YOUR_ACCOUNT_NAME);
            _NoAccountEnteredYet.Style.Display = Display.None;

            _PasswordRow = row.Div();
            _PasswordRow.Style.Display = Display.None;
            _Password = _PasswordRow.Password();
            _Password.Placeholder = SR.LABEL_SECRET_PASS_PHRASE;
            _Password.OnEnterKey(OnPasswordEnterKey);

            _KeyWitheldRow = row.Div();
            _KeyWitheldRow.Style.Display = Display.None;
            _KeyWitheldRow.Div("row-spaces").Button(SR.LABEL_SELECT_MY_KEY_FILE, OnSelectPKFile, "blue-button");
            _KeyWitheldRow.Div("row-spaces").A(SR.LABEL_PASTE_FROM_TEXT_INSTEAD, OnPasteKeyFromText);
            
            _SecurityReminder.OnChange = (e) => {
                var ch = _SecurityReminder;
                SetSigningMode();

                reminder.Style.Display = ch.Checked ? Display.None : Display.Block;
                confirm.Style.Display = ch.Checked ? Display.None : Display.Block;
                _Password.Focus();

            };
        }

        void OnSanePrivateKey(RSAParameters rsa) {
            _PrivateKey = rsa;
            _KeyWitheldRow.Clear();

            _KeyWitheldRow.Span(Assets.SVG.CircleTick.ToString(16, 16, Assets.SVG.STATUS_GREEN_COLOR) + " " + SR.LABEL_SIGNING_KEY_LOOKS_OK, "signature-ok");
            OnPasswordReadyStateChanged?.Invoke(true);
        }

        private void OnPasteKeyFromText(MouseEvent<HTMLAnchorElement> e) {
            var div = e.CurrentTarget.ParentElement;
            var base64 = new HTMLTextAreaElement();
            base64.Placeholder = "---------BEGIN RSA PRIVATE KEY-----------\r\n...  PKCS#8 or PKCS#1 base64 ...\r\n---------END RSA PRIVATE KEY-----------";
            div.AppendChild(base64);
            var feedback = new Feedback(div);
            base64.OnChange = (ee) => {
                try {
                    var bytes = CM.Cryptography.ASN.Base64BlobStringToBytes(base64.Value);
                    var asn = new CM.Cryptography.ASN(bytes);
                    var key = asn.ToRSAParameters();
                    if (key == null || key.D == null)
                        throw new Exception(CMResult.E_Crypto_Invalid_RSA_PrivateKey_Invalid.GetLocalisedDescription());

                    if (key.D.Length < Constants.MinimumRSAKeySizeInBytes)
                        throw new Exception(CMResult.E_Crypto_Invalid_RSA_PrivateKey_TooWeak.GetLocalisedDescription());

                    // Do a basic early sanity check to inform the 
                    // user whether they've selected the wrong key file.
                    PublicKey currentPublicKey;
                    if (!_Account.TryFindPublicKey(DateTime.UtcNow, out currentPublicKey))
                        throw new Exception(CMResult.E_Account_Missing_Public_Key.GetLocalisedDescription());

                    if (!Helpers.IsHashEqual(currentPublicKey.Key, key.Modulus))
                        throw new Exception(CMResult.E_Crypto_Invalid_RSA_PrivateKey_Mismatch.GetLocalisedDescription());

                    OnSanePrivateKey(key);

                } catch (Exception ex) {

                    feedback.Set(Assets.SVG.Warning, FeedbackType.Error, SR.LABEL_INVALID_RSA_KEY_TEXT_BLOB + " " + ex.Message);
                }
            };
        }

        public byte[] PasswordOrPrivateKey {
            get {
                if (_Account == null) return null;
                if (_Account.PrivateKey.SchemeID == PrivateKeySchemeID.KeyWithheld) {
                    return _PrivateKey.D;
                } else {
                    return System.Text.Encoding.UTF8.GetBytes(_Password.Value);
                }
            }
        }

        public Account Signer {
            get { return _Account; }
            set {
                if (_Account != value) {
                    _Account = value;
                    SetSigningMode();
                }
            }
        }
        private void OnSelectPKFile(MouseEvent<HTMLButtonElement> arg) {
            var f = new HTMLInputElement() {
                Type = InputType.File,
                Multiple = false,
                Accept = "application/pkcs8"
            };
            f.Style.Display = Display.None;
            _KeyWitheldRow.AppendChild(f);
            f.OnChange = (fileChanged) => {
                if (f.Files.Length == 0)
                    return;
                var r = new FileReader();
                r.OnLoad = (e) => {
                    var pk = e.Target["result"].As<string>();
                    try {
                        var bytes = CM.Cryptography.ASN.Base64BlobStringToBytes(pk);
                        var asn = new CM.Cryptography.ASN(bytes);
                        var key = asn.ToRSAParameters();

                        if (key == null || key.D == null)
                            throw new Exception(CMResult.E_Crypto_Invalid_RSA_PrivateKey_Invalid.GetLocalisedDescription());

                        if (key.D.Length < Constants.MinimumRSAKeySizeInBytes)
                            throw new Exception(CMResult.E_Crypto_Invalid_RSA_PrivateKey_TooWeak.GetLocalisedDescription());

                        // Do a basic early sanity check to inform the 
                        // user whether they've selected the wrong key file.
                        PublicKey currentPublicKey;
                        if (!_Account.TryFindPublicKey(DateTime.UtcNow, out currentPublicKey))
                            throw new Exception(CMResult.E_Account_Missing_Public_Key.GetLocalisedDescription());

                        if (!Helpers.IsHashEqual(currentPublicKey.Key, key.Modulus))
                            throw new Exception(CMResult.E_Crypto_Invalid_RSA_PrivateKey_Mismatch.GetLocalisedDescription());

                        OnSanePrivateKey(key);
                    } catch (Exception ex) {
                        var msg = CMResult.E_Crypto_Invalid_RSA_PrivateKey_Invalid.GetLocalisedDescription();
                        if (ex.Message != msg)
                            msg += " " + ex.Message; // append a more useful error message if possible
                        Window.Alert(msg);
                    }
                };

                r.ReadAsText(f.Files[0]);
            };
            f.Click();
        }

        private void SetSigningMode() {
            var ch = _SecurityReminder;
            var a = _Account;
            var keyWithheld = a != null && a.PrivateKey.SchemeID == Schema.PrivateKeySchemeID.KeyWithheld;
            _NoAccountEnteredYet.Style.Display = a == null && ch.Checked ? Display.Block : Display.None;
            _KeyWitheldRow.Style.Display = keyWithheld && ch.Checked && a != null ? Display.Block : Display.None;
            _PasswordRow.Style.Display = !keyWithheld && ch.Checked && a != null ? Display.Block : Display.None;

            if (a != null && ch.Checked && !keyWithheld)
                OnPasswordReadyStateChanged?.Invoke(true);
        }
    }
}