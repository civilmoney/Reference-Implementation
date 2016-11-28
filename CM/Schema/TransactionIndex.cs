#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Schema {

    /// <summary>
    /// A TransactionIndex is the single-line data returned during a LIST query. As well, each DHT
    /// peer keeps these in each payee/payer/region container which allows for faster queries.
    /// </summary>
    public class TransactionIndex {
        public decimal Amount;
        public DateTime CreatedUtc;
        public string ID;
        public string Payee;
        public string PayeeRegion;
        public PayeeStatus PayeeStatus;
        public string Payer;
        public string PayerRegion;
        public PayerStatus PayerStatus;
        public DateTime UpdatedUtc;

        public TransactionIndex() {
        }

        public TransactionIndex(Transaction t) {
            ID = t.ID;
            CreatedUtc = t.CreatedUtc;
            UpdatedUtc = t.UpdatedUtc;
            Payee = t.PayeeID;
            Payer = t.PayerID;
            Amount = t.Amount;
            PayerStatus = t.PayerStatus;
            PayeeStatus = t.PayeeStatus;
            PayerRegion = t.PayerRegion;
            PayeeRegion = t.PayeeRegion;
        }

        public TransactionIndex(string data) {
            Parse(data);
        }

        public void Parse(string data) {
            var ar = data.Split(' ');
            CreatedUtc = Helpers.DateFromISO8601(ar[0]);
            Payee = ar[1];
            Payer = ar[2];
            ID = ar[0] + " " + ar[1] + " " + ar[2];
            Amount = decimal.Parse(ar[3]);
            UpdatedUtc = Helpers.DateFromISO8601(ar[4]);
            PayeeStatus = (PayeeStatus)int.Parse(ar[5]);
            PayerStatus = (PayerStatus)int.Parse(ar[6]);
            PayeeRegion = ar[7];
            PayerRegion = ar[8];
        }

        /// <summary>
        /// The format of a TransactionIndex string is:
        /// {ID} + " " + {Amount} + " " + {Updated Utc} + " " + {Payee Status Byte}
        /// + " " + {Payer Status Byte} + " " + {Payee Region} + " " + {Payer Region}
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return Helpers.DateToISO8601(CreatedUtc)
                + " " + Payee
                + " " + Payer
                + " " + Amount
                + " " + Helpers.DateToISO8601(UpdatedUtc)
                + " " + ((int)PayeeStatus)
                + " " + ((int)PayerStatus)
                + " " + PayeeRegion
                + " " + PayerRegion
                ;
        }
    }
}