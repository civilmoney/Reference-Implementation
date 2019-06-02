#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CM.Schema;

namespace CM.Server {

    /// <summary>
    /// Performs civil.money 'root server' administrative reporting for tax revenue etc. This code
    /// only runs if the server is configured to do so. There are no security implications in running
    /// it on a non-authoritative server. It is basically just a report generator.  
    /// </summary>
    internal partial class AuthoritativeDomainReporter : IDisposable {
        private const int PAGINATION_SIZE = 1000;
        private const string FOLDER_REPORT_DATA = "report-data";
        private const string FOLDER_RAW = "raw";
        private const string FOLDER_COMPILED = "compiled";

        /// <summary>
        /// Each peer endpoint gets its own primary key for space saving reasons. This is our
        /// global counter which gets kept in the persisted linear hash table settings.
        /// </summary>
        private uint _CurrentIPPrimaryKey;

        private object _CurrentIPPrimaryKeyLock;

        private DistributedHashTable _DHT;
        private Storage _Storage;
        private string _Folder;
        internal string _FolderCompiledData;
        private string _FolderRawData;
        private Intervals _Intervals;
        private bool _IsTransactionCompileInProgress;
        private bool _IsTransactionPollInProgress;
        private bool _IsVoteCompileInProgress;
        private bool _IsVotePollInProgress;
        private TimeSpan _LastNetworkPoll;
        private TimeSpan _LastTransactionCompile;
        private TimeSpan _LastTransactionPoll;
        private TimeSpan _LastVoteCompile;
        private TimeSpan _LastVotePoll;
        private Log _Log;
        private LinearHashTable<string, string> _Persisted;

        static readonly Newtonsoft.Json.JsonSerializerSettings _JsonSettings = new Newtonsoft.Json.JsonSerializerSettings() {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver()
        };

        public AuthoritativeDomainReporter(string dataFolder, DistributedHashTable dht, Storage storage, Log log) {
            _DHT = dht;
            _Storage = storage;
            _Log = log;
            _Folder = dataFolder;

            _FolderCompiledData = Path.Combine(Path.Combine(dataFolder, FOLDER_REPORT_DATA), FOLDER_COMPILED);
            _FolderRawData = Path.Combine(Path.Combine(dataFolder, FOLDER_REPORT_DATA), FOLDER_RAW);
            _CurrentIPPrimaryKeyLock = new object();
            _Intervals = new Intervals();
            _Persisted = new LinearHashTable<string, string>(
                System.IO.Path.Combine(dataFolder, "reports"),
                Storage.Container.OnHashKey,
                Storage.Container.OnSerialiseKey,
                Storage.Container.OnDeserialiseKey,
                Storage.Container.OnSerialiseKey,
                Storage.Container.OnDeserialiseKey);

            LoadVoteTallys();

            _Telem = new TelemetryReport();
        }
        
        static string Serialize<T>(T obj) {

            return Newtonsoft.Json.JsonConvert.SerializeObject(obj, _JsonSettings);

        }
       
        /// <summary>
        /// Initiates network scans, transaction/vote polling and tax revenue compilation.
        /// </summary>
        public async void Poll(CancellationToken token) {
            try {
                if ((Clock.Elapsed - _LastNetworkPoll) > _Intervals.NetworkPoll
                    && !NetworkScan.IsInProgress) {
                    var scanTask = NetworkScan.Update(_Log, _DHT, Constants.Seeds, token);
                    using (var waitCancel = new CancellationTokenSource()) {
                        await Task.WhenAny(scanTask, Task.Delay((int)_Intervals.NetworkPoll.TotalMilliseconds, waitCancel.Token));
                        waitCancel.Cancel();
                    }
                    if (!scanTask.IsCompleted) {
                        _Log.Write(this, LogLevel.WARN, "NetworkScan " + scanTask.Id + " timed out (" + scanTask.Status + "). "+ scanTask.Exception);
                        NetworkScan.IsInProgress = false;
                    }
                    _LastNetworkPoll = Clock.Elapsed;
                }

                if (!NetworkScan.IsInProgress) {
                    
                    // COLLECT TRANSACTIONS
                    if ((Clock.Elapsed - _LastTransactionPoll) > _Intervals.TransactionPoll
                       // Compile + Collect both need access to dump files so only allow one to run at a time.
                       && !_IsTransactionCompileInProgress) {
                        if (!_IsTransactionPollInProgress) {
                            _IsTransactionPollInProgress = true;
                            CollectTransactionsAsync(token);
                        } else {
                            // Anticipating that intervals will need tweaked. This tells us when.
                            var runTime = (Clock.Elapsed - _LastTransactionPoll);
                            _Log.Write(this, LogLevel.WARN, "Transaction poll is taking {0}", runTime);
                            if (runTime.TotalDays > 1) {
                                // Recover from inexplicable CollectTransactionsAsync finaliser never running.
                                _IsTransactionPollInProgress = false;
                            }
                        }
                    }

                    // COMPILE TRANSACTIONS
                    if ((Clock.Elapsed - _LastTransactionCompile) > _Intervals.TransactionCompile
                        && !_IsTransactionPollInProgress) {
                        if (!_IsTransactionCompileInProgress) {
                            _IsTransactionCompileInProgress = true;
                            CompileTransactions(token);
                        } else
                            _Log.Write(this, LogLevel.WARN, "Transaction compilation is taking {0}", (Clock.Elapsed - _LastTransactionCompile));
                    }

                    // COLLECT VOTES
                    if ((Clock.Elapsed - _LastVotePoll) > _Intervals.VotePoll
                      // Compile + Collect both need access to dump files so only allow one to run at a time.
                      && !_IsVoteCompileInProgress) {
                        if (!_IsVotePollInProgress) {
                            _IsVotePollInProgress = true;
                            CollectVotesAsync(token);
                        } else // Anticipating that intervals will need tweaked. This tells us when.
                            _Log.Write(this, LogLevel.WARN, "Vote poll is taking {0}", (Clock.Elapsed - _LastVotePoll));
                    }

                    // COMPILE VOTES
                    if ((Clock.Elapsed - _LastVoteCompile) > _Intervals.VoteCompile
                        && !_IsVotePollInProgress) {
                        if (!_IsVoteCompileInProgress) {
                            _IsVoteCompileInProgress = true;
                            CompileVotesAsync(token);
                        } else
                            _Log.Write(this, LogLevel.WARN, "Vote compilation is taking {0}", (Clock.Elapsed - _LastVoteCompile));
                    }

                    // SUBMIT TELEMETRY
                    _Telem?.Flush(_Log);
                }

            } catch (Exception ex) {
                _Log.Write(this, LogLevel.FAULT, "Poll error: " + ex.ToString());
            } finally {
                _Persisted.Flush();
            }
        }

    }
}