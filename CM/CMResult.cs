#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

namespace CM {
    /// <summary>
    /// CMResult is used in place of error messages for multilingual reasons. The description in the
    /// struct should not be used on client UIs.
    /// </summary>
    public struct CMResult {
        public CMResult(uint code, string desc) { Code = unchecked((int)code); Description = desc; }
        public int Code;
        public string Description;

        private const uint ERROR = 0x80000000;
        private const uint CRYPTO = 0x1000;
        private const uint ACCOUNT = 0x2000;
        private const uint TRANSACTION = 0x3000;
        private const uint VOTE = 0x4000;
        private const uint SUCCESS = 0;

        public override string ToString() {
            var desc = Description;
#if JAVASCRIPT
            Bridge.Script.Write(@"
            if(desc==null)
            for(var v in CM.CMResult)
                 if(CM.CMResult[v] && CM.CMResult[v].code==this.code){
                     desc = CM.CMResult[v].description;
                     break;
                 }
            ");
            // The uint cast below is a Bridge bug workaround
#endif
            return "0x" + ((uint)Code).ToString("X") + " " + desc;
        }
        public static bool operator ==(CMResult a, CMResult b) {
            return a.Code == b.Code;
        }
        public static bool operator !=(CMResult a, CMResult b) {
            return a.Code != b.Code;
        }
        public override bool Equals(object obj) {
            if (!(obj is CMResult)) return false;
            return Code == ((CMResult)obj).Code;
        }
        public override int GetHashCode() {
            return Code.GetHashCode();
        }
        /// <summary>
        /// Returns true if the status code is positive or zero.
        /// </summary>
        public bool Success {
            get { return Code >= SUCCESS; }
        }

        /// <summary>
        /// OK
        /// </summary>
        public readonly static CMResult S_OK = new CMResult(SUCCESS, "OK");
        /// <summary>
        /// Successful, but false
        /// </summary>
        public readonly static CMResult S_False = new CMResult(SUCCESS + 1, "False");
       
        /// <summary>
        /// Successful, At least 1 copy of the item was found, but the minimum number of copies required are not met.
        /// </summary>
        public readonly static CMResult S_Item_Transient = new CMResult(SUCCESS + 2, "At least 1 copy of the item was found, but the minimum number of copies required are not met.");
        /// <summary>
        /// Unknown error
        /// </summary>
        public readonly static CMResult E_General_Failure = new CMResult(ERROR, "General failure.");
        /// <summary>
        /// The web socket is not currently connected.
        /// </summary>
        public readonly static CMResult E_Not_Connected = new CMResult(ERROR+1, "The web socket is not currently connected.");
        /// <summary>
        /// Time-out waiting on a reply.
        /// </summary>
        public readonly static CMResult E_Timeout_Waiting_On_Reply = new CMResult(ERROR + 2, "Time-out waiting on a reply.");
        /// <summary>
        /// Invalid action.
        /// </summary>
        public readonly static CMResult E_Invalid_Action = new CMResult(ERROR + 3, "Invalid action.");
        /// <summary>
        /// The item was not found
        /// </summary>
        public readonly static CMResult E_Item_Not_Found = new CMResult(ERROR + 4, "The item was not found.");
        /// <summary>
        /// Invalid request
        /// </summary>
        public readonly static CMResult E_Invalid_Request = new CMResult(ERROR + 5, "Invalid request.");
        /// <summary>
        /// There were not enough available peers to corroborate the request.
        /// </summary>
        public readonly static CMResult E_Not_Enough_Peers = new CMResult(ERROR + 6, "There were not enough available peers to corroborate the request.");
        /// <summary>
        /// The requested GET or PUT path is not valid for the item provided.
        /// </summary>
        public readonly static CMResult E_Invalid_Object_Path = new CMResult(ERROR + 7, "The requested GET or PUT path is not valid for the item provided.");
        /// <summary>
        /// A newer version of this item is already being committed.
        /// </summary>
        public readonly static CMResult E_Object_Superseded = new CMResult(ERROR + 8, "A newer version of this item is already being committed.");
        /// <summary>
        /// The maximum number of DHT peer hops have been reached.
        /// </summary>
        public readonly static CMResult E_Max_Hops_Reached = new CMResult(ERROR + 9, "The maximum number of DHT peer hops have been reached.");
        /// <summary>
        /// Unable to connect to any servers within a reasonable time-out period.
        /// </summary>
        public readonly static CMResult E_Connect_Attempt_Timeout = new CMResult(ERROR + 10, "Unable to connect to any servers within a reasonable time-out period.");
        /// <summary>
        /// Invalid search date range.
        /// </summary>
        public readonly static CMResult E_Invalid_Search_Date = new CMResult(ERROR + 11, "Invalid search date range.");

        /// <summary>
        /// Unknown API version.
        /// </summary>
        public readonly static CMResult E_Unknown_API_Version = new CMResult(ERROR + 12, "Unknown API version.");
        /// <summary>
        /// The operation has been cancelled.
        /// </summary>
        public readonly static CMResult E_Operation_Cancelled = new CMResult(ERROR + 13, "The operation has been cancelled.");

        #region Crypto
        /// <summary>
        /// The specified password didn't work for decryption.
        /// </summary>
        public readonly static CMResult E_Crypto_Invalid_Password = new CMResult(ERROR + CRYPTO + 0, "The specified password didn't work for decryption.");
        /// <summary>
        /// The account private key scheme ID is not recognised.
        /// </summary>
        public readonly static CMResult E_Crypto_Unrecognized_SchemeID = new CMResult(ERROR + CRYPTO + 1, "The account private key scheme ID is not recognised.");
        /// <summary>
        /// Unable to obtain an encryption key using Rfc2898.
        /// </summary>
        public readonly static CMResult E_Crypto_Rfc2898_General_Failure = new CMResult(ERROR + CRYPTO + 2, "Unable to obtain an encryption key using Rfc2898.");
        /// <summary>
        /// Unable to sign the data using RSA
        /// </summary>
        public readonly static CMResult E_Crypto_RSA_Signing_General_Failure = new CMResult(ERROR + CRYPTO + 3, "Unable to sign the data using RSA.");
        /// <summary>
        /// Unable to verify the data using RSA
        /// </summary>
        public readonly static CMResult E_Crypto_RSA_Verify_General_Failure = new CMResult(ERROR + CRYPTO + 4, "Unable to verify the data using RSA.");
        /// <summary>
        /// Unable to generate an RSA key.
        /// </summary>
        public readonly static CMResult E_Crypto_RSA_Key_Gen_Failure = new CMResult(ERROR + CRYPTO + 5, "Unable to generate an RSA key.");

        #endregion

        #region Account
        /// <summary>
        /// No valid public key was found on the account for the specified time.
        /// </summary>
        public readonly static CMResult E_Account_Missing_Public_Key = new CMResult(ERROR + ACCOUNT + 0, "No valid public key was found on the account for the specified time.");
        /// <summary>
        /// The account ID is invalid.
        /// </summary>
        public readonly static CMResult E_Account_ID_Invalid = new CMResult(ERROR + ACCOUNT + 1, "The account ID is invalid.");
        /// <summary>
        /// Account IDs are read-only.
        /// </summary>
        public readonly static CMResult E_Account_IDs_Are_Readonly = new CMResult(ERROR + ACCOUNT + 2, "Account IDs are read-only.");
        /// <summary>
        /// Created UTC is too far ahead of the server's current time.
        /// </summary>
        public readonly static CMResult E_Account_Created_Utc_Out_Of_Range = new CMResult(ERROR + ACCOUNT + 3, "Created UTC is too far ahead of the server's current time.");
        /// <summary>
        /// Created UTC is read-only.
        /// </summary>
        public readonly static CMResult E_Account_Created_Utc_Is_Readonly = new CMResult(ERROR + ACCOUNT + 4, "Created UTC is read-only.");
        /// <summary>
        /// Updated UTC is too far ahead of the server's current time.
        /// </summary>
        public readonly static CMResult E_Account_Updated_Utc_Out_Of_Range = new CMResult(ERROR + ACCOUNT + 5, "Updated UTC is too far ahead of the server's current time.");
        /// <summary>
        /// The account Updated UTC is out-dated. A newer copy exists.
        /// </summary>
        public readonly static CMResult E_Account_Updated_Utc_Is_Old = new CMResult(ERROR + ACCOUNT + 6, "The account Updated UTC is out-dated. A newer copy exists.");
        /// <summary>
        /// The number of public keys specified are less than the existing record's.
        /// </summary>
        public readonly static CMResult E_Account_Too_Few_Public_Keys = new CMResult(ERROR + ACCOUNT + 7, "The number of public keys specified are less than the existing record's.");
        /// <summary>
        /// Unable to corroborate account information with the network.
        /// </summary>
        public readonly static CMResult E_Account_Cant_Corroborate = new CMResult(ERROR + ACCOUNT + 8, "Unable to corroborate account information with the network.");
        /// <summary>
        /// Unable to corroborate account information with the network. The network's copy has too fewer keys than the record provided.
        /// </summary>
        public readonly static CMResult E_Account_Cant_Corroborate_Public_Keys = new CMResult(ERROR + ACCOUNT + 9, "Unable to corroborate account information with the network. The network's copy has too fewer keys than the record provided.");
        /// <summary>
        /// The newest public key entry must equal the account's Updated UTC when adding new keys.
        /// </summary>
        public readonly static CMResult E_Account_Invalid_New_Public_Key_Date = new CMResult(ERROR + ACCOUNT + 10, "The newest public key entry must equal the account's Updated UTC when adding new keys.");
        /// <summary>
        /// One or more public keys do not match the existing account.
        /// </summary>
        public readonly static CMResult E_Account_Public_Key_Mismatch = new CMResult(ERROR + ACCOUNT + 11, "One or more public keys do not match the existing account.");
        /// <summary>
        /// One of the public keys in the account have an invalid RSA signature.
        /// </summary>
        public readonly static CMResult E_Account_Public_Key_Signature_Error = new CMResult(ERROR + ACCOUNT + 12, "One of the public keys in the account have an invalid RSA signature.");
        /// <summary>
        /// The account RSA signature is invalid.
        /// </summary>
        public readonly static CMResult E_Account_Signature_Error = new CMResult(ERROR + ACCOUNT + 13, "The account RSA signature is invalid.");
        /// <summary>
        /// Invalid account region specified.
        /// </summary>
        public readonly static CMResult E_Account_Invalid_Region = new CMResult(ERROR + ACCOUNT + 14, "Invalid account region specified.");
        /// <summary>
        /// Account names that are equal to an ISO3166-2 subdivision code require a valid governing authority attribute.
        /// </summary>
        public readonly static CMResult E_Account_Governing_Authority_Attribute_Required = new CMResult(ERROR + ACCOUNT + 15, "Account names that are equal to an ISO3166-2 subdivision code require a valid governing authority attribute.");

        #endregion


        #region Transactions

        /// <summary>
        /// The payee could not be found on the network.
        /// </summary>
        public readonly static CMResult E_Transaction_Payee_Not_Found = new CMResult(ERROR + TRANSACTION + 0, "The payee could not be found on the network.");
        /// <summary>
        /// The payer could not be found on the network.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Not_Found = new CMResult(ERROR + TRANSACTION + 1, "The payer could not be found on the network.");
        /// <summary>
        /// Invalid payee signature.
        /// </summary>
        public readonly static CMResult E_Transaction_Invalid_Payee_Signature = new CMResult(ERROR + TRANSACTION + 2, "Invalid payee signature.");
        /// <summary>
        /// Invalid payer signature.
        /// </summary>
        public readonly static CMResult E_Transaction_Invalid_Payer_Signature = new CMResult(ERROR + TRANSACTION + 3, "Invalid payer signature.");
        /// <summary>
        /// The payer's signature is required.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Signature_Required = new CMResult(ERROR + TRANSACTION + 4, "The payer's signature is required.");
        /// <summary>
        /// A payee ID is required.
        /// </summary>
        public readonly static CMResult E_Transaction_PayeeID_Required = new CMResult(ERROR + TRANSACTION + 5, "A payee ID is required.");
        /// <summary>
        /// A payer ID is required.
        /// </summary>
        public readonly static CMResult E_Transaction_PayerID_Required = new CMResult(ERROR + TRANSACTION + 6, "A payer ID is required.");
        /// <summary>
        /// The transaction's Created UTC time is out of range. Please check your device's clock and try again.
        /// </summary>
        public readonly static CMResult E_Transaction_Created_Utc_Out_Of_Range = new CMResult(ERROR + TRANSACTION + 7, "The transaction's Created UTC time is out of range. Please check your device's clock and try again.");
        /// <summary>
        /// The payee's updated UTC time must be greater than Created UTC.
        /// </summary>
        public readonly static CMResult E_Transaction_Payee_Updated_Utc_Out_Of_Range = new CMResult(ERROR + TRANSACTION + 8, "The payee's updated UTC time must be greater than Created UTC.");
        /// <summary>
        /// The payer's updated UTC time must be greater than Created UTC.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Updated_Utc_Out_Of_Range = new CMResult(ERROR + TRANSACTION + 9, "The payer's updated UTC time must be greater than Created UTC.");
        /// <summary>
        /// The transaction amount cannot be altered.
        /// </summary>
        public readonly static CMResult E_Transaction_Amount_Is_Readonly = new CMResult(ERROR + TRANSACTION + 10, "The transaction amount cannot be altered.");
        /// <summary>
        /// The transaction created UTC cannot be altered.
        /// </summary>
        public readonly static CMResult E_Transaction_Created_Utc_Is_Readonly = new CMResult(ERROR + TRANSACTION + 11, "The transaction created UTC cannot be altered.");
        /// <summary>
        /// The transaction payee cannot be altered.
        /// </summary>
        public readonly static CMResult E_Transaction_Payee_Is_Readonly = new CMResult(ERROR + TRANSACTION + 12, "The transaction payee cannot be altered.");
        /// <summary>
        /// The transaction payer cannot be altered.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Is_Readonly = new CMResult(ERROR + TRANSACTION + 13, "The transaction payer cannot be altered.");
        /// <summary>
        /// The transaction memo cannot be altered.
        /// </summary>
        public readonly static CMResult E_Transaction_Memo_Is_Readonly = new CMResult(ERROR + TRANSACTION + 14, "The transaction memo cannot be altered.");

        /// <summary>
        /// The transaction amount is invalid.
        /// </summary>
        public readonly static CMResult E_Transaction_Invalid_Amount = new CMResult(ERROR + TRANSACTION + 15, "The transaction amount is invalid.");
        /// <summary>
        /// A payee region is required.
        /// </summary>
        public readonly static CMResult E_Transaction_Payee_Region_Required = new CMResult(ERROR + TRANSACTION + 16, "A payee region is required.");
        /// <summary>
        /// A payer region is required.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Region_Required = new CMResult(ERROR + TRANSACTION + 17, "A payer region is required.");
        /// <summary>
        /// The payee region is read-only.
        /// </summary>
        public readonly static CMResult E_Transaction_Payee_Region_Is_Readonly = new CMResult(ERROR + TRANSACTION + 18, "The payee region is read-only.");
        /// <summary>
        /// The payer region is read-only.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Region_Is_Readonly = new CMResult(ERROR + TRANSACTION + 19, "The payer region is read-only.");
        /// <summary>
        /// The payer status must be set to Accept during initial creation.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Accept_Status_Required = new CMResult(ERROR + TRANSACTION + 20, "The payer status must be set to Accept during initial creation.");
        /// <summary>
        /// The payee status must not be set without the payee's signature.
        /// </summary>
        public readonly static CMResult E_Transaction_Payee_Status_Invalid = new CMResult(ERROR + TRANSACTION + 21, "The payee status must not be set without the payee's signature.");
        /// <summary>
        /// The new payee status value is not permitted, based on its previous status.
        /// </summary>
        public readonly static CMResult E_Transaction_Payee_Status_Change_Not_Allowed = new CMResult(ERROR + TRANSACTION + 22, "The new payee status value is not permitted, based on its previous status.");
        /// <summary>
        /// The new payer status value is not permitted, based on its previous status.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Status_Change_Not_Allowed = new CMResult(ERROR + TRANSACTION + 23, "The new payee status value is not permitted, based on its previous status.");
        /// <summary>
        /// The payee and payer must be different accounts.
        /// </summary>
        public readonly static CMResult E_Transaction_Payer_Payee_Must_Differ = new CMResult(ERROR + TRANSACTION + 24, "The payee and payer must be different accounts.");
        /// <summary>
        /// The payee and payer tags must be no more than 48 UTF8 bytes in length.
        /// </summary>
        public readonly static CMResult E_Transaction_Tag_Too_Long = new CMResult(ERROR + TRANSACTION + 25, "The payee and payer tags must be no more than 48 UTF8 bytes in length.");
        /// <summary>
        /// The memo must be no more than 48 UTF8 bytes in length.
        /// </summary>
        public readonly static CMResult E_Transaction_Memo_Too_Long = new CMResult(ERROR + TRANSACTION + 26, "The memo must be no more than 48 UTF8 bytes in length.");

        #endregion

        #region Votes
        /// <summary>
        /// The vote account ID was not found.
        /// </summary>
        public readonly static CMResult E_Vote_Account_Not_Found = new CMResult(ERROR + VOTE + 0, "The vote account ID was not found.");

        /// <summary>
        /// The vote's signature is invalid.
        /// </summary>
        public readonly static CMResult E_Vote_Signature_Error = new CMResult(ERROR + VOTE + 1, "The vote's signature is invalid.");
        /// <summary>
        /// XXXX
        /// </summary>
        public readonly static CMResult E_Vote_XXXX = new CMResult(ERROR + VOTE + 2, "XXXX");

        /// <summary>
        /// Created UTC is too far ahead of the server's current time.
        /// </summary>
        public readonly static CMResult E_Vote_Created_Utc_Out_Of_Range = new CMResult(ERROR + VOTE + 3, "Created UTC is too far ahead of the server's current time.");
        /// <summary>
        /// Created UTC is read-only.
        /// </summary>
        public readonly static CMResult E_Vote_Created_Utc_Is_Readonly = new CMResult(ERROR + VOTE + 4, "Created UTC is read-only.");
        /// <summary>
        /// Updated UTC is too far ahead of the server's current time.
        /// </summary>
        public readonly static CMResult E_Vote_Updated_Utc_Out_Of_Range = new CMResult(ERROR + VOTE + 5, "Updated UTC is too far ahead of the server's current time.");
        /// <summary>
        /// The vote Updated UTC is out-dated. A newer copy exists.
        /// </summary>
        public readonly static CMResult E_Vote_Updated_Utc_Is_Old = new CMResult(ERROR + VOTE + 6, "The vote Updated UTC is out-dated. A newer copy exists.");

        #endregion
    }
}
