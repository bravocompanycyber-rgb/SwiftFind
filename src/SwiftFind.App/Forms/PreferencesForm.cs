using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SwiftFind.Controls;
using SwiftFind.Core;
using SwiftFind.Models;
using SwiftFind.Services;

namespace SwiftFind.Forms
{
    /// <summary>Settings window: left navigation, one scrollable page per topic.</summary>
    public class PreferencesForm : Form
    {
        readonly AppSettings settings;
        readonly List<Panel> pages = new List<Panel>();
        readonly List<FlatButton> navButtons = new List<FlatButton>();
        const int PageWidth = 580;

        // results for the caller
        public bool IndexSettingsChanged { get; private set; }
        public bool AppearanceChanged { get; private set; }
        public bool AdminRestartRequested { get; private set; }
        public bool ReplacedSettingsFile { get; private set; }

        // scope & drives
        RadioButton radAll, radFolder;
        CheckBox chkRemovable, chkNetwork;
        TextBox txtRoot;
        ListView lvDrives;
        // indexing
        ComboBox cmbPriority;
        NumericUpDown numRefresh;
        CheckBox chkAuto, chkHidden;
        // search & results
        NumericUpDown numMax, numDelay;
        ComboBox cmbGroup, cmbDouble, cmbDate;
        CheckBox chkFoldersFirst, chkColor, chkMatchPath, chkConfirm;
        // appearance
        ComboBox cmbTheme, cmbTextSize;
        // startup & tray
        CheckBox chkStartWin, chkStartMin, chkTray, chkHotkey, chkClearHotkey;
        ComboBox cmbHotkey;
        // exclusions
        ListBox lstFolders;
        TextBox txtExtensions;

        static readonly string[] PriorityValues = { "Gentle", "Auto", "Fast" };
        static readonly string[] ThemeValues = { "Dark", "Light", "System" };
        static readonly int[] TextSizes = { 90, 100, 115, 130 };
        static readonly string[] DateValues = { "Iso", "System", "Relative" };

        // ---------------------------------------------------------------- page builder

        sealed class Page
        {
            public readonly Panel Panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = false, Padding = new Padding(28, 20, 28, 20) };
            int y = 4;

            public void Gap(int h) { y += h; }

            public T Add<T>(T c, int height, int left = 0) where T : Control
            {
                c.Left = left; c.Top = y; c.Height = height;
                if (c is Label || c is CheckBox || c is RadioButton) c.Width = PageWidth - left;
                var lbl = c as Label; if (lbl != null) lbl.UseMnemonic = false;
                Panel.Controls.Add(c);
                y += height + 8;
                return c;
            }

            public void Heading(string text)
            {
                Add(new Label { Text = text, Font = Theme.Ui(14f, FontStyle.Bold), ForeColor = Theme.Text, Width = PageWidth }, 32);
                y += 4;
            }

            public void Note(string text, int lines = 1)
            {
                // Measure the wrapped text so nothing is ever cut off, whatever the font size.
                var font = Theme.Ui(9.5f);
                int h = TextRenderer.MeasureText(text, font, new Size(PageWidth, 2000), TextFormatFlags.WordBreak).Height + 6;
                Add(new Label { Text = text, ForeColor = Theme.TextDim, Font = font, Width = PageWidth }, Math.Max(20, h));
            }

            public void Caption(string text)
            {
                y -= 2;
                Add(new Label { Text = text, ForeColor = Theme.TextMuted, Width = PageWidth }, 20);
                y -= 6;
            }

            public CheckBox Check(string text, int left = 0)
            {
                return Add(new CheckBox { Text = text, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat, Width = PageWidth - left }, 26, left);
            }

            public ComboBox Combo(string caption, string[] items, int width = 300)
            {
                Caption(caption);
                var c = new ComboBox { Width = width };
                Theme.StyleCombo(c);
                c.Items.AddRange(items);
                return Add(c, 28);
            }

            public NumericUpDown Number(string caption, int min, int max, int step, int width = 120)
            {
                Caption(caption);
                return Add(new NumericUpDown { Minimum = min, Maximum = max, Increment = step, Width = width, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle }, 26);
            }
        }

        Page NewPage(string navTitle)
        {
            var p = new Page();
            pages.Add(p.Panel);
            int index = pages.Count - 1;
            var btn = new FlatButton(navTitle) { Dock = DockStyle.Top, Height = 40, Margin = new Padding(0) };
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(14, 0, 0, 0);
            btn.Click += (s, e) => ShowPage(index);
            navButtons.Add(btn);
            return p;
        }

        void ShowPage(int index)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == index;
                navButtons[i].SetStyle(i == index ? FlatButton.Style.Primary : FlatButton.Style.Ghost);
            }
        }

        // ---------------------------------------------------------------- construction

        public PreferencesForm(AppSettings s)
        {
            settings = s;
            Theme.StyleForm(this);
            Text = "Preferences";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            // Fit small laptop screens at any display scaling (the window scales up from these design units).
            float scale = 1f;
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) scale = Math.Max(1f, g.DpiY / 96f); } catch { }
            int room = (int)(Screen.PrimaryScreen.WorkingArea.Height / scale) - 90;
            ClientSize = new Size(860, Math.Max(480, Math.Min(660, room)));

            BuildScopePage();
            BuildIndexingPage();
            BuildSearchPage();
            BuildAppearancePage();
            BuildStartupPage();
            BuildExclusionsPage();
            BuildMaintenancePage();

            var host = new Panel { Dock = DockStyle.Fill };
            foreach (var p in pages) host.Controls.Add(p);

            var nav = new Panel { Dock = DockStyle.Left, Width = 190, BackColor = Theme.Panel, Padding = new Padding(10, 16, 10, 10) };
            for (int i = navButtons.Count - 1; i >= 0; i--) nav.Controls.Add(navButtons[i]);   // Dock.Top stacks in reverse

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64 };
            var save = new FlatButton("Save", FlatButton.Style.Primary) { Left = 440, Top = 14, Width = 100, Height = 36 };
            var cancel = new FlatButton("Cancel") { Left = 548, Top = 14, Width = 100, Height = 36, DialogResult = DialogResult.Cancel };
            save.Click += (se, ev) => { if (Apply()) DialogResult = DialogResult.OK; };
            footer.Controls.AddRange(new Control[] { save, cancel });
            AcceptButton = save; CancelButton = cancel;

            Controls.Add(host);
            Controls.Add(footer);
            Controls.Add(nav);
            host.BringToFront();

            LoadValues();
            ShowPage(0);
        }

        // ---------------------------------------------------------------- pages

        void BuildScopePage()
        {
            var p = NewPage("Drives & scope");
            p.Heading("What to search");
            radAll = p.Add(new RadioButton { Text = "Everywhere \u2014 index and search all drives together", ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat }, 26);
            chkRemovable = p.Check("Include removable drives (USB sticks, SD cards)", 26);
            chkNetwork = p.Check("Include network drives (slower, scanned rather than fast-indexed)", 26);
            radFolder = p.Add(new RadioButton { Text = "Only this folder:", ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat }, 26);
            txtRoot = p.Add(new TextBox { Width = 440, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle }, 26, 26);
            var browse = new FlatButton("Browse\u2026") { Left = 480, Top = txtRoot.Top - 3, Width = 100, Height = 30 };
            browse.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Choose the folder to index", SelectedPath = txtRoot.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) { txtRoot.Text = d.SelectedPath; radFolder.Checked = true; }
            };
            p.Panel.Controls.Add(browse);
            radAll.CheckedChanged += (s, e) => SyncScopeControls();
            radFolder.CheckedChanged += (s, e) => SyncScopeControls();

            p.Gap(8);
            p.Caption("Connected drives \u2014 untick any drive you never want searched");
            lvDrives = new ListView { View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false, Width = PageWidth, BorderStyle = BorderStyle.FixedSingle, BackColor = Theme.Surface, ForeColor = Theme.Text };
            lvDrives.Columns.Add("Drive", 130); lvDrives.Columns.Add("Kind", 120); lvDrives.Columns.Add("Format", 62); lvDrives.Columns.Add("How it is indexed", 245);
            if (Theme.IsDark) Native.UseDarkControlTheme(lvDrives);
            p.Add(lvDrives, 170);
            var refresh = new FlatButton("Refresh drive list") { Width = 160, Height = 30 };
            refresh.Click += (s, e) =>
            {
                Cursor = Cursors.WaitCursor;
                try { DriveMonitor.Shared.RefreshNow(); FillDrives(); }
                finally { Cursor = Cursors.Default; }
            };
            p.Add(refresh, 30);
            p.Note("SwiftFind notices when a drive is plugged in or removed and updates the index by itself.", 1);
        }

        void BuildIndexingPage()
        {
            var p = NewPage("Indexing");
            p.Heading("Indexing");
            cmbPriority = p.Combo("How hard should indexing work?", new[] { "Gentle \u2014 lowest impact, keeps your PC smooth", "Auto \u2014 adapts to your PC and each drive (recommended)", "Fast \u2014 use as much of the PC as it can" }, 440);
            p.Note("Solid-state drives are scanned with many threads at once; spinning disks and USB drives with few, because parallel reads slow them down.", 2);
            numRefresh = p.Number("Refresh the index in the background every (hours, 0 = never)", 0, 168, 1);
            p.Note("Applies to NTFS drives when SwiftFind runs as administrator, where a refresh takes seconds.", 1);
            p.Gap(6);
            chkAuto = p.Check("Rebuild the whole index every time SwiftFind starts");
            chkHidden = p.Check("Show hidden and system files in results");
            p.Gap(10);
            bool admin = IsAdmin();
            p.Note(admin ? "Running as administrator: the NTFS fast index is available." : "Not running as administrator: large drives are scanned, which is much slower than the NTFS fast index.", 2);
            if (!admin)
            {
                var btn = new FlatButton("Restart as administrator", FlatButton.Style.Orange) { Width = 240, Height = 36 };
                btn.Click += (s, e) => { if (Apply()) { AdminRestartRequested = true; DialogResult = DialogResult.OK; } };
                p.Add(btn, 36);
            }
        }

        void BuildSearchPage()
        {
            var p = NewPage("Search & results");
            p.Heading("Search & results");
            numMax = p.Number("Maximum results shown (more = slower list)", AppSettings.MinResults, AppSettings.MaxResultsLimit, 500);
            numDelay = p.Number("Wait this long after typing before searching (milliseconds)", 0, 1000, 10);
            cmbGroup = p.Combo("Group results by", new[] { "None", "Extension", "Folder", "Modified", "Size" }, 220);
            cmbDouble = p.Combo("Double-clicking a result", new[] { "Opens the file", "Shows it in its folder" }, 260);
            cmbDate = p.Combo("Date format", new[] { "2026-11-07 14:30", "Windows regional format", "Relative (2 h ago)" }, 260);
            p.Gap(6);
            chkFoldersFirst = p.Check("List folders before files");
            chkColor = p.Check("Colour-code folders, favourites, recent and large files");
            chkMatchPath = p.Check("Match search words against the full path, not just the name");
            chkConfirm = p.Check("Ask before moving files to the Recycle Bin");
        }

        void BuildAppearancePage()
        {
            var p = NewPage("Appearance");
            p.Heading("Appearance");
            cmbTheme = p.Combo("Theme", new[] { "Dark", "Light", "Follow Windows" }, 220);
            cmbTextSize = p.Combo("Text size", new[] { "Small", "Normal", "Large", "Extra large" }, 220);
            p.Note("Appearance changes apply after SwiftFind restarts \u2014 you will be asked when you save.", 2);
        }

        void BuildStartupPage()
        {
            var p = NewPage("Startup & tray");
            p.Heading("Startup & tray");
            chkStartWin = p.Check("Start SwiftFind when I sign in to Windows");
            chkStartMin = p.Check("Start hidden in the tray", 26);
            chkTray = p.Check("Closing the window keeps SwiftFind running in the tray");
            p.Gap(8);
            chkHotkey = p.Check("Global hotkey brings SwiftFind to the front from anywhere");
            cmbHotkey = p.Combo("Hotkey", MainForm.HotkeyNames, 220);
            chkClearHotkey = p.Check("Start with an empty search box when opened by the hotkey");
            p.Note("If a hotkey is already used by another program, pick a different one.", 1);
        }

        void BuildExclusionsPage()
        {
            var p = NewPage("Exclusions");
            p.Heading("Exclusions");
            p.Caption("Folders that are never indexed");
            lstFolders = new ListBox { Width = 420, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            p.Add(lstFolders, 170);
            int top = lstFolders.Top;
            var add = new FlatButton("Add folder\u2026") { Left = 436, Top = top, Width = 144, Height = 32 };
            var addName = new FlatButton("Add by name\u2026") { Left = 436, Top = top + 38, Width = 144, Height = 32 };
            var suggest = new FlatButton("Suggested\u2026") { Left = 436, Top = top + 76, Width = 144, Height = 32 };
            var remove = new FlatButton("Remove") { Left = 436, Top = top + 114, Width = 144, Height = 32 };
            add.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Choose a folder to exclude" })
                    if (d.ShowDialog(this) == DialogResult.OK) AddRule(d.SelectedPath);
            };
            addName.Click += (s, e) =>
            {
                string name = Dialogs.Prompt(this, "Exclude by name", "Folder name to skip everywhere (for example  node_modules):", "", t => string.IsNullOrWhiteSpace(t) ? "Enter a folder name." : null);
                if (!string.IsNullOrWhiteSpace(name)) AddRule(name.Trim());
            };
            suggest.Click += (s, e) =>
            {
                foreach (string r in new[] { "$Recycle.Bin", "System Volume Information", "WinSxS", "node_modules", "__pycache__" }) AddRule(r);
            };
            remove.Click += (s, e) => { if (lstFolders.SelectedIndex >= 0) lstFolders.Items.RemoveAt(lstFolders.SelectedIndex); };
            p.Panel.Controls.AddRange(new Control[] { add, addName, suggest, remove });

            p.Note("A full path skips that folder. A bare name (like node_modules) skips every folder with that name.", 2);
            p.Caption("File extensions that are never indexed (comma separated)");
            txtExtensions = p.Add(new TextBox { Width = PageWidth, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle }, 26);
            p.Note("Example:  tmp, log, bak.   Changes apply the next time the index is rebuilt.", 1);
        }

        void AddRule(string rule)
        {
            if (!lstFolders.Items.Cast<string>().Contains(rule, StringComparer.OrdinalIgnoreCase)) lstFolders.Items.Add(rule);
        }

        void BuildMaintenancePage()
        {
            var p = NewPage("Maintenance");
            p.Heading("Maintenance");
            p.Note("Move your setup to another PC, or start over.", 1);
            var export = p.Add(new FlatButton("Export settings\u2026") { Width = 220 }, 36);
            var import = p.Add(new FlatButton("Import settings\u2026") { Width = 220 }, 36);
            var reset = p.Add(new FlatButton("Reset everything to defaults") { Width = 260 }, 36);
            p.Gap(10);
            var openData = p.Add(new FlatButton("Open data folder") { Width = 220 }, 36);
            var openLog = p.Add(new FlatButton("View error log") { Width = 220 }, 36);

            export.Click += (s, e) =>
            {
                using (var d = new SaveFileDialog { Filter = "SwiftFind settings (*.ini)|*.ini", FileName = "SwiftFind-settings.ini" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    try { if (!File.Exists(AppPaths.SettingsFile)) settings.Save(); File.Copy(AppPaths.SettingsFile, d.FileName, true); MessageBox.Show(this, "Your saved settings were exported.", "Preferences"); }
                    catch (Exception ex) { Log.Error("Export settings", ex); MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };
            import.Click += (s, e) =>
            {
                using (var d = new OpenFileDialog { Filter = "SwiftFind settings (*.ini)|*.ini" })
                {
                    if (d.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        File.Copy(d.FileName, AppPaths.SettingsFile, true);
                        ReplacedSettingsFile = true;
                        DialogResult = DialogResult.OK;
                    }
                    catch (Exception ex) { Log.Error("Import settings", ex); MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                }
            };
            reset.Click += (s, e) =>
            {
                if (MessageBox.Show(this, "Reset all settings to their defaults?\nYour index and favourites are kept. SwiftFind will restart.", "Reset settings", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try
                {
                    if (File.Exists(AppPaths.SettingsFile)) File.Delete(AppPaths.SettingsFile);
                    ReplacedSettingsFile = true;
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex) { Log.Error("Reset settings", ex); }
            };
            openData.Click += (s, e) => { try { System.Diagnostics.Process.Start("explorer.exe", "\"" + AppPaths.DataDir + "\""); } catch { } };
            openLog.Click += (s, e) =>
            {
                try
                {
                    if (!File.Exists(AppPaths.LogFile)) MessageBox.Show(this, "No errors have been logged.", "Error log");
                    else System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppPaths.LogFile) { UseShellExecute = true });
                }
                catch { }
            };
        }

        // ---------------------------------------------------------------- values

        static bool IsAdmin()
        {
            try
            {
                using (var id = System.Security.Principal.WindowsIdentity.GetCurrent())
                    return new System.Security.Principal.WindowsPrincipal(id).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        void SyncScopeControls()
        {
            chkRemovable.Enabled = chkNetwork.Enabled = lvDrives.Enabled = radAll.Checked;
            txtRoot.Enabled = radFolder.Checked;
        }

        void FillDrives()
        {
            var excluded = new HashSet<string>(settings.ExcludedDrives, StringComparer.OrdinalIgnoreCase);
            // keep the user's unsaved ticks while refreshing
            foreach (ListViewItem it in lvDrives.Items) { string root = (string)it.Tag; if (it.Checked) excluded.Remove(root); else excluded.Add(root); }

            lvDrives.Items.Clear();
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DriveDetails d in DriveMonitor.Shared.Drives)
            {
                shown.Add(d.Root);
                int threads = SwiftFind.Indexing.IndexTuning.ScanThreads(d.Kind, d.IsExternal, settings.IndexPriority, Environment.ProcessorCount, Native.TotalRamBytes());
                string fast = "fast scan, " + threads + (threads == 1 ? " thread" : " threads");
                string how = d.IsNtfs ? (IsAdmin() ? "NTFS fast index" : "NTFS index (admin) or " + fast) : fast;
                var item = new ListViewItem(d.Display) { Tag = d.Root, Checked = !excluded.Contains(d.Root) };
                item.SubItems.Add(d.KindText); item.SubItems.Add(string.IsNullOrEmpty(d.Format) ? "?" : d.Format); item.SubItems.Add(how);
                lvDrives.Items.Add(item);
            }
            foreach (string root in excluded.Where(r => !shown.Contains(r)))
            {
                var item = new ListViewItem(root.TrimEnd('\\') + "  (not connected)") { Tag = root, Checked = false, ForeColor = Theme.TextDim };
                item.SubItems.Add(""); item.SubItems.Add(""); item.SubItems.Add("Left out");
                lvDrives.Items.Add(item);
            }
        }

        static int IndexOf(string[] values, string v, int fallback)
        {
            int i = Array.IndexOf(values, v);
            return i < 0 ? fallback : i;
        }

        void LoadValues()
        {
            radAll.Checked = settings.IndexAllDrives; radFolder.Checked = !settings.IndexAllDrives;
            chkRemovable.Checked = settings.IncludeRemovable; chkNetwork.Checked = settings.IncludeNetwork;
            txtRoot.Text = settings.RootPath;
            FillDrives();
            SyncScopeControls();

            cmbPriority.SelectedIndex = IndexOf(PriorityValues, settings.IndexPriority, 1);
            numRefresh.Value = Math.Max(numRefresh.Minimum, Math.Min(numRefresh.Maximum, settings.AutoRefreshHours));
            chkAuto.Checked = settings.AutoIndexOnStartup;
            chkHidden.Checked = settings.ShowHidden;

            numMax.Value = Math.Max(numMax.Minimum, Math.Min(numMax.Maximum, settings.MaxResults));
            numDelay.Value = Math.Max(numDelay.Minimum, Math.Min(numDelay.Maximum, settings.SearchDelayMs));
            cmbGroup.SelectedItem = settings.GroupBy; if (cmbGroup.SelectedIndex < 0) cmbGroup.SelectedIndex = 0;
            cmbDouble.SelectedIndex = settings.DoubleClick == "Reveal" ? 1 : 0;
            cmbDate.SelectedIndex = IndexOf(DateValues, settings.DateFormat, 0);
            chkFoldersFirst.Checked = settings.ShowFoldersFirst; chkColor.Checked = settings.ColorCode;
            chkMatchPath.Checked = settings.MatchPath; chkConfirm.Checked = settings.ConfirmDelete;

            cmbTheme.SelectedIndex = IndexOf(ThemeValues, settings.ThemeMode, 0);
            int sizeIdx = Array.IndexOf(TextSizes, settings.TextSizePercent);
            cmbTextSize.SelectedIndex = sizeIdx < 0 ? 1 : sizeIdx;

            chkStartWin.Checked = settings.StartWithWindows; chkStartMin.Checked = settings.StartMinimized;
            chkTray.Checked = settings.CloseToTray; chkHotkey.Checked = settings.GlobalHotkey;
            cmbHotkey.SelectedIndex = Math.Max(0, Math.Min(cmbHotkey.Items.Count - 1, settings.HotkeyPreset));
            chkClearHotkey.Checked = settings.ClearSearchOnHotkey;

            foreach (string f in settings.ExcludeFolders) lstFolders.Items.Add(f);
            txtExtensions.Text = string.Join(", ", settings.ExcludeExtensions.Select(x => x.TrimStart('.')));
        }

        bool Apply()
        {
            string root = txtRoot.Text.Trim();
            if (radFolder.Checked && !Directory.Exists(root))
            {
                ShowPage(0);
                MessageBox.Show(this, "That folder doesn't exist:\n" + root, "Preferences", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (root.Length == 0 || !Directory.Exists(root)) root = settings.RootPath;

            var folders = lstFolders.Items.Cast<string>().ToList();
            var exts = txtExtensions.Text
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant()).Select(x => x.StartsWith(".") ? x : "." + x).Distinct().ToList();

            var excludedDrives = lvDrives.Items.Cast<ListViewItem>().Where(i => !i.Checked).Select(i => (string)i.Tag).ToList();

            IndexSettingsChanged = IndexSettingsChanged ||
                settings.IndexAllDrives != radAll.Checked ||
                (radAll.Checked && (settings.IncludeRemovable != chkRemovable.Checked || settings.IncludeNetwork != chkNetwork.Checked)) ||
                (radAll.Checked && !excludedDrives.OrderBy(x => x).SequenceEqual(settings.ExcludedDrives.OrderBy(x => x), StringComparer.OrdinalIgnoreCase)) ||
                (radFolder.Checked && !string.Equals(root, settings.RootPath, StringComparison.OrdinalIgnoreCase)) ||
                !folders.OrderBy(x => x).SequenceEqual(settings.ExcludeFolders.OrderBy(x => x), StringComparer.OrdinalIgnoreCase) ||
                !exts.OrderBy(x => x).SequenceEqual(settings.ExcludeExtensions.OrderBy(x => x));

            string newTheme = ThemeValues[Math.Max(0, cmbTheme.SelectedIndex)];
            int newSize = TextSizes[Math.Max(0, cmbTextSize.SelectedIndex)];
            AppearanceChanged = AppearanceChanged || newTheme != settings.ThemeMode || newSize != settings.TextSizePercent;

            settings.IndexAllDrives = radAll.Checked;
            settings.IncludeRemovable = chkRemovable.Checked; settings.IncludeNetwork = chkNetwork.Checked;
            settings.RootPath = root;
            settings.ExcludedDrives = excludedDrives;

            settings.IndexPriority = PriorityValues[Math.Max(0, cmbPriority.SelectedIndex)];
            settings.AutoRefreshHours = (int)numRefresh.Value;
            settings.AutoIndexOnStartup = chkAuto.Checked;
            settings.ShowHidden = chkHidden.Checked;

            settings.MaxResults = (int)numMax.Value;
            settings.SearchDelayMs = (int)numDelay.Value;
            settings.GroupBy = (string)cmbGroup.SelectedItem ?? "None";
            settings.DoubleClick = cmbDouble.SelectedIndex == 1 ? "Reveal" : "Open";
            settings.DateFormat = DateValues[Math.Max(0, cmbDate.SelectedIndex)];
            settings.ShowFoldersFirst = chkFoldersFirst.Checked; settings.ColorCode = chkColor.Checked;
            settings.MatchPath = chkMatchPath.Checked; settings.ConfirmDelete = chkConfirm.Checked;

            settings.ThemeMode = newTheme; settings.TextSizePercent = newSize;

            settings.StartWithWindows = chkStartWin.Checked; settings.StartMinimized = chkStartMin.Checked;
            settings.CloseToTray = chkTray.Checked; settings.GlobalHotkey = chkHotkey.Checked;
            settings.HotkeyPreset = Math.Max(0, cmbHotkey.SelectedIndex);
            settings.ClearSearchOnHotkey = chkClearHotkey.Checked;

            settings.ExcludeFolders = folders;
            settings.ExcludeExtensions = exts;
            return true;
        }
    }
}
