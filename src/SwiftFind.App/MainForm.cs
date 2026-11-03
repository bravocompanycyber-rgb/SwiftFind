using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using SwiftFind.Controls;
using SwiftFind.Core;
using SwiftFind.Forms;
using SwiftFind.Helpers;
using SwiftFind.Models;
using SwiftFind.Search;
using SwiftFind.Services;

namespace SwiftFind
{
    /// <summary>Main window: layout and wiring. Behaviour lives in the MainForm.*.cs partials.</summary>
    public partial class MainForm : Form
    {
        // ---- state
        readonly AppSettings settings = AppSettings.Load();
        volatile FileEntry[] index = new FileEntry[0];
        readonly HashSet<string> favorites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly IconCache icons = new IconCache();
        string indexMethod = "none";
        DateTime indexBuiltAt = DateTime.MinValue;
        bool favoritesOnly;
        int sortColumn;
        bool sortAscending = true;

        // ---- controls
        MenuStrip menu;
        PictureBox logoBox;
        Label lblIndexInfo, lblRoot;
        SearchBox searchBox;
        FlatButton btnChoose, btnIndex, btnFavorites;
        ComboBox cmbType, cmbSize, cmbDate;
        ListView resultsView;
        Label emptyLabel;
        Label lblStatus, lblCount;
        ProgressBar busyBar;
        System.Windows.Forms.Timer debounce;
        ContextMenuStrip rowMenu;
        ToolStripMenuItem miFavorite, miRename, miPaste, miOpenWith;
        readonly Dictionary<string, ToolStripMenuItem> groupItems = new Dictionary<string, ToolStripMenuItem>();
        ToolStripMenuItem miFoldersFirst, miColor, miMatchPath, miFavOnly;

        static readonly string[] ColumnNames = { "Name", "Location", "Size", "Modified", "Type" };

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Theme.StyleForm(this);
            Text = "SwiftFind";
            Icon = AppIcon.Load();
            MinimumSize = new Size(900, 600);
            KeyPreview = true;
            DoubleBuffered = true;

            sortColumn = settings.SortColumn;
            sortAscending = settings.SortAscending;

            BuildMenu();
            BuildLayout();
            BuildContextMenu();
            RestoreWindow();
            LoadFavorites();
            ApplyMenuState();
            UpdateHeaders();

            debounce = new System.Windows.Forms.Timer { Interval = 90 };
            debounce.Tick += (s, e) => { debounce.Stop(); ApplySearch(); };

            Shown += (s, e) => { searchBox.Input.Focus(); LoadCacheThenMaybeIndex(); };
            FormClosing += OnClosing;
        }

        // ================================================================ menu

        sealed class DarkColors : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Theme.SurfaceHi; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.SurfaceHi; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.SurfaceHi; } }
            public override Color MenuItemBorder { get { return Theme.SurfaceHi; } }
            public override Color MenuItemPressedGradientBegin { get { return Theme.Surface; } }
            public override Color MenuItemPressedGradientEnd { get { return Theme.Surface; } }
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
            public override Color MenuStripGradientBegin { get { return Theme.Bg; } }
            public override Color MenuStripGradientEnd { get { return Theme.Bg; } }
            public override Color SeparatorDark { get { return Theme.Border; } }
            public override Color SeparatorLight { get { return Theme.Border; } }
            public override Color CheckBackground { get { return Theme.Accent; } }
            public override Color CheckSelectedBackground { get { return Theme.Accent; } }
            public override Color CheckPressedBackground { get { return Theme.Accent; } }
        }

        sealed class DarkRenderer : ToolStripProfessionalRenderer
        {
            public DarkRenderer() : base(new DarkColors()) { RoundedEdges = false; }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextDim;
                base.OnRenderItemText(e);
            }
            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Theme.TextMuted;
                base.OnRenderArrow(e);
            }
        }

        ToolStripMenuItem Item(string text, Keys shortcut, EventHandler click)
        {
            var mi = new ToolStripMenuItem(text) { ForeColor = Theme.Text };
            if (shortcut != Keys.None) mi.ShortcutKeys = shortcut;
            if (click != null) mi.Click += click;
            return mi;
        }

        void BuildMenu()
        {
            menu = new MenuStrip { Renderer = new DarkRenderer(), BackColor = Theme.Bg, ForeColor = Theme.Text, Font = Theme.Ui(9.5f), Padding = new Padding(8, 2, 0, 2) };

            var file = new ToolStripMenuItem("File");
            file.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("Choose folder\u2026", Keys.Control | Keys.O, (s, e) => ChooseFolder()),
                Item("Build / rebuild index", Keys.Control | Keys.R, (s, e) => ToggleIndexing()),
                new ToolStripSeparator(),
                Item("Export results to CSV\u2026", Keys.Control | Keys.E, (s, e) => ExportCsv()),
                Item("Open data folder", Keys.None, (s, e) => Reveal(AppPaths.DataDir, true)),
                new ToolStripSeparator(),
                Item("Exit", Keys.Alt | Keys.F4, (s, e) => Close())
            });

            var view = new ToolStripMenuItem("View");
            var group = new ToolStripMenuItem("Group by") { ForeColor = Theme.Text };
            foreach (string g in new[] { "None", "Extension", "Folder", "Modified", "Size" })
            {
                string name = g;
                var gi = Item(g, Keys.None, (s, e) => { settings.GroupBy = name; settings.Save(); ApplyMenuState(); ApplySearch(); });
                groupItems[g] = gi;
                group.DropDownItems.Add(gi);
            }
            miFoldersFirst = Item("Folders first", Keys.None, (s, e) => { settings.ShowFoldersFirst = !settings.ShowFoldersFirst; settings.Save(); ApplyMenuState(); ApplySearch(); });
            miColor = Item("Colour-code results", Keys.None, (s, e) => { settings.ColorCode = !settings.ColorCode; settings.Save(); ApplyMenuState(); ApplySearch(); });
            miMatchPath = Item("Match full path", Keys.None, (s, e) => { settings.MatchPath = !settings.MatchPath; settings.Save(); ApplyMenuState(); ApplySearch(); });
            miFavOnly = Item("Favourites only", Keys.Control | Keys.B, (s, e) => ToggleFavoritesView());
            view.DropDownItems.AddRange(new ToolStripItem[] { group, new ToolStripSeparator(), miFoldersFirst, miColor, miMatchPath, new ToolStripSeparator(), miFavOnly });

            var tools = new ToolStripMenuItem("Tools");
            tools.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("Preferences\u2026", Keys.Control | Keys.Oemcomma, (s, e) => ShowPreferences()),
                Item("Clear saved index", Keys.None, (s, e) => ClearCache())
            });

            var help = new ToolStripMenuItem("Help");
            help.DropDownItems.AddRange(new ToolStripItem[]
            {
                Item("Search syntax", Keys.F1, (s, e) => ShowSyntaxHelp()),
                Item("View error log", Keys.None, (s, e) => OpenLog()),
                new ToolStripSeparator(),
                Item("About SwiftFind", Keys.None, (s, e) => ShowAbout())
            });

            menu.Items.AddRange(new ToolStripItem[] { file, view, tools, help });
            MainMenuStrip = menu;
        }

        void ApplyMenuState()
        {
            foreach (var kv in groupItems) kv.Value.Checked = kv.Key == settings.GroupBy;
            miFoldersFirst.Checked = settings.ShowFoldersFirst;
            miColor.Checked = settings.ColorCode;
            miMatchPath.Checked = settings.MatchPath;
            miFavOnly.Checked = favoritesOnly;
        }

        // ================================================================ layout

        Label Caption(string text)
        {
            return new Label { Text = text, AutoSize = true, ForeColor = Theme.TextMuted, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 6, 0) };
        }

        void BuildLayout()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = Theme.Bg, Padding = new Padding(20, 6, 20, 0) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));   // header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // search
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));   // folder / actions
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // filters
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // results
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // status

            // header
            var header = new Panel { Dock = DockStyle.Fill };
            logoBox = new PictureBox { Left = 0, Top = 6, Width = 48, Height = 48, SizeMode = PictureBoxSizeMode.Zoom, Image = AppIcon.LoadLogo() };
            var title = new Label { Text = "SwiftFind", Left = 58, Top = 4, AutoSize = true, Font = Theme.Ui(18f, FontStyle.Bold), ForeColor = Theme.Text };
            var tag = new Label { UseMnemonic = false, Text = "FAST DESKTOP SEARCH & INDEXING", Left = 60, Top = 38, AutoSize = true, Font = Theme.Ui(7.5f, FontStyle.Bold), ForeColor = Theme.Orange };
            lblIndexInfo = new Label { Dock = DockStyle.Right, Width = 520, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.TextMuted };
            header.Controls.AddRange(new Control[] { lblIndexInfo, logoBox, title, tag });

            // search
            searchBox = new SearchBox { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4) };
            searchBox.Input.TextChanged += (s, e) => { debounce.Stop(); debounce.Start(); };
            searchBox.Input.KeyDown += SearchKeyDown;

            // folder row
            var folderRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblRoot = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.TextMuted, AutoEllipsis = true };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            btnChoose = new FlatButton("Choose folder\u2026") { Width = 156, Margin = new Padding(0, 6, 8, 6) };
            btnFavorites = new FlatButton("\u2605 Favourites") { Width = 130, Margin = new Padding(0, 6, 8, 6) };
            btnIndex = new FlatButton("Build index", FlatButton.Style.Orange) { Width = 130, Margin = new Padding(0, 6, 0, 6) };
            btnChoose.Click += (s, e) => ChooseFolder();
            btnFavorites.Click += (s, e) => ToggleFavoritesView();
            btnIndex.Click += (s, e) => ToggleIndexing();
            buttons.Controls.AddRange(new Control[] { btnChoose, btnFavorites, btnIndex });
            folderRow.Controls.Add(lblRoot, 0, 0);
            folderRow.Controls.Add(buttons, 1, 0);

            // filters
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight };
            cmbType = MakeCombo(new[] { "All types", "Documents", "Images", "Audio", "Video", "Archives", "Code", "Executables", "Folders" });
            cmbSize = MakeCombo(new[] { "Any size", "Empty", "Under 1 MB", "1 - 10 MB", "10 - 100 MB", "100 MB - 1 GB", "Over 1 GB" });
            cmbDate = MakeCombo(new[] { "Any date", "Today", "Yesterday", "Last 7 days", "Last 30 days", "This year" });
            var reset = new FlatButton("Reset") { Width = 80, Height = 30, Margin = new Padding(8, 3, 0, 3) };
            reset.Click += (s, e) => { cmbType.SelectedIndex = 0; cmbSize.SelectedIndex = 0; cmbDate.SelectedIndex = 0; searchBox.Input.Clear(); searchBox.Input.Focus(); };
            filters.Controls.AddRange(new Control[] { Caption("Type"), cmbType, Space(10), Caption("Size"), cmbSize, Space(10), Caption("Modified"), cmbDate, reset });

            // results
            var host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            resultsView = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, MultiSelect = true,
                BorderStyle = BorderStyle.None, BackColor = Theme.Bg, ForeColor = Theme.Text, Font = Theme.Ui(10f),
                SmallImageList = icons.Images, ShowItemToolTips = true, AllowColumnReorder = false
            };
            typeof(ListView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(resultsView, true, null);
            Native.UseDarkControlTheme(resultsView);
            resultsView.Columns.Add(ColumnNames[0], 330);
            resultsView.Columns.Add(ColumnNames[1], 400);
            resultsView.Columns.Add(ColumnNames[2], 95, HorizontalAlignment.Right);
            resultsView.Columns.Add(ColumnNames[3], 150);
            resultsView.Columns.Add(ColumnNames[4], 90);
            resultsView.ColumnClick += (s, e) => SortBy(e.Column);
            resultsView.DoubleClick += (s, e) => OpenSelected();
            resultsView.KeyDown += ResultsKeyDown;
            resultsView.ItemDrag += OnItemDrag;
            resultsView.SelectedIndexChanged += (s, e) => UpdateSelectionStatus();

            emptyLabel = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.TextMuted, Font = Theme.Ui(12f), BackColor = Theme.Bg, Visible = false };
            host.Controls.Add(emptyLabel);
            host.Controls.Add(resultsView);
            resultsView.BringToFront();

            // status
            var status = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            status.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            lblStatus = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Theme.TextMuted, AutoEllipsis = true };
            busyBar = new ProgressBar { Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 30, Height = 6, Width = 130, Anchor = AnchorStyles.None, Visible = false };
            lblCount = new Label { AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, ForeColor = Theme.TextMuted };
            status.Controls.Add(lblStatus, 0, 0);
            status.Controls.Add(busyBar, 1, 0);
            status.Controls.Add(lblCount, 2, 0);

            root.Controls.Add(header, 0, 0);
            root.Controls.Add(searchBox, 0, 1);
            root.Controls.Add(folderRow, 0, 2);
            root.Controls.Add(filters, 0, 3);
            root.Controls.Add(host, 0, 4);
            root.Controls.Add(status, 0, 5);

            // Dock order: the Fill control must be frontmost in z-order, the menu behind it.
            Controls.Add(root);
            Controls.Add(menu);
            root.BringToFront();

            lblRoot.Text = "\uD83D\uDCC1  " + settings.RootPath;
        }

        Control Space(int w) { return new Panel { Width = w, Height = 1, Margin = new Padding(0) }; }

        ComboBox MakeCombo(string[] items)
        {
            var c = new ComboBox { Width = 140, Margin = new Padding(0, 4, 0, 0), Font = Theme.Ui(9.5f) };
            Theme.StyleCombo(c);
            c.Items.AddRange(items);
            c.SelectedIndex = 0;
            c.SelectedIndexChanged += (s, e) => ApplySearch();
            return c;
        }

        void RestoreWindow()
        {
            Size = new Size(settings.WindowWidth, settings.WindowHeight);
            if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
            {
                var r = new Rectangle(settings.WindowLeft, settings.WindowTop, settings.WindowWidth, settings.WindowHeight);
                if (Screen.AllScreens.Any(sc => sc.WorkingArea.IntersectsWith(r)))
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = new Point(settings.WindowLeft, settings.WindowTop);
                }
                else StartPosition = FormStartPosition.CenterScreen;
            }
            else StartPosition = FormStartPosition.CenterScreen;
            if (settings.WindowMaximized) WindowState = FormWindowState.Maximized;
        }

        void OnClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                CancelBackgroundWork();
                var b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                settings.WindowLeft = b.Left; settings.WindowTop = b.Top;
                settings.WindowWidth = b.Width; settings.WindowHeight = b.Height;
                settings.WindowMaximized = WindowState == FormWindowState.Maximized;
                settings.SortColumn = sortColumn; settings.SortAscending = sortAscending;
                settings.Save();
                FlushCacheIfDirty();
            }
            catch (Exception ex) { Log.Error("Closing", ex); }
        }
    }
}
