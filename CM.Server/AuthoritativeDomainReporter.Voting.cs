#region License
// 
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise 
// denoted in the source file.
//
#endregion

using CM.Schema;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server
{
    internal partial class AuthoritativeDomainReporter {

        private static string _CurrentVotingPropositionsJSON;
        ConcurrentDictionary<string, VoteItemCache> _MemCacheVotes = new ConcurrentDictionary<string, VoteItemCache>();

        private static readonly VotingProposition[] Propositions = new VotingProposition[] {
            // TEST PROPOSITION
            new VotingProposition() {
                 ID = 1,
                 CreatedUtc = new DateTime(2016,09,12),
                 CloseUtc = new DateTime(2100,09,12),
                 Translations = new [] {
                     new VotingProposition.TranslatedDetails() {
                          Code = "EN-GB",
                          Title = "Test proposition",
                          Description = "People are encouraged to try voting on this test proposition in order to familiarise themselves with the process, even if your account is new and your vote ultimately ineligible.",
                          PositiveImpacts = "Familiarises people with the voting system.\nProvides test data for developers.\nVerifies that voting works correctly.",
                          NegativeImpacts = "There are currently no known negative impacts of the test proposition."
                     },
                     new VotingProposition.TranslatedDetails() {
                          Code = "EN-US",
                          Title = "Test proposition",
                          Description = "People are encouraged to try voting on this test proposition in order to familiarize themselves with the process, even if your account is new and your vote ultimately ineligible.",
                          PositiveImpacts = "Familiarizes people with the voting system.\nProvides test data for developers.\nVerifies that voting works correctly.",
                          NegativeImpacts = "There are currently no known negative impacts of the test proposition."
                     }
                 }
            }
        };
  
        enum VoteStatus {
            Unknown = 0,
            Against,
            For,
            Ineligible_ReceivedAfterClose,
            Ineligible_AccountNotFound,
            Ineligible_AccountHistoryNotEligible,
            Ineligible_VoteRecordNotFound,
            Ineligible_VoteSignatureCheckFailure
        }
        public async Task HandleApiGetVotingPropositions(SslWebContext context) {
            var json = _CurrentVotingPropositionsJSON;
            if (json == null) {
                json = Serialize<VotingProposition[]>(Propositions);
                _CurrentVotingPropositionsJSON = json;
            }
            await context.ReplyAsync(HttpStatusCode.OK, "application/json", json);
        }

        public async Task HandleApiGetVoteData(SslWebContext context) {
            uint id;
            if (!uint.TryParse(context.QueryString["proposition-id"] ?? String.Empty, out id)) {
                await context.ReplyAsync(HttpStatusCode.BadRequest, "text/plain", "Expected a proposition-id parameter.");
                return;
            }
            var files = new List<string>(Directory.GetFiles(_FolderCompiledData, "prop" + id + "-votes-report-*.zip"));
            if (files.Count==0) {
                await context.ReplyAsync(HttpStatusCode.NotFound, "text/plain", "Results are not yet available.");
                return;
            }
            files.Sort();
            var fileName = files[files.Count - 1];
            using (var fs = System.IO.File.OpenRead(fileName)) {
                var headers = new Dictionary<string, string>();
                headers["Content-Type"] = "application/octet-stream";
                headers["Content-Length"] = fs.Length.ToString();
                headers["Content-Disposition"] = "attachment; filename=" + Path.GetFileName(fileName);
              await context.ReplyAsync(HttpStatusCode.OK, headers, fs);

            }
        }

        /// <summary>
        /// Fore Vote collection we query every node with this:
        ///
        /// CMD LIST
        /// PATH: VOTES/{proposition id}
        /// UPD-UTC: {last query time}
        /// SORT: UPD-UTC ASC
        ///
        /// We'll dump all data into massive text files, one for each proposition, and then process it later. 
        /// This will enable offline analysis/testing/debugging of voting data.
        /// </summary>
        private async void CollectVotesAsync(CancellationToken token) {
            var files = new Dictionary<string, System.IO.StreamWriter>();
            try {
                _IsVotePollInProgress = true;
                _LastVotePoll = Clock.Elapsed;

                var now = DateTime.UtcNow;

                var ar = NetworkScan.Peers.Keys.ToArray();

                if (!System.IO.Directory.Exists(_FolderRawData))
                    System.IO.Directory.CreateDirectory(_FolderRawData);
                _Log.Write(this, LogLevel.INFO, "CollectVotes started");
                for (int i = 0; i < ar.Length && !token.IsCancellationRequested; i++) {
                    var epString = ar[i];                
                    try {
                        var ep = Helpers.ParseEP(epString);
                        using (var conn = await _DHT.Connect(epString)) {
                            if (conn.IsValid) {
                                // When was the last time we queried this guy?
                                DateTime timeFrom = DateTime.MinValue;
                                string lastPollString;
                                string dictionaryKey = FindOrCreateIDForIPAddress(ep) + "_lstqry_votes";
                                if (_Persisted.TryGetValue(dictionaryKey, out lastPollString))
                                    timeFrom = Helpers.DateFromISO8601(lastPollString);
                                if (timeFrom == DateTime.MinValue)
                                    timeFrom = Constants.MinimumSaneDateTime;

                                foreach (var prop in Propositions) {
                                    uint start = 0;
                                    uint total = 0;
                                    var items = new List<string>();
                                    do {
                                        var m = await conn.Connection.SendAndReceive("LIST", new ListRequest() {
                                            APIVersion = Constants.APIVersion,
                                            Sort = "UPD-UTC ASC",
                                            UpdatedUtcFromInclusive = timeFrom,
                                            UpdatedUtcToExclusive = now,
                                            StartAt = start,
                                            Max = PAGINATION_SIZE
                                        }, Constants.PATH_VOTES + "/" + prop.ID);
                                        Schema.ListResponse res = m.Cast<Schema.ListResponse>();
                                        if (total == 0)
                                            total = res.Total;
                                        for (int x = 0; x < res.Values.Count; x++) {
                                            if (res.Values[x].Name == "ITEM") {
                                                var idx = new VoteIndex(res.Values[x].Value);
                                                System.IO.StreamWriter sw;
                                                if (!files.TryGetValue(prop.ID.ToString(), out sw)) {
                                                    var dataDumpFile = System.IO.Path.Combine(_FolderRawData, prop.ID + "-votes.txt");
                                                    sw = new System.IO.StreamWriter(System.IO.File.OpenWrite(dataDumpFile));
                                                    sw.BaseStream.Position = sw.BaseStream.Length;
                                                    files.Add(prop.ID.ToString(), sw);
                                                }

                                                // The data we log is the {end-point} +" "+ {vote index}
                                                // Always use Windows line-endings for portability.
                                                sw.Write(epString + " " + res.Values[x].Value + "\r\n");
                                                sw.Flush();
                                            }
                                        }
                                        start += res.Count;
                                    } while (start < total);
                                }

                                // No problems
                                _Persisted.Set(dictionaryKey, Helpers.DateToISO8601(now));
                            }
                        }
                    } catch (Exception ex) {
                        // Any problems.. move along..
                        _Log.Write(this, LogLevel.INFO, "Vote collection with {0} failed: {1}", epString, ex.Message);
                    }
                }
                _Log.Write(this, LogLevel.INFO, "CollectVotes finished");
            } finally {
                foreach (var s in files.Values) {
                    s.Flush();
                    s.Dispose();
                }
                _IsVotePollInProgress = false;
            }
        }

        /// <summary>
        /// Takes the raw data dumps and calculates and validates votes. 
        /// - Consensus is a tally of witness on the highest timestamp for any given account/proposition.
        /// </summary>
        private async void CompileVotesAsync(CancellationToken token) {
            try {
                _IsVoteCompileInProgress = true;
                _LastVoteCompile = Clock.Elapsed;

                if (!System.IO.Directory.Exists(_FolderRawData))
                    return;

                if (!System.IO.Directory.Exists(_FolderCompiledData))
                    System.IO.Directory.CreateDirectory(_FolderCompiledData);
                _Log.Write(this, LogLevel.INFO, "CompileVotes started");
                var invalidatedRegions = new HashSet<string>();
                var now = DateTime.UtcNow;
                DateTime lastSuccessfulCompilation = DateTime.MinValue;
                string s;
                string dictionaryKey = "last-compile-votes";
                if (_Persisted.TryGetValue(dictionaryKey, out s))
                    lastSuccessfulCompilation = Helpers.DateFromISO8601(s);

                foreach (var prop in Propositions) {

                    var cache = LoadVoteItemCache(prop.ID.ToString());

                    var dir = new System.IO.DirectoryInfo(_FolderRawData);
                    var dataDumpFile = System.IO.Path.Combine(_FolderRawData, prop.ID + "-votes.txt");



                    var vote = new VoteIndex();
                    if (File.Exists(dataDumpFile)) {
                        using (var dr = new System.IO.StreamReader(System.IO.File.OpenRead(dataDumpFile))) {
                            long lastOffset = 0;
                            var offsetDictionaryKey = dataDumpFile + "_offset";
                            if (_Persisted.TryGetValue(offsetDictionaryKey, out s)
                                && !cache.ForceRecount)
                                lastOffset = long.Parse(s);
                            dr.BaseStream.Position = lastOffset;

                            try {
                                string line;
                                while ((line = dr.ReadLine()) != null) {
                                    if (line.Length == 0)
                                        continue;
                                    var idx = line.IndexOf(' ');
                                    if (idx == -1)
                                        continue;
                                    var peer = line.Substring(0, idx);
                                    var ep = Helpers.ParseEP(peer);
                                    var peerID = FindOrCreateIDForIPAddress(ep);
                                    vote.Parse(line.Substring(idx + 1));


                                    VoteItem item;
                                    if (!cache.Dic.TryGetValue(vote.VoterID, out item)) {
                                        item = new VoteItem(vote);
                                        cache.Dic[vote.VoterID] = item;
                                    }

                                    if (item.CreatedUtc != vote.CreatedUtc)
                                        continue;// Creation times are read-only, so something's fishy..


                                    if (vote.UpdatedUtc <= item.UpdatedUtc)
                                        continue; // current/obsolete 

                                    // Queue for re-evaluation
                                    item.UpdatedUtc = vote.UpdatedUtc;
                                    item.CurrentStatus = VoteStatus.Unknown;
                                    cache.IsInvalidated = true;
                                }
                            } finally {
                                _Persisted.Set(offsetDictionaryKey, dr.BaseStream.Position.ToString());
                            }
                        }
                    }

                    if (cache.IsInvalidated) {
                        // Save imported 'Unknown' vote states now in case
                        // we exit before validations complete.
                        SaveVotes(prop.ID.ToString(), cache);
                    }

                    var results = cache.Dic.Values.ToArray();
                    var toValidate = new List<VoteItem>();
                    for (int i = 0; i < results.Length; i++) {
                        var item = results[i];
                        if (item.CurrentStatus == VoteStatus.Unknown)
                            toValidate.Add(item);
                    }

                    if (toValidate.Count > 0) {

                        await toValidate.ForEachAsync(2, async (v) => await CalculateVoteStatus(prop, v),
                            (v, res) => {
                                v.CurrentStatus = res;
                            });

                        SaveVotes(prop.ID.ToString(), cache);
                        UpdateVotingResults(prop, results);
                    }

                }
                _Persisted.Set(dictionaryKey, Helpers.DateToISO8601(now));
                PersistVoteTallys();
                _CurrentVotingPropositionsJSON = null; // invalidate cached JSON
                _Log.Write(this, LogLevel.INFO, "CompileVotes finished");
            } finally {
                _IsVoteCompileInProgress = false;
            }
        }

        /// <summary>
        /// Compiles a new count and zipped .csv report, based on each in-memory VoteItem.
        /// </summary>
        void UpdateVotingResults(VotingProposition prop, VoteItem[] results) {

            uint votesFor = 0;
            uint votesAgainst = 0;
            uint voteIneligible = 0;
            var uniqueReportName = "prop" + prop.ID + "-votes-report-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var reportFolder = System.IO.Path.Combine(_FolderCompiledData, uniqueReportName);

            Directory.CreateDirectory(reportFolder);
            var reportFile = System.IO.Path.Combine(reportFolder, uniqueReportName + ".csv");
            using (var fs = new System.IO.FileStream(reportFile, FileMode.OpenOrCreate, FileAccess.ReadWrite)) {
                var b = System.Text.Encoding.UTF8.GetBytes("Voter ID,Created UTC,Updated UTC,Current Status\r\n");
                fs.Write(b, 0, b.Length);
                for (int i = 0; i < results.Length; i++) {
                    var item = results[i];
                    if (item.CurrentStatus == VoteStatus.For)
                        votesFor++;
                    else if (item.CurrentStatus == VoteStatus.Against)
                        votesAgainst++;
                    else
                        voteIneligible++;
                    b = System.Text.Encoding.UTF8.GetBytes(item.VoterID + "," + Helpers.DateToISO8601(item.CreatedUtc) + "," + Helpers.DateToISO8601(item.UpdatedUtc) + "," + item.CurrentStatus + "\r\n");
                    fs.Write(b, 0, b.Length);
                }
                fs.Flush();
            }
            System.IO.Compression.ZipFile.CreateFromDirectory(reportFolder,
                reportFolder + ".zip", System.IO.Compression.CompressionLevel.Optimal,
                false);
            Directory.Delete(reportFolder, true);
            prop.For = votesFor;
            prop.Against = votesAgainst;
            prop.Ineligible = voteIneligible;

        }


        /// <summary>
        /// Votes must be:
        /// - Within the proposition time period.
        /// - Voter must have settled transactions for 12 of the past 14 months.
        /// </summary>
        /// <param name="prop"></param>
        /// <param name="vote"></param>
        /// <returns></returns>
        async Task<VoteStatus> CalculateVoteStatus(VotingProposition prop, VoteItem vote) {
   
            if (vote.UpdatedUtc < prop.CreatedUtc || vote.UpdatedUtc >= prop.CloseUtc)
                return VoteStatus.Ineligible_ReceivedAfterClose;

            // Validate account
            var path = Constants.PATH_ACCNT + "/" + vote.VoterID + "?calculations-date=" + Helpers.DateToISO8601(vote.CreatedUtc);
            var a = await _Storage.TryFindOnNetwork<Schema.Account>(path);

            if (a == null || !a.Response.IsSuccessful || !a.ConsensusOK)
                return VoteStatus.Ineligible_AccountNotFound;

            if (a.AccountCalculations == null || !a.AccountCalculations.IsEligibleForVoting)
                return VoteStatus.Ineligible_AccountHistoryNotEligible;

            // Validate vote
            path = Constants.PATH_VOTES + "/" + prop.ID + "/" + vote.VoterID;
            var v = await _Storage.TryFindOnNetwork<Schema.Vote>(path);
            if (v == null || !v.Response.IsSuccessful || !v.ConsensusOK)
                return VoteStatus.Ineligible_VoteRecordNotFound;
            AsyncRequest<DataVerifyRequest> verify = new AsyncRequest<DataVerifyRequest>();
            verify.Item = new DataVerifyRequest() {
                DataDateUtc = v.UpdatedUtc,
                Input = v.GetSigningData(),
                Signature = v.Signature
            };
            a.VerifySignature(verify, CryptoFunctions.Identity);
            if (verify.Result != CMResult.S_OK)
                return VoteStatus.Ineligible_VoteSignatureCheckFailure;

            // Looks good
            return v.Value ? VoteStatus.For : VoteStatus.Against;
        }

        void PersistVoteTallys() {
            foreach (var prop in Propositions) {
                _Persisted.Set("vote_tally_" + prop.ID, prop.For + "\n" + prop.Against + "\n" + prop.Ineligible);
            }
        }

        void LoadVoteTallys() {
            foreach (var prop in Propositions) {
                string data;
                if (_Persisted.TryGetValue("vote_tally_" + prop.ID, out data)
                    && data != null) {
                    var ar = data.Split('\n');
                    prop.For = uint.Parse(ar[0]);
                    prop.Against = uint.Parse(ar[1]);
                    prop.Ineligible = uint.Parse(ar[2]);
                }
            }
        }

        /// <summary>
        /// Describes a Vote and keeps track of status and how many peers
        /// have corroborated it etc.
        /// </summary>
        private class VoteItem {

            /// <summary>
            /// The voter ID
            /// </summary>
            public string VoterID;
            /// <summary>
            /// The creation time of the vote
            /// </summary>
            public DateTime CreatedUtc;
            /// <summary>
            /// Last known update time of the vote
            /// </summary>
            public DateTime UpdatedUtc;
            /// <summary>
            /// The computed status for the vote
            /// </summary>
            public VoteStatus CurrentStatus;
            
            public VoteItem(VoteIndex idx) {
                VoterID = idx.VoterID;
                CreatedUtc = idx.CreatedUtc;
            }

            public VoteItem(BinaryReader br) {
                VoterID = br.ReadString();
                CreatedUtc = new DateTime(br.ReadInt64());
                UpdatedUtc = new DateTime(br.ReadInt64());
                CurrentStatus = (VoteStatus)br.ReadInt32();
            }

            public void Serialise(BinaryWriter bw) {
                bw.Write(VoterID);
                bw.Write(CreatedUtc.Ticks);
                bw.Write(UpdatedUtc.Ticks);
                bw.Write((int)CurrentStatus);
            }

        }

        /// <summary>
        /// Tracks vote dictionary states and whether or not it needs to be re-serialised to disk.
        /// </summary>
        private class VoteItemCache {
            public ConcurrentDictionary<string, VoteItem> Dic;

            /// <summary>
            /// True if it needs to be serialised to disk
            /// </summary>
            public bool IsInvalidated;
            public bool ForceRecount;
        }

        VoteItemCache LoadVoteItemCache(string propositionID) {
           

            VoteItemCache cache;
            if (_MemCacheVotes.TryGetValue(propositionID, out cache))
                return cache;

            // Load from disk 
            var file = Path.Combine(Path.Combine(_Folder, FOLDER_REPORT_DATA), FOLDER_COMPILED);
            file = Path.Combine(file, "votes-" + propositionID + ".dat");
            var dic = new ConcurrentDictionary<string, VoteItem>();
            if (File.Exists(file)) {
                using (var fs = new System.IO.FileStream(file, FileMode.Open, FileAccess.Read)) {
                    using (var br = new BinaryReader(fs)) {
                        while (fs.Position < fs.Length) {
                            var item = new VoteItem(br);
                            dic[item.VoterID] = item;
                        }
                    }
                }
            } 
            cache = new VoteItemCache();
            // We will assume that if the proposition.dat file is new/empty then entire file
            // should be re-processed from scratch. This we make re-counts easy to trigger in the
            // event of a problem.
            cache.ForceRecount = true;
            cache.Dic = dic;
            _MemCacheVotes[propositionID] = cache;
            return cache;
        }

        /// <summary>
        /// Flushes VoteItem data to disk.
        /// </summary>
        private void SaveVotes(string propositionID, VoteItemCache c) {
            lock (c) {
                var file = Path.Combine(Path.Combine(_Folder, FOLDER_REPORT_DATA), FOLDER_COMPILED);
                if (!Directory.Exists(file))
                    Directory.CreateDirectory(file);
                file = Path.Combine(file, "votes-" + propositionID + ".dat");
                using (var fs = new System.IO.FileStream(file, FileMode.OpenOrCreate, FileAccess.Write)) {
                    using (var bw = new BinaryWriter(fs)) {
                        fs.SetLength(0);
                        foreach (VoteItem item in c.Dic.Values)
                            item.Serialise(bw);
                        c.IsInvalidated = false;
                    }
                }
                c.ForceRecount = false;
            }
        }


    }
}
