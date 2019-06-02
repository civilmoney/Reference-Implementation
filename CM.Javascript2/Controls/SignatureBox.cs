using CM.Schema;
using Retyped;
using SichboUI;
using System;

namespace CM.JS.Controls {

    internal class SignatureBox : StackPanel {

        private Account _Account;
        private Field _Password;
        private RSAParameters _PrivateKey;
        bool _Confirmed;

        public SignatureBox(Account a = null) {
            _Account = a;
            Margin.Value = new Thickness(60, 0);
            HorizontalAlignment = Alignment.Stretch;
            El("h3", text: "Signature");
            Div("instructions").Html.innerHTML = SR.LABEL_CIVIL_MONEY_SECURITY_REMINDER;
            var confirm = new Field(FieldType.Toggle, "");
            confirm.LabelElement.Html.innerHTML = SR.HTML_IVE_CHECKED_MY_WEB_BROWSER_ADDRESS;
            Add(confirm);
            confirm.ValueOrCheckedChanged += OnConfirmed;
        }

        public Account Account {
            get => _Account;
            set {
                if (_Account != value) {
                    _Account = value;
                    if (_Confirmed)
                        OnConfirmed(null);
                }
            }
        }

        void OnConfirmed(Field ch) {
            _Confirmed = true;
            Clear();
            El("h3", text: "Signature");
            if (_Account == null) {
                Div("instructions", text: SR.LABEL_PLEASE_ENTER_YOUR_ACCOUNT_NAME);
                return;
            }

            if (_Account.PrivateKey.SchemeID == PrivateKeySchemeID.AES_CBC_PKCS7_RFC2898_HMACSHA1_10000) {
                _Password = new Field(FieldType.Textbox, SR.LABEL_SECRET_PASS_PHRASE);
                _Password.Margin.Value = new Thickness(15, 0);
                Add(_Password);
            } else {
                Add(new Button(ButtonStyle.BigRed, SR.LABEL_SELECT_MY_KEY_FILE, (b) => {
                    var f = new dom.HTMLInputElement() {
                        type = "file",
                        multiple = false,
                        accept = "application/pkcs8"
                    };
                    f.style.display = "none";

                    f.onchange = (fileChanged) => {
                        if (f.files.length == 0)
                            return;
                        var r = new dom.FileReader();
                        r.onload = (ev) => {
                            var pk = ev.target["result"].As<string>();
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
                                _PrivateKey = key;
                                b.Remove();
                                Div(text: SR.LABEL_SIGNING_KEY_LOOKS_OK + " 👍");
                            } catch (Exception ex) {
                                var msg = CMResult.E_Crypto_Invalid_RSA_PrivateKey_Invalid.GetLocalisedDescription();
                                if (ex.Message != msg)
                                    msg += " " + ex.Message; // append a more useful error message if possible
                                Notification.Show("Invalid private key", msg);

                            }
                        };

                        r.readAsText(f.files[0]);
                    };
                    Html.appendChild(f);
                    f.click();
                }, margin: new Thickness(15, 0, 0, 0)) {
                    HorizontalAlignment = Alignment.Center
                });
            }
        }

        public byte[] PasswordOrPrivateKey {
            get {
                if (_Account == null) return null;
                if (_Account.PrivateKey.SchemeID == PrivateKeySchemeID.KeyWithheld) {
                    return _PrivateKey == null ? null : _PrivateKey.D;
                } else {
                    return System.Text.Encoding.UTF8.GetBytes(_Password.Value);
                }
            }
        }
    }
}