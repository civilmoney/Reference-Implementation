using CM.Schema;
using CM.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CM.AttackClient {

    /// <summary>
    /// The purpose of the CM.AttackClient is to stress test the CM.Server. Please don't be a douche
    /// and run this on the production network. Whilst the whole point is that the production network
    /// should be impervious to attacks simulated here, it adds an unnecessary burden during Civil
    /// Money's initial pilot phase having only a small number of servers.
    /// </summary>
    public class Program {
        public static readonly string BaseDirectory;
        private const int PortRangeEnd = 8010;
        private const int PortRangeStart = 8000;
        private const string VALID_ACCOUNT_CHARS = @"-0987654321abcdefghijklmnopqrstuvwxyzالمالالمدنيכסףהאזרחיनागरिकपैसेгражданскипари民间资金民間資金íüαστικέςχρήματα市民のお金시민돈āųųųųėėċċċċċپولمدنیąгражданскоеденьгиเงินทางแพ่งцивільнігрошіسولقمtiềndânsự";
        private static object _LogSync = new object();
        private static Random _Rnd = new Random();
        private static int _Lines = 3; // Console window line overwriting

        static Program() {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            BaseDirectory = dir.Parent.FullName;
        }

        public static Account CreateAccount() {
            int len = _Rnd.Next(3, 40);
            string id = "";
            for (int i = 0; i < len && System.Text.Encoding.UTF8.GetByteCount(id) < 45; i++) {
                id += VALID_ACCOUNT_CHARS[_Rnd.Next(i == 0 ? 11 : 0, VALID_ACCOUNT_CHARS.Length)];
            }
            System.Diagnostics.Debug.Assert(Helpers.IsIDValid(id), "Account ID '" + id + "' doesn't pass.");

            var a = new Account() {
                ID = id,
                APIVersion = Constants.APIVersion,
                Iso31662Region = ISO31662.Values[_Rnd.Next(0, ISO31662.Values.Length - 1)].ID,
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow
            };
            a.ChangePasswordAndSign(new AsyncRequest<PasswordRequest>() {
                Item = new PasswordRequest() {
                    NewPass = id
                }
            }, CM.Server.CryptoFunctions.Identity);
            return a;
        }

        public static void Main(string[] args) {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            while (true) {
                _Lines = 3;
                Console.WriteLine(@"
Enter attack command:
    0. Exit
    1. Connection Flood
    2. Generate Accounts
    3. Upload Accounts
");
                Console.Write("Command: ");
                string cmd = Console.ReadLine();
                switch (cmd.Trim()) {
                    case "1":
                        ConnectionFlood();
                        break;

                    case "2":
                        GenerateAccounts();
                        break;

                    case "3":
                        Task.Run(async () => { await UploadAccounts(0); }).Wait();
                        break;

                    case "4":
                        // TODO: transaction flood
                        break;

                    case "0":
                        // Exit
                        return;

                    default:
                        Console.WriteLine("Unknown command '" + cmd + "'");
                        break;
                }
            }
        }

        private static void ConnectionFlood() {
            Console.WriteLine("Connection Flood started...");
            var rnd = new Random();
            int max = 100;
            int done = 0;
            int worked = 0;
            Parallel.For(0, max, async (i) => {
                try {
                    using (var peer = new PeerConnection(new IPEndPoint(IPAddress.Parse("127.0.0.1"), rnd.Next(PortRangeStart, PortRangeEnd)))) {
                        if (await peer.ConnectAsync()) {
                            var m = await peer.Connection.SendAndReceive("PING", new PingRequest());
                            var ping = m.Cast<PingResponse>();
                            if (ping.Response.Code != CMResult.S_OK)
                                Console.WriteLine(i + ": " + ping.Response.Code);
                            else
                                System.Threading.Interlocked.Increment(ref worked);
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine(i + ": " + ex.Message);
                }
                System.Threading.Interlocked.Increment(ref done);
                if (done == max) {
                    Console.WriteLine(String.Format("Connection Flood Finished. {0} of {1} connections weren't blocked.", worked, max));
                }
            });
        }
        /// <summary>
        /// Helper function for locating an account on the network.
        /// </summary>
        /// <param name="peers">Pointer to an empty list to populate with found endpoints.</param>
        /// <param name="failingCopies">Pointer to an empty list to populate with failing copy numbers.</param>
        /// <param name="id">The account ID to find.</param>
        private static async Task FindResponsiblePeersForAccount(List<string> peers, List<string> failingCopies, string id) {
            string[] copyIDs = new string[Constants.NumberOfCopies];
            for (int i = 0; i < copyIDs.Length; i++) {
                copyIDs[i] = "copy" + (i + 1) + id;
            }

            await copyIDs.ForEachAsync(2, async (copyID) => {
                var ep = new IPEndPoint(IPAddress.Parse("127.0.0.1"), _Rnd.Next(PortRangeStart, PortRangeEnd));
                using (var peer = new PeerConnection(ep)) {
                    if (await peer.ConnectAsync()) {
                        var m = await peer.Connection.SendAndReceive("FIND", new FindResponsiblePeerRequest() {
                            MaxHopCount = Constants.MaxHopCount,
                            DHTID = Helpers.DHT_ID(copyID)
                        });
                        return new Tuple<IPEndPoint, FindResponsiblePeerResponse>(ep, m.Cast<FindResponsiblePeerResponse>());
                    } else {
                        return new Tuple<IPEndPoint, FindResponsiblePeerResponse>(ep, null);
                    }
                }
            }, (copyID, res) => {
                if (res.Item2 != null
                    && res.Item2.Response.IsSuccessful) {
                    lock (peers) {
                        if (res.Item2.PeerEndpoint != null
                            && !peers.Contains(res.Item2.PeerEndpoint)) {
                            peers.Add(res.Item2.PeerEndpoint);
                        }
                    }
                } else {
                    lock (failingCopies)
                        failingCopies.Add(res.Item1 + ": " + copyID);
                }
            });
        }

        /// <summary>
        /// Pre-computes accounts into a text file, each account delimited with a \0.
        /// </summary>
        private static void GenerateAccounts(int numberOfAccounts = 100000) {
            var file = System.IO.Path.Combine(BaseDirectory, "generated-accounts") + ".txt";

            int done = 0;
            Console.Clear();
            Console.WriteLine("Generate " + numberOfAccounts.ToString("N0") + " Accounts started...");
            var fs = new System.IO.FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);

            // Read in the existing IDs so we don't make any duplicates.
            var s = new StringBuilder();
            byte[] b = new byte[4096];
            var offsets = new List<long>();
            offsets.Add(0);
            long cursor = 0;
            int read;
            var ids = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            while ((read = fs.Read(b, 0, b.Length)) != 0) {
                for (int i = 0; i < read; i++) {
                    cursor++;
                    if (b[i] == 0)
                        offsets.Add(cursor);
                }
            }
            fs.Position = 0;
            byte[] tmp = null;
            for (int i = 0; i < offsets.Count - 1; i++) {
                fs.Position = offsets[i];
                int len = (int)(offsets[i + 1] - offsets[i]);
                if (tmp == null || tmp.Length < len) {
                    tmp = new byte[len];
                }
                fs.Read(tmp, 0, len);
                string str = Encoding.UTF8.GetString(tmp, 0, len);
                ids[new Account(str).ID] = null;
            }

            fs.Position = fs.Length;

            // Generate random accounts
            Parallel.For(0, numberOfAccounts, (i) => {
                Account a = CreateAccount();
                if (ids.TryAdd(a.ID, null)) {
                    var data = a.ToContent();

                    lock (fs) {
                        fs.Write(data, 0, data.Length);
                        fs.WriteByte(0);
                        // Give up some CPU time to the OS (runs this utility quietly.)
                        System.Threading.Thread.Sleep(50);
                    }
                }

                if ((System.Threading.Interlocked.Increment(ref done) % 100) == 0) {
                    Console.SetCursorPosition(0, 1);
                    Console.WriteLine(" " + (done / (double)numberOfAccounts).ToString("P0"));
                }

                if (done == numberOfAccounts) {
                    fs.Dispose();
                    Console.WriteLine("Finished.");
                }
            });
        }

        private static void Log(string line) {
            lock (_LogSync) {
                Console.SetCursorPosition(0, ++_Lines);
                Console.WriteLine(line);
                System.Diagnostics.Debug.WriteLine(line);
            }
        }

        /// <summary>
        /// Stress tests pushing thousands of accounts onto the network.
        /// </summary>
        private static async Task UploadAccounts(int startAt = 0) {
            Console.Clear();
            Console.WriteLine("Push Accounts started...");
            var file = System.IO.Path.Combine(BaseDirectory, "generated-accounts") + ".txt";
            var fs = new System.IO.FileStream(file, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var s = new StringBuilder();
            byte[] b = new byte[4096];
            var offsets = new List<long>();
            offsets.Add(0);
            long cursor = 0;
            int read;
            var ids = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
            while ((read = fs.Read(b, 0, b.Length)) != 0) {
                for (int i = 0; i < read; i++) {
                    cursor++;
                    if (b[i] == 0)
                        offsets.Add(cursor);
                }
            }
            fs.Position = 0;
            byte[] tmp = null;
            var ar = new List<Account>(offsets.Count);
            for (int i = 0; i < offsets.Count - 1; i++) {
                fs.Position = offsets[i];
                int len = (int)(offsets[i + 1] - offsets[i]);
                if (tmp == null || tmp.Length < len) {
                    tmp = new byte[len];
                }
                fs.Read(tmp, 0, len);
                string str = Encoding.UTF8.GetString(tmp, 0, len);
                ar.Add(new Account(str));
            }
            Console.WriteLine(" pushing " + ar.Count.ToString("N0") + " accounts");
            for (int i = startAt; i < ar.Count; i++) {
                var a = ar[i];
                Console.SetCursorPosition(30, 0);
                Console.Write(i + "/" + ar.Count + " " + (i / (double)ar.Count).ToString("P0"));
                if (!await UploadOneAccount(a)) {
                    Log((i + 1).ToString() + " - not enough peers: " + a.ID);
                }
            }
        }

        /// <summary>
        /// Puts an account onto the network, following the standard API procedure
        /// of FindResponsiblePeers and then calling PUT, QUERY-COMMIT and COMMIT.
        /// </summary>
        /// <param name="a">The account to put.</param>
        private static async Task<bool> UploadOneAccount(Account a) {
            bool hasRetried = false;
            retry:
            var peers = new List<string>();
            var failing = new List<string>();
            await FindResponsiblePeersForAccount(peers, failing, a.ID);
            if (failing.Count > 0) {
                if (!hasRetried) {
                    hasRetried = true;
                    goto retry;
                }
                Log(failing.Count + " copy searches failed.");
            }
            if (peers.Count < Constants.MinimumNumberOfCopies)
                return false;
            Dictionary<string, string> commitTokens = new Dictionary<string, string>();
            await peers.ForEachAsync(4, async (ep) => {
                using (var peer = new PeerConnection(Helpers.ParseEP(ep))) {
                    if (await peer.ConnectAsync()) {
                        var m = await peer.Connection.SendAndReceive("PUT", a, a.Path);
                        if (m.Response.IsSuccessful) {
                            return m.Response.Arguments[0];
                        } else {
                            Log(" PUT failed " + ep + " - " + m.Response.Code);
                        }
                    } else {
                        Log(" PUT failed " + ep + " - can't connect");
                    }
                    return null;
                }
            }, (ep, token) => {
                lock (commitTokens)
                    commitTokens[ep] = token;
            });

            if (commitTokens.Count < Constants.MinimumNumberOfCopies) {
                Log(" Not enough peers during PUT");
                return false;
            }
            Dictionary<string, CMResult> queryStatuses = new Dictionary<string, CMResult>();

            await commitTokens.ForEachAsync(4, async (keyPair) => {
                var ep = Helpers.ParseEP(keyPair.Key);
                using (var peer = new PeerConnection(ep)) {
                    if (await peer.ConnectAsync()) {
                        var m = await peer.Connection.SendAndReceive("QUERY-COMMIT", null, a.Path);
                        if (m.Response.Code != CMResult.S_OK) {
                            Log(" QRY " + keyPair.Key + " - " + m.Response.Code);
                        } else {
                            var updatedUtc = Helpers.DateFromISO8601(m.Response.Arguments[0]);
                            if (a.UpdatedUtc != updatedUtc) {
                                Log(" QRY " + keyPair.Key + " - UPDATED-UTC CONFLICT");
                                return CMResult.E_Object_Superseded;
                            }
                        }
                        return m.Response.Code;
                    } else {
                        lock (queryStatuses)
                            return CMResult.E_Not_Connected;
                    }
                }
            }, (kp, res) => {
                lock (queryStatuses)
                    queryStatuses[kp.Key] = res;
            });

            int successes = queryStatuses.Values.Where(x => x.Success).Count();
            if (successes < Constants.MinimumNumberOfCopies) {
                Log(" Not enough peers during QUERY-COMMIT");
                return false;
            }

            Dictionary<string, CMResult> commitStatuses = new Dictionary<string, CMResult>();
            await commitTokens.ForEachAsync(4, async (keyPair) => {
                CMResult queryStatus;
                string token = keyPair.Value;
                if (queryStatuses.TryGetValue(keyPair.Key, out queryStatus)
                    && queryStatus == CMResult.S_OK) {
                    var ep = Helpers.ParseEP(keyPair.Key);
                    using (var peer = new PeerConnection(ep)) {
                        if (await peer.ConnectAsync()) {
                            var m = await peer.Connection.SendAndReceive("COMMIT", null, token);
                            if (m.Response.Code != CMResult.S_OK) {
                                Log(" CMT " + keyPair.Key + " - " + m.Response.Code);
                            }
                            return m.Response.Code;
                        } else {
                            return CMResult.E_Not_Connected;
                        }
                    }
                } else {
                    return CMResult.E_General_Failure;
                }
            }, (kp, res) => {
                lock (queryStatuses)
                    commitStatuses[kp.Key] = res;
            });
            successes = commitStatuses.Values.Where(x => x.Success).Count();
            if (successes < Constants.MinimumNumberOfCopies) {
                Log(" Not enough peers during COMMIT");
                return false;
            }

            return true;
        }
    }
}