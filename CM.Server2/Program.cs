using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CM.Server {
    public class Program {


        public static void Main(string[] args) {

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;


#if DEBUG
            ApiController.EnableJavascriptAppDebugMode = true;
            for (int i = 0; i < 4; i++) {
                RunTestServer(8000 + i);
            }
            Console.ReadLine();
#else
            var config = ServerConfiguration.Load();
            var server = new Server();
            server.Run(config);
#endif  
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            System.IO.File.WriteAllText(Path.Combine(ServerConfiguration.BaseDirectory, "crash.txt"), e.ExceptionObject.ToString());
        }

        static async void RunTestServer(int port) {
            await Task.Delay(1).ConfigureAwait(false);
            var server = new Server();
            server.Run(new ServerConfiguration() {
                IP = "0.0.0.0",
                Port = port,
                DataFolder = "test-data-" + port,
            });
        }
      
    }
}
