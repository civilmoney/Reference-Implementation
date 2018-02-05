#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

#if DEBUG
#define TEST
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace CM.Server
{
    /// <summary>
    /// This is the server entry point.
    /// </summary>
    public class Program {

        public static readonly string BaseDirectory;

        static Program() {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            BaseDirectory = dir.Parent.FullName;
        }
        
#if TEST
        const int NumberOfTestServers = 20;

        public static void Main(string[] args) {
           
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            Debug.WriteLine("=========");
            Debug.WriteLine("You can visit the web server at https://" + Constants.Seeds[0].Domain);
            Debug.WriteLine("Hit Enter to exit, or 'k' to kill a random peer.");
            Debug.WriteLine("=========");

            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            // Keep large test data out of the Visual Studio/SVN directory tree.
            var folder = System.IO.Path.Combine(dir.Root.FullName, "test-data");
            Console.WriteLine("Data folder: " + folder);
            var servers = new List<CM.Server.Server>();
            CM.Server.Server toStop = null;
            for (int i = 0; i < NumberOfTestServers; i++) {
                var s = new CM.Server.Server();
                s.Log.Sink = OnLog;
                s.Configuration.Port = 8000 + i;
                s.Configuration.DataFolder = System.IO.Path.Combine(folder, s.Configuration.Port.ToString());

                // Test authoritative features on first server only.
                s.Configuration.EnableAuthoritativeDomainFeatures = i == 0;
                //s.Configuration.EnablePort80Redirect = i == 0;
              
                s.Start();
                if (i == 0)
                    toStop = s;
                servers.Add(s);
            }

            
            CommandLoop(servers);
        }
#else

        public static void Main(string[] args) {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;

            var s = new CM.Server.Server();
            try {
                string appConfig = System.IO.Path.Combine(BaseDirectory, "settings.json");
                if (!System.IO.File.Exists(appConfig))
                    throw new Exception("Expecting configuration file: "+ appConfig);
                var json = System.IO.File.ReadAllText(appConfig);
                s.Configuration = new ServerConfiguration(json);
                s.Log.Sink = OnLog;
                s.Start();
                CommandLoop(null);
                s.Stop();
            } catch (Exception ex) {
                OnLog(s, LogSource.SERVER, LogLevel.FAULT, "Launch failure: " + ex.ToString());
            }
        }
#endif

        static void CommandLoop(List<CM.Server.Server> debugServers) {
            string line;
            while ((line = Console.ReadLine()).Length > 0) {
                switch (line) {
#if TEST
                    case "k": {
                            var rnd = new Random();
                            if (debugServers.Count > 0) {
                                var i = rnd.Next(0, debugServers.Count - 1);
                                Console.WriteLine("Stopping " + debugServers[i].Configuration.Port);
                                debugServers[i].Stop();
                                debugServers.RemoveAt(i);
                            }
                            if (debugServers.Count==0)
                                Console.WriteLine("All gone");
                            else
                                Console.WriteLine(debugServers.Count+" left");
                        }
                        break;
#endif
                    default:
                        Console.WriteLine("Unrecognised command '" + line + "'");
                        break;
                }
            }
        }

        static void OnLog(CM.Server.Server s,
            CM.Server.LogSource src,
            CM.Server.LogLevel lvl, string msg) {
            var line = String.Format("[{0}, {1}, {2}] {3}", s.Configuration.Port, lvl, src, msg);
            Debug.WriteLine(line);
            Console.WriteLine(line);
        }
     

    }
}
