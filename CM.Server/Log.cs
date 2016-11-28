#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;

namespace CM.Server {

    public enum LogLevel {
        INFO = 0,
        WARN = 1,
        FAULT = 2
    }

    public enum LogSource {
        UNKNOWN = -1,
        SERVER = 0,
        DHT = 1,
        STORE,
        SYNC,
        HTTP,
        REPORT,
        DNS,
        QOS
    }

    public class Log {
        private Server _Owner;

        public Log(Server owner) {
            _Owner = owner;
        }

        public Action<Server, LogSource, LogLevel, string> Sink { get; set; }

        public void Write(object sender, LogLevel level, string message, params object[] args) {
            var del = Sink;
            if (del == null)
                return;
            var src = (sender is Server) ? LogSource.SERVER
                : sender is DistributedHashTable ? LogSource.DHT
                : sender is Storage ? LogSource.STORE
                : sender is SynchronisationManager ? LogSource.SYNC
                : sender is SslWebServer ? LogSource.HTTP
                : sender is AuthoritativeDomainReporter ? LogSource.REPORT
                : sender is UntrustedNameServer ? LogSource.DNS
                : sender is AttackMitigation.IPStat ? LogSource.QOS
                : LogSource.UNKNOWN;
            del(_Owner, src, level, String.Format(message, args));
        }
    }
}