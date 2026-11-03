using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SwiftFind.Core;
using SwiftFind.Forms;
using SwiftFind.Models;
using SwiftFind.Search;

namespace SwiftFind
{
    public partial class MainForm
    {
        CancellationTokenSource searchCts;
        int searchVersion;
        List<FileEntry> lastShown = new List<FileEntry>();

        static readonly Dictionary<string, string[]> TypeSets = new Dictionary<string, string[]>
        {
            { "Documents", new[] { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf", ".odt", ".ods", ".odp", ".csv", ".md" } },
            { "Images", new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".tif", ".tiff", ".ico", ".heic" } },
            { "Audio", new[] { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma" } },
            { "Video", new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm", ".flv", ".m4v" } },
            { "Archives", new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2", ".xz", ".iso" } },
            { "Code", new[] { ".cs", ".js", ".ts", ".py", ".java", ".cpp", ".c", ".h", ".php", ".html", ".css", ".json", ".xml", ".sql", ".go", ".rs", ".dart", ".kt", ".swift", ".sh", ".ps1" } },
            { "Executables", new[] { ".exe", ".msi", ".bat", ".cmd", ".dll", ".apk" } }
        };

        // ================================================================ search

        SearchEngine.Query BuildQuery()
        {
            var q = SearchEngine.Parse(searchBox.Input.Text, settings.MatchPath);

            string type = (string)cmbType.SelectedItem;
            if (type == "Folders") q.FoldersOnly = true;
            else if (type != null && TypeSets.ContainsKey(type) && q.Extensions.Count == 0)
                foreach (string e in TypeSets[type]) q.Extensions.Add(e);

            if (!q.MinSize.HasValue && !q.MaxSize.HasValue)
            {
                const long KB = 1024, MB = 1024 * KB, GB = 1024 * MB;
                switch (cmbSize.SelectedIndex)
                {
                    case 1: q.MinSize = 0; q.MaxSize = 0; q.FilesOnly = true; break;
                    case 2: q.MaxSize = MB - 1; q.MinSize = 1; break;
                    case 3: q.MinSize = MB; q.MaxSize = 10 * MB; break;
                    case 4: q.MinSize = 10 * MB; q.MaxSize = 100 * MB; break;
                    case 5: q.MinSize = 100 * MB; q.MaxSize = GB; break;
                    case 6: q.MinSize = GB; break;
                }
            }

            if (!q.ModifiedAfter.HasValue && !q.ModifiedBefore.HasValue)
            {
                DateTime today = DateTime.Now.Date;
                switch (cmbDate.SelectedIndex)
                {
                    case 1: q.ModifiedAfter = today; break;
                    case 2: q.ModifiedAfter = today.AddDays(-1); q.ModifiedBefore = today; break;
                    case 3: q.ModifiedAfter = today.AddDays(-7); break;
                    case 4: q.ModifiedAfter = today.AddDays(-30); break;
                    case 5: q.ModifiedAfter = new DateTime(today.Year, 1, 1); break;
                }
            }

            if (favoritesOnly) q.Extra = f => favorites.Contains(f.FullPath);
            return q;
        }

        /// <summary>Runs the current query on a background thread; stale results are discarded.</summary>
        void ApplySearch()
        {
            if (!IsHandleCreated && !Visible) { /* still constructing */ }

            if (searchCts != null) searchCts.Cancel();
            var cts = searchCts = new CancellationTokenSource();
            int version = ++searchVersion;

            FileEntry[] snapshot = index;
            SearchEngine.Query query = BuildQuery();
            int max = settings.MaxResults;
            var sort = SearchEngine.BuildComparison(sortColumn, sortAscending, settings.ShowFoldersFirst);
            var clock = Stopwatch.StartNew();

            Task.Factory.StartNew(() => SearchEngine.Execute(snapshot, query, max, sort, cts.Token), cts.Token)
                .ContinueWith(t =>
                {
                    if (version != searchVersion || IsDisposed) return;
                    if (t.IsCanceled) return;
                    if (t.IsFaulted)
                    {
                        Log.Error("Search", t.Exception);
                        SetStatus("Search failed - see the error log (Help menu).");
                        return;
                    }
                    clock.Stop();
                    ShowResults(t.Result, snapshot.Length, clock.ElapsedMilliseconds);
                }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        void ShowResults(SearchEngine.Result r, int indexSize, long ms)
        {
            lastShown = r.Items;
            PopulateList(r.Items);

            bool hasIndex = indexSize > 0;
            if (!hasIndex && !indexing) ShowEmpty("No index yet\n\nChoose a folder, then press  Build index  (Ctrl+R)");
            else if (r.Error != null) ShowEmpty(r.Error);
            else if (r.Items.Count == 0 && hasIndex) ShowEmpty("No results\n\nTry fewer words, or check the type, size and date filters.");
            else HideEmpty();

            string shown = r.Items.Count.ToString("N0", CultureInfo.CurrentCulture);
            string total = r.TotalMatches.ToString("N0", CultureInfo.CurrentCulture);
            lblCount.Text = r.Truncated ? shown + " of " + total + " matches" : shown + (r.Items.Count == 1 ? " result" : " results");
            if (!indexing && !enriching)
                SetStatus(r.Truncated
                    ? "Showing the first " + shown + " of " + total + " matches \u2014 refine your search or raise the limit in Preferences."
                    : (hasIndex ? "Found in " + ms + " ms" : "Ready"));
            UpdateIndexInfo();
        }

        void ShowEmpty(string text)
        {
            emptyLabel.Text = text;
            resultsView.Visible = false;
            emptyLabel.Visible = true;
        }

        void HideEmpty()
        {
            emptyLabel.Visible = false;
            resultsView.Visible = true;
        }

        // ================================================================ list

        static string FormatSize(long bytes)
        {
            if (bytes < 0) return "\u2026";
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return i == 0 ? bytes.ToString("N0", CultureInfo.CurrentCulture) + " B" : v.ToString("0.#", CultureInfo.CurrentCulture) + " " + u[i];
        }

        static string TypeText(FileEntry f)
        {
            if (f.IsDirectory) return "Folder";
            string e = f.Extension;
            return e.Length == 0 ? "File" : e.Substring(1).ToUpperInvariant();
        }

        struct GroupKey { public string Name; public int Order; }

        GroupKey GroupOf(FileEntry f)
        {
            DateTime today = DateTime.Now.Date;
            switch (settings.GroupBy)
            {
                case "Extension": return new GroupKey { Name = f.IsDirectory ? "Folders" : (f.Extension.Length == 0 ? "No extension" : f.Extension.Substring(1).ToUpperInvariant() + " files"), Order = f.IsDirectory ? 0 : 1 };
                case "Folder": return new GroupKey { Name = f.Directory, Order = 0 };
                case "Modified":
                    if (f.Modified == DateTime.MinValue) return new GroupKey { Name = "Unknown", Order = 6 };
                    if (f.Modified >= today) return new GroupKey { Name = "Today", Order = 0 };
                    if (f.Modified >= today.AddDays(-1)) return new GroupKey { Name = "Yesterday", Order = 1 };
                    if (f.Modified >= today.AddDays(-7)) return new GroupKey { Name = "Earlier this week", Order = 2 };
                    if (f.Modified >= today.AddDays(-30)) return new GroupKey { Name = "Earlier this month", Order = 3 };
                    if (f.Modified >= new DateTime(today.Year, 1, 1)) return new GroupKey { Name = "Earlier this year", Order = 4 };
                    return new GroupKey { Name = "Older", Order = 5 };
                case "Size":
                    if (f.IsDirectory) return new GroupKey { Name = "Folders", Order = 0 };
                    if (f.Size < 0) return new GroupKey { Name = "Unknown", Order = 8 };
                    if (f.Size == 0) return new GroupKey { Name = "Empty", Order = 1 };
                    if (f.Size < 16 * 1024) return new GroupKey { Name = "Tiny (under 16 KB)", Order = 2 };
                    if (f.Size < 1024 * 1024) return new GroupKey { Name = "Small (under 1 MB)", Order = 3 };
                    if (f.Size < 16L * 1024 * 1024) return new GroupKey { Name = "Medium (under 16 MB)", Order = 4 };
                    if (f.Size < 128L * 1024 * 1024) return new GroupKey { Name = "Large (under 128 MB)", Order = 5 };
                    return new GroupKey { Name = "Huge (128 MB +)", Order = 6 };
                default: return new GroupKey { Name = null, Order = 0 };
            }
        }

        void PopulateList(List<FileEntry> items)
        {
            DateTime recentCutoff = DateTime.Now.AddHours(-24);
            bool grouping = settings.GroupBy != "None";
            var rows = new ListViewItem[items.Count];
            var groupOrder = new Dictionary<string, int>();
            var groupOf = new string[items.Count];

            for (int i = 0; i < items.Count; i++)
            {
                FileEntry f = items[i];
                var row = new ListViewItem(f.Name, icons.KeyFor(f)) { Tag = f, ToolTipText = f.FullPath };
                row.SubItems.Add(f.Directory);
                row.SubItems.Add(f.IsDirectory ? "" : FormatSize(f.Size));
                row.SubItems.Add(f.Modified == DateTime.MinValue ? "" : f.Modified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                row.SubItems.Add(TypeText(f));

                Color c = Theme.Text;
                if (settings.ColorCode)
                {
                    if (favorites.Contains(f.FullPath)) c = Theme.ColorFav;
                    else if (f.IsDirectory) c = Theme.ColorFolder;
                    else if (f.Size >= 100L * 1024 * 1024) c = Theme.ColorLarge;
                    else if (f.Modified >= recentCutoff) c = Theme.ColorRecent;
                }
                row.ForeColor = c;
                if (favorites.Contains(f.FullPath)) row.Text = "\u2605 " + f.Name;
                rows[i] = row;

                if (grouping)
                {
                    GroupKey g = GroupOf(f);
                    groupOf[i] = g.Name;
                    if (!groupOrder.ContainsKey(g.Name)) groupOrder[g.Name] = g.Order;
                }
            }

            resultsView.BeginUpdate();
            try
            {
                resultsView.Items.Clear();
                resultsView.Groups.Clear();
                resultsView.ShowGroups = grouping;
                if (grouping)
                {
                    var counts = new Dictionary<string, int>();
                    foreach (string name in groupOf) { int c; counts.TryGetValue(name, out c); counts[name] = c + 1; }
                    var made = new Dictionary<string, ListViewGroup>();
                    foreach (var kv in groupOrder.OrderBy(k => k.Value).ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        var grp = new ListViewGroup(kv.Key + "  (" + counts[kv.Key].ToString("N0", CultureInfo.CurrentCulture) + ")");
                        made[kv.Key] = grp;
                        resultsView.Groups.Add(grp);
                    }
                    for (int i = 0; i < rows.Length; i++) rows[i].Group = made[groupOf[i]];
                }
                resultsView.Items.AddRange(rows);
            }
            finally { resultsView.EndUpdate(); }
            UpdateSelectionStatus();
        }

        void UpdateSelectionStatus()
        {
            int n = resultsView.SelectedItems.Count;
            if (n == 0) return;
            long total = 0; int files = 0;
            foreach (ListViewItem it in resultsView.SelectedItems)
            {
                var f = (FileEntry)it.Tag;
                if (!f.IsDirectory && f.Size > 0) { total += f.Size; files++; }
            }
            SetStatus(n == 1
                ? ((FileEntry)resultsView.SelectedItems[0].Tag).FullPath
                : n + " selected" + (files > 0 ? "  \u2022  " + FormatSize(total) : ""));
        }

        // ================================================================ sorting

        void SortBy(int column)
        {
            if (sortColumn == column) sortAscending = !sortAscending;
            else { sortColumn = column; sortAscending = true; }
            UpdateHeaders();
            ApplySearch();
        }

        void UpdateHeaders()
        {
            for (int i = 0; i < resultsView.Columns.Count; i++)
            {
                string arrow = i == sortColumn ? (sortAscending ? "  \u25B2" : "  \u25BC") : "";
                resultsView.Columns[i].Text = ColumnNames[i] + arrow;
            }
        }

        // ================================================================ status

        void SetStatus(string text, bool busy = false)
        {
            lblStatus.Text = text;
            busyBar.Visible = busy;
        }

        void UpdateIndexInfo()
        {
            if (index.Length == 0) { lblIndexInfo.Text = ""; return; }
            string method = indexMethod == "usn" ? "NTFS fast index" : "folder scan";
            string age = indexBuiltAt == DateTime.MinValue ? "" : "  \u2022  updated " + Ago(indexBuiltAt);
            lblIndexInfo.Text = index.Length.ToString("N0", CultureInfo.CurrentCulture) + " items  \u2022  " + method + age;
        }

        static string Ago(DateTime t)
        {
            TimeSpan d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "just now";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + " min ago";
            if (d.TotalHours < 24) return (int)d.TotalHours + " h ago";
            return (int)d.TotalDays + " d ago";
        }

        // ================================================================ keyboard

        void SearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && resultsView.Items.Count > 0)
            {
                resultsView.Focus();
                if (resultsView.SelectedItems.Count == 0) resultsView.Items[0].Selected = true;
                resultsView.Items[Math.Max(0, resultsView.SelectedIndices.Count > 0 ? resultsView.SelectedIndices[0] : 0)].Focused = true;
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (resultsView.SelectedItems.Count == 0 && resultsView.Items.Count > 0) resultsView.Items[0].Selected = true;
                if (e.Control) RevealSelected(); else OpenSelected();
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                searchBox.Input.Clear();
                e.Handled = e.SuppressKeyPress = true;
            }
        }

        void ResultsKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { if (e.Control) RevealSelected(); else OpenSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.F2) { RenameSelected(); e.Handled = true; }
            else if (e.Control && e.Shift && e.KeyCode == Keys.C) { CopyPathOfSelected(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { ClipboardFiles(false); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.X) { ClipboardFiles(true); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.V) { PasteHere(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.D) { ToggleFavoriteSelected(); e.Handled = true; }
            else if (e.Alt && e.KeyCode == Keys.Enter) { ShowProperties(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.A) { foreach (ListViewItem it in resultsView.Items) it.Selected = true; e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { searchBox.Input.Focus(); e.Handled = true; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F) || keyData == (Keys.Control | Keys.L))
            {
                searchBox.Input.Focus(); searchBox.Input.SelectAll(); return true;
            }
            if (keyData == Keys.F5) { ToggleIndexing(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ================================================================ help

        void ShowSyntaxHelp()
        {
            MessageBox.Show(this,
                "Words            all words must match (name, or full path)\n" +
                "\"two words\"      exact phrase\n" +
                "!word  -word     exclude\n" +
                "*.pdf  rep?rt*   wildcards\n" +
                "ext:jpg;png      file extensions\n" +
                "size:>10mb       size:<1kb   size:1mb..5mb\n" +
                "dm:today         yesterday | week | month | year | 2026-03-01 | >2026-01-01\n" +
                "path:projects    match the full path only\n" +
                "file:  folder:   only files / only folders\n" +
                "regex:^img_\\d+   regular expression\n" +
                "case:            case-sensitive\n\n" +
                "Example:  invoice ext:pdf dm:month !draft",
                "Search syntax", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ShowAbout()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Index:  " + index.Length.ToString("N0") + " items (" + (indexMethod == "usn" ? "NTFS fast index" : indexMethod) + ")");
            sb.AppendLine("Folder: " + settings.RootPath);
            sb.AppendLine("Data:   " + AppPaths.DataDir + (AppPaths.IsPortable ? "  (portable mode)" : ""));
            sb.AppendLine("Admin:  " + (IsAdministrator() ? "yes - NTFS fast indexing available" : "no - run as administrator for fast NTFS indexing"));
            sb.AppendLine();
            sb.AppendLine("Shortcuts: Ctrl+F search, Ctrl+R index, Ctrl+B favourites, F2 rename, Del recycle,");
            sb.AppendLine("Ctrl+C/X/V copy/cut/paste, Ctrl+Shift+C copy path, Alt+Enter properties, F1 syntax help.");
            Dialogs.About(this, sb.ToString());
        }

        static bool IsAdministrator()
        {
            try
            {
                using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                    return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        void OpenLog()
        {
            try
            {
                if (!File.Exists(AppPaths.LogFile)) { MessageBox.Show(this, "No errors have been logged. \u2713", "Error log"); return; }
                Process.Start(new ProcessStartInfo(AppPaths.LogFile) { UseShellExecute = true });
            }
            catch (Exception ex) { Log.Error("Open log", ex); }
        }

        void ExportCsv()
        {
            if (lastShown.Count == 0) { MessageBox.Show(this, "There are no results to export.", "Export"); return; }
            using (var d = new SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "SwiftFind-results.csv" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var sb = new StringBuilder("Name,Location,Size (bytes),Modified,Type\r\n");
                    foreach (FileEntry f in lastShown)
                        sb.Append(Csv(f.Name)).Append(',').Append(Csv(f.Directory)).Append(',')
                          .Append(f.IsDirectory ? "" : f.Size.ToString(CultureInfo.InvariantCulture)).Append(',')
                          .Append(f.Modified == DateTime.MinValue ? "" : f.Modified.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
                          .Append(TypeText(f)).Append("\r\n");
                    File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
                    SetStatus("Exported " + lastShown.Count.ToString("N0") + " rows to " + d.FileName);
                }
                catch (Exception ex) { Log.Error("Export", ex); MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        static string Csv(string s)
        {
            if (s == null) return "";
            // Neutralise spreadsheet formula injection as well as quoting.
            if (s.Length > 0 && (s[0] == '=' || s[0] == '+' || s[0] == '@')) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
