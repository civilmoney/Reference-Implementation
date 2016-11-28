#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.Text;

namespace CM.Schema {

    public enum PayeeStatus : byte {
        NotSet = 0,
        Accept = 1,
        Decline = 2,
        Refund = 3,
    }

    public enum PayerStatus : byte {
        NotSet = 0,
        Accept = 1,
        Dispute = 2,
        Cancel = 3
    }

    /// <summary>
    /// Describes a digitally signed money transfer between accounts.
    /// </summary>
    public class Transaction : Message, IStorable {

        public Transaction() : base() {
        }

        public Transaction(string payload) : base(payload) {
        }

        /// <summary>
        /// Gets the transaction globally unique identifier which consists of
        /// ISO8601(CreatedUtc) + " " + PayeeID + " " + PayerID
        /// </summary>
        public string ID {
            get {
                return Helpers.DateToISO8601(CreatedUtc) + " " + PayeeID + " " + PayerID;
            }
        }

        #region IStorable

        public int ConsensusCount { get; set; }

        public bool ConsensusOK { get { return ConsensusCount >= Constants.MinimumNumberOfCopies; } }

        public string Path {
            get {
                // 2016-06-05T09:48:12 account1 account2

                return "TRANS/" + ID;
            }
        }

        public string PayeePath {
            get {
                return "ACCNT/" + PayeeID + "/" + Path;
            }
        }

        public string PayerPath {
            get {
                return "ACCNT/" + PayerID + "/" + Path;
            }
        }

        public string RegionPath {
            get {
                return "REGION/" + PayeeRegion + "/" + Path;
            }
        }

        #endregion IStorable

        /// <summary>
        /// Currently always 1
        /// </summary>
        public uint APIVersion { get { return Values.Get<uint>("VER"); } set { Values.Set<uint>("VER", value); } }

        /// <summary>
        /// Creation time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime CreatedUtc { get { return Values.Get<DateTime>("UTC"); } set { Values.Set<DateTime>("UTC", value); } }

        /// <summary>
        /// The amount of the transaction up to 6 decimal places.
        /// </summary>
        public decimal Amount { get { return Values.Get<decimal>("AMNT"); } set { Values.Set<decimal>("AMNT", value); } }

        /// <summary>
        /// A reader-friendly plain-text UTF8 description or note about the transaction. Maximum allowed length is 255 UTF-8 bytes.
        /// Trusted implementations which generate HTML must HTML-encode all memos.
        /// </summary>
        public string Memo { get { return Values.Get<string>("MEMO"); } set { Values.Set<string>("MEMO", value); } }

        /// <summary>
        /// The recipient/payee ID
        /// </summary>
        public string PayeeID { get { return Values.Get<string>("PYE-ID"); } set { Values.Set<string>("PYE-ID", value); } }

        /// <summary>
        /// The Payee's region at time of transaction.
        /// </summary>
        public string PayeeRegion { get { return Values.Get<string>("PYE-REG"); } set { Values.Set<string>("PYE-REG", value); } }

        /// <summary>
        /// An optional electronic tag which can only be set by the recipient before signing. Up to 48 UTF8 bytes.
        /// </summary>
        public string PayeeTag { get { return Values.Get<string>("PYE-TAG"); } set { Values.Set<string>("PYE-TAG", value); } }

        /// <summary>
        /// Last payee update time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime PayeeUpdatedUtc { get { return Values.Get<DateTime>("PYE-UTC"); } set { Values.Set<DateTime>("PYE-UTC", value); } }

        /// <summary>
        /// Accept, Decline or Refund
        /// </summary>
        public PayeeStatus PayeeStatus { get { return Values.Get<PayeeStatus>("PYE-STAT"); } set { Values.Set<PayeeStatus>("PYE-STAT", value); } }

        /// <summary>
        /// Signature of the payee. Basically confirms that 'yes, I recognise the sender and I'm expecting this payment.'
        /// </summary>
        public byte[] PayeeSignature { get { return Values.Get<byte[]>("PYE-SIG"); } set { Values.Set<byte[]>("PYE-SIG", value); } }

        /// <summary>
        /// The sender/payer ID
        /// </summary>
        public string PayerID { get { return Values.Get<string>("PYR-ID"); } set { Values.Set<string>("PYR-ID", value); } }

        /// <summary>
        /// The payer's region at time of transaction.
        /// </summary>
        public string PayerRegion { get { return Values.Get<string>("PYR-REG"); } set { Values.Set<string>("PYR-REG", value); } }

        /// <summary>
        /// An optional electronic tag which can only be set by the payer before signing. Up to 48 UTF8 bytes.
        /// </summary>
        public string PayerTag { get { return Values.Get<string>("PYR-TAG"); } set { Values.Set<string>("PYR-TAG", value); } }

        /// <summary>
        /// Last payer update time (ISO-8601 UTC date string)
        /// </summary>
        public DateTime PayerUpdatedUtc { get { return Values.Get<DateTime>("PYR-UTC"); } set { Values.Set<DateTime>("PYR-UTC", value); } }

        /// <summary>
        /// Accept, Dispute
        /// </summary>
        public PayerStatus PayerStatus { get { return Values.Get<PayerStatus>("PYR-STAT"); } set { Values.Set<PayerStatus>("PYR-STAT", value); } }

        /// <summary>
        /// Signature of the payer. Confirms that 'yes, I'm authorising this payment'.
        /// </summary>
        public byte[] PayerSignature { get { return Values.Get<byte[]>("PYR-SIG"); } set { Values.Set<byte[]>("PYR-SIG", value); } }

        /// <summary>
        /// Transaction signing data for the PAYER/SENDER consists of:
        /// - CreatedUtc      25 UTF-8 bytes
        /// - Amount          N UTF-8 bytes, value formatted with 6 decimal places, so 0 = '0.000000'. Decimal symbol must be a period.
        /// - Payee ID        N UTF-8 bytes
        /// - Payer ID        N UTF-8 bytes
        /// - Memo            N UTF-8 bytes
        /// - Payer Updated   8 bytes uimsbf
        /// - Payer Tag       N bytes
        /// - Payer Response  1 byte
        /// - Payer Region    N UTF-8 bytes
        /// </summary>
        public byte[] GetPayerSigningData() {
            var ar = new List<byte>();
            // Common fields
            ar.AddRange(Encoding.UTF8.GetBytes(Helpers.DateToISO8601(CreatedUtc)));
            ar.AddRange(Encoding.UTF8.GetBytes(Amount.ToString("0.000000")));
            ar.AddRange(Encoding.UTF8.GetBytes(PayeeID));
            ar.AddRange(Encoding.UTF8.GetBytes(PayerID));
            if (Memo != null) ar.AddRange(Encoding.UTF8.GetBytes(Memo));
            // Payer-specific fields to sign
            ar.AddRange(Encoding.UTF8.GetBytes(Helpers.DateToISO8601(PayerUpdatedUtc)));
            if (PayerTag != null) ar.AddRange(Encoding.UTF8.GetBytes(PayerTag));
            ar.Add((byte)PayerStatus);
            if (PayerRegion != null)
                ar.AddRange(Encoding.UTF8.GetBytes(PayerRegion));
            return ar.ToArray();
        }

        /// <summary>
        /// Transaction signing data for the PAYEE/RECEIVER consists of:
        /// - CreatedUtc      25 UTF-8 bytes
        /// - Amount          N UTF-8 bytes, value formatted with 6 decimal places, so 0 = '0.000000'. Decimal symbol must be a period.
        /// - Payee ID        N UTF-8 bytes
        /// - Payer ID        N UTF-8 bytes
        /// - Memo            N UTF-8 bytes
        /// - Payee Updated   8 bytes uimsbf
        /// - Payee Tag       N bytes
        /// - Payee Response  1 byte
        /// - Payee Region    N UTF-8 bytes
        /// </summary>
        public byte[] GetPayeeSigningData() {
            var ar = new List<byte>();
            // Common fields
            ar.AddRange(Encoding.UTF8.GetBytes(Helpers.DateToISO8601(CreatedUtc)));
            ar.AddRange(Encoding.UTF8.GetBytes(Amount.ToString("0.000000")));
            ar.AddRange(Encoding.UTF8.GetBytes(PayeeID));
            ar.AddRange(Encoding.UTF8.GetBytes(PayerID));
            if (Memo != null) ar.AddRange(Encoding.UTF8.GetBytes(Memo));
            // Payee-specific fields to sign
            ar.AddRange(Encoding.UTF8.GetBytes(Helpers.DateToISO8601(PayeeUpdatedUtc)));
            if (PayeeTag != null) ar.AddRange(Encoding.UTF8.GetBytes(PayeeTag));
            ar.Add((byte)PayeeStatus);
            if (PayeeRegion != null)
                ar.AddRange(Encoding.UTF8.GetBytes(PayeeRegion));
            return ar.ToArray();
        }

        /// <summary>
        /// gets the MAX of PAYEE-UTC and PAYER-UTC
        /// </summary>
        public DateTime UpdatedUtc {
            get {
                return (PayeeUpdatedUtc > PayerUpdatedUtc ? PayeeUpdatedUtc : PayerUpdatedUtc);
            }
        }
    }
}