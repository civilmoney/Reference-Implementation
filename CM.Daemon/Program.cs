#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using System;
using System.IO;
using System.Linq;

namespace CM.Daemon {

    /// <summary>
    /// The CM Daemon is responsible for launching, monitoring and upgrading the main 'CM.Server.dll' server process. Under Windows it can
    /// also install itself as an NT service.
    /// </summary>
    public class Program {

        private const string UNIX_SYSTEMD_CONFIG_TEMPLATE = @"
[Unit]
Description=Civil Money
DefaultDependencies=no
Wants=network.target
After=network.target

[Service]
User={2}
ExecStart={0}
WorkingDirectory={1}
Restart=always
RestartSec=30
KillSignal=SIGINT
SyslogIdentifier=civilmoney

[Install]
WantedBy=multi-user.target
";

        private static string DotNetRuntimePath {
            get {
                var dotNetDir = new DirectoryInfo(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory());
                //eg: C:\Program Files\dotnet\shared\Microsoft.NETCore.App\2.0.0\
                dotNetDir = dotNetDir.Parent.Parent.Parent; //C:\Program Files\dotnet\
                return Path.Combine(dotNetDir.FullName, "dotnet");
            }
        }

        private static string SystemDFilePath {
            get {
                return "/etc/systemd/system/civilmoney.service";
            }
        }

        public static void Main(string[] args) {
            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            if (args.Contains("--service")) {
                // Run as service
                var serviceHost = new Win32Service(new NTServiceWrapper());
                serviceHost.Run();
            } else if (args.Contains("--install")) {
                if (isWindows) {
                    TryInstallWindowsNTService();
                } else {
                    var user = args.FirstOrDefault(x => x.StartsWith("user:", StringComparison.OrdinalIgnoreCase));
                    if (String.IsNullOrEmpty(user))
                        user = Environment.UserName;
                    else
                        user = user.Substring("user:".Length);
                    TryInstallSystemD(user);
                }
            } else if (args.Contains("--uninstall")) {
                if (isWindows) {
                    string err;
                    if (Win32Service.TryDeleteService("CivilMoney", out err))
                        Console.WriteLine("The Civil Money Windows Service has been deleted successfully.");
                    else
                        Console.WriteLine("The Civil Money Windows Service could not be deleted  - " + err);
                } else {
                    if (File.Exists(SystemDFilePath)) {
                        System.IO.File.Delete(SystemDFilePath);
                        Console.WriteLine("The systemd service file has been deleted successfully.");
                    } else {
                        Console.WriteLine("The systemd service file is not present. No uninstall necessary.");
                    }
                }
            } else {
                // Command line / Debugging / Linux ends up here.
                var token = new System.Threading.CancellationTokenSource();
                var ctrl = new ServerController();
                ctrl.Start(token.Token);
                var evt = new System.Threading.ManualResetEventSlim();
                Console.CancelKeyPress += (sender, e) => {
                    Console.WriteLine("Exiting...");
                    ctrl.Stop();
                    token.Cancel();
                    evt.Set();
                };
                evt.Wait();
            }
        }

        [System.Runtime.InteropServices.DllImport("libc")]
        private static extern int chmod(string path, uint mode);

        private static void TryInstallSystemD(string runAsUser) {
            var systemdFile = SystemDFilePath;

            if (Directory.Exists(Path.GetDirectoryName(systemdFile))) {
                var systemdConfig = String.Format(UNIX_SYSTEMD_CONFIG_TEMPLATE,
                    $@"{DotNetRuntimePath} CM.Daemon.dll",
                    AppContext.BaseDirectory,
                    runAsUser
                    ).Trim().Replace("\r", "");
                try {
                    System.IO.File.WriteAllText(systemdFile, systemdConfig);
                } catch (Exception ex) {
                    Console.WriteLine($"Unable to write to systemd folder - {ex.Message}. May need to run as elevated.");
                    return;
                }
                chmod(systemdFile, 420);//644 in octal
                Console.WriteLine("The Civil Money systemd config file has been installed successfully. ");
                Console.WriteLine("Enter 'sudo systemctl enable civilmoney.service' to enable it, followed by 'systemctl start civilmoney'.");
                Console.WriteLine("Remember to check 'journalctl -fu civilmoney' for errors.");
            } else {
                Console.WriteLine("systemd is not present.");
            }
        }

        private static void TryInstallWindowsNTService() {
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
        }
        private class NTServiceWrapper : IWin32Service {
            private ServerController _Controller;
            private Action _OnStopped;
            private System.Threading.CancellationTokenSource _Token;
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