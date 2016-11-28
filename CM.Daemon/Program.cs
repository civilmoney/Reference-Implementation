#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CM.Daemon {
    /// <summary>
    /// The CM Daemon is responsible for launching, monitoring and upgrading the main 'CM.Server.dll' server process. Under Windows it can
    /// also install itself as an NT service.
    /// </summary>
    public class Program {

        public static void Main(string[] args) {
            if (args.Contains("--service")) {
                // Run as service
                var serviceHost = new Win32Service(new NTServiceWrapper());
                serviceHost.Run();
            } else if (args.Contains("--install")) {
                // Install Windows NT Service
                // To install by hand you would go: 
                // > sc.exe create CivilMoney DisplayName="Civil Money" binpath="C:\Program Files\dotnet\dotnet.exe \"C:\\Path To\CM.Daemon.dll\" --service" 
                // The Win32Service can make it a bit simpler.
                string err;
                string cmd = String.Format(@"C:\Program Files\dotnet\dotnet.exe ""{0}\CM.Daemon.dll"" --service", AppContext.BaseDirectory);
                if (Win32Service.TryInstallService("CivilMoney", "Civil Money", "A server for the https://civil.money movement.", cmd, null, null, out err))
                    Console.WriteLine("The Civil Money Windows Service has been installed successfully.");
                else
                    Console.WriteLine("The Civil Money Windows Service could not be installed  - " + err);

            } else if (args.Contains("--uninstall")) {
                // Uninstalls a Windows NT Service
                // > sc.exe delete CivilMoney
                string err;
                if (Win32Service.TryDeleteService("CivilMoney", out err))
                    Console.WriteLine("The Civil Money Windows Service has been deleted successfully.");
                else
                    Console.WriteLine("The Civil Money Windows Service could not be deleted  - " + err);

            } else {
                // Command line / Debugging / Linux ends up here.
                var token = new System.Threading.CancellationTokenSource();
                var ctrl = new ServerController();
                ctrl.Start(token.Token);

                Console.ReadLine();
                Console.WriteLine("Exiting...");
                ctrl.Stop();
                token.Cancel();
            }
        }

        class NTServiceWrapper : IWin32Service {
            ServerController _Controller;
            System.Threading.CancellationTokenSource _Token;
            Action _OnStopped;
            public string ServiceName {
                get {
                    return "Civil Money";
                }
            }

            public void Start(string[] startupArguments, Action serviceStoppedCallback) {
                if (_Controller != null) {
                    Stop();
                }
                _Controller = new ServerController();
                _Token = new System.Threading.CancellationTokenSource();
                _Controller.Start(_Token.Token);
                _OnStopped = serviceStoppedCallback;
            }

            public void Stop() {
                _Controller.Stop();
                _Token.Cancel();
                _Controller = null;
                _Token = null;
                _OnStopped?.Invoke();
            }
        }

    }


}
