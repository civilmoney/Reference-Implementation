#region License

//
// Civil Money is free and unencumbered software released into the public domain (unlicense.org), unless otherwise
// denoted in the source file.
//

#endregion License

using CM.Schema;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CM.Server {
    /*
     * The sync procedure is:
     *
     * foreach (Container account in Storage) {
     *
     *   if (account.LastSyncAttempt > some hours ago)
     *     AnnounceAccount(account)
     *
     *   foreach (Deferral account in deferredInboundSyncs) {
     *      if(account.LastSyncAttempt > deferalWait) {
     *         PullAccount(account)
     *      }
     *   }
     * }
     *
     *
     *  void AnnounceAccount (account) {
     *    var peers = new List<string>();
     *    FindResponsiblePeersForAccount(peers, id);
     *    foreach(peer in peers)
     *       AnnounceToPeer(peer, account);
     *  }
     *
     *  void AnnounceToPeer(peer, account) {
     *
     *      Send:
     *         CMD SYNC [Nonce]
     *         VER: 1
     *         ID: <id>
     *         UPD-UTC: <account updated utc>
     *         TRANS-HASH: SHA256_OF(All Transaction Updated UTC utf8)
     *         VOTES-HASH: SHA256_OF(All Transaction Updated UTC utf8)
     *         END [Nonce]
     *
     *      Receive:
     *         RES S_OK [NOnce]
     *         END [NOnce]
     *
     *  }
     *
     * The destination peer potentially performs a "pull" of account data
     * upon receiving an announcement.
     *
     * void PullAccount(SyncAnnounce m) {
     *
     *    if(AccountInfoNeedsUpdated(m)) {
     *
     *      var corroboratedAccount = TryFindOnNetwork<Account>(id);
     *
     *      if(!Validate(corroboratedAccount))
     *          return;
     *
     *      if(corroboratedAccount.IsTransient) {
     *
     *        // Not enough servers can corroborate right now,
     *        // re-schedule it for the "near"-future. If the
     *        // Transient state persists for a long period of time,
     *        // only then will we give up and accept a transient copy.
     *        EnqueueDeferredAccountSync(m);
     *
     *        return;
     *
     *      } else {
     *
     *          StoreAccount(corroboratedAccount);
     *
     *      }
     *
     *    }
     *
     *    Send to caller:
     *       CMD LIST [Nonce] /ACCNT/<id>/TRANS
     *       MAX: 1000
     *       SORT: UPD-UTC DESC
     *       END [Nonce]
     *
     *    Receive:
     *       RES 0x0 [Nonce]
     *       ITEM: ...
     *       END [Nonce]
     *
     *    foreach(transaction in list)
     *      CorrelateAndUpdate(transaction)
     * }
     */

    /// <summary>
    /// Performs inter-peer storage sync
    /// </summary>
    internal class SynchronisationManager : IDisposable {

        private class SynchronisationIntervals {
#if DEBUGx
            public TimeSpan AnnounceAccountsWhenResponsible = TimeSpan.FromMinutes(2);
            public TimeSpan AnnounceAccountsWhenNotResponsible = TimeSpan.FromMinutes(15);
#else

            /// <summary>
            /// Announcement interval for accounts that we're presently responsible for.
            /// </summary>
            /// <remarks>
            /// Initially this interval is low. Depending on how stable servers on the network end up
            /// becoming, it could be bumped to as high as 5 hours, maybe even more. Consider that 5x
            /// servers all announce the same account within the time frame. Probably we will end up
            /// with a 1hr interval, meaning at least 5 announcements per account per hour. If each
            /// server hosts only 3600 accounts, it should be roughly 1 announcement per second. That
            /// means we want about 300 servers to handle 1 million accounts. One million servers
            /// will handle 3.6 billion. 75 million servers on the internet back in 2014. 1 million
            /// by Microsoft alone. Maybe not a huge stretch of the imagination for a global monetary system.
            /// </remarks>
            public TimeSpan AnnounceAccountsWhenResponsible = TimeSpan.FromMinutes(30);

            /// <summary>
            /// Announcement interval for accounts that we're no longer responsible for. This should
            /// be quite long in order to minimise noise on the network. This is a safety feature in
            /// case multiple peers for a particular account all permanently disappear at once.
            /// </summary>
            public TimeSpan AnnounceAccountsWhenNotResponsible = TimeSpan.FromMinutes(60);

#endif

            /// <summary>
            /// The number of times to blindly repeat an announcement, every 'AnnounceAccounts'
            /// interval (i.e. 5 minutes.)
            /// </summary>
            public int MaxPerPeerAnnouncementRepeats = 2;

            /// <summary>
            /// Regardless of how many times a peer has been sent an announcement, we
            /// always send the account at least once a day, just for safety.
            /// </summary>
            public TimeSpan RepeatPeerAnnouncementAlwaysEvery = TimeSpan.FromHours(12);

            /// <summary>
            /// If an account is in a Transient state or can only be located on the original
            /// announcing peer, we first retry fully corroborated network-based fetches for the
            /// account at these offsets.
            ///
            /// After no success and the final interval has elapsed, we give up and accept the
            /// transient object, with the hope that the network has not been overrun with DDoS
            /// attacks. Rather, the network underwent a rapid rebalancing act, which changed all of
            /// the responsible peers for the account ID in question, before the data could be synced.
            /// </summary>
            public TimeSpan[] DeferredRetryIntervals = new TimeSpan[] {
                TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5),
#if !DEBUG
                TimeSpan.FromMinutes(10),
                TimeSpan.FromMinutes(30),
                TimeSpan.FromHours(1),
                TimeSpan.FromHours(2),
               // TimeSpan.FromHours(3),
               // TimeSpan.FromHours(4),
               // TimeSpan.FromHours(5),
               // TimeSpan.FromHours(6),
               // TimeSpan.FromHours(7),
               // TimeSpan.FromHours(8),
               // TimeSpan.FromHours(24)
#endif
            };

            /// <summary>
            /// Basically don't start synchronising anything until we've been joined with the same
            /// Successor and Predecessor for a reasonable length of time.
            /// </summary>
#if DEBUG
            public TimeSpan MinimumDHTStablisedDuration = TimeSpan.FromSeconds(60);
#else
            public TimeSpan MinimumDHTStablisedDuration = TimeSpan.FromMinutes(2);
#endif
        }

        private Storage _Store;
        private LinearHashTable<string, SyncState> _Persisted;
        private ConcurrentDictionary<string, SyncState> _MemCached;
        private ConcurrentDictionary<string, SyncState> _Deferred;
        private ConcurrentDictionary<string, PeerSyncHistory> _PeerSyncHistory;

        private Task _CurrentSyncTask;// for loop task debugging
        private Task<bool> _CurrentPullTask; // for loop task debugging

        private bool _IsRunning;
        private SynchronisationIntervals _Intervals;
        private DistributedHashTable _DHT;
        private Log _Log;
#if DEBUG
        private const int PAGINATION_SIZE = 1000; // during development we have to make sure pagination works correctly
#else
        private const int PAGINATION_SIZE = 10 * 1000;
#endif
        public bool VerboseSyncDebugging { get; set; } = false;

        public SynchronisationManager(string dataFolder, Storage s, DistributedHashTable dht, Log log) {
            _Log = log;
            _Store = s;
            _Store.ObjectModified += _Store_ObjectModified;
            _DHT = dht;
            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);
            var syncFile = System.IO.Path.Combine(dataFolder, "sync");

            try {
                _Persisted = new LinearHashTable<string, SyncState>(
                   syncFile,
                    Storage.Container.OnHashKey,
                    Storage.Container.OnSerialiseKey,
                    Storage.Container.OnDeserialiseKey,
                    OnSerialiseSyncState,
                    OnDeserialiseSyncState);
            } catch (Exception ex) {
                log.Write(this, LogLevel.WARN, "Sync database needs reset: " + ex);
                var ext = new[] { ".htdata", ".htindex", ".htindex-bak" };
                for (int i = 0; i < ext.Length; i++)
                    if (System.IO.File.Exists(syncFile + ext[i]))
                        System.IO.File.Delete(syncFile + ext[i]);

                _Persisted = new LinearHashTable<string, SyncState>(
                 syncFile,
                  Storage.Container.OnHashKey,
                  Storage.Container.OnSerialiseKey,
                  Storage.Container.OnDeserialiseKey,
                  OnSerialiseSyncState,
                  OnDeserialiseSyncState);

                log.Write(this, LogLevel.INFO, "Sync database has been reset successfully.");
            }
            _MemCached = new ConcurrentDictionary<string, SyncState>();
            _Deferred = new ConcurrentDictionary<string, SyncState>();
            _PeerSyncHistory = new ConcurrentDictionary<string, PeerSyncHistory>();
            _Intervals = new SynchronisationIntervals();
        }

        private void _Store_ObjectModified(IStorable obj) {
            if (obj is Account) {
                var a = obj as Account;
                EnsureAccountState(a.ID, true);
            } else if (obj is Transaction) {
                var t = obj as Transaction;
                EnsureAccountState(t.PayeeID, true);
                EnsureAccountState(t.PayerID, true);
            } else if (obj is Vote) {
                var v = obj as Vote;
                EnsureAccountState(v.VoterID, true);
            }
        }

        internal static void OnSerialiseSyncState(SyncState s, BinaryWriter bw) {
            s.Serialise(bw);
        }

        internal static SyncState OnDeserialiseSyncState(BinaryReader br) {
            return new SyncState(br);
        }

        public void Start() {
            System.Diagnostics.Debug.Assert(!_IsRunning);
            if (_IsRunning)
                return;
            _IsRunning = true;
            Loop();
        }

        public void Stop() {
            _IsRunning = false;
        }

        private async void Loop() {
            CMResult hr;

            // Full update of _MemCached only needs to happen one time. An AccountCreated event will
            // ensure newly added accounts get added.
            if (!(hr = UpdateAccountStates()).Success) {
                _Log.Write(this, LogLevel.WARN, "Unable to list accounts {0}", hr);
            }
            var queue = new List<KeyValuePair<string, SyncState>>();
            var lastSummaryReport = Clock.Elapsed;
            const int taskTimeout = 60 * 1000;
            try {
                while (_IsRunning) {
                    bool didSomething = false;

                    // Only sync once we're stable within the DHT network.
                    if (_DHT.StablisedDuration > _Intervals.MinimumDHTStablisedDuration) {
                        queue.Clear();
                        foreach (var kp in _MemCached) {
                            var state = kp.Value;
                            if (state.Status == SyncStatus.None) {
                                state.Status = IsCurrentlyResponsible(kp.Key) ? SyncStatus.ResponsibleForAccount
                                     : SyncStatus.NotResponsibleForAccount;
                            }

                            var interval = state.Status == SyncStatus.NotResponsibleForAccount ? _Intervals.AnnounceAccountsWhenNotResponsible
                                : _Intervals.AnnounceAccountsWhenResponsible;

                            if ((DateTime.UtcNow - state.LastAnnounceUtc) > interval
                                && state.Status != SyncStatus.InProgress
                                && state.Status != SyncStatus.Enqueued
                                && (state.Status != SyncStatus.StorageError || (DateTime.UtcNow - state.LastAnnounceUtc).TotalDays > 1)
                                ) {
                                // Storage repair retry. The backing file path may have been restored.
                                if (state.Status == SyncStatus.StorageError)
                                    state.Status = SyncStatus.None;
                                queue.Add(kp);
                            }
                        }
                        if (queue.Count > 0) {
                            // Sort oldest first
                            queue.Sort((a, b) => { return a.Value.LastAnnounceUtc.CompareTo(b.Value.LastAnnounceUtc); });
                            for (int i = 0; i < queue.Count && i < 100; i++) {
                                var kp = queue[i];
                                _CurrentSyncTask = AnnounceAccount(kp.Key, kp.Value);
                                using (var waitCancel = new CancellationTokenSource()) {
                                    await Task.WhenAny(_CurrentSyncTask, Task.Delay(taskTimeout, waitCancel.Token));
                                    waitCancel.Cancel();
                                }
                                if (!_CurrentSyncTask.IsCompleted) {
                                    _Log.Write(this, LogLevel.WARN, "AnnounceAccount {0} timed out.", kp.Key);
                                }
                                _CurrentSyncTask = null;
                                didSomething = true;
                            }
                            if ((Clock.Elapsed - lastSummaryReport).TotalMinutes >= 1) {
                                double per = _MemCached.Count != 0 ? queue.Count / (double)_MemCached.Count : 0;

                                _Log.Write(this, LogLevel.INFO, "Accounts {0}, Queue {1} ({2}), Deferred {3}",
                                   _MemCached.Count.ToString("N0"), queue.Count.ToString("N0"),
                                   per.ToString("P0"), _Deferred.Count.ToString("N0"));
                                lastSummaryReport = Clock.Elapsed;
                            }
                        }
                        var deferred = _Deferred.ToArray();
                        int deferredPulls = 0;
                        foreach (var kp in deferred) {
                            if (kp.Value.PullDeferalCount >= _Intervals.DeferredRetryIntervals.Length
                                || (DateTime.UtcNow - kp.Value.LastPullUtc) > _Intervals.DeferredRetryIntervals[kp.Value.PullDeferalCount]) {
                                if (kp.Value.Status != SyncStatus.InProgress
                                    && kp.Value.Status != SyncStatus.Enqueued) {
                                    if (kp.Value.RemoteSyncAnnounce != null) {
                                        didSomething = true;
                                        kp.Value.Status = SyncStatus.Enqueued;
                                        _CurrentPullTask = PullAccount(kp.Key, kp.Value, kp.Value.RemoteSyncAnnounce);
                                        using (var waitCancel = new CancellationTokenSource()) {
                                            await Task.WhenAny(_CurrentPullTask, Task.Delay(taskTimeout, waitCancel.Token));
                                            waitCancel.Cancel();
                                        }
                                        if (_CurrentPullTask.IsCompleted
                                            && _CurrentPullTask.Result) {
                                            SyncState tmp;
                                            _Deferred.TryRemove(kp.Key, out tmp);
                                        }
                                        _CurrentPullTask = null;
                                    } else {
                                        SyncState tmp;
                                        _Deferred.TryRemove(kp.Key, out tmp);
                                    }
                                    Persist(kp.Key, kp.Value);
                                    if (deferredPulls++ == 100)
                                        break;
                                }
                            }
                        }
                    }

                    if (!didSomething)
                        await Task.Delay(5000);
                    else
                        _Persisted.Flush();
                }
            } catch (Exception ex) {
                _Log.Write(this, LogLevel.FAULT, "SynchronisationManager has crashed: {0}", ex);
            } finally {
                _IsRunning = false;
            }
        }

        private bool IsCurrentlyResponsible(string id) {
            for (int i = 0; i < Constants.NumberOfCopies; i++) {
                if (_DHT.IsCurrentlyResponsible(Helpers.FromBigEndian(Helpers.DHT_ID("copy" + (i + 1) + id))))
                    return true;
            }
            return false;
        }

        private async Task<bool> AnnounceAccount(string accountID, SyncState state) {
            SyncAnnounce sync = state.LocalSyncAnnounce;
            try {
                state.LastAnnounceUtc = DateTime.UtcNow;

                // make sure the account still exists and hasn't been deleted off disk
                if (sync != null
                    && !_Store.DoesAccountExist(accountID)) {
                    sync = null;
                    state.LocalSyncAnnounce = null;
                }

                if (sync == null
                    && !GenerateSyncAnnounce(accountID, out sync)) {
                    _Log.Write(this, LogLevel.WARN, "Storage problem generating sync announce for {0}", accountID);
                    state.Status = SyncStatus.StorageError;
                    return false;
                }

                if (state.LocalSyncAnnounce == null)
                    state.LocalSyncAnnounce = sync;

                sync.MyEndPoint = _DHT.MyEndpoint;

                // Am I responsible for this account?
                if (!IsCurrentlyResponsible(accountID)) {
                    // _Log.Write(this, LogLevel.INFO, "No longer responsible for {0}", id);
                    // if not, do I have a newer copy than the network?
                    var a = await _Store.TryFindOnNetwork<Account>(Constants.PATH_ACCNT + "/" + accountID);
                    if (a != null) {
                        if (sync.UpdatedUtc == a.UpdatedUtc) {
                            // Mine's the same as the network's. Don't bother announcing.
                            return false;
                        }

                        if (sync.UpdatedUtc < a.UpdatedUtc) {
                            // My copy is out of date, update it and then stay quiet.
                            _Log.Write(this, LogLevel.INFO, "My copy of {0} is obsolete", accountID);

                            var res = await _Store.Put(a);
                            var hr = res.Code;
                            if (hr == CMResult.S_OK) {
                                hr = await _Store.Commit(res.Token,
                                    skipNetworkCorroboration: true,
                                    sendPushNotifications: false);
                            }

                            if (hr == CMResult.S_OK)
                                _Log.Write(this, LogLevel.INFO, "{0} updated", accountID);
                            else
                                _Log.Write(this, LogLevel.WARN, "{0} update failed {1}", accountID, hr);
                            return false;
                        }

                        // I have a newer copy that the network needs to
                        // know about.
                    }
                }

                PeerSyncHistory hist;
                if (!_PeerSyncHistory.TryGetValue(accountID, out hist)) {
                    hist = new PeerSyncHistory();
                    _PeerSyncHistory[accountID] = hist;
                }

                hist.Update(sync);

                bool announcedToAnyone = false;
                var peers = new List<string>();
                await _Store.FindResponsiblePeersForAccount(peers, accountID);

                // No need to sync to yourself
                peers.Remove(_DHT.MyEndpoint);

                if (peers.Count == 0) {
                    _Log.Write(this, LogLevel.INFO, "No peers available for {0}", accountID);
                    state.Status = SyncStatus.NoPeersAvailable;
                    return false;
                }

                if (VerboseSyncDebugging) {
                    _Log.Write(this, LogLevel.INFO, "Announcing '{0}' to {1} peers:", accountID, peers.Count);
                }

                foreach (var peer in peers) {
                    // Filter away peers we've already notified recently.
                    bool shouldAnnounce = hist.ShouldAnnounce(_Intervals, peer);
                    string status = "Skip";
                    if (shouldAnnounce) {
                        if (await AnnounceToPeer(peer, accountID, sync)) {
                            status = "OK";
                            hist.Increment(peer);
                            announcedToAnyone = true;
                        } else {
                            status = "Failed";
                        }
                    }
                    if (VerboseSyncDebugging) {
                        _Log.Write(this, LogLevel.INFO, "   {0}: {1}", peer, status);
                    }
                }
                // _Log.Write(this, LogLevel.INFO, "Announce completed for {0}", id);
                return announcedToAnyone;
            } catch (Exception ex) {
                _Log.Write(this, LogLevel.WARN, "Sync announce for {0} failed with {1}", accountID, ex);
                if (ex is MessageValueException)
                    state.LocalSyncAnnounce = null; // reset
                return false;
            } finally {
                state.LastAnnounceUtc = DateTime.UtcNow;
                Persist(accountID, state);
            }
        }

        private bool DoesAccountInfoNeedUpdated(SyncAnnounce myCopy, SyncAnnounce peersCopy) {
            return myCopy == null
                || myCopy.UpdatedUtc < peersCopy.UpdatedUtc;
        }

        public void OnSyncAnnounceReceived(SyncAnnounce peersCopy, string senderIP) {
            // Validate the SyncAnnounce.MyEndPoint
            if (peersCopy.MyEndPoint == null
                || !peersCopy.MyEndPoint.StartsWith(senderIP + ":")) {
                _Log.Write(this, LogLevel.WARN, "Remote IP {0} sent a bad SyncAnnounce, claiming endpoint '{1}'",
                    senderIP,
                    peersCopy.MyEndPoint);
                return;
            }

            string accountID = peersCopy.Request.FirstArgument;

            if (!Helpers.IsIDValid(accountID)) {
                _Log.Write(this, LogLevel.WARN, "Remote IP {0} sent a bad SyncAnnounce, with account ID '{1}'",
                    senderIP,
                    accountID ?? String.Empty);
                return;
            }

            SyncState state;
            if (!_MemCached.TryGetValue(accountID, out state)) {
                // check disk
                _Persisted.TryGetValue(accountID, out state);
                if (state == null)
                    state = new SyncState();
                _MemCached[accountID] = state;
            }
            lock (state.IncomingEndPoints) {
                if (!state.IncomingEndPoints.Contains(peersCopy.MyEndPoint))
                    state.IncomingEndPoints.Add(peersCopy.MyEndPoint);
            }
            if (state.Status == SyncStatus.InProgress
                || state.Status == SyncStatus.Enqueued
                || state.Status == SyncStatus.Deferring)
                return;
            state.Status = SyncStatus.Enqueued;
            Task.Run(() => PullAccount(accountID, state, peersCopy));
        }

        /// <summary>
        /// Attempts to synchronise the specified account according the SyncAnnounce contents.
        /// </summary>
        /// <returns>
        /// True if the sync completed successfully. Otherwise false, which indicates that the pull
        /// should be deferred to another time.
        /// </returns>
        private async Task<bool> PullAccount(string accountID, SyncState state, SyncAnnounce peersCopy) {
            CMResult hr;
            lock (state) {
                if (state.Status == SyncStatus.InProgress)
                    return false;
                state.Status = SyncStatus.InProgress;
            }

            try {
                SyncAnnounce myCopy = state.LocalSyncAnnounce;

                // make sure the account still exists and hasn't been deleted off disk
                if (myCopy != null
                    && !_Store.DoesAccountExist(accountID)) {
                    _Log.Write(this, LogLevel.INFO, "Account {0} re-syncing due to loss.", accountID);
                    myCopy = null;
                }

                if (myCopy == null) {
                    GenerateSyncAnnounce(accountID, out myCopy);
                    state.LocalSyncAnnounce = myCopy;
                }

                if (DoesAccountInfoNeedUpdated(myCopy, peersCopy)) {
                    // We set to true when the deferred attempts have all failed
                    // and the only copy in existence is that of the announcing
                    // peer.
                    bool canSkipNetworkCorroboration = false;

                    var a = await _Store.TryFindOnNetwork<Account>(Constants.PATH_ACCNT + "/" + accountID);

                    if (a == null || !a.ConsensusOK) {
                        if (!_Deferred.ContainsKey(accountID)) {
                            state.RemoteSyncAnnounce = peersCopy;
                            state.LastPullUtc = DateTime.UtcNow;
                            state.PullDeferalCount = 0;
                            _Deferred[accountID] = state;
                        } else {
                            state.PullDeferalCount++;
                        }

                        if (state.PullDeferalCount >= _Intervals.DeferredRetryIntervals.Length) {
                            // We've reached the end of the deferral waiting limit.
                            // If a record exists and it's better than ours, then
                            // we'll take the transient one, if we can...
                            if (a == null) {
                                // The responsible peers don't know about this account, try pulling it from any senders
                                // who have announced a copy of it.
                                List<Account> copies = new List<Account>();
                                foreach (var peer in state.IncomingEndPoints) {
                                    using (var conn = await _DHT.Connect(peer)) {
                                        if (conn.IsValid) {
                                            var peerData = await conn.Connection.SendAndReceive("GET", null, Constants.PATH_ACCNT + "/" + accountID);
                                            if (peerData.Response.IsSuccessful) {
                                                var peerAccount = peerData.Cast<Account>();
                                                if (String.Equals(accountID, peerAccount.ID, StringComparison.OrdinalIgnoreCase)) {
                                                    //a = peerAccount;
                                                    copies.Add(peerAccount);
                                                } else {
                                                    _Log.Write(this, LogLevel.WARN, "Peer {0} returned account ID {1} upon request of {2}. ", peersCopy.MyEndPoint, peerAccount.ID, accountID);
                                                }
                                            }
                                        }
                                    }
                                }

                                // We still attempt a majority consensus when
                                // pulling from announcers.
                                Helpers.CheckConsensus<Account>(copies, out a);

                                if (a == null) {
                                    if (VerboseSyncDebugging) {
                                        _Log.Write(this, LogLevel.WARN, "Given up deferring sync of {0} after {1} tries. And the announcing peer doesn't work.", accountID, state.PullDeferalCount);
                                    }
                                    return true; // nothing to work from
                                }
                            }
                            canSkipNetworkCorroboration = true;
                            _Log.Write(this, LogLevel.WARN, "Given up deferring sync of {0} after {1} tries.", accountID, state.PullDeferalCount);
                            state.Status = SyncStatus.InProgress;
                        } else {
                            state.Status = SyncStatus.Deferring;
                            // Try again later.
                            if (VerboseSyncDebugging) {
                                _Log.Write(this, LogLevel.WARN, "Deferred sync of {0} {1} time(s).", accountID, state.PullDeferalCount);
                            }
                            return false;
                        }
                    }


                    System.Diagnostics.Debug.Assert(a != null, "Account shouldn't be null here.");
                    if ((myCopy == null || myCopy.UpdatedUtc < a.UpdatedUtc)) {
                        // Storage.Put will validate the account signatures etc
                        var res = await _Store.Put(a);
                        hr = res.Code;
                        if (hr != CMResult.S_OK) {
                            _Log.Write(this, LogLevel.WARN, "Failed to put account {0}: {1}", a.ID, hr);
                            return false;
                        }
                        // Storage.Put will also do a final QueryCommit against the network, prior to commit
                        // if we have a corroborated network copy.
                        hr = await _Store.Commit(res.Token,
                            canSkipNetworkCorroboration,
                            sendPushNotifications: false);
                        if (hr != CMResult.S_OK) {
                            _Log.Write(this, LogLevel.WARN, "Failed to commit account {0}: {1}", a.ID, hr);
                            return false;
                        }
                        _Log.Write(this, LogLevel.INFO, "Account sync committed OK for {0}", a.ID);
                    } else {
                        _Log.Write(this, LogLevel.INFO, "Skipping pull of account {0} because mine is newer.", a.ID);
                    }
                    state.PullDeferalCount = 0;
                    state.RemoteSyncAnnounce = null;
                }

                await SyncTransactions(accountID, myCopy, peersCopy);
                await SyncVotes(accountID, myCopy, peersCopy);

                state.LastPullUtc = DateTime.UtcNow;

                return true;
            } finally {
                if (state.Status == SyncStatus.InProgress) {
                    state.Status = IsCurrentlyResponsible(accountID) ? SyncStatus.ResponsibleForAccount
                                 : SyncStatus.NotResponsibleForAccount;
                }
                Persist(accountID, state);
            }
        }

        private async Task SyncTransactions(string accountID, SyncAnnounce myCopy, SyncAnnounce peersCopy) {
            if (myCopy != null
                && Helpers.IsHashEqual(myCopy.TransactionsHash, peersCopy.TransactionsHash))
                return; // nothing to do

            // Pull transactions from the announcing peer
            var list = await ListRemoteItems(peersCopy.MyEndPoint, accountID, Constants.PATH_TRANS);
            var remoteTrans = new Dictionary<string, TransactionIndex>();
            for (int i = 0; i < list.Count; i++) {
                var t = new TransactionIndex(list[i]);
                remoteTrans.Add(t.ID, t);
            }

            // Pull transactions from local storage
            var local = ListLocalItems(accountID, Constants.PATH_TRANS);
            var localTrans = new Dictionary<string, TransactionIndex>();
            for (int i = 0; i < local.Count; i++) {
                var t = new TransactionIndex(local[i]);
                localTrans.Add(t.ID, t);
            }

            // Compare
            var toSync = new List<TransactionIndex>();
            foreach (var kp in remoteTrans) {
                TransactionIndex mine;
                if (!localTrans.TryGetValue(kp.Key, out mine)
                    || mine.UpdatedUtc < kp.Value.UpdatedUtc) {
                    toSync.Add(kp.Value);
                }
            }

            if (toSync.Count == 0)
                return;

            _Log.Write(this, LogLevel.INFO, "Syncing {0} transactions for {1}", toSync.Count, accountID);

            var startTime = Clock.Elapsed;
            for (int i = 0; i < toSync.Count; i++) {
                var path = Constants.PATH_TRANS + "/" + toSync[i].ID;

                // Always prefer the corroborated network copy
                var t = await _Store.TryFindOnNetwork<Transaction>(path);
                bool canSkipNetworkCorroboration = false;
                // If the network doesn't know about the transaction
                // we'll take the announcer's copy.
                if (t == null) {
                    using (var conn = await _DHT.Connect(peersCopy.MyEndPoint)) {
                        if (conn.IsValid) {
                            var m = await conn.Connection.SendAndReceive("GET", null, path);
                            if (m.Response.IsSuccessful) {
                                t = m.Cast<Transaction>();
                                canSkipNetworkCorroboration = true;
                            }
                        }
                    }
                }

                if (t == null) {
                    _Log.Write(this, LogLevel.WARN, "Transaction {0} for {1} is not on network.", toSync[i].ID, accountID);
                    continue;
                }

                System.Diagnostics.Debug.Assert(String.Compare(toSync[i].ID, t.ID, true) == 0, "Mismatched IDs should never happen.");

                if (String.Compare(toSync[i].ID, t.ID, true) != 0) {
                    _Log.Write(this, LogLevel.WARN, "Transaction {0} for {1} inexplicably came in as {2}.", toSync[i].ID, accountID, t.ID);
                    continue;
                }

                // Check one last time that the network copy is better than ours
                TransactionIndex mine;
                if (!localTrans.TryGetValue(toSync[i].ID, out mine)
                    || mine.UpdatedUtc < t.UpdatedUtc) {
                    // Put will validate the transaction
                    var res = await _Store.Put(t);
                    CMResult hr = res.Code;
                    if (hr != CMResult.S_OK) {
                        _Log.Write(this, LogLevel.WARN, "Failed to put transaction {0}: {1}", t.ID, hr);
                        continue;
                    }
                    // Store will do a final QueryCommit against the network, prior to commit.
                    hr = await _Store.Commit(res.Token,
                        canSkipNetworkCorroboration,
                        sendPushNotifications: false);
                    if (hr != CMResult.S_OK) {
                        _Log.Write(this, LogLevel.WARN, "Failed to commit transaction {0}: {1}", t.ID, hr);
                        continue;
                    }
                }
            }
            _Log.Write(this, LogLevel.INFO, "Sync of {0} transactions for {1} completed in {2}.", toSync.Count, accountID, (Clock.Elapsed - startTime));
        }

        private async Task SyncVotes(string accountID, SyncAnnounce myCopy, SyncAnnounce peersCopy) {
            if (myCopy != null
                && Helpers.IsHashEqual(myCopy.VotesHash, peersCopy.VotesHash))
                return; // nothing to do

            // Pull votes from the announcing peer
            var list = await ListRemoteItems(peersCopy.MyEndPoint, accountID, Constants.PATH_VOTES);
            var remoteVotes = new Dictionary<uint, VoteIndex>();
            for (int i = 0; i < list.Count; i++) {
                var t = new VoteIndex(list[i]);
                remoteVotes.Add(t.PropositionID, t);
            }

            // Pull votes from local storage
            var local = ListLocalItems(accountID, Constants.PATH_VOTES);
            var localVotes = new Dictionary<uint, VoteIndex>();
            for (int i = 0; i < local.Count; i++) {
                var t = new VoteIndex(local[i]);
                localVotes.Add(t.PropositionID, t);
            }

            // Compare
            var toSync = new List<VoteIndex>();
            foreach (var kp in remoteVotes) {
                VoteIndex mine;
                if (!localVotes.TryGetValue(kp.Key, out mine)
                    || mine.UpdatedUtc < kp.Value.UpdatedUtc) {
                    toSync.Add(kp.Value);
                }
            }

            if (toSync.Count == 0)
                return;

            _Log.Write(this, LogLevel.INFO, "Syncing {0} votes for {1}", toSync.Count, accountID);

            var startTime = Clock.Elapsed;
            for (int i = 0; i < toSync.Count; i++) {
                var path = Constants.PATH_VOTES + "/" + toSync[i].PropositionID + "/" + toSync[i].VoterID;

                // Always prefer the corroborated network copy
                var v = await _Store.TryFindOnNetwork<Vote>(path);
                bool canSkipNetworkCorroboration = false;
                // If the network doesn't know about the vote we'll take the announcer's copy.
                if (v == null) {
                    using (var conn = await _DHT.Connect(peersCopy.MyEndPoint)) {
                        if (conn.IsValid) {
                            var m = await conn.Connection.SendAndReceive("GET", null, path);
                            if (m.Response.IsSuccessful) {
                                v = m.Cast<Vote>();
                                canSkipNetworkCorroboration = true;
                            }
                        }
                    }
                }

                if (v == null) {
                    _Log.Write(this, LogLevel.WARN, "Vote {0} for {1} is not on the network.",
                        toSync[i].PropositionID, accountID);
                    continue;
                }

                if (toSync[i].PropositionID != v.PropositionID
                    || toSync[i].VoterID != v.VoterID) {
                    System.Diagnostics.Debug.Assert(false, "Mismatched vote IDs should never happen.");

                    _Log.Write(this, LogLevel.WARN, "Vote '{0}' for {1} inexplicably came in as '{2}'.",
                        toSync[i].PropositionID + "/" + toSync[i].VoterID,
                        accountID,
                        v.PropositionID + "/" + v.VoterID);
                    continue;
                }

                // Check one last time that the network copy is better than ours
                VoteIndex mine;
                if (!localVotes.TryGetValue(toSync[i].PropositionID, out mine)
                    || mine.UpdatedUtc < v.UpdatedUtc) {
                    // Put will validate the vote
                    var res = await _Store.Put(v);
                    CMResult hr = res.Code;
                    if (hr != CMResult.S_OK) {
                        _Log.Write(this, LogLevel.WARN, "Failed to put vote {0}: {1}", v.PropositionID + "/" + v.VoterID, hr);
                        continue;
                    }
                    // Store will do a final QueryCommit against the network, prior to commit.
                    hr = await _Store.Commit(res.Token,
                        canSkipNetworkCorroboration,
                        sendPushNotifications: false);
                    if (hr != CMResult.S_OK) {
                        _Log.Write(this, LogLevel.WARN, "Failed to commit vote {0}: {1}", v.PropositionID + "/" + v.VoterID, hr);
                        continue;
                    }
                }
            }
            _Log.Write(this, LogLevel.INFO, "Sync of {0} votes for {1} completed in {2}.", toSync.Count, accountID, (Clock.Elapsed - startTime));
        }

        private async Task<List<string>> ListRemoteItems(string remoteEndpoint, string id, string type) {
            var ar = new List<string>();
            uint start = 0;
            uint total = 0;
            using (var conn = await _DHT.Connect(remoteEndpoint)) {
                if (conn.IsValid) {
                    do {
                        var m = await conn.Connection.SendAndReceive("LIST", new ListRequest() {
                            APIVersion = Constants.APIVersion,
                            UpdatedUtcFromInclusive = Constants.MinimumSaneDateTime,
                            UpdatedUtcToExclusive = DateTime.UtcNow.AddDays(1),
                            Sort = "UPD-UTC DESC",
                            Max = PAGINATION_SIZE,
                            StartAt = start
                        }, Constants.PATH_ACCNT + "/" + id + "/" + type);
                        Schema.ListResponse res = m.Cast<Schema.ListResponse>();
                        if (total == 0) {
                            total = res.Total;
                        }
                        for (int i = 0; i < res.Values.Count; i++) {
                            if (res.Values[i].Name == "ITEM") {
                                ar.Add(res.Values[i].Value);
                            }
                        }
                        start += res.Count;
                    } while (start < total);
                }
            }
            return ar;
        }

        private void Persist(string id, SyncState state) {
            _Persisted.Set(id, state);
        }

        /// <summary>
        /// Generates a SyncAnnounce payload which can be used to
        /// announce an account latest copy, or to compare it with
        /// another peer's incoming announcement.
        /// </summary>
        /// <param name="id">The account ID to inspect.</param>
        /// <param name="sync">Pointer to receive the SyncAnnounce.</param>
        /// <returns>True if the account exists locally, otherwise false.</returns>
        private bool GenerateSyncAnnounce(string id, out SyncAnnounce sync) {
            sync = null;
            IStorable item;
            _Store.Get(Constants.PATH_ACCNT + "/" + id, out item);
            if (item == null) {
                // Item doesn't exist, remove it from MemCached. EnsureAccountState will
                // raise when/if the .htindex/data files become re-created.
                SyncState s;
                _MemCached.TryRemove(id, out s);
                _Persisted.TryRemove(id, out s);
                return false;
            }
            sync = new SyncAnnounce() {
                APIVersion = Constants.APIVersion,
                UpdatedUtc = item.UpdatedUtc,
                MyEndPoint = _DHT.MyEndpoint
            };
            sync.TransactionsHash = HashAccountItems(id, Constants.PATH_TRANS);
            sync.VotesHash = HashAccountItems(id, Constants.PATH_VOTES);
            return true;
        }

        private byte[] HashAccountItems(string id, string type) {
            var timestamps = ListLocalItemUpdatedUTCs(id, type);
            var ar = new List<byte>();
            for (int i = 0; i < timestamps.Count; i++) {
                ar.AddRange(Encoding.UTF8.GetBytes(timestamps[i]));
            }
            if (ar.Count == 0)
                return null;
            return CryptoFunctions.Identity.SHA256Hash(ar.ToArray());
        }

        private List<string> ListLocalItemUpdatedUTCs(string id, string type) {
            if (type != Constants.PATH_TRANS
                && type != Constants.PATH_VOTES)
                throw new NotImplementedException();

            var ar = new List<string>();
            Schema.ListResponse res;
            uint start = 0;
            uint total = 0;
            do {
                var hr = _Store.List(new Schema.ListRequest() {
                    Request = new Message.RequestHeader("LIST", "", "ACCNT/" + id + "/" + type),
                    UpdatedUtcFromInclusive = Constants.MinimumSaneDateTime,
                    UpdatedUtcToExclusive = DateTime.UtcNow.AddDays(1),
                    Max = PAGINATION_SIZE,
                    StartAt = start
                }, out res);
                if (hr != CMResult.S_OK || res == null) {
                    _Log.Write(this, LogLevel.WARN, "Problem with ListLocalItems '" + id + "' - " + hr);
                    break;
                }
                if (total == 0)
                    total = res.Total;
                for (int i = 0; i < res.Values.Count; i++) {
                    if (res.Values[i].Name == "ITEM") {
                        ar.Add(res.Values[i].Value.Split(' ')[4]);
                    }
                }
                start += res.Count;
            } while (start < total);

            // Account object hashes are hashed in Ascending order.
            ar.Sort();

            return ar;
        }

        private List<string> ListLocalItems(string id, string type) {
            var ar = new List<string>();
            Schema.ListResponse res;
            uint start = 0;
            uint total = 0;
            do {
                var hr = _Store.List(new Schema.ListRequest() {
                    Request = new Message.RequestHeader("LIST", "", "ACCNT/" + id + "/" + type),
                    UpdatedUtcFromInclusive = Constants.MinimumSaneDateTime,
                    UpdatedUtcToExclusive = DateTime.UtcNow.AddDays(1),
                    Sort = "UTC ASC",
                    Max = PAGINATION_SIZE,
                    StartAt = start
                }, out res);
                if (hr != CMResult.S_OK || res == null) {
                    _Log.Write(this, LogLevel.WARN, "Problem with ListLocalItems '" + id + "' - " + hr);
                    break;
                }
                if (total == 0)
                    total = res.Total;
                for (int i = 0; i < res.Values.Count; i++) {
                    if (res.Values[i].Name == "ITEM") {
                        ar.Add(res.Values[i].Value);
                    }
                }
                start += res.Count;
            } while (start < total);
            return ar;
        }

        private async Task<bool> AnnounceToPeer(string peer, string accountID, SyncAnnounce sync) {
            using (var conn = await _DHT.Connect(peer)) {
                if (conn.IsValid) {
                    var res = await conn.Connection.SendAndReceive("SYNC", sync, accountID);
                    return res.Response.IsSuccessful;
                } else {
                    return false;
                }
            }
        }

        private void EnsureAccountState(string id, bool invalidate) {
            SyncState sync;
            if (!_MemCached.TryGetValue(id, out sync)) {
                // try load our disk-persisted state
                _Persisted.TryGetValue(id, out sync);
                if (sync == null)
                    sync = new SyncState();
                _MemCached[id] = sync;
            }
            if (invalidate)
                sync.LocalSyncAnnounce = null;
        }

        private CMResult UpdateAccountStates() {
            Schema.ListResponse res;
            CMResult hr;
            if ((hr = _Store.List(new Schema.ListRequest() {
                Request = new Message.RequestHeader("LIST", null, Constants.PATH_ACCNT + "/"),
                UpdatedUtcFromInclusive = Constants.MinimumSaneDateTime,
                UpdatedUtcToExclusive = DateTime.UtcNow.AddDays(1),
                Max = uint.MaxValue,
                StartAt = 0
            }, out res)) != CMResult.S_OK)
                return hr;
            for (int i = 0; i < res.Values.Count; i++) {
                if (res.Values[i].Name == "ITEM") {
                    EnsureAccountState(res.Values[i].Value, false);
                }
            }
            return CMResult.S_OK;
        }

        public void Dispose() {
            _Store = null;
            _Persisted?.Dispose();
        }

        internal enum SyncStatus {
            None = 0,
            ResponsibleForAccount,
            NotResponsibleForAccount,
            InProgress,
            NoPeersAvailable,
            StorageError,
            Deferring,
            Enqueued
        }

        internal class SyncState {

            /// <summary>
            /// For future changes to SyncState.
            /// </summary>
            public byte Version = 2;

            public byte PullDeferalCount;

            /// <summary>
            /// Defaults to UtcNow so that announcements don't happen as soon as
            /// new accounts arrive.
            /// </summary>
            public DateTime LastAnnounceUtc;

            public SyncAnnounce RemoteSyncAnnounce;
            public SyncAnnounce LocalSyncAnnounce;
            public DateTime LastPullUtc;

            /// <summary>
            /// The status is not serialised, but used for in-memory reference/diagnostics.
            /// </summary>
            public SyncStatus Status;

            /// <summary>
            /// Non-serialised list of endpoints who have announced the account.
            /// </summary>
            public List<string> IncomingEndPoints = new List<string>();

            public SyncState() {
                LastAnnounceUtc = DateTime.UtcNow;
            }

            public SyncState(BinaryReader br) {
                Version = br.ReadByte();
                PullDeferalCount = br.ReadByte();
                long time = br.ReadInt64();
                LastAnnounceUtc = new DateTime(time);
                time = br.ReadInt64();
                LastPullUtc = new DateTime(time);
                string announce = br.ReadString();
                if (!String.IsNullOrEmpty(announce))
                    RemoteSyncAnnounce = new SyncAnnounce(announce);
                if (Version < 2)
                    return;
                announce = br.ReadString();
                if (!String.IsNullOrEmpty(announce))
                    LocalSyncAnnounce = new SyncAnnounce(announce);
                if (Version < 3)
                    return;
            }

            public void Serialise(BinaryWriter bw) {
                bw.Write(Version);
                bw.Write(PullDeferalCount);
                bw.Write(LastAnnounceUtc.Ticks);
                bw.Write(LastPullUtc.Ticks);
                bw.Write(RemoteSyncAnnounce != null ? RemoteSyncAnnounce.ToContentString() : String.Empty);
                bw.Write(LocalSyncAnnounce != null ? LocalSyncAnnounce.ToContentString() : String.Empty);
            }
        }

        /// <summary>
        /// To reduce network chatter, we keep a history of which peers
        /// we've announced a particular version of an account to. We will
        /// announce at most 5 times.
        /// </summary>
        private class PeerSyncHistory {

            public class Destination {
                public string Endpoint;
                public int SentCount;
                public TimeSpan LastSent;
            }

            public List<Destination> Destinations = new List<Destination>();
            public byte[] TransactionsHash;
            public byte[] VotesHash;
            public DateTime UpdatedUtc;
            private object _Sync = new object();

            /// <summary>
            /// Determines whether or not an announcements should be made to the specified
            /// peer based on current SynchronisationIntervals.
            /// </summary>
            public bool ShouldAnnounce(SynchronisationIntervals intervals, string endpoint) {
                lock (_Sync) {
                    for (int i = 0; i < Destinations.Count; i++) {
                        var d = Destinations[i];
                        if (d.Endpoint == endpoint) {
                            return d.SentCount < intervals.MaxPerPeerAnnouncementRepeats
                                || (Clock.Elapsed - d.LastSent) > intervals.RepeatPeerAnnouncementAlwaysEvery;
                        }
                    }
                    return true;
                }
            }

            /// <summary>
            /// Increments the successful send counter for the specified end point.
            /// </summary>
            public void Increment(string endpoint) {
                lock (_Sync) {
                    for (int i = 0; i < Destinations.Count; i++) {
                        var d = Destinations[i];
                        if (d.Endpoint == endpoint) {
                            d.SentCount++;
                            d.LastSent = Clock.Elapsed;
                            return;
                        }
                    }
                    Destinations.Add(new Destination() {
                        Endpoint = endpoint,
                        SentCount = 1,
                        LastSent = Clock.Elapsed
                    });
                }
            }

            /// <summary>
            /// Determines whether the account has changed, and if so, resets
            /// the history so that an announcement will be made to everybody.
            /// </summary>
            public void Update(SyncAnnounce sync) {
                lock (_Sync) {
                    if (!Helpers.IsHashEqual(sync.TransactionsHash, TransactionsHash)
                        || !Helpers.IsHashEqual(sync.VotesHash, VotesHash)
                        || sync.UpdatedUtc != UpdatedUtc) {
                        UpdatedUtc = sync.UpdatedUtc;
                        TransactionsHash = sync.TransactionsHash;
                        VotesHash = sync.VotesHash;
                        for (int i = 0; i < Destinations.Count; i++)
                            Destinations[i].SentCount = 0;
                    }
                }
            }
        }
    }

#if DEBUG
    /// <summary>
    /// Offline SynchronisationManager.SyncState inspection utility.
    /// </summary>
    public class SynchronisationUtil {
        public static string TryExtractSyncStateInformation(string syncFile, string id) {
            using (var dic = new LinearHashTable<string, SynchronisationManager.SyncState>(
                syncFile, Storage.Container.OnHashKey,
                    Storage.Container.OnSerialiseKey,
                    Storage.Container.OnDeserialiseKey,
                    SynchronisationManager.OnSerialiseSyncState,
                    SynchronisationManager.OnDeserialiseSyncState)) {
                SynchronisationManager.SyncState state;
                dic.TryGetValue(id, out state);
                return state != null ? Newtonsoft.Json.JsonConvert.SerializeObject(state) : "Not found";
            }

        }
    }
#endif
}