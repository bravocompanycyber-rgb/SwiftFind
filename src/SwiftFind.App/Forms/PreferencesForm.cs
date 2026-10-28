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
            Size = new Size(640, 580);
            MinimumSize = new Size(580, 500);
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

            // General
            var tabGeneral = new TabPage("General") { BackColor = Bg, ForeColor = Color.White };
            var pGen = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18), BackColor = Bg };

            chkAutoIndex = MakeCheck("Automatically build index when SwiftFind starts");
            chkFoldersFirst = MakeCheck("Show folders before files in results");
            chkColorCode = MakeCheck("Enable color coding (folders, favorites, large files, recent)");
            chkMatchPath = MakeCheck("Match search terms against full path (not only file name)");

            var lblMax = new Label { Text = "Maximum results to display:", AutoSize = true };
            numMaxResults = new NumericUpDown
            {
                Minimum = 500, Maximum = 50000, Value = 10000, Width = 110,
                BackColor = Surface, ForeColor = Color.White
            };

            var lblGroup = new Label { Text = "Default grouping:", AutoSize = true };
            cmbGroupBy = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Width = 180,
                BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
            };
            cmbGroupBy.Items.AddRange(new object[] { "None", "Extension", "Folder", "Modified", "Size" });

            int y = 14;
            foreach (var c in new Control[] { chkAutoIndex, chkFoldersFirst, chkColorCode, chkMatchPath })
            {
                c.Top = y; c.Left = 0; pGen.Controls.Add(c); y += 34;
            }
            y += 8;
            lblMax.Top = y; lblMax.Left = 0; pGen.Controls.Add(lblMax);
            numMaxResults.Top = y - 2; numMaxResults.Left = 210; pGen.Controls.Add(numMaxResults);
            y += 42;
            lblGroup.Top = y; lblGroup.Left = 0; pGen.Controls.Add(lblGroup);
            cmbGroupBy.Top = y - 2; cmbGroupBy.Left = 210; pGen.Controls.Add(cmbGroupBy);

            tabGeneral.Controls.Add(pGen);
            tabs.TabPages.Add(tabGeneral);

            // Exclusions
            var tabEx = new TabPage("Exclusions") { BackColor = Bg, ForeColor = Color.White };
            var pEx = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(14),
                ColumnCount = 2, RowCount = 4, BackColor = Bg
            };
            pEx.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            pEx.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            pEx.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            pEx.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            pEx.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            pEx.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

            pEx.Controls.Add(new Label { Text = "Excluded folders (skipped during index)", Dock = DockStyle.Fill, ForeColor = Color.Silver }, 0, 0);
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

            var lblHint = new Label
            {
                Text = "Tip: Exclude Windows, Program Files, node_modules, .git, temp folders and extensions like .tmp .log .cache for faster, cleaner indexes. Changes apply on the next Build index.",
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
            btnRemFolder.Click += (s, e) => { if (lstFolders.SelectedIndex >= 0) lstFolders.Items.RemoveAt(lstFolders.SelectedIndex); };
            btnAddExt.Click += (s, e) =>
            {
                string input = SimpleInput("Add excluded extension", "Extension (e.g. tmp or .log):", ".tmp");
                if (string.IsNullOrWhiteSpace(input)) return;
                string ext = input.Trim().ToLowerInvariant();
                if (!ext.StartsWith(".")) ext = "." + ext;
                if (!lstExts.Items.Contains(ext)) lstExts.Items.Add(ext);
            };
            btnRemExt.Click += (s, e) => { if (lstExts.SelectedIndex >= 0) lstExts.Items.RemoveAt(lstExts.SelectedIndex); };

            tabEx.Controls.Add(pEx);
            tabs.TabPages.Add(tabEx);

            // Bottom buttons
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Bg, Padding = new Padding(14) };
            var btnOk = new Button
            {
                Text = "Save", Width = 100, Height = 32, DialogResult = DialogResult.OK,
                BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnOk.FlatAppearance.BorderSize = 0;
            var btnCancel = new Button
            {
                Text = "Cancel", Width = 90, Height = 32, DialogResult = DialogResult.Cancel,
                BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            bottom.Controls.Add(btnOk);
            bottom.Controls.Add(btnCancel);
            bottom.Resize += (s, e) =>
            {
                btnOk.Left = bottom.ClientSize.Width - 210;
                btnCancel.Left = bottom.ClientSize.Width - 100;
                btnOk.Top = btnCancel.Top = 10;
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
                FlatStyle = FlatStyle.Flat
            };
        }

        Button MakeSmallButton(string text)
        {
            var b = new Button
            {
                Text = text, Width = 114, Height = 28,
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

        static string SimpleInput(string title, string prompt, string defaultValue)
        {
            using (var form = new Form())
            {
                form.Text = title;
                form.Size = new Size(400, 150);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.BackColor = Bg;
                form.ForeColor = Color.White;

                var lbl = new Label { Text = prompt, Left = 14, Top = 14, AutoSize = true };
                var tb = new TextBox
                {
                    Left = 14, Top = 40, Width = 350, Text = defaultValue,
                    BackColor = Surface, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle
                };
                var ok = new Button
                {
                    Text = "OK", Left = 200, Top = 78, Width = 80,
                    DialogResult = DialogResult.OK, BackColor = Accent, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
                };
                ok.FlatAppearance.BorderSize = 0;
                var cancel = new Button
                {
                    Text = "Cancel", Left = 290, Top = 78, Width = 80,
                    DialogResult = DialogResult.Cancel, BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat
                };
                cancel.FlatAppearance.BorderSize = 0;

                form.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
                form.AcceptButton = ok;
                form.CancelButton = cancel;
                return form.ShowDialog() == DialogResult.OK ? tb.Text.Trim() : null;
            }
        }
    }
}
