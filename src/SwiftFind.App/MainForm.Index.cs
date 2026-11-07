using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwiftFind.Core;
using SwiftFind.Forms;
using SwiftFind.Indexing;
using SwiftFind.Models;
using SwiftFind.Services;

namespace SwiftFind
{
    public partial class MainForm
    {
        bool indexing, enriching, cacheDirty, purging, driveEventsHooked;
        CancellationTokenSource indexCts, enrichCts;
        System.Windows.Forms.Timer saveTimer, refreshTimer;

        /// <summary>Roots currently represented in the index, with the volume serial seen when each was indexed.</summary>
        readonly Dictionary<string, uint> indexedRoots = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        sealed class IndexOutcome
        {
            public FileEntry[] Merged;
            public int NewItems;
            public string Method;
            public string FallbackReason;
        }

        sealed class LoadOutcome
        {
            public FileEntry[] Entries = new FileEntry[0];
            public Dictionary<string, uint> Roots = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            public string Method = "none";
            public DateTime BuiltAt = DateTime.MinValue;
        }

        // ================================================================ startup

        /// <summary>Loads the saved index off the UI thread, drops anything on drives that are gone, then catches up.</summary>
        void LoadCacheThenMaybeIndex()
        {
            SetStatus("Starting\u2026", true);
            Task.Factory.StartNew(() =>
            {
                DriveMonitor.Shared.RefreshNow();            // may take a moment; never on the UI thread
                var result = new LoadOutcome();
                IndexStore.Snapshot snap = IndexStore.Load();
                if (snap == null || !string.Equals(snap.Scope, IndexScope.Key(settings), StringComparison.OrdinalIgnoreCase))
                    return result;

                var keep = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
                foreach (string root in IndexScope.Roots(settings))
                {
                    uint was;
                    if (!snap.Roots.TryGetValue(root, out was)) continue;
                    uint now = IndexScope.SerialOf(root);
                    if (was == 0 || now == 0 || was == now) keep[root] = now != 0 ? now : was;   // same disk -> cache still valid
                }
                if (keep.Count == 0) return result;

                bool everythingStillThere = keep.Count == snap.Roots.Count;
                IEnumerable<FileEntry> kept = snap.Entries;
                if (!everythingStillThere)
                {
                    string[] keys = keep.Keys.ToArray();
                    kept = snap.Entries.Where(e => keys.Any(r => IndexScope.UnderRoot(r, e.FullPath)));
                }
                result.Entries = kept.ToArray();
                result.Roots = keep;
                result.Method = snap.Method;
                result.BuiltAt = snap.BuiltAt;
                return result;
            }).ContinueWith(t =>
            {
                if (IsDisposed) return;
                if (!t.IsFaulted && t.Result != null)
                {
                    index = t.Result.Entries;
                    indexedRoots.Clear();
                    foreach (var kv in t.Result.Roots) indexedRoots[kv.Key] = kv.Value;
                    indexMethod = t.Result.Method;
                    indexBuiltAt = t.Result.BuiltAt;
                }
                else if (t.IsFaulted) Log.Error("Startup load", t.Exception);

                UpdateScopeLabel();
                SetStatus("Ready");
                HookDriveEvents();
                ReconcileDrives(true);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void HookDriveEvents()
        {
            if (driveEventsHooked) return;
            driveEventsHooked = true;
            DriveMonitor.Shared.Changed += () => { try { BeginInvoke((Action)OnDrivesChanged); } catch { } };
            DriveMonitor.Shared.Start();

            refreshTimer = new System.Windows.Forms.Timer { Interval = 10 * 60 * 1000 };
            refreshTimer.Tick += (s, e) =>
            {
                if (indexing || enriching || settings.AutoRefreshHours <= 0 || indexMethod != "usn" || !IsAdministrator()) return;
                if ((DateTime.Now - indexBuiltAt).TotalHours >= settings.AutoRefreshHours) StartIndexing(null);
            };
            refreshTimer.Start();
        }

        // ================================================================ drives coming and going

        void OnDrivesChanged()
        {
            if (IsDisposed) return;
            UpdateScopeLabel();
            ReconcileDrives(false);
        }

        /// <summary>
        /// Brings the index in line with the drives that are connected right now:
        /// removed or replaced drives are purged, new ones are indexed (only those, not everything).
        /// </summary>
        void ReconcileDrives(bool startup)
        {
            if (indexing || purging) return;   // the running job calls ReconcileDrives again when it finishes

            List<string> roots = IndexScope.Roots(settings);
            bool knownDrives = DriveMonitor.Shared.Drives.Count > 0;

            var gone = indexedRoots.Keys.Where(r =>
            {
                if (!roots.Contains(r, StringComparer.OrdinalIgnoreCase)) return true;           // removed or now excluded
                if (!settings.IndexAllDrives && knownDrives && !IndexScope.IsOnline(r)) return true; // folder's drive unplugged
                uint now = IndexScope.SerialOf(r), was = indexedRoots[r];
                return now != 0 && was != 0 && now != was;                                        // different disk, same letter
            }).ToList();

            if (gone.Count > 0)
            {
                PurgeRoots(gone);
                return;
            }

            var add = roots.Where(r => !indexedRoots.ContainsKey(r) && (settings.IndexAllDrives || !knownDrives || IndexScope.IsOnline(r))).ToList();

            bool stale = settings.AutoRefreshHours > 0 && indexBuiltAt != DateTime.MinValue &&
                         (DateTime.Now - indexBuiltAt).TotalHours > settings.AutoRefreshHours;
            bool cheapRefresh = stale && indexMethod == "usn" && IsAdministrator();

            if (startup && (settings.AutoIndexOnStartup || cheapRefresh || (indexedRoots.Count == 0 && add.Count > 0)))
            {
                StartIndexing(null);
                return;
            }
            if (add.Count > 0)
            {
                if (!startup) SetStatus("New drive detected: " + string.Join(", ", add.Select(a => a.TrimEnd('\\'))) + " \u2014 indexing it now\u2026", true);
                StartIndexing(add);
                return;
            }

            ApplySearch();
            if (startup && index.Any(f => !f.HasDetails)) StartEnrichment();
        }

        void PurgeRoots(List<string> goneRoots)
        {
            purging = true;
            FileEntry[] snapshot = index;
            string[] gone = goneRoots.ToArray();
            SetStatus("Drive " + string.Join(", ", gone.Select(g => g.TrimEnd('\\'))) + " is no longer connected \u2014 removing its files from the list\u2026", true);

            Task.Factory.StartNew(() => snapshot.Where(e => !gone.Any(r => IndexScope.UnderRoot(r, e.FullPath))).ToArray())
                .ContinueWith(t =>
                {
                    purging = false;
                    if (IsDisposed) return;
                    if (t.IsFaulted) { Log.Error("Purge drive", t.Exception); return; }
                    index = t.Result;
                    foreach (string r in gone) indexedRoots.Remove(r);
                    MarkIndexChanged();
                    SetStatus("Removed " + string.Join(", ", gone.Select(g => g.TrimEnd('\\'))) + " from the index (drive disconnected).");
                    UpdateIndexInfo();
                    ReconcileDrives(false);   // now handle anything that was added in the meantime
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // ================================================================ indexing

        void ToggleIndexing()
        {
            if (indexing) { if (indexCts != null) indexCts.Cancel(); SetStatus("Cancelling\u2026", true); }
            else StartIndexing(null);
        }

        /// <param name="only">Index just these roots and merge them in; null = rebuild everything.</param>
        void StartIndexing(List<string> only)
        {
            if (indexing) return;   // ReconcileDrives runs again when the current job finishes

            List<string> targets = only ?? IndexScope.Roots(settings);
            if (!settings.IndexAllDrives && !Directory.Exists(settings.RootPath))
            {
                MessageBox.Show(this, "The folder to index isn't available:\n" + settings.RootPath + "\n\nIs its drive connected? Otherwise choose another folder, or switch to searching everywhere.",
                    "SwiftFind", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (targets.Count == 0) { SetStatus("No drives to index."); ApplySearch(); return; }

            if (enrichCts != null) enrichCts.Cancel();
            indexing = true;
            var cts = indexCts = new CancellationTokenSource();
            btnIndex.Text = "Cancel";
            btnIndex.SetStyle(SwiftFind.Controls.FlatButton.Style.Primary);
            HideEmpty();
            SetStatus("Indexing " + string.Join(", ", targets.Select(r => r.TrimEnd('\\'))) + "\u2026", true);

            var filter = new PathFilter(settings.ExcludeFolders, settings.ExcludeExtensions);
            FileEntry[] before = index;
            var serials = targets.ToDictionary(r => r, r => IndexScope.SerialOf(r), StringComparer.OrdinalIgnoreCase);
            bool lowPriority = settings.IndexPriority != "Fast";
            string priority = settings.IndexPriority;
            int cores = Environment.ProcessorCount;
            long ram = Native.TotalRamBytes();
            int baseCount = 0;
            Action<int> progress = n =>
            {
                int total = baseCount + n;
                try { BeginInvoke((Action)(() => { if (indexing) lblCount.Text = total.ToString("N0") + " items\u2026"; })); } catch { }
            };

            Task.Factory.StartNew(() =>
            {
                ThreadPriority oldPriority = Thread.CurrentThread.Priority;
                if (lowPriority) Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                try
                {
                    var found = new List<FileEntry>();
                    var outcome = new IndexOutcome();
                    Func<bool> cancelled = () => cts.IsCancellationRequested;
                    int usnCount = 0, scanCount = 0;

                    for (int i = 0; i < targets.Count; i++)
                    {
                        string root = targets[i];
                        if (cancelled()) return null;
                        DriveDetails drive = IndexScope.DriveFor(root);
                        int threads = IndexTuning.ScanThreads(drive != null ? drive.Kind : DriveKind.Unknown, drive != null && drive.IsExternal, priority, cores, ram);
                        string label = "Indexing " + root.TrimEnd('\\') + (drive != null ? " (" + drive.KindText + ")" : "") + (targets.Count > 1 ? "  " + (i + 1) + " of " + targets.Count : "") + "\u2026";
                        try { BeginInvoke((Action)(() => { if (indexing) SetStatus(label, true); })); } catch { }

                        List<FileEntry> part = null;
                        if (UsnIndexer.IsSupported(root))
                        {
                            part = UsnIndexer.TryIndex(root, filter, progress, cancelled);
                            if (part != null) usnCount++;
                            else if (outcome.FallbackReason == null) outcome.FallbackReason = UsnIndexer.LastFailure;
                        }
                        if (cancelled()) return null;
                        if (part == null)
                        {
                            part = FastScanner.Index(root, filter, threads, lowPriority, progress, cancelled);
                            if (part == null) return null;
                            scanCount++;
                        }
                        found.AddRange(part);
                        baseCount = found.Count;
                    }

                    outcome.Method = scanCount == 0 ? "usn" : (usnCount == 0 ? "scan" : "mixed");
                    outcome.NewItems = found.Count;
                    if (only == null) outcome.Merged = found.ToArray();
                    else
                    {
                        string[] roots = targets.ToArray();
                        var merged = new List<FileEntry>(before.Length + found.Count);
                        foreach (FileEntry e in before)
                            if (!roots.Any(r => IndexScope.UnderRoot(r, e.FullPath))) merged.Add(e);
                        merged.AddRange(found);
                        outcome.Merged = merged.ToArray();
                    }
                    return outcome;
                }
                finally { if (lowPriority) Thread.CurrentThread.Priority = oldPriority; }
            }, cts.Token).ContinueWith(t =>
            {
                indexing = false;
                btnIndex.Text = "Build index";
                btnIndex.SetStyle(SwiftFind.Controls.FlatButton.Style.Orange);
                if (IsDisposed) return;

                if (t.IsFaulted)
                {
                    Log.Error("Indexing", t.Exception);
                    SetStatus("Indexing failed - see the error log (Help menu).");
                    ApplySearch();
                    return;
                }
                IndexOutcome result = t.IsCanceled ? null : t.Result;
                if (result == null) { SetStatus("Indexing cancelled."); ApplySearch(); return; }

                index = result.Merged;
                if (only == null) indexedRoots.Clear();
                foreach (string r in targets) indexedRoots[r] = serials[r];
                indexMethod = only == null || indexMethod == "none" || indexMethod == result.Method ? result.Method : "mixed";
                indexBuiltAt = DateTime.Now;
                settings.LastIndexMethod = indexMethod;
                settings.LastIndexTicks = indexBuiltAt.Ticks;
                settings.Save();
                cacheDirty = true;
                FlushCacheInBackground();
                ApplySearch();

                string note = result.Method != "usn" && result.FallbackReason != null
                    ? "  (NTFS fast index unavailable: " + result.FallbackReason + ")" : "";
                SetStatus(only == null
                    ? "Indexed " + index.Length.ToString("N0") + " items." + note
                    : "Added " + result.NewItems.ToString("N0") + " items from " + string.Join(", ", targets.Select(r => r.TrimEnd('\\'))) + "." + note);

                if (result.Method != "scan") StartEnrichment();
                ReconcileDrives(false);       // a drive may have come or gone while we were busy
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Reads size/date for USN-sourced entries in the background, gently.</summary>
        void StartEnrichment()
        {
            if (enriching) return;
            FileEntry[] snapshot = index;
            if (snapshot.Length == 0) return;
            enriching = true;
            var cts = enrichCts = new CancellationTokenSource();
            string priority = settings.IndexPriority;
            var kinds = indexedRoots.Keys.Select(r => IndexScope.DriveFor(r)).Where(d => d != null).ToList();
            DriveKind slowest = IndexTuning.Slowest(kinds.Select(d => d.Kind));
            int degree = IndexTuning.EnrichThreads(slowest, kinds.Any(d => d.IsExternal), priority, Environment.ProcessorCount, Native.TotalRamBytes());
            bool lowPriority = priority != "Fast";

            Task.Factory.StartNew(() =>
            {
                DetailsEnricher.Run(snapshot, (done, total) =>
                {
                    int pct = (int)(done * 100L / Math.Max(1, total));
                    try { BeginInvoke((Action)(() => { if (enriching) SetStatus("Reading file sizes and dates\u2026 " + pct + "%", true); })); } catch { }
                }, cts.Token, degree, lowPriority);
            }, cts.Token).ContinueWith(t =>
            {
                enriching = false;
                if (IsDisposed) return;
                if (t.IsFaulted) Log.Error("Enrichment", t.Exception);
                if (t.IsCanceled || cts.IsCancellationRequested) return;
                cacheDirty = true;
                FlushCacheInBackground();
                SetStatus("Index ready.");
                ApplySearch();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void CancelBackgroundWork()
        {
            if (indexCts != null) indexCts.Cancel();
            if (enrichCts != null) enrichCts.Cancel();
            if (searchCts != null) searchCts.Cancel();
        }

        // ================================================================ cache

        void FlushCacheInBackground()
        {
            if (!cacheDirty) return;
            cacheDirty = false;
            FileEntry[] snapshot = index;
            string scope = IndexScope.Key(settings), method = indexMethod;
            DateTime built = indexBuiltAt;
            var roots = new Dictionary<string, uint>(indexedRoots, StringComparer.OrdinalIgnoreCase);
            Task.Factory.StartNew(() => IndexStore.Save(scope, method, built, roots, snapshot));
        }

        void FlushCacheIfDirty()
        {
            if (!cacheDirty || index.Length == 0) return;
            cacheDirty = false;
            IndexStore.Save(IndexScope.Key(settings), indexMethod, indexBuiltAt, new Dictionary<string, uint>(indexedRoots), index);
        }

        /// <summary>Debounced save after small edits (rename, delete, paste).</summary>
        void MarkIndexChanged()
        {
            cacheDirty = true;
            if (saveTimer == null)
            {
                saveTimer = new System.Windows.Forms.Timer { Interval = 4000 };
                saveTimer.Tick += (s, e) => { saveTimer.Stop(); FlushCacheInBackground(); };
            }
            saveTimer.Stop(); saveTimer.Start();
        }

        void ClearCache()
        {
            if (MessageBox.Show(this, "Remove the saved index and rebuild it now?", "Clear saved index",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { if (File.Exists(AppPaths.CacheFile)) File.Delete(AppPaths.CacheFile); }
            catch (Exception ex) { Log.Error("Clear cache", ex); }
            index = new FileEntry[0];
            indexedRoots.Clear();
            cacheDirty = false;
            ApplySearch();
            StartIndexing(null);
        }

        // ================================================================ incremental index edits

        void RemoveFromIndex(string path, bool isDirectory)
        {
            RemoveFromIndex(new[] { new KeyValuePair<string, bool>(path, isDirectory) });
        }

        /// <summary>Removes many paths in a single pass (a folder also removes everything below it).</summary>
        void RemoveFromIndex(IEnumerable<KeyValuePair<string, bool>> items)
        {
            var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var prefixes = new List<string>();
            foreach (var kv in items)
            {
                exact.Add(kv.Key);
                if (kv.Value) prefixes.Add(kv.Key.TrimEnd('\\') + "\\");
            }
            if (exact.Count == 0) return;
            string[] pre = prefixes.ToArray();
            index = index.Where(f => !exact.Contains(f.FullPath) &&
                                     (pre.Length == 0 || !pre.Any(p => f.FullPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))).ToArray();
            MarkIndexChanged();
        }

        void AddToIndex(string path)
        {
            try
            {
                var added = new List<FileEntry>();
                if (Directory.Exists(path))
                {
                    var di = new DirectoryInfo(path);
                    added.Add(new FileEntry { Name = di.Name, FullPath = di.FullName, Directory = di.Parent != null ? di.Parent.FullName : "", IsDirectory = true, Modified = di.LastWriteTime, Flags = (byte)((int)di.Attributes & 6) });
                    var filter = new PathFilter(settings.ExcludeFolders, settings.ExcludeExtensions);
                    var sub = RecursiveIndexer.Index(path, filter);
                    if (sub != null) added.AddRange(sub);
                }
                else if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    added.Add(new FileEntry { Name = fi.Name, FullPath = fi.FullName, Directory = fi.DirectoryName, Size = fi.Length, Modified = fi.LastWriteTime, Flags = (byte)((int)fi.Attributes & 6) });
                }
                if (added.Count == 0) return;
                index = index.Concat(added).ToArray();
                MarkIndexChanged();
            }
            catch (Exception ex) { Log.Error("Add to index", ex); }
        }

        // ================================================================ scope

        void UpdateScopeLabel()
        {
            lblRoot.Text = "\uD83D\uDCC1  " + IndexScope.Describe(settings);
        }

        void UseEverywhere()
        {
            if (settings.IndexAllDrives) return;
            settings.IndexAllDrives = true;
            settings.Save();
            UpdateScopeLabel();
            index = new FileEntry[0];
            indexedRoots.Clear();
            ApplySearch();
            StartIndexing(null);
        }

        /// <summary>Relaunches SwiftFind, optionally elevated (unlocks the NTFS fast index).</summary>
        void RestartApplication(bool elevated, bool keepSettingsFile = false)
        {
            try
            {
                if (keepSettingsFile) skipSettingsSave = true; else settings.Save();
                FlushCacheIfDirty();
                var psi = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath, "--wait-for-previous") { UseShellExecute = true };
                if (elevated) psi.Verb = "runas";
                System.Diagnostics.Process.Start(psi);
                exiting = true;
                Close();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                SetStatus("Restart was cancelled.");
            }
            catch (Exception ex) { Log.Error("Restart", ex); }
        }

        void RestartAsAdministrator() { RestartApplication(true); }

        // ================================================================ folder / preferences

        void ChooseFolder()
        {
            using (var d = new FolderBrowserDialog { Description = "Choose the folder or drive to index", SelectedPath = settings.RootPath })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (!settings.IndexAllDrives && string.Equals(d.SelectedPath, settings.RootPath, StringComparison.OrdinalIgnoreCase)) return;
                settings.RootPath = d.SelectedPath;
                settings.IndexAllDrives = false;
                settings.Save();
                UpdateScopeLabel();
                index = new FileEntry[0];
                indexedRoots.Clear();
                ApplySearch();
                StartIndexing(null);
            }
        }

        void ShowPreferences()
        {
            using (var f = new PreferencesForm(settings))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;

                if (f.ReplacedSettingsFile)           // imported or reset: reload everything
                {
                    RestartApplication(false, true);
                    return;
                }

                settings.Save();
                ApplyRuntimeSettings();

                if (f.AppearanceChanged &&
                    MessageBox.Show(this, "The new look needs SwiftFind to restart.\nRestart now?", "Preferences", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    RestartApplication(false);
                    return;
                }
                if (f.AdminRestartRequested) { RestartAsAdministrator(); return; }

                if (f.IndexSettingsChanged)
                {
                    var r = MessageBox.Show(this, "Your drive or exclusion settings changed.\nRebuild the index now?", "Preferences", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes) { index = new FileEntry[0]; indexedRoots.Clear(); StartIndexing(null); return; }
                    ReconcileDrives(false);
                }
                ApplySearch();
            }
        }

        /// <summary>Applies settings that can change while the app runs.</summary>
        void ApplyRuntimeSettings()
        {
            ApplyMenuState();
            debounce.Interval = Math.Max(1, settings.SearchDelayMs);
            SetupTrayAndHotkey();
            StartupRegistration.Apply(settings.StartWithWindows, settings.StartMinimized);
            UpdateScopeLabel();
        }

        // ================================================================ favourites

        void LoadFavorites()
        {
            try
            {
                if (File.Exists(AppPaths.FavoritesFile))
                    foreach (string line in File.ReadAllLines(AppPaths.FavoritesFile))
                        if (!string.IsNullOrWhiteSpace(line)) favorites.Add(line.Trim());
            }
            catch (Exception ex) { Log.Error("Load favourites", ex); }
        }

        void SaveFavorites()
        {
            try { File.WriteAllLines(AppPaths.FavoritesFile, favorites.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)); }
            catch (Exception ex) { Log.Error("Save favourites", ex); }
        }

        void ToggleFavoritesView()
        {
            favoritesOnly = !favoritesOnly;
            btnFavorites.Active = favoritesOnly;
            ApplyMenuState();
            ApplySearch();
        }

        void ToggleFavoriteSelected()
        {
            var items = SelectedEntries();
            if (items.Count == 0) return;
            bool addAll = items.Any(f => !favorites.Contains(f.FullPath));
            foreach (var f in items)
            {
                if (addAll) favorites.Add(f.FullPath); else favorites.Remove(f.FullPath);
            }
            SaveFavorites();
            ApplySearch();
        }
    }
}
