using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SwiftFind.Models;

namespace SwiftFind.Forms
{
    public class PreferencesForm : Form
    {
        AppSettings settings;
        ListBox lstFolders;
        ListBox lstExts;
        NumericUpDown numMaxResults;
        CheckBox chkAutoIndex;
        CheckBox chkFoldersFirst;
        CheckBox chkColorCode;
        CheckBox chkMatchPath;
        ComboBox cmbGroupBy;

        static readonly Color Bg = Color.FromArgb(22, 26, 36);
        static readonly Color Surface = Color.FromArgb(32, 38, 52);
        static readonly Color Accent = Color.FromArgb(64, 120, 240);

        public PreferencesForm(AppSettings current)
        {
            settings = current;
            Text = "Preferences — SwiftFind";
            Size = new Size(620, 560);
            MinimumSize = new Size(560, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Bg;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 9.5f);

            BuildUI();
            LoadValues();
        }

        void BuildUI()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(8, 8) };
            tabs.Font = new Font("Segoe UI", 9.5f);

            // --- General tab ---
            var tabGeneral = new TabPage("General") { BackColor = Bg, ForeColor = Color.White };
            var pGen = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16), BackColor = Bg };

            chkAutoIndex = MakeCheck("Automatically build index when SwiftFind starts");
            chkFoldersFirst = MakeCheck("Show folders before files in results");
            chkColorCode = MakeCheck("Enable color coding (folders, favorites, large files, recent)");
            chkMatchPath = MakeCheck("Match search terms against full path (not only file name)");

            var lblMax = new Label { Text = "Maximum results to display:", AutoSize = true, Top = 0 };
            numMaxResults = new NumericUpDown
            {
                Minimum = 500, Maximum = 50000, Value = 10000, Width = 100,
                BackColor = Surface, ForeColor = Color.White
            };

            var lblGroup = new Label { Text = "Default grouping:", AutoSize = true };
            cmbGroupBy = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Width = 180,
                BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            cmbGroupBy.Items.AddRange(new object[] { "None", "Extension", "Folder", "Modified", "Size" });

            int y = 12;
            pGen.Controls.Add(chkAutoIndex); chkAutoIndex.Top = y; y += 32;
            pGen.Controls.Add(chkFoldersFirst); chkFoldersFirst.Top = y; y += 32;
            pGen.Controls.Add(chkColorCode); chkColorCode.Top = y; y += 32;
            pGen.Controls.Add(chkMatchPath); chkMatchPath.Top = y; y += 40;
            pGen.Controls.Add(lblMax); lblMax.Top = y; lblMax.Left = 0;
            pGen.Controls.Add(numMaxResults); numMaxResults.Top = y - 2; numMaxResults.Left = 200; y += 40;
            pGen.Controls.Add(lblGroup); lblGroup.Top = y; lblGroup.Left = 0;
            pGen.Controls.Add(cmbGroupBy); cmbGroupBy.Top = y - 2; cmbGroupBy.Left = 200;

            tabGeneral.Controls.Add(pGen);
            tabs.TabPages.Add(tabGeneral);

            // --- Exclusions tab ---
            var tabEx = new TabPage("Exclusions") { BackColor = Bg, ForeColor = Color.White };
            var pEx = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(12),
                ColumnCount = 2, RowCount = 4, BackColor = Bg
            };
            pEx.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            pEx.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            pEx.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            pEx.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            pEx.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            pEx.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            pEx.Controls.Add(new Label { Text = "Excluded folders (not indexed)", Dock = DockStyle.Fill, ForeColor = Color.Silver }, 0, 0);
            pEx.Controls.Add(new Label { Text = "Excluded extensions", Dock = DockStyle.Fill, ForeColor = Color.Silver }, 1, 0);

            lstFolders = new ListBox { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            lstExts = new ListBox { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            pEx.Controls.Add(lstFolders, 0, 1);
            pEx.Controls.Add(lstExts, 1, 1);

            var btnAddFolder = MakeSmallButton("Add folder…");
            var btnRemFolder = MakeSmallButton("Remove");
            var btnAddExt = MakeSmallButton("Add extension");
            var btnRemExt = MakeSmallButton("Remove");

            var panelFolderBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            panelFolderBtns.Controls.Add(btnAddFolder);
            panelFolderBtns.Controls.Add(btnRemFolder);
            var panelExtBtns = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            panelExtBtns.Controls.Add(btnAddExt);
            panelExtBtns.Controls.Add(btnRemExt);

            pEx.Controls.Add(panelFolderBtns, 0, 2);
            pEx.Controls.Add(panelExtBtns, 1, 2);

            // Common excludes helper
            var lblHint = new Label
            {
                Text = "Tip: Common system folders and temp extensions are good candidates for exclusion. Changes apply on next index build.",
                Dock = DockStyle.Fill, ForeColor = Color.FromArgb(140, 150, 170)
            };
            pEx.Controls.Add(lblHint, 0, 3);
            pEx.SetColumnSpan(lblHint, 2);

            btnAddFolder.Click += (s, e) =>
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.Description = "Select a folder to exclude from indexing";
                    if (d.ShowDialog(this) == DialogResult.OK && !lstFolders.Items.Contains(d.SelectedPath))
                        lstFolders.Items.Add(d.SelectedPath);
                }
            };
            btnRemFolder.Click += (s, e) =>
            {
                if (lstFolders.SelectedIndex >= 0) lstFolders.Items.RemoveAt(lstFolders.SelectedIndex);
            };
            btnAddExt.Click += (s, e) =>
            {
                string input = Microsoft.VisualBasic.Interaction.InputBox(
                    "Enter extension (e.g. tmp or .log)", "Add excluded extension", ".tmp");
                if (string.IsNullOrWhiteSpace(input)) return;
                string ext = input.Trim().ToLowerInvariant();
                if (!ext.StartsWith(".")) ext = "." + ext;
                if (!lstExts.Items.Contains(ext)) lstExts.Items.Add(ext);
            };
            btnRemExt.Click += (s, e) =>
            {
                if (lstExts.SelectedIndex >= 0) lstExts.Items.RemoveAt(lstExts.SelectedIndex);
            };

            tabEx.Controls.Add(pEx);
            tabs.TabPages.Add(tabEx);

            // Buttons
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Bg, Padding = new Padding(12) };
            var btnOk = new Button
            {
                Text = "Save", Width = 100, Height = 32, DialogResult = DialogResult.OK,
                BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Location = new Point(bottom.Width - 220, 10);
            var btnCancel = new Button
            {
                Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel,
                BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Right
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Location = new Point(bottom.Width - 110, 10);

            bottom.Controls.Add(btnOk);
            bottom.Controls.Add(btnCancel);
            bottom.Resize += (s, e) =>
            {
                btnOk.Left = bottom.ClientSize.Width - 220;
                btnCancel.Left = bottom.ClientSize.Width - 110;
            };

            Controls.Add(tabs);
            Controls.Add(bottom);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            btnOk.Click += (s, e) => ApplyAndClose();
        }

        CheckBox MakeCheck(string text)
        {
            return new CheckBox
            {
                Text = text, AutoSize = true, ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat, Left = 0
            };
        }

        Button MakeSmallButton(string text)
        {
            var b = new Button
            {
                Text = text, Width = 110, Height = 28,
                BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        void LoadValues()
        {
            chkAutoIndex.Checked = settings.AutoIndexOnStartup;
            chkFoldersFirst.Checked = settings.ShowFoldersFirst;
            chkColorCode.Checked = settings.ColorCode;
            chkMatchPath.Checked = settings.MatchPath;
            numMaxResults.Value = Math.Max(500, Math.Min(50000, settings.MaxResults));
            int idx = cmbGroupBy.Items.IndexOf(settings.GroupBy);
            cmbGroupBy.SelectedIndex = idx >= 0 ? idx : 0;

            lstFolders.Items.Clear();
            foreach (string f in settings.ExcludeFolders) lstFolders.Items.Add(f);

            lstExts.Items.Clear();
            foreach (string e in settings.ExcludeExtensions) lstExts.Items.Add(e);

            // Sensible defaults if empty
            if (lstExts.Items.Count == 0)
            {
                foreach (string d in new[] { ".tmp", ".temp", ".log", ".bak", ".old", ".cache" })
                    lstExts.Items.Add(d);
            }
        }

        void ApplyAndClose()
        {
            settings.AutoIndexOnStartup = chkAutoIndex.Checked;
            settings.ShowFoldersFirst = chkFoldersFirst.Checked;
            settings.ColorCode = chkColorCode.Checked;
            settings.MatchPath = chkMatchPath.Checked;
            settings.MaxResults = (int)numMaxResults.Value;
            settings.GroupBy = cmbGroupBy.SelectedItem as string ?? "None";

            settings.ExcludeFolders = lstFolders.Items.Cast<string>().ToList();
            settings.ExcludeExtensions = lstExts.Items.Cast<string>().ToList();

            settings.Save();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
