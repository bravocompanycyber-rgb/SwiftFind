using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SwiftFind.Indexing;
using SwiftFind.Models;
using SwiftFind.Search;

namespace SwiftFind
{
    public class MainForm : Form
    {
        // UI
        TextBox queryBox;
        TextBox pathBox;
        ListView resultsView;
        Label statusLabel;
        Button btnChoose;
        Button btnIndex;
        Button btnClear;
        Button btnFav;
        ComboBox filterExt;
        ComboBox filterSize;
        ComboBox filterDate;
        ProgressBar progressBar;
        ToolStripStatusLabel methodLabel;

        // Data
        List<FileEntry> index = new List<FileEntry>();
        List<FileEntry> visible = new List<FileEntry>();
        List<string> favorites = new List<string>();
        List<string> recentQueries = new List<string>();
        string rootPath;
        string cachePath;
        string favPath;
        string settingsPath;
        bool isIndexing;
        string lastIndexMethod = "none";
        DateTime lastIndexTime = DateTime.MinValue;

        // Clipboard
        List<string> clipboardPaths = new List<string>();
        bool clipboardIsCut;

        // Sort
        int sortColumn = 0;
        bool sortAsc = true;

        // Colors
        static readonly Color Bg = Color.FromArgb(18, 22, 32);
        static readonly Color Surface = Color.FromArgb(28, 34, 48);
        static readonly Color Surface2 = Color.FromArgb(38, 46, 64);
        static readonly Color Accent = Color.FromArgb(64, 120, 240);
        static readonly Color AccentGreen = Color.FromArgb(40, 160, 110);
        static readonly Color TextMuted = Color.FromArgb(160, 170, 190);
        static readonly Color FavColor = Color.FromArgb(255, 200, 80);

        BackgroundWorker indexer = new BackgroundWorker();
        Timer searchTimer = new Timer();

        public MainForm()
        {
            Text = "SwiftFind";
            Size = new Size(1320, 860);
            MinimumSize = new Size(960, 580);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;
            DoubleBuffered = true;

            string dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SwiftFind");
            Directory.CreateDirectory(dataDir);
            cachePath = Path.Combine(dataDir, "index.cache");
            favPath = Path.Combine(dataDir, "favorites.txt");
            settingsPath = Path.Combine(dataDir, "settings.txt");

            // Portable mode: if settings.txt next to EXE, use that folder
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string portable = Path.Combine(exeDir, "SwiftFind.portable");
                if (File.Exists(portable))
                {
                    dataDir = exeDir;
                    cachePath = Path.Combine(dataDir, "index.cache");
                    favPath = Path.Combine(dataDir, "favorites.txt");
                    settingsPath = Path.Combine(dataDir, "settings.txt");
                }
            }
            catch { }

            rootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            LoadSettings();
            LoadFavorites();

            BuildUI();
            WireEvents();

            indexer.WorkerReportsProgress = true;
            indexer.WorkerSupportsCancellation = true;
            indexer.DoWork += Indexer_DoWork;
            indexer.ProgressChanged += Indexer_ProgressChanged;
            indexer.RunWorkerCompleted += Indexer_Completed;

            searchTimer.Interval = 120;
            searchTimer.Tick += (s, e) => { searchTimer.Stop(); ApplySearch(); };

            LoadCache();
            ApplySearch();
            UpdateStatus();
        }

        void BuildUI()
        {
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(16),
                BackColor = Bg
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            Controls.Add(main);

            // Header
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            var logo = new Label
            {
                Text = "S",
                Size = new Size(40, 40),
                Location = new Point(0, 6),
                BackColor = Accent,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 18, FontStyle.Bold)
            };
            var title = new Label
            {
                Text = "SwiftFind",
                AutoSize = true,
                Location = new Point(50, 4),
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White
            };
            var subtitle = new Label
            {
                Text = "Instant search  ·  File manager  ·  USN fast path  ·  Windows 8 – 11",
                AutoSize = true,
                Location = new Point(52, 34),
                Font = new Font("Segoe UI", 8),
                ForeColor = TextMuted
            };
            header.Controls.Add(logo);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            main.Controls.Add(header, 0, 0);

            // Search
            queryBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 14),
                BackColor = Surface,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            main.Controls.Add(queryBox, 0, 1);

            // Path bar
            var pathPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 5,
                BackColor = Bg
            };
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));

            pathBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Surface,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = rootPath
            };
            btnChoose = MakeButton("Choose folder", Accent);
            btnIndex = MakeButton("Build index", AccentGreen);
            btnClear = MakeButton("Clear", Surface2);
            btnFav = MakeButton("★ Fav", Surface2);

            pathPanel.Controls.Add(pathBox, 0, 0);
            pathPanel.Controls.Add(btnChoose, 1, 0);
            pathPanel.Controls.Add(btnIndex, 2, 0);
            pathPanel.Controls.Add(btnClear, 3, 0);
            pathPanel.Controls.Add(btnFav, 4, 0);
            main.Controls.Add(pathPanel, 0, 2);

            // Filters
            var filterPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Bg,
                WrapContents = false
            };

            filterExt = MakeFilterCombo("Any type", new[] {
                "Any type", ".exe", ".dll", ".txt", ".pdf", ".docx", ".xlsx",
                ".jpg", ".png", ".mp4", ".mp3", ".zip", ".cs", ".js", ".html", ".json"
            });
            filterSize = MakeFilterCombo("Any size", new[] {
                "Any size", "Empty", "< 1 KB", "< 100 KB", "< 1 MB", "< 10 MB", "< 100 MB", "> 100 MB"
            });
            filterDate = MakeFilterCombo("Any date", new[] {
                "Any date", "Today", "Yesterday", "Last 7 days", "Last 30 days", "This year"
            });

            filterPanel.Controls.Add(new Label { Text = "Type", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(0, 8, 6, 0) });
            filterPanel.Controls.Add(filterExt);
            filterPanel.Controls.Add(new Label { Text = "Size", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(14, 8, 6, 0) });
            filterPanel.Controls.Add(filterSize);
            filterPanel.Controls.Add(new Label { Text = "Modified", AutoSize = true, ForeColor = TextMuted, Margin = new Padding(14, 8, 6, 0) });
            filterPanel.Controls.Add(filterDate);

            var tip = new Label
            {
                Text = "  Tips:  ext:pdf   size:>10MB   dm:week   *report*   regex:",
                AutoSize = true,
                ForeColor = Color.FromArgb(120, 130, 150),
                Margin = new Padding(18, 8, 0, 0),
                Font = new Font("Segoe UI", 8f)
            };
            filterPanel.Controls.Add(tip);
            main.Controls.Add(filterPanel, 0, 3);

            // Results
            resultsView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                HideSelection = false,
                BackColor = Surface,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9.5f),
                AllowDrop = true,
                HeaderStyle = ColumnHeaderStyle.Clickable
            };
            resultsView.Columns.Add("Name", 340);
            resultsView.Columns.Add("Location", 400);
            resultsView.Columns.Add("Size", 90);
            resultsView.Columns.Add("Modified", 140);
            resultsView.Columns.Add("Type", 70);

            resultsView.ContextMenuStrip = BuildContextMenu();
            main.Controls.Add(resultsView, 0, 4);

            // Status
            var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            statusLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = TextMuted,
                Text = "Ready"
            };
            progressBar = new ProgressBar
            {
                Dock = DockStyle.Right,
                Width = 200,
                Height = 14,
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            statusPanel.Controls.Add(statusLabel);
            statusPanel.Controls.Add(progressBar);
            main.Controls.Add(statusPanel, 0, 5);
        }

        ComboBox MakeFilterCombo(string first, string[] items)
        {
            var c = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 118,
                BackColor = Surface,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            c.Items.AddRange(items);
            c.SelectedIndex = 0;
            return c;
        }

        Button MakeButton(string text, Color color)
        {
            var b = new Button
            {
                Text = text,
                Dock = DockStyle.Fill,
                BackColor = color,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(4, 0, 0, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        ContextMenuStrip BuildContextMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, (s, e) => OpenSelected(false));
            menu.Items.Add("Open containing folder", null, (s, e) => OpenSelected(true));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Copy", null, (s, e) => ClipboardCopy(false));
            menu.Items.Add("Cut", null, (s, e) => ClipboardCopy(true));
            menu.Items.Add("Paste", null, (s, e) => ClipboardPaste());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Rename", null, (s, e) => RenameSelected());
            menu.Items.Add("Delete", null, (s, e) => DeleteSelected());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Add to Favorites", null, (s, e) => AddFavorite());
            menu.Items.Add("New folder here", null, (s, e) => CreateNewFolder());
            menu.Items.Add("Copy full path", null, (s, e) => CopyPaths());
            menu.Items.Add("Properties", null, (s, e) => ShowProperties());
            return menu;
        }

        void WireEvents()
        {
            queryBox.TextChanged += (s, e) => { searchTimer.Stop(); searchTimer.Start(); };
            queryBox.KeyDown += QueryBox_KeyDown;

            btnChoose.Click += (s, e) => ChooseFolder();
            btnIndex.Click += (s, e) => StartIndexing();
            btnClear.Click += (s, e) => { queryBox.Clear(); queryBox.Focus(); };
            btnFav.Click += (s, e) => ShowFavorites();

            filterExt.SelectedIndexChanged += (s, e) => ApplySearch();
            filterSize.SelectedIndexChanged += (s, e) => ApplySearch();
            filterDate.SelectedIndexChanged += (s, e) => ApplySearch();

            resultsView.DoubleClick += (s, e) => OpenSelected(false);
            resultsView.KeyDown += ResultsView_KeyDown;
            resultsView.ItemDrag += ResultsView_ItemDrag;
            resultsView.ColumnClick += ResultsView_ColumnClick;

            this.KeyDown += MainForm_KeyDown;
        }

        void QueryBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down && resultsView.Items.Count > 0)
            {
                resultsView.Focus();
                resultsView.Items[0].Selected = true;
                resultsView.Items[0].Focused = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && resultsView.Items.Count > 0)
            {
                resultsView.Items[0].Selected = true;
                OpenSelected(false);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                queryBox.Clear();
            }
        }

        void ResultsView_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { OpenSelected(false); e.Handled = true; }
            else if (e.KeyCode == Keys.F2) { RenameSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { ClipboardCopy(false); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.X) { ClipboardCopy(true); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.V) { ClipboardPaste(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.A)
            {
                foreach (ListViewItem item in resultsView.Items) item.Selected = true;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Back)
            {
                queryBox.Focus();
            }
        }

        void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                queryBox.Focus();
                queryBox.SelectAll();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.R)
            {
                StartIndexing();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.D)
            {
                AddFavorite();
                e.Handled = true;
            }
        }

        void ResultsView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var paths = GetSelectedPaths();
            if (paths.Count == 0) return;
            var data = new DataObject(DataFormats.FileDrop, paths.ToArray());
            resultsView.DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
        }

        void ResultsView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == sortColumn)
                sortAsc = !sortAsc;
            else
            {
                sortColumn = e.Column;
                sortAsc = true;
            }
            ApplySearch();
        }

        // ---------- Indexing ----------

        void ChooseFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choose a folder or drive to index";
                dlg.SelectedPath = Directory.Exists(rootPath) ? rootPath : "C:\\";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    rootPath = dlg.SelectedPath;
                    pathBox.Text = rootPath;
                    index.Clear();
                    resultsView.Items.Clear();
                    SaveSettings();
                    UpdateStatus("Folder selected — click Build index");
                }
            }
        }

        void StartIndexing()
        {
            if (isIndexing || indexer.IsBusy) return;
            if (!Directory.Exists(rootPath))
            {
                MessageBox.Show("Please choose a valid folder first.", "SwiftFind",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            isIndexing = true;
            btnIndex.Enabled = false;
            btnChoose.Enabled = false;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            UpdateStatus("Indexing...");
            indexer.RunWorkerAsync(rootPath);
        }

        void Indexer_DoWork(object sender, DoWorkEventArgs e)
        {
            string root = (string)e.Argument;
            List<FileEntry> found = null;
            string method = "recursive";

            // Try fast USN path first on NTFS
            if (UsnIndexer.IsSupported(root))
            {
                try
                {
                    found = UsnIndexer.TryIndex(root, count =>
                    {
                        if (indexer.CancellationPending) return;
                        indexer.ReportProgress(0, "USN " + count.ToString("N0"));
                    });
                    if (found != null && found.Count > 0)
                        method = "USN";
                }
                catch { found = null; }
            }

            // Fallback or primary recursive
            if (found == null)
            {
                found = RecursiveIndexer.Index(root, count =>
                {
                    if (indexer.CancellationPending) return;
                    indexer.ReportProgress(0, count.ToString("N0"));
                });
                method = "recursive";
            }

            // For USN results, sizes/dates are often missing — enrich a sample if needed
            // (full enrichment would be slow; we keep name-first speed)

            e.Result = new object[] { found, method };
        }

        void Indexer_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            statusLabel.Text = "Indexing... " + (e.UserState as string ?? "");
        }

        void Indexer_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            isIndexing = false;
            btnIndex.Enabled = true;
            btnChoose.Enabled = true;
            progressBar.Visible = false;

            if (e.Cancelled)
            {
                UpdateStatus("Indexing cancelled");
                return;
            }
            if (e.Error != null)
            {
                UpdateStatus("Index error: " + e.Error.Message);
                return;
            }

            var result = (object[])e.Result;
            index = (List<FileEntry>)result[0];
            lastIndexMethod = (string)result[1];
            lastIndexTime = DateTime.Now;

            SaveCache();
            SaveSettings();
            ApplySearch();
            UpdateStatus();
            queryBox.Focus();
        }

        // ---------- Search ----------

        void ApplySearch()
        {
            string text = queryBox.Text.Trim();

            // Remember recent queries
            if (text.Length > 1 && !recentQueries.Contains(text))
            {
                recentQueries.Insert(0, text);
                if (recentQueries.Count > 20) recentQueries.RemoveAt(20);
            }

            var q = SearchEngine.Parse(text);

            // Apply UI filters on top of query syntax
            string extFilter = filterExt.SelectedItem as string ?? "Any type";
            if (extFilter != "Any type" && string.IsNullOrEmpty(q.Extension))
                q.Extension = extFilter.ToLowerInvariant();

            string sizeFilter = filterSize.SelectedItem as string ?? "Any size";
            if (sizeFilter != "Any size")
            {
                switch (sizeFilter)
                {
                    case "Empty": q.MaxSize = 0; break;
                    case "< 1 KB": q.MaxSize = 1024; break;
                    case "< 100 KB": q.MaxSize = 100 * 1024; break;
                    case "< 1 MB": q.MaxSize = 1024 * 1024; break;
                    case "< 10 MB": q.MaxSize = 10 * 1024 * 1024; break;
                    case "< 100 MB": q.MaxSize = 100L * 1024 * 1024; break;
                    case "> 100 MB": q.MinSize = 100L * 1024 * 1024; break;
                }
            }

            string dateFilter = filterDate.SelectedItem as string ?? "Any date";
            if (dateFilter != "Any date" && !q.ModifiedAfter.HasValue)
            {
                DateTime now = DateTime.Now.Date;
                switch (dateFilter)
                {
                    case "Today": q.ModifiedAfter = now; break;
                    case "Yesterday": q.ModifiedAfter = now.AddDays(-1); q.ModifiedBefore = now; break;
                    case "Last 7 days": q.ModifiedAfter = now.AddDays(-7); break;
                    case "Last 30 days": q.ModifiedAfter = now.AddDays(-30); break;
                    case "This year": q.ModifiedAfter = new DateTime(now.Year, 1, 1); break;
                }
            }

            visible = SearchEngine.Execute(index, q, 10000);

            // Column sort
            visible = SortVisible(visible);

            PopulateList();
            UpdateStatus();
        }

        List<FileEntry> SortVisible(List<FileEntry> list)
        {
            IOrderedEnumerable<FileEntry> ordered;
            switch (sortColumn)
            {
                case 1: // Location
                    ordered = sortAsc
                        ? list.OrderBy(f => f.Directory, StringComparer.OrdinalIgnoreCase)
                        : list.OrderByDescending(f => f.Directory, StringComparer.OrdinalIgnoreCase);
                    break;
                case 2: // Size
                    ordered = sortAsc
                        ? list.OrderBy(f => f.Size)
                        : list.OrderByDescending(f => f.Size);
                    break;
                case 3: // Modified
                    ordered = sortAsc
                        ? list.OrderBy(f => f.Modified)
                        : list.OrderByDescending(f => f.Modified);
                    break;
                case 4: // Type
                    ordered = sortAsc
                        ? list.OrderBy(f => f.IsDirectory ? "" : f.Extension)
                        : list.OrderByDescending(f => f.IsDirectory ? "" : f.Extension);
                    break;
                default: // Name
                    ordered = sortAsc
                        ? list.OrderBy(f => f.IsDirectory ? 0 : 1).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                        : list.OrderBy(f => f.IsDirectory ? 0 : 1).ThenByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    break;
            }
            return ordered.ToList();
        }

        void PopulateList()
        {
            resultsView.BeginUpdate();
            resultsView.Items.Clear();

            foreach (var f in visible)
            {
                bool isFav = favorites.Contains(f.FullPath);
                string prefix = f.IsDirectory ? "📁 " : (isFav ? "★ " : "");
                var item = new ListViewItem(prefix + f.Name);
                item.SubItems.Add(f.Directory ?? "");
                item.SubItems.Add(f.IsDirectory ? "" : FormatSize(f.Size));
                item.SubItems.Add(f.Modified == DateTime.MinValue ? "" : f.Modified.ToString("yyyy-MM-dd HH:mm"));
                item.SubItems.Add(f.IsDirectory ? "Folder" : (f.Extension.Length > 0 ? f.Extension : "file"));
                item.Tag = f;
                item.ForeColor = f.IsDirectory ? Color.FromArgb(140, 190, 255) : (isFav ? FavColor : Color.White);
                resultsView.Items.Add(item);
            }

            resultsView.EndUpdate();
        }

        // ---------- File operations ----------

        List<FileEntry> GetSelectedEntries()
        {
            var list = new List<FileEntry>();
            foreach (ListViewItem item in resultsView.SelectedItems)
            {
                var e = item.Tag as FileEntry;
                if (e != null) list.Add(e);
            }
            return list;
        }

        List<string> GetSelectedPaths()
        {
            return GetSelectedEntries().Select(e => e.FullPath).ToList();
        }

        void OpenSelected(bool containingFolder)
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;

            foreach (var e in entries)
            {
                try
                {
                    if (containingFolder)
                    {
                        if (File.Exists(e.FullPath) || Directory.Exists(e.FullPath))
                            Process.Start("explorer.exe", "/select,\"" + e.FullPath + "\"");
                    }
                    else
                    {
                        if (e.IsDirectory)
                            Process.Start("explorer.exe", e.FullPath);
                        else if (File.Exists(e.FullPath))
                            Process.Start(e.FullPath);
                        else
                            MessageBox.Show("Item no longer exists. Rebuild the index.", "SwiftFind");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "SwiftFind");
                }
            }
        }

        void ClipboardCopy(bool cut)
        {
            var paths = GetSelectedPaths();
            if (paths.Count == 0) return;

            clipboardPaths = paths;
            clipboardIsCut = cut;

            try
            {
                var data = new DataObject();
                data.SetData(DataFormats.FileDrop, paths.ToArray());
                if (cut)
                {
                    byte[] effect = new byte[] { 2, 0, 0, 0 };
                    data.SetData("Preferred DropEffect", new MemoryStream(effect));
                }
                Clipboard.SetDataObject(data, true);
            }
            catch { }

            UpdateStatus((cut ? "Cut " : "Copied ") + paths.Count + " item(s)");
        }

        void ClipboardPaste()
        {
            List<string> sources = clipboardPaths;
            bool isCut = clipboardIsCut;

            if (sources.Count == 0 && Clipboard.ContainsFileDropList())
            {
                var files = Clipboard.GetFileDropList();
                sources = new List<string>();
                foreach (string f in files) sources.Add(f);
                isCut = false;
            }

            if (sources.Count == 0) return;

            string destDir = rootPath;
            var selected = GetSelectedEntries();
            if (selected.Count > 0)
            {
                var first = selected[0];
                destDir = first.IsDirectory ? first.FullPath : first.Directory;
            }

            if (!Directory.Exists(destDir))
            {
                MessageBox.Show("Destination folder does not exist.", "SwiftFind");
                return;
            }

            int ok = 0;
            foreach (string src in sources)
            {
                try
                {
                    string name = Path.GetFileName(src);
                    string dest = Path.Combine(destDir, name);

                    if (File.Exists(dest) || Directory.Exists(dest))
                    {
                        string baseName = Path.GetFileNameWithoutExtension(name);
                        string ext = Path.GetExtension(name);
                        int n = 1;
                        do
                        {
                            dest = Path.Combine(destDir, baseName + " (" + n + ")" + ext);
                            n++;
                        } while (File.Exists(dest) || Directory.Exists(dest));
                    }

                    if (Directory.Exists(src))
                    {
                        CopyDirectory(src, dest);
                        if (isCut) Directory.Delete(src, true);
                    }
                    else if (File.Exists(src))
                    {
                        File.Copy(src, dest, false);
                        if (isCut) File.Delete(src);
                    }
                    ok++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed on " + src + ":\n" + ex.Message, "SwiftFind");
                }
            }

            if (isCut) clipboardPaths.Clear();
            UpdateStatus("Pasted " + ok + " item(s) — rebuild index to see them");
        }

        void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(src))
            {
                string name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(dest, name), false);
            }
            foreach (string dir in Directory.GetDirectories(src))
            {
                string name = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(dest, name));
            }
        }

        void RenameSelected()
        {
            var entries = GetSelectedEntries();
            if (entries.Count != 1) return;

            var entry = entries[0];
            string newName = ShowInputDialog("Rename", "New name:", entry.Name);
            if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name) return;

            try
            {
                string dest = Path.Combine(entry.Directory, newName);
                if (entry.IsDirectory)
                    Directory.Move(entry.FullPath, dest);
                else
                    File.Move(entry.FullPath, dest);

                entry.Name = newName;
                entry.FullPath = dest;
                ApplySearch();
                UpdateStatus("Renamed to " + newName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SwiftFind");
            }
        }

        void DeleteSelected()
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;

            string msg = entries.Count == 1
                ? "Delete \"" + entries[0].Name + "\"?\n\nThis cannot be undone from SwiftFind."
                : "Delete " + entries.Count + " items?\n\nThis cannot be undone from SwiftFind.";

            if (MessageBox.Show(msg, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int ok = 0;
            foreach (var e in entries)
            {
                try
                {
                    if (e.IsDirectory)
                        Directory.Delete(e.FullPath, true);
                    else
                        File.Delete(e.FullPath);

                    index.RemoveAll(x => x.FullPath == e.FullPath);
                    favorites.Remove(e.FullPath);
                    ok++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Could not delete " + e.Name + ":\n" + ex.Message, "SwiftFind");
                }
            }

            SaveFavorites();
            ApplySearch();
            UpdateStatus("Deleted " + ok + " item(s)");
        }

        void CreateNewFolder()
        {
            string parent = rootPath;
            var selected = GetSelectedEntries();
            if (selected.Count > 0)
            {
                var first = selected[0];
                parent = first.IsDirectory ? first.FullPath : first.Directory;
            }

            string name = ShowInputDialog("New Folder", "Folder name:", "New folder");
            if (string.IsNullOrWhiteSpace(name)) return;

            try
            {
                string path = Path.Combine(parent, name);
                Directory.CreateDirectory(path);

                var entry = new FileEntry
                {
                    Name = name,
                    FullPath = path,
                    Directory = parent,
                    Size = 0,
                    Modified = DateTime.Now,
                    IsDirectory = true
                };
                index.Add(entry);
                ApplySearch();
                UpdateStatus("Created folder: " + name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SwiftFind");
            }
        }

        void CopyPaths()
        {
            var paths = GetSelectedPaths();
            if (paths.Count == 0) return;
            Clipboard.SetText(string.Join(Environment.NewLine, paths));
            UpdateStatus("Copied " + paths.Count + " path(s)");
        }

        void ShowProperties()
        {
            var entries = GetSelectedEntries();
            if (entries.Count != 1) return;

            var e = entries[0];
            try
            {
                string info = e.Name + "\n\n" +
                    "Path: " + e.FullPath + "\n" +
                    "Type: " + (e.IsDirectory ? "Folder" : e.Extension) + "\n" +
                    "Size: " + (e.IsDirectory ? "—" : FormatSize(e.Size)) + "\n" +
                    "Modified: " + (e.Modified == DateTime.MinValue ? "—" : e.Modified.ToString("yyyy-MM-dd HH:mm:ss"));
                MessageBox.Show(info, "Properties — " + e.Name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SwiftFind");
            }
        }

        // ---------- Favorites ----------

        void AddFavorite()
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;

            int added = 0;
            foreach (var e in entries)
            {
                if (!favorites.Contains(e.FullPath))
                {
                    favorites.Add(e.FullPath);
                    added++;
                }
            }
            SaveFavorites();
            ApplySearch();
            UpdateStatus("Added " + added + " to favorites");
        }

        void ShowFavorites()
        {
            if (favorites.Count == 0)
            {
                MessageBox.Show("No favorites yet.\nSelect items and press Ctrl+D or use the context menu.", "Favorites");
                return;
            }

            queryBox.Text = "";
            visible = index.Where(f => favorites.Contains(f.FullPath)).ToList();
            PopulateList();
            UpdateStatus("Showing " + visible.Count + " favorites");
        }

        // ---------- Persistence ----------

        void SaveCache()
        {
            try
            {
                string temp = cachePath + ".tmp";
                using (var w = new StreamWriter(temp, false, Encoding.UTF8))
                {
                    w.WriteLine(rootPath);
                    w.WriteLine(index.Count);
                    w.WriteLine(lastIndexMethod);
                    w.WriteLine(lastIndexTime.Ticks);
                    foreach (var f in index)
                    {
                        w.WriteLine(
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(f.FullPath)) + "|" +
                            f.Size + "|" +
                            f.Modified.Ticks + "|" +
                            (f.IsDirectory ? "1" : "0"));
                    }
                }
                if (File.Exists(cachePath)) File.Delete(cachePath);
                File.Move(temp, cachePath);
            }
            catch { }
        }

        void LoadCache()
        {
            if (!File.Exists(cachePath)) return;
            try
            {
                using (var r = new StreamReader(cachePath, Encoding.UTF8))
                {
                    string savedRoot = r.ReadLine();
                    if (savedRoot != rootPath) return;

                    string countLine = r.ReadLine();
                    int expected;
                    if (!int.TryParse(countLine, out expected)) return;

                    lastIndexMethod = r.ReadLine() ?? "recursive";
                    long ticks;
                    if (long.TryParse(r.ReadLine(), out ticks))
                        lastIndexTime = new DateTime(ticks);

                    var loaded = new List<FileEntry>();
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length != 4) continue;

                        long size, t;
                        if (!long.TryParse(parts[1], out size)) continue;
                        if (!long.TryParse(parts[2], out t)) continue;

                        string path = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                        bool isDir = parts[3] == "1";

                        loaded.Add(new FileEntry
                        {
                            Name = Path.GetFileName(path),
                            FullPath = path,
                            Directory = Path.GetDirectoryName(path),
                            Size = size,
                            Modified = new DateTime(t),
                            IsDirectory = isDir
                        });
                    }
                    index = loaded;
                }
            }
            catch
            {
                index.Clear();
            }
        }

        void SaveFavorites()
        {
            try { File.WriteAllLines(favPath, favorites); }
            catch { }
        }

        void LoadFavorites()
        {
            try
            {
                if (File.Exists(favPath))
                    favorites = new List<string>(File.ReadAllLines(favPath));
            }
            catch { }
        }

        void SaveSettings()
        {
            try
            {
                File.WriteAllText(settingsPath, rootPath + Environment.NewLine + lastIndexMethod);
            }
            catch { }
        }

        void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    var lines = File.ReadAllLines(settingsPath);
                    if (lines.Length > 0 && Directory.Exists(lines[0]))
                        rootPath = lines[0];
                }
            }
            catch { }
        }

        // ---------- Helpers ----------

        void UpdateStatus(string extra = null)
        {
            if (extra != null)
            {
                statusLabel.Text = extra;
                return;
            }

            string method = lastIndexMethod == "USN" ? "USN fast" : lastIndexMethod;
            string when = lastIndexTime == DateTime.MinValue ? "" : "  ·  indexed " + lastIndexTime.ToString("HH:mm");
            statusLabel.Text = string.Format(
                "{0:N0} indexed ({1})  ·  showing {2:N0}{3}  ·  {4}",
                index.Count, method, visible.Count, when, rootPath);
        }

        static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double v = bytes;
            int i = 0;
            while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
            return v.ToString("0.##") + " " + units[i];
        }

        static string ShowInputDialog(string title, string prompt, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.Size = new Size(420, 160);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.BackColor = Bg;
                form.ForeColor = Color.White;
                form.Font = new Font("Segoe UI", 10);

                var lbl = new Label { Text = prompt, Left = 16, Top = 16, AutoSize = true };
                var tb = new TextBox
                {
                    Left = 16, Top = 44, Width = 370,
                    Text = defaultValue,
                    BackColor = Surface, ForeColor = Color.White,
                    BorderStyle = BorderStyle.FixedSingle
                };
                var ok = new Button
                {
                    Text = "OK", Left = 220, Top = 85, Width = 80,
                    DialogResult = DialogResult.OK,
                    BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
                };
                ok.FlatAppearance.BorderSize = 0;
                var cancel = new Button
                {
                    Text = "Cancel", Left = 310, Top = 85, Width = 80,
                    DialogResult = DialogResult.Cancel,
                    BackColor = Surface2, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
                };
                cancel.FlatAppearance.BorderSize = 0;

                form.Controls.Add(lbl);
                form.Controls.Add(tb);
                form.Controls.Add(ok);
                form.Controls.Add(cancel);
                form.AcceptButton = ok;
                form.CancelButton = cancel;

                return form.ShowDialog() == DialogResult.OK ? tb.Text.Trim() : null;
            }
        }
    }
}
