#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Cryptography {

    public class RNG {
#if JAVASCRIPT
        public static void RandomBytes(byte[] b) {
            // Only use a proper browser RNG
            Bridge.Script.Write(@"
if(typeof(window.Uint8Array)=='function'){
    var tmp = new Uint8Array(b.length);
    (window.crypto || window.msCrypto).getRandomValues(tmp);
    for(var i=0;i<tmp.length;i++)
    b[i] = tmp[i];
} else {
    //throw ""unsupported web browser"";
");
            try {
                var r = new Bridge.Html5.XMLHttpRequest();
                var host = Constants.Seeds[0].Domain;
#if DEBUG
                host = DNS.EndpointToUntrustedDomain(host, true);
#endif
                r.Open("GET", "https://" + host + "/api/get-random?length=" + b.Length, false); // we need to be synchronous here, unfortunately.
                r.Send();
                if (r.Status != 200)
                    throw new Exception("Unable to get 'last resort' random bytes.");
                var res = Convert.FromBase64String(r.ResponseText);
                for (int i = 0; i < b.Length; i++)
                    b[i] = res[i];
            } catch (Exception ex) {
                Console.Write(ex.ToString());
                Bridge.Html5.Window.Alert("Unfortunately this web browser is not compatible with Civil Money. Please try a recent version of Internet Explorer, Chrome, FireFox or a more recent smart phone device.");
            }
            Bridge.Script.Write(@"
}
");
        }
#else

        /// <summary>
        /// Offloads to native mobile/windows RNG libraries
        /// </summary>
        public static Action<byte[]> RandomBytes;

#endif
    }
}