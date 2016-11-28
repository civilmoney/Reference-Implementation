#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM {

    public class MessageValueException : Exception {

        public MessageValueException(string fieldName)
            : base(fieldName + " is invalid") {
        }
    }
}