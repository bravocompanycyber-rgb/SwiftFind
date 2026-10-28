using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SwiftFind.Forms;
using SwiftFind.Indexing;
using SwiftFind.Models;
using SwiftFind.Search;

namespace SwiftFind
{
    public class MainForm : Form
    {
        // UI
        MenuStrip menu;
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
        ToolStripStatusLabel lblMethod;

        // Data
        List<FileEntry> index = new List<FileEntry>();
        List<FileEntry> visible = new List<FileEntry>();
        List<string> favorites = new List<string>();
        AppSettings settings;
        string cachePath;
        string favPath;
        bool isIndexing;

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
        static readonly Color ColorFolder = Color.FromArgb(130, 180, 255);
        static readonly Color ColorFav = Color.FromArgb(255, 200, 70);
        static readonly Color ColorLarge = Color.FromArgb(255, 140, 120);
        static readonly Color ColorRecent = Color.FromArgb(140, 220, 160);

        BackgroundWorker indexer = new BackgroundWorker();
        Timer searchTimer = new Timer();

        public MainForm()
        {
            try
            {
                settings = AppSettings.Load();
                Text = "SwiftFind";
                Size = new Size(1360, 880);
                MinimumSize = new Size(980, 600);
                StartPosition = FormStartPosition.CenterScreen;
                BackColor = Bg;
                ForeColor = Color.White;
                Font = new Font("Segoe UI", 9.5f);
                KeyPreview = true;
                DoubleBuffered = true;

                string dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SwiftFind");
                try { Directory.CreateDirectory(dataDir); } catch { }

                // Portable override
                try
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (File.Exists(Path.Combine(exeDir, "SwiftFind.portable")))
                        dataDir = exeDir;
                }
                catch { }

                cachePath = Path.Combine(dataDir, "index.cache");
                favPath = Path.Combine(dataDir, "favorites.txt");

                BuildMenu();
                BuildUI();
                WireEvents();

                indexer.WorkerReportsProgress = true;
                indexer.WorkerSupportsCancellation = true;
                indexer.DoWork += Indexer_DoWork;
                indexer.ProgressChanged += Indexer_ProgressChanged;
                indexer.RunWorkerCompleted += Indexer_Completed;

                searchTimer.Interval = 110;
                searchTimer.Tick += (s, e) => { searchTimer.Stop(); SafeApplySearch(); };

                LoadFavorites();
                LoadCache();
                pathBox.Text = settings.RootPath;

                SafeApplySearch();
                UpdateStatus();

                if (settings.AutoIndexOnStartup && Directory.Exists(settings.RootPath))
                    BeginInvoke(new Action(() => StartIndexing()));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error:\n" + ex.Message, "SwiftFind", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void BuildMenu()
        {
            menu = new MenuStrip
            {
                BackColor = Color.FromArgb(24, 28, 40),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f),
                Renderer = new DarkMenuRenderer()
            };

            // File
            var mFile = new ToolStripMenuItem("&File");
            mFile.DropDownItems.Add("&Choose folder…", null, (s, e) => ChooseFolder());
            mFile.DropDownItems.Add("&Build / Refresh index\tCtrl+R", null, (s, e) => StartIndexing());
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add("E&xit", null, (s, e) => Close());

            // Edit
            var mEdit = new ToolStripMenuItem("&Edit");
            mEdit.DropDownItems.Add("&Copy\tCtrl+C", null, (s, e) => ClipboardCopy(false));
            mEdit.DropDownItems.Add("Cu&t\tCtrl+X", null, (s, e) => ClipboardCopy(true));
            mEdit.DropDownItems.Add("&Paste\tCtrl+V", null, (s, e) => ClipboardPaste());
            mEdit.DropDownItems.Add(new ToolStripSeparator());
            mEdit.DropDownItems.Add("Select &All\tCtrl+A", null, (s, e) =>
            {
                foreach (ListViewItem i in resultsView.Items) i.Selected = true;
            });

            // View
            var mView = new ToolStripMenuItem("&View");
            var mGroup = new ToolStripMenuItem("&Group by");
            foreach (string g in new[] { "None", "Extension", "Folder", "Modified", "Size" })
            {
                var item = new ToolStripMenuItem(g) { Tag = g, Checked = settings.GroupBy == g };
                item.Click += (s, e) =>
                {
                    settings.GroupBy = (string)((ToolStripMenuItem)s).Tag;
                    settings.Save();
                    foreach (ToolStripMenuItem mi in mGroup.DropDownItems) mi.Checked = false;
                    ((ToolStripMenuItem)s).Checked = true;
                    SafeApplySearch();
                };
                mGroup.DropDownItems.Add(item);
            }
            mView.DropDownItems.Add(mGroup);
            mView.DropDownItems.Add(new ToolStripSeparator());

            var mColor = new ToolStripMenuItem("Color coding") { Checked = settings.ColorCode, CheckOnClick = true };
            mColor.Click += (s, e) =>
            {
                settings.ColorCode = mColor.Checked;
                settings.Save();
                SafeApplySearch();
            };
            mView.DropDownItems.Add(mColor);

            var mFoldersFirst = new ToolStripMenuItem("Folders first") { Checked = settings.ShowFoldersFirst, CheckOnClick = true };
            mFoldersFirst.Click += (s, e) =>
            {
                settings.ShowFoldersFirst = mFoldersFirst.Checked;
                settings.Save();
                SafeApplySearch();
            };
            mView.DropDownItems.Add(mFoldersFirst);

            // Tools
            var mTools = new ToolStripMenuItem("&Tools");
            mTools.DropDownItems.Add("&Preferences…\tCtrl+,\", null, (s, e) => OpenPreferences());
            mTools.DropDownItems.Add("Show &Favorites\tCtrl+D", null, (s, e) => ShowFavorites());
            mTools.DropDownItems.Add(new ToolStripSeparator());
            mTools.DropDownItems.Add("Clear search", null, (s, e) => { queryBox.Clear(); queryBox.Focus(); });

            // Help
            var mHelp = new ToolStripMenuItem("&Help");
            mHelp.DropDownItems.Add("&Keyboard shortcuts", null, (s, e) => ShowShortcuts());
            mHelp.DropDownItems.Add("Query syntax", null, (s, e) => ShowQueryHelp());
            mHelp.DropDownItems.Add(new ToolStripSeparator());
            mHelp.DropDownItems.Add("About SwiftFind", null, (s, e) =>
            {
                MessageBox.Show(
                    "SwiftFind 2.5\n\nInstant search + Explorer-like file manager\nWindows 8 – 11\n\nBuilt with care for speed, clarity and control.",
                    "About SwiftFind",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            });

            menu.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mView, mTools, mHelp });
            MainMenuStrip = menu;
            Controls.Add(menu);
        }

        void BuildUI()
        {
            var main = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new Padding(14, 8, 14, 10),
                BackColor = Bg
            };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));  // header
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));  // search
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));  // path
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));  // filters
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // list
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));  // status
            Controls.Add(main);

            // Header
            var header = new Panel { Dock = DockStyle.Fill, BackColor = Bg };
            var logo = new Label
            {
                Text = "S",
                Size = new Size(36, 36),
                Location = new Point(0, 4),
                BackColor = Accent,
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 16, FontStyle.Bold)
            };
            var title = new Label
            {
                Text = "SwiftFind",
                AutoSize = true,
                Location = new Point(46, 2),
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.White
            };
            var subtitle = new Label
            {
                Text = "Professional desktop search & file manager",
                AutoSize = true,
                Location = new Point(48, 28),
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
                Font = new Font("Segoe UI", 13),
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
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));

            pathBox = new TextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Surface,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
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

            filterPanel.Controls.Add(LabelMuted("Type"));
            filterPanel.Controls.Add(filterExt);
            filterPanel.Controls.Add(LabelMuted("Size"));
            filterPanel.Controls.Add(filterSize);
            filterPanel.Controls.Add(LabelMuted("Modified"));
            filterPanel.Controls.Add(filterDate);

            var tip = new Label
            {
                Text = "  ext:pdf   size:>10MB   dm:week   *report*   regex:",
                AutoSize = true,
                ForeColor = Color.FromArgb(110, 120, 140),
                Margin = new Padding(14, 7, 0, 0),
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
                HeaderStyle = ColumnHeaderStyle.Clickable,
                ShowGroups = true
            };
            resultsView.Columns.Add("Name", 350);
            resultsView.Columns.Add("Location", 390);
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
                Width = 180,
                Height = 12,
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            statusPanel.Controls.Add(statusLabel);
            statusPanel.Controls.Add(progressBar);
            main.Controls.Add(statusPanel, 0, 5);
        }

        Label LabelMuted(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = TextMuted,
                Margin = new Padding(text == "Type" ? 0 : 12, 7, 6, 0)
            };
        }

        ComboBox MakeFilterCombo(string first, string[] items)
        {
            var c = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 112,
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

            filterExt.SelectedIndexChanged += (s, e) => SafeApplySearch();
            filterSize.SelectedIndexChanged += (s, e) => SafeApplySearch();
            filterDate.SelectedIndexChanged += (s, e) => SafeApplySearch();

            resultsView.DoubleClick += (s, e) => OpenSelected(false);
            resultsView.KeyDown += ResultsView_KeyDown;
            resultsView.ItemDrag += ResultsView_ItemDrag;
            resultsView.ColumnClick += ResultsView_ColumnClick;

            this.KeyDown += MainForm_KeyDown;
        }

        // ---------- Keyboard & interaction (same robust handlers) ----------

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
            else if (e.KeyCode == Keys.Escape) queryBox.Clear();
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
            else if (e.KeyCode == Keys.Back) queryBox.Focus();
        }

        void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.F) { queryBox.Focus(); queryBox.SelectAll(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.R) { StartIndexing(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.D) { AddFavorite(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Oemcomma) { OpenPreferences(); e.Handled = true; }
        }

        void ResultsView_ItemDrag(object sender, ItemDragEventArgs e)
        {
            var paths = GetSelectedPaths();
            if (paths.Count == 0) return;
            try
            {
                var data = new DataObject(DataFormats.FileDrop, paths.ToArray());
                resultsView.DoDragDrop(data, DragDropEffects.Copy | DragDropEffects.Move);
            }
            catch { }
        }

        void ResultsView_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == sortColumn) sortAsc = !sortAsc;
            else { sortColumn = e.Column; sortAsc = true; }
            SafeApplySearch();
        }

        // ---------- Indexing ----------

        void ChooseFolder()
        {
            try
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Choose a folder or drive to index";
                    dlg.SelectedPath = Directory.Exists(settings.RootPath) ? settings.RootPath : "C:\\";
                    if (dlg.ShowDialog(this) == DialogResult.OK)
                    {
                        settings.RootPath = dlg.SelectedPath;
                        pathBox.Text = settings.RootPath;
                        settings.Save();
                        index.Clear();
                        resultsView.Items.Clear();
                        UpdateStatus("Folder selected — click Build index");
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "SwiftFind"); }
        }

        void StartIndexing()
        {
            if (isIndexing || indexer.IsBusy) return;
            if (!Directory.Exists(settings.RootPath))
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
            UpdateStatus("Indexing…");
            indexer.RunWorkerAsync(settings.RootPath);
        }

        void Indexer_DoWork(object sender, DoWorkEventArgs e)
        {
            string root = (string)e.Argument;
            List<FileEntry> found = null;
            string method = "recursive";

            var exclFolders = settings.GetExcludeFolderSet();
            var exclExts = settings.GetExcludeExtSet();

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
                    {
                        // Apply extension excludes post-USN
                        if (exclExts.Count > 0)
                            found = found.Where(f => f.IsDirectory || !exclExts.Contains(f.Extension)).ToList();
                        // Apply folder excludes
                        if (exclFolders.Count > 0)
                            found = found.Where(f => !exclFolders.Any(ex =>
                                f.FullPath.StartsWith(ex.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
                                f.FullPath.Equals(ex, StringComparison.OrdinalIgnoreCase))).ToList();
                        method = "USN";
                    }
                }
                catch { found = null; }
            }

            if (found == null)
            {
                found = RecursiveIndexer.Index(root,
                    count => { if (!indexer.CancellationPending) indexer.ReportProgress(0, count.ToString("N0")); },
                    exclFolders, exclExts);
                method = "recursive";
            }

            e.Result = new object[] { found ?? new List<FileEntry>(), method };
        }

        void Indexer_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            try { statusLabel.Text = "Indexing… " + (e.UserState as string ?? ""); }
            catch { }
        }

        void Indexer_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            isIndexing = false;
            btnIndex.Enabled = true;
            btnChoose.Enabled = true;
            progressBar.Visible = false;

            if (e.Cancelled) { UpdateStatus("Indexing cancelled"); return; }
            if (e.Error != null) { UpdateStatus("Index error: " + e.Error.Message); return; }

            try
            {
                var result = (object[])e.Result;
                index = (List<FileEntry>)result[0];
                settings.LastIndexMethod = (string)result[1];
                settings.LastIndexTicks = DateTime.Now.Ticks;
                settings.Save();
                SaveCache();
                SafeApplySearch();
                UpdateStatus();
                queryBox.Focus();
            }
            catch (Exception ex)
            {
                UpdateStatus("Error applying index: " + ex.Message);
            }
        }

        // ---------- Search + Grouping + Color ----------

        void SafeApplySearch()
        {
            try { ApplySearch(); }
            catch (Exception ex) { statusLabel.Text = "Search error: " + ex.Message; }
        }

        void ApplySearch()
        {
            string text = queryBox.Text.Trim();
            var q = SearchEngine.Parse(text);
            q.MatchPath = settings.MatchPath;

            // UI filters
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

            visible = SearchEngine.Execute(index, q, settings.MaxResults);
            visible = SortVisible(visible);
            PopulateList();
            UpdateStatus();
        }

        List<FileEntry> SortVisible(List<FileEntry> list)
        {
            IOrderedEnumerable<FileEntry> ordered;
            Func<FileEntry, int> folderKey = f => settings.ShowFoldersFirst ? (f.IsDirectory ? 0 : 1) : 0;

            switch (sortColumn)
            {
                case 1:
                    ordered = sortAsc
                        ? list.OrderBy(folderKey).ThenBy(f => f.Directory, StringComparer.OrdinalIgnoreCase)
                        : list.OrderBy(folderKey).ThenByDescending(f => f.Directory, StringComparer.OrdinalIgnoreCase);
                    break;
                case 2:
                    ordered = sortAsc
                        ? list.OrderBy(folderKey).ThenBy(f => f.Size)
                        : list.OrderBy(folderKey).ThenByDescending(f => f.Size);
                    break;
                case 3:
                    ordered = sortAsc
                        ? list.OrderBy(folderKey).ThenBy(f => f.Modified)
                        : list.OrderBy(folderKey).ThenByDescending(f => f.Modified);
                    break;
                case 4:
                    ordered = sortAsc
                        ? list.OrderBy(folderKey).ThenBy(f => f.IsDirectory ? "" : f.Extension)
                        : list.OrderBy(folderKey).ThenByDescending(f => f.IsDirectory ? "" : f.Extension);
                    break;
                default:
                    ordered = sortAsc
                        ? list.OrderBy(folderKey).ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                        : list.OrderBy(folderKey).ThenByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase);
                    break;
            }
            return ordered.ToList();
        }

        void PopulateList()
        {
            resultsView.BeginUpdate();
            resultsView.Items.Clear();
            resultsView.Groups.Clear();

            bool useGroups = settings.GroupBy != "None" && visible.Count > 0;
            Dictionary<string, ListViewGroup> groups = null;

            if (useGroups)
            {
                groups = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in visible)
                {
                    string key = GetGroupKey(f);
                    if (!groups.ContainsKey(key))
                    {
                        var g = new ListViewGroup(key, key);
                        groups[key] = g;
                        resultsView.Groups.Add(g);
                    }
                }
            }

            DateTime recentThreshold = DateTime.Now.AddDays(-7);

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

                if (settings.ColorCode)
                {
                    if (f.IsDirectory) item.ForeColor = ColorFolder;
                    else if (isFav) item.ForeColor = ColorFav;
                    else if (f.Size >= 100L * 1024 * 1024) item.ForeColor = ColorLarge;
                    else if (f.Modified >= recentThreshold) item.ForeColor = ColorRecent;
                    else item.ForeColor = Color.White;
                }
                else
                {
                    item.ForeColor = f.IsDirectory ? ColorFolder : Color.White;
                }

                if (useGroups)
                {
                    string key = GetGroupKey(f);
                    item.Group = groups[key];
                }

                resultsView.Items.Add(item);
            }

            resultsView.EndUpdate();
        }

        string GetGroupKey(FileEntry f)
        {
            switch (settings.GroupBy)
            {
                case "Extension":
                    return f.IsDirectory ? "Folders" : (string.IsNullOrEmpty(f.Extension) ? "(no extension)" : f.Extension);
                case "Folder":
                    return string.IsNullOrEmpty(f.Directory) ? "(root)" : f.Directory;
                case "Modified":
                    if (f.Modified == DateTime.MinValue) return "Unknown";
                    var days = (DateTime.Now.Date - f.Modified.Date).TotalDays;
                    if (days < 1) return "Today";
                    if (days < 2) return "Yesterday";
                    if (days < 7) return "This week";
                    if (days < 30) return "This month";
                    if (f.Modified.Year == DateTime.Now.Year) return "This year";
                    return f.Modified.Year.ToString();
                case "Size":
                    if (f.IsDirectory) return "Folders";
                    if (f.Size == 0) return "Empty";
                    if (f.Size < 1024) return "< 1 KB";
                    if (f.Size < 100 * 1024) return "1 KB – 100 KB";
                    if (f.Size < 1024 * 1024) return "100 KB – 1 MB";
                    if (f.Size < 10 * 1024 * 1024) return "1 MB – 10 MB";
                    if (f.Size < 100L * 1024 * 1024) return "10 MB – 100 MB";
                    return "> 100 MB";
                default:
                    return "All";
            }
        }

        // ---------- File operations (crash-hardened) ----------

        List<FileEntry> GetSelectedEntries()
        {
            var list = new List<FileEntry>();
            try
            {
                foreach (ListViewItem item in resultsView.SelectedItems)
                {
                    var e = item.Tag as FileEntry;
                    if (e != null) list.Add(e);
                }
            }
            catch { }
            return list;
        }

        List<string> GetSelectedPaths()
        {
            return GetSelectedEntries().Select(e => e.FullPath).ToList();
        }

        void OpenSelected(bool containingFolder)
        {
            foreach (var e in GetSelectedEntries())
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
                        if (e.IsDirectory && Directory.Exists(e.FullPath))
                            Process.Start("explorer.exe", e.FullPath);
                        else if (File.Exists(e.FullPath))
                            Process.Start(e.FullPath);
                        else
                            MessageBox.Show("Item no longer exists. Rebuild the index.", "SwiftFind");
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "SwiftFind"); }
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
                sources = new List<string>();
                foreach (string f in Clipboard.GetFileDropList()) sources.Add(f);
                isCut = false;
            }
            if (sources.Count == 0) return;

            string destDir = settings.RootPath;
            var selected = GetSelectedEntries();
            if (selected.Count > 0)
                destDir = selected[0].IsDirectory ? selected[0].FullPath : selected[0].Directory;

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
                        do { dest = Path.Combine(destDir, baseName + " (" + n++ + ")" + ext); }
                        while (File.Exists(dest) || Directory.Exists(dest));
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
                catch (Exception ex) { MessageBox.Show("Failed: " + ex.Message, "SwiftFind"); }
            }
            if (isCut) clipboardPaths.Clear();
            UpdateStatus("Pasted " + ok + " item(s) — rebuild index to refresh");
        }

        void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (string file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), false);
            foreach (string dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
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
                if (entry.IsDirectory) Directory.Move(entry.FullPath, dest);
                else File.Move(entry.FullPath, dest);
                entry.Name = newName;
                entry.FullPath = dest;
                SafeApplySearch();
                UpdateStatus("Renamed to " + newName);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "SwiftFind"); }
        }

        void DeleteSelected()
        {
            var entries = GetSelectedEntries();
            if (entries.Count == 0) return;
            string msg = entries.Count == 1
                ? "Delete \"" + entries[0].Name + "\"?\nThis cannot be undone from SwiftFind."
                : "Delete " + entries.Count + " items?\nThis cannot be undone from SwiftFind.";
            if (MessageBox.Show(msg, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int ok = 0;
            foreach (var e in entries)
            {
                try
                {
                    if (e.IsDirectory) Directory.Delete(e.FullPath, true);
                    else File.Delete(e.FullPath);
                    index.RemoveAll(x => x.FullPath == e.FullPath);
                    favorites.Remove(e.FullPath);
                    ok++;
                }
                catch (Exception ex) { MessageBox.Show("Could not delete " + e.Name + ":\n" + ex.Message, "SwiftFind"); }
            }
            SaveFavorites();
            SafeApplySearch();
            UpdateStatus("Deleted " + ok + " item(s)");
        }

        void CreateNewFolder()
        {
            string parent = settings.RootPath;
            var selected = GetSelectedEntries();
            if (selected.Count > 0)
                parent = selected[0].IsDirectory ? selected[0].FullPath : selected[0].Directory;

            string name = ShowInputDialog("New Folder", "Folder name:", "New folder");
            if (string.IsNullOrWhiteSpace(name)) return;
            try
            {
                string path = Path.Combine(parent, name);
                Directory.CreateDirectory(path);
                index.Add(new FileEntry
                {
                    Name = name, FullPath = path, Directory = parent,
                    Size = 0, Modified = DateTime.Now, IsDirectory = true
                });
                SafeApplySearch();
                UpdateStatus("Created folder: " + name);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "SwiftFind"); }
        }

        void CopyPaths()
        {
            var paths = GetSelectedPaths();
            if (paths.Count == 0) return;
            try { Clipboard.SetText(string.Join(Environment.NewLine, paths)); }
            catch { }
            UpdateStatus("Copied " + paths.Count + " path(s)");
        }

        void ShowProperties()
        {
            var entries = GetSelectedEntries();
            if (entries.Count != 1) return;
            var e = entries[0];
            string info = e.Name + "\n\nPath: " + e.FullPath +
                "\nType: " + (e.IsDirectory ? "Folder" : e.Extension) +
                "\nSize: " + (e.IsDirectory ? "—" : FormatSize(e.Size)) +
                "\nModified: " + (e.Modified == DateTime.MinValue ? "—" : e.Modified.ToString("yyyy-MM-dd HH:mm:ss"));
            MessageBox.Show(info, "Properties — " + e.Name);
        }

        // ---------- Favorites & Preferences ----------

        void AddFavorite()
        {
            int added = 0;
            foreach (var e in GetSelectedEntries())
            {
                if (!favorites.Contains(e.FullPath)) { favorites.Add(e.FullPath); added++; }
            }
            SaveFavorites();
            SafeApplySearch();
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

        void OpenPreferences()
        {
            using (var dlg = new PreferencesForm(settings))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // settings already updated and saved by the dialog
                    SafeApplySearch();
                    UpdateStatus("Preferences saved");
                }
            }
        }

        void ShowShortcuts()
        {
            MessageBox.Show(
                "Ctrl+F          Focus search\n" +
                "Ctrl+R          Rebuild index\n" +
                "Ctrl+D          Add to Favorites\n" +
                "Ctrl+,          Preferences\n" +
                "Enter           Open\n" +
                "F2              Rename\n" +
                "Delete          Delete\n" +
                "Ctrl+C / X / V  Copy / Cut / Paste\n" +
                "Ctrl+A          Select all\n" +
                "Esc             Clear search\n" +
                "↓ from search   Jump to results",
                "Keyboard Shortcuts",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ShowQueryHelp()
        {
            MessageBox.Show(
                "Free text          Matches name or path\n" +
                "*report*           Wildcards\n" +
                "ext:pdf            Extension filter\n" +
                "size:>10MB         Minimum size\n" +
                "size:<1KB          Maximum size\n" +
                "dm:today / week / month / year\n" +
                "regex: pattern     Regular expression\n\n" +
                "Combine freely:  invoice ext:pdf size:>1MB dm:month",
                "Query Syntax",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ---------- Persistence ----------

        void SaveCache()
        {
            try
            {
                string temp = cachePath + ".tmp";
                using (var w = new StreamWriter(temp, false, Encoding.UTF8))
                {
                    w.WriteLine(settings.RootPath);
                    w.WriteLine(index.Count);
                    w.WriteLine(settings.LastIndexMethod);
                    w.WriteLine(settings.LastIndexTicks);
                    foreach (var f in index)
                    {
                        w.WriteLine(
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(f.FullPath)) + "|" +
                            f.Size + "|" + f.Modified.Ticks + "|" + (f.IsDirectory ? "1" : "0"));
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
                    if (savedRoot != settings.RootPath) return;
                    r.ReadLine(); // count
                    settings.LastIndexMethod = r.ReadLine() ?? "recursive";
                    long ticks;
                    if (long.TryParse(r.ReadLine(), out ticks)) settings.LastIndexTicks = ticks;

                    var loaded = new List<FileEntry>();
                    string line;
                    while ((line = r.ReadLine()) != null)
                    {
                        string[] parts = line.Split('|');
                        if (parts.Length != 4) continue;
                        long size, t;
                        if (!long.TryParse(parts[1], out size) || !long.TryParse(parts[2], out t)) continue;
                        string path = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
                        loaded.Add(new FileEntry
                        {
                            Name = Path.GetFileName(path),
                            FullPath = path,
                            Directory = Path.GetDirectoryName(path),
                            Size = size,
                            Modified = new DateTime(t),
                            IsDirectory = parts[3] == "1"
                        });
                    }
                    index = loaded;
                }
            }
            catch { index.Clear(); }
        }

        void SaveFavorites()
        {
            try { File.WriteAllLines(favPath, favorites); } catch { }
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

        void UpdateStatus(string extra = null)
        {
            try
            {
                if (extra != null) { statusLabel.Text = extra; return; }
                string method = settings.LastIndexMethod == "USN" ? "USN fast" : settings.LastIndexMethod;
                string when = settings.LastIndexTicks == 0 ? "" :
                    "  ·  " + new DateTime(settings.LastIndexTicks).ToString("HH:mm");
                statusLabel.Text = string.Format("{0:N0} indexed ({1})  ·  showing {2:N0}{3}  ·  {4}",
                    index.Count, method, visible.Count, when, settings.RootPath);
            }
            catch { }
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
                    Left = 16, Top = 44, Width = 370, Text = defaultValue,
                    BackColor = Surface, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle
                };
                var ok = new Button
                {
                    Text = "OK", Left = 220, Top = 85, Width = 80,
                    DialogResult = DialogResult.OK, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
                };
                ok.FlatAppearance.BorderSize = 0;
                var cancel = new Button
                {
                    Text = "Cancel", Left = 310, Top = 85, Width = 80,
                    DialogResult = DialogResult.Cancel, BackColor = Surface2, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
                };
                cancel.FlatAppearance.BorderSize = 0;

                form.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? tb.Text.Trim() : null;
            }
        }
    }

    // Simple dark menu renderer for a polished look
    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                using (var b = new SolidBrush(Color.FromArgb(50, 60, 85)))
                    e.Graphics.FillRectangle(b, e.Item.ContentRectangle);
            }
            else
                base.OnRenderMenuItemBackground(e);
        }
    }

    public class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return Color.FromArgb(24, 28, 40); } }
        public override Color MenuStripGradientEnd { get { return Color.FromArgb(24, 28, 40); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(50, 60, 85); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(50, 60, 85); } }
        public override Color MenuBorder { get { return Color.FromArgb(40, 46, 60); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(28, 34, 48); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(28, 34, 48); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(28, 34, 48); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(28, 34, 48); } }
        public override Color SeparatorDark { get { return Color.FromArgb(50, 56, 70); } }
        public override Color SeparatorLight { get { return Color.FromArgb(50, 56, 70); } }
    }
}
