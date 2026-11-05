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
        bool indexing, enriching, cacheDirty;
        CancellationTokenSource indexCts, enrichCts;
        System.Windows.Forms.Timer saveTimer;

        sealed class IndexOutcome
        {
            public List<FileEntry> Entries;
            public string Method;
            public string FallbackReason;
        }

        // ================================================================ startup

        void LoadCacheThenMaybeIndex()
        {
            SetStatus("Loading saved index\u2026", true);
            Task.Factory.StartNew(() => IndexStore.Load()).ContinueWith(t =>
            {
                if (IsDisposed) return;
                IndexStore.Snapshot snap = t.IsFaulted ? null : t.Result;
                bool sameRoot = snap != null && string.Equals(snap.Root, IndexScope.Key(settings), StringComparison.OrdinalIgnoreCase);
                if (sameRoot)
                {
                    index = snap.Entries.ToArray();
                    indexMethod = snap.Method;
                    indexBuiltAt = snap.BuiltAt;
                }
                SetStatus("Ready");

                // Index automatically on first run, when the saved index doesn't match, or when it is old and refreshing is cheap (admin + NTFS).
                bool stale = sameRoot && indexBuiltAt != DateTime.MinValue && (DateTime.Now - indexBuiltAt).TotalHours > 6;
                bool cheapRefresh = stale && indexMethod != "scan" && IsAdministrator();
                if (settings.AutoIndexOnStartup || !sameRoot || index.Length == 0 || cheapRefresh) StartIndexing();
                else
                {
                    ApplySearch();
                    if (sameRoot && index.Any(f => !f.HasDetails)) StartEnrichment();
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        // ================================================================ indexing

        void ToggleIndexing()
        {
            if (indexing) { if (indexCts != null) indexCts.Cancel(); SetStatus("Cancelling\u2026", true); }
            else StartIndexing();
        }

        void StartIndexing()
        {
            if (indexing) return;
            List<string> roots = IndexScope.Roots(settings);
            if (!settings.IndexAllDrives && !Directory.Exists(settings.RootPath))
            {
                MessageBox.Show(this, "The folder to index doesn't exist:\n" + settings.RootPath + "\n\nChoose another folder, or switch to searching everywhere.", "SwiftFind", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (enrichCts != null) enrichCts.Cancel();
            indexing = true;
            var cts = indexCts = new CancellationTokenSource();
            btnIndex.Text = "Cancel";
            btnIndex.SetStyle(SwiftFind.Controls.FlatButton.Style.Primary);
            HideEmpty();
            SetStatus("Indexing " + IndexScope.Describe(settings) + "\u2026", true);

            var filter = new PathFilter(settings.ExcludeFolders, settings.ExcludeExtensions);
            int baseCount = 0;
            Action<int> progress = n =>
            {
                int total = baseCount + n;
                try { BeginInvoke((Action)(() => { if (indexing) lblCount.Text = total.ToString("N0") + " items\u2026"; })); } catch { }
            };

            Task.Factory.StartNew(() =>
            {
                var outcome = new IndexOutcome { Entries = new List<FileEntry>() };
                Func<bool> cancelled = () => cts.IsCancellationRequested;
                int usnCount = 0, scanCount = 0;

                for (int i = 0; i < roots.Count; i++)
                {
                    string root = roots[i];
                    if (cancelled()) return null;
                    string label = roots.Count > 1 ? "Indexing " + root.TrimEnd('\\') + "  (" + (i + 1) + " of " + roots.Count + ")\u2026" : "Indexing " + root + "\u2026";
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
                        part = RecursiveIndexer.Index(root, filter, progress, cancelled);
                        if (part == null) return null;
                        scanCount++;
                    }
                    outcome.Entries.AddRange(part);
                    baseCount = outcome.Entries.Count;
                }
                outcome.Method = scanCount == 0 ? "usn" : (usnCount == 0 ? "scan" : "mixed");
                return outcome;
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

                index = result.Entries.ToArray();
                indexMethod = result.Method;
                indexBuiltAt = DateTime.Now;
                settings.LastIndexMethod = indexMethod;
                settings.LastIndexTicks = indexBuiltAt.Ticks;
                settings.Save();
                cacheDirty = true;
                FlushCacheInBackground();
                ApplySearch();

                string note = "";
                if (result.Method != "usn" && result.FallbackReason != null)
                    note = "  (NTFS fast index unavailable: " + result.FallbackReason + ")";
                SetStatus("Indexed " + index.Length.ToString("N0") + " items." + note);

                if (result.Method != "scan") StartEnrichment();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Reads size/date for USN-sourced entries in the background.</summary>
        void StartEnrichment()
        {
            if (enriching) return;
            FileEntry[] snapshot = index;
            if (snapshot.Length == 0) return;
            enriching = true;
            var cts = enrichCts = new CancellationTokenSource();

            Task.Factory.StartNew(() =>
            {
                DetailsEnricher.Run(snapshot, (done, total) =>
                {
                    int pct = (int)(done * 100L / Math.Max(1, total));
                    try { BeginInvoke((Action)(() => { if (enriching) SetStatus("Reading file sizes and dates\u2026 " + pct + "%", true); })); } catch { }
                }, cts.Token);
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
            string root = IndexScope.Key(settings), method = indexMethod;
            DateTime built = indexBuiltAt;
            Task.Factory.StartNew(() => IndexStore.Save(root, method, built, snapshot));
        }

        void FlushCacheIfDirty()
        {
            if (!cacheDirty || index.Length == 0) return;
            cacheDirty = false;
            IndexStore.Save(IndexScope.Key(settings), indexMethod, indexBuiltAt, index);
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
            if (MessageBox.Show(this, "Remove the saved index? You will need to build it again.", "Clear saved index",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try { if (File.Exists(AppPaths.CacheFile)) File.Delete(AppPaths.CacheFile); }
            catch (Exception ex) { Log.Error("Clear cache", ex); }
            index = new FileEntry[0];
            cacheDirty = false;
            ApplySearch();
        }

        // ================================================================ incremental index edits

        void RemoveFromIndex(string path)
        {
            string prefix = path.TrimEnd('\\') + "\\";
            var list = index.Where(f => !f.FullPath.Equals(path, StringComparison.OrdinalIgnoreCase) &&
                                        !f.FullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            index = list;
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
                    added.Add(new FileEntry { Name = di.Name, FullPath = di.FullName, Directory = di.Parent != null ? di.Parent.FullName : "", IsDirectory = true, Modified = di.LastWriteTime });
                    var filter = new PathFilter(settings.ExcludeFolders, settings.ExcludeExtensions);
                    var sub = RecursiveIndexer.Index(path, filter);
                    if (sub != null) added.AddRange(sub);
                }
                else if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    added.Add(new FileEntry { Name = fi.Name, FullPath = fi.FullName, Directory = fi.DirectoryName, Size = fi.Length, Modified = fi.LastWriteTime });
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
            ApplySearch();
            StartIndexing();
        }

        /// <summary>Relaunches elevated so the NTFS fast index (MFT) becomes available.</summary>
        void RestartAsAdministrator()
        {
            try
            {
                FlushCacheIfDirty();
                var psi = new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath, "--wait-for-previous")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                System.Diagnostics.Process.Start(psi);
                exiting = true;
                Close();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                SetStatus("Administrator restart was cancelled.");
            }
            catch (Exception ex) { Log.Error("Restart as admin", ex); }
        }

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
                ApplySearch();
                StartIndexing();
            }
        }

        void ShowPreferences()
        {
            using (var f = new PreferencesForm(settings))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                settings.Save();
                ApplyMenuState();
                SetupTrayAndHotkey();
                UpdateScopeLabel();
                if (f.IndexSettingsChanged)
                {
                    var r = MessageBox.Show(this, "Your folder or exclusion settings changed.\nRebuild the index now?", "Preferences", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes) { index = new FileEntry[0]; StartIndexing(); return; }
                }
                ApplySearch();
            }
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
