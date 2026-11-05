using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SwiftFind.Controls;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Forms
{
    public class PreferencesForm : Form
    {
        readonly AppSettings settings;
        readonly Panel general = new Panel { Dock = DockStyle.Fill };
        readonly Panel exclusions = new Panel { Dock = DockStyle.Fill, Visible = false };
        FlatButton tabGeneral, tabExclusions;

        TextBox txtRoot, txtExtensions;
        RadioButton radAll, radFolder;
        CheckBox chkRemovable;
        NumericUpDown numMax;
        CheckBox chkAuto, chkFoldersFirst, chkColor, chkMatchPath, chkHotkey, chkTray;
        ComboBox cmbGroup;
        ListBox lstFolders;

        public bool IndexSettingsChanged { get; private set; }

        public PreferencesForm(AppSettings s)
        {
            settings = s;
            Theme.StyleForm(this);
            Text = "Preferences";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(600, 660);

            BuildTabs();
            BuildGeneral();
            BuildExclusions();
            BuildFooter();
            LoadValues();
        }

        void BuildTabs()
        {
            var bar = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(20, 12, 20, 8) };
            tabGeneral = new FlatButton("General", FlatButton.Style.Primary) { Left = 20, Top = 12, Width = 120, Height = 34 };
            tabExclusions = new FlatButton("Exclusions") { Left = 148, Top = 12, Width = 120, Height = 34 };
            tabGeneral.Click += (s, e) => ShowPage(true);
            tabExclusions.Click += (s, e) => ShowPage(false);
            bar.Controls.AddRange(new Control[] { tabGeneral, tabExclusions });

            var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20, 4, 20, 0) };
            host.Controls.Add(exclusions);
            host.Controls.Add(general);
            Controls.Add(host);
            Controls.Add(bar);
        }

        void ShowPage(bool showGeneral)
        {
            general.Visible = showGeneral;
            exclusions.Visible = !showGeneral;
            tabGeneral.SetStyle(showGeneral ? FlatButton.Style.Primary : FlatButton.Style.Ghost);
            tabExclusions.SetStyle(showGeneral ? FlatButton.Style.Ghost : FlatButton.Style.Primary);
        }

        Label Caption(string text, int top)
        {
            return new Label { Text = text, Left = 0, Top = top, Width = 540, Height = 20, ForeColor = Theme.TextMuted };
        }

        CheckBox Check(string text, int top)
        {
            return new CheckBox { Text = text, Left = 0, Top = top, Width = 540, Height = 24, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
        }

        TextBox Input(int left, int top, int width)
        {
            return new TextBox { Left = left, Top = top, Width = width, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Ui(10f) };
        }

        void BuildGeneral()
        {
            general.Controls.Add(Caption("What to search", 4));
            radAll = new RadioButton { Text = "Everywhere \u2014 index and search all drives together", Left = 0, Top = 28, Width = 540, Height = 24, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            chkRemovable = new CheckBox { Text = "Include removable drives (USB sticks, SD cards)", Left = 24, Top = 54, Width = 516, Height = 24, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            radFolder = new RadioButton { Text = "Only this folder:", Left = 0, Top = 86, Width = 540, Height = 24, ForeColor = Theme.Text, FlatStyle = FlatStyle.Flat };
            txtRoot = Input(24, 114, 406);
            var browse = new FlatButton("Browse\u2026") { Left = 440, Top = 111, Width = 100, Height = 30 };
            browse.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Choose the folder to index", SelectedPath = txtRoot.Text })
                    if (d.ShowDialog(this) == DialogResult.OK) { txtRoot.Text = d.SelectedPath; radFolder.Checked = true; }
            };
            radAll.CheckedChanged += (s, e) => SyncScopeControls();
            radFolder.CheckedChanged += (s, e) => SyncScopeControls();
            general.Controls.AddRange(new Control[] { radAll, chkRemovable, radFolder, txtRoot, browse });

            general.Controls.Add(Caption("Maximum results shown (more = slower list)", 162));
            numMax = new NumericUpDown { Left = 0, Top = 186, Width = 120, Minimum = AppSettings.MinResults, Maximum = AppSettings.MaxResultsLimit, Increment = 500, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            general.Controls.Add(numMax);

            general.Controls.Add(Caption("Group results by", 224));
            cmbGroup = new ComboBox { Left = 0, Top = 248, Width = 200 };
            Theme.StyleCombo(cmbGroup);
            cmbGroup.Items.AddRange(new object[] { "None", "Extension", "Folder", "Modified", "Size" });
            general.Controls.Add(cmbGroup);

            chkAuto = Check("Rebuild the index every time SwiftFind starts", 292);
            chkFoldersFirst = Check("List folders before files", 322);
            chkColor = Check("Colour-code folders, favourites, recent and large files", 352);
            chkMatchPath = Check("Match search words against the full path, not just the name", 382);
            chkHotkey = Check("Global hotkey Ctrl+Alt+Space brings SwiftFind to the front", 412);
            chkTray = Check("Closing the window keeps SwiftFind running in the tray", 442);
            general.Controls.AddRange(new Control[] { chkAuto, chkFoldersFirst, chkColor, chkMatchPath, chkHotkey, chkTray });

            general.Controls.Add(new Label
            {
                Text = "Tip: run SwiftFind as administrator for near-instant indexing of NTFS drives (File menu \u2192 Restart as administrator).",
                Left = 0, Top = 480, Width = 540, Height = 40, ForeColor = Theme.TextDim
            });
        }

        void SyncScopeControls()
        {
            chkRemovable.Enabled = radAll.Checked;
            txtRoot.Enabled = radFolder.Checked;
        }

        void BuildExclusions()
        {
            exclusions.Controls.Add(Caption("Folders that are never indexed", 8));
            lstFolders = new ListBox { Left = 0, Top = 32, Width = 430, Height = 190, BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            var add = new FlatButton("Add\u2026") { Left = 440, Top = 32, Width = 100, Height = 32 };
            var remove = new FlatButton("Remove") { Left = 440, Top = 70, Width = 100, Height = 32 };
            add.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog { Description = "Choose a folder to exclude" })
                    if (d.ShowDialog(this) == DialogResult.OK && !lstFolders.Items.Cast<string>().Contains(d.SelectedPath, StringComparer.OrdinalIgnoreCase))
                        lstFolders.Items.Add(d.SelectedPath);
            };
            remove.Click += (s, e) => { if (lstFolders.SelectedIndex >= 0) lstFolders.Items.RemoveAt(lstFolders.SelectedIndex); };
            exclusions.Controls.AddRange(new Control[] { lstFolders, add, remove });

            exclusions.Controls.Add(Caption("File extensions that are never indexed (comma separated)", 240));
            txtExtensions = Input(0, 264, 540);
            exclusions.Controls.Add(txtExtensions);
            exclusions.Controls.Add(new Label
            {
                Text = "Example:  tmp, log, bak     Changes apply the next time you rebuild the index.",
                Left = 0, Top = 296, Width = 540, Height = 22, ForeColor = Theme.TextDim
            });
        }

        void BuildFooter()
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 64 };
            var save = new FlatButton("Save", FlatButton.Style.Primary) { Left = 380, Top = 14, Width = 100, Height = 36 };
            var cancel = new FlatButton("Cancel") { Left = 488, Top = 14, Width = 92, Height = 36, DialogResult = DialogResult.Cancel };
            save.Click += (s, e) => { if (Apply()) DialogResult = DialogResult.OK; };
            footer.Controls.AddRange(new Control[] { save, cancel });
            Controls.Add(footer);
            AcceptButton = save;
            CancelButton = cancel;
        }

        void LoadValues()
        {
            txtRoot.Text = settings.RootPath;
            radAll.Checked = settings.IndexAllDrives;
            radFolder.Checked = !settings.IndexAllDrives;
            chkRemovable.Checked = settings.IncludeRemovable;
            SyncScopeControls();
            numMax.Value = Math.Max(numMax.Minimum, Math.Min(numMax.Maximum, settings.MaxResults));
            cmbGroup.SelectedItem = settings.GroupBy;
            if (cmbGroup.SelectedIndex < 0) cmbGroup.SelectedIndex = 0;
            chkAuto.Checked = settings.AutoIndexOnStartup;
            chkFoldersFirst.Checked = settings.ShowFoldersFirst;
            chkColor.Checked = settings.ColorCode;
            chkMatchPath.Checked = settings.MatchPath;
            chkHotkey.Checked = settings.GlobalHotkey;
            chkTray.Checked = settings.CloseToTray;
            foreach (string f in settings.ExcludeFolders) lstFolders.Items.Add(f);
            txtExtensions.Text = string.Join(", ", settings.ExcludeExtensions.Select(x => x.TrimStart('.')));
        }

        bool Apply()
        {
            string root = txtRoot.Text.Trim();
            if (radFolder.Checked && !System.IO.Directory.Exists(root))
            {
                ShowPage(true);
                MessageBox.Show(this, "That folder doesn't exist:\n" + root, "Preferences", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (root.Length == 0 || !System.IO.Directory.Exists(root)) root = settings.RootPath; // keep the previous folder when searching everywhere

            var folders = lstFolders.Items.Cast<string>().ToList();
            var exts = txtExtensions.Text
                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToLowerInvariant())
                .Select(x => x.StartsWith(".") ? x : "." + x)
                .Distinct().ToList();

            IndexSettingsChanged =
                settings.IndexAllDrives != radAll.Checked ||
                (radAll.Checked && settings.IncludeRemovable != chkRemovable.Checked) ||
                (radFolder.Checked && !string.Equals(root, settings.RootPath, StringComparison.OrdinalIgnoreCase)) ||
                !folders.OrderBy(x => x).SequenceEqual(settings.ExcludeFolders.OrderBy(x => x), StringComparer.OrdinalIgnoreCase) ||
                !exts.OrderBy(x => x).SequenceEqual(settings.ExcludeExtensions.OrderBy(x => x));

            settings.RootPath = root;
            settings.IndexAllDrives = radAll.Checked;
            settings.IncludeRemovable = chkRemovable.Checked;
            settings.MaxResults = (int)numMax.Value;
            settings.GroupBy = (string)cmbGroup.SelectedItem ?? "None";
            settings.AutoIndexOnStartup = chkAuto.Checked;
            settings.ShowFoldersFirst = chkFoldersFirst.Checked;
            settings.ColorCode = chkColor.Checked;
            settings.MatchPath = chkMatchPath.Checked;
            settings.GlobalHotkey = chkHotkey.Checked;
            settings.CloseToTray = chkTray.Checked;
            settings.ExcludeFolders = folders;
            settings.ExcludeExtensions = exts;
            return true;
        }
    }
}
