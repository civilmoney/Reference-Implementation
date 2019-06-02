using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CM.Server {
    [Flags]
    public enum DeviceFlags {
        Unknown = 0,

        // Type
        Desktop = 1 << 0,

        Mobile = 1 << 1,
        Tablet = 1 << 2,
        SmartTV = 1 << 3,
        Bot = 1 << 4,

        // OS
        Windows = 1 << 8,

        Apple = 1 << 9,
        Linux = 1 << 10,
        Android = 1 << 11,
        ChomeOS = 1 << 12,

        // Clients
        IE = 1 << 16,

        Edge = 1 << 17,
        Chrome = 1 << 18,
        Opera = 1 << 19,
        Firefox = 1 << 20,
        Safari = 1 << 21
    }


    public static class Extensions {
        public static DeviceFlags DetermineDevice(this Microsoft.AspNetCore.Http.HttpContext context) {
            context.Request.Headers.TryGetValue("User-Agent", out var agentVals);
            var agent = (string)agentVals;
            if (String.IsNullOrEmpty(agent))
                return DeviceFlags.Unknown;
            var device = agent.IndexOf("Windows NT") > -1 ? DeviceFlags.Windows | DeviceFlags.Desktop
             : agent.IndexOf("iPhone") > -1 ? DeviceFlags.Apple | DeviceFlags.Mobile
             : agent.IndexOf("iPad") > -1 ? DeviceFlags.Apple | DeviceFlags.Tablet
             : agent.IndexOf("Windows Phone") > -1 ? DeviceFlags.Windows | DeviceFlags.Mobile
             : agent.IndexOf("Android") > -1 ? DeviceFlags.Android | DeviceFlags.Mobile
             : agent.IndexOf("OS X") > -1 || agent.IndexOf("Macintosh") > -1 ? DeviceFlags.Apple | DeviceFlags.Desktop
             : agent.IndexOf("Linux") > -1 ? DeviceFlags.Linux | DeviceFlags.Desktop
             : agent.IndexOf("CrOS") > -1 ? DeviceFlags.ChomeOS | DeviceFlags.Tablet
             : agent.IndexOf("bot", StringComparison.OrdinalIgnoreCase) > -1 ? DeviceFlags.Bot
             : DeviceFlags.Unknown;

            if (device == DeviceFlags.Unknown) {
                return device;
            }

            device |= agent.IndexOf("Trident/") > -1 ? DeviceFlags.IE
                : agent.IndexOf("Edge/") > -1 ? DeviceFlags.Edge
                : agent.IndexOf("Chrome/") > -1 ? DeviceFlags.Chrome
                : agent.IndexOf("OPR/") > -1 ? DeviceFlags.Opera
                : agent.IndexOf("Safari/") > -1 ? DeviceFlags.Safari
                : agent.IndexOf("Firefox/") > -1 ? DeviceFlags.Firefox
                : 0;
            return device;
        }

        public static System.Net.IPAddress GetIPAddress(this Microsoft.AspNetCore.Http.HttpContext context, ServerConfiguration config) {
            System.Net.IPAddress ip;
            string fwd = context.Request.Headers["X-Forwarded-For"];
            if (fwd == null
                || context.Connection.RemoteIpAddress.ToString() != config.PermittedForwardingProxyIP
                || !System.Net.IPAddress.TryParse(fwd, out ip)) {
                ip = context.Connection.RemoteIpAddress;
            }
            return ip;
        }

        // Useful async enumeration pattern from https://blogs.msdn.microsoft.com/pfxteam/2012/03/04/implementing-a-simple-foreachasync/

        public static Task ForEachAsync<TSource, TResult>(
            this IEnumerable<TSource> source, int maxConcurrency,
            Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor) {
            // SemaphoreSlim.WaitHandle is never created so dispose is a no-op and OK to let 
            // GC clean up at some later time.
            var limit = new System.Threading.SemaphoreSlim(maxConcurrency, maxConcurrency);
            return Task.WhenAll(
                    from item in source
                    select ProcessAsync(item, taskSelector, resultProcessor, limit));
        }

        private static async Task ProcessAsync<TSource, TResult>(
            TSource item,
            Func<TSource, Task<TResult>> taskSelector, Action<TSource, TResult> resultProcessor,
             System.Threading.SemaphoreSlim limit) {
            TResult result = await taskSelector(item);
            await limit.WaitAsync();
            try {
                resultProcessor(item, result);
            } finally {
                limit.Release();
            }
        }
    }
}
