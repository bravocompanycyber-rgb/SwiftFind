
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$app = Join-Path $root "src\SwiftFind.App"
$sourceFile = Join-Path $app "SwiftFind.cs"
$exe = Join-Path $root "SwiftFind.exe"

New-Item -ItemType Directory -Force -Path $app | Out-Null

$source = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.ComponentModel;

namespace SwiftFind
{
    class FileEntry
    {
        public string Name, Path, Folder;
        public long Size;
        public DateTime Modified;
    }

    class MainForm : Form
    {
        TextBox query = new TextBox();
        TextBox folder = new TextBox();
        ListView list = new ListView();
        Label status = new Label();
        Button scan = new Button();
        BackgroundWorker worker = new BackgroundWorker();
        List<FileEntry> files = new List<FileEntry>();
        Timer timer = new Timer();
        Color bg = Color.FromArgb(23, 29, 41);
        Color surface = Color.FromArgb(34, 42, 57);
        Color blue = Color.FromArgb(73, 119, 232);
        int scanned;

        public MainForm()
        {
            Text = "SwiftFind | Desktop Search";
            Width = 1120; Height = 720;
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = bg; ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            KeyPreview = true;

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(20),
                ColumnCount = 1, RowCount = 5, BackColor = bg
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            Controls.Add(root);

            var header = new Panel { Dock = DockStyle.Fill };
            var mark = new Label {
                Text = "S", Bounds = new Rectangle(0, 5, 44, 44),
                BackColor = blue, ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 23, FontStyle.Bold)
            };
            var title = new Label {
                Text = "SwiftFind", Location = new Point(56, 0),
                AutoSize = true, Font = new Font("Segoe UI", 23, FontStyle.Bold)
            };
            var sub = new Label {
                Text = "FAST DESKTOP FILE SEARCH",
                Location = new Point(59, 39), AutoSize = true,
                ForeColor = Color.Silver, Font = new Font("Segoe UI", 8)
            };
            header.Controls.Add(mark); header.Controls.Add(title);
            header.Controls.Add(sub); root.Controls.Add(header, 0, 0);

            query.Dock = DockStyle.Fill;
            query.Font = new Font("Segoe UI", 14);
            query.BackColor = surface; query.ForeColor = Color.White;
            query.BorderStyle = BorderStyle.FixedSingle;
            query.Enabled = false;
            root.Controls.Add(query, 0, 1);

            var bar = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, BackColor = bg
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
            folder.Dock = DockStyle.Fill; folder.ReadOnly = true;
            folder.BackColor = surface; folder.ForeColor = Color.White;
            folder.Text = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            var choose = MakeButton("Choose folder", blue);
            scan = MakeButton("Build index", Color.FromArgb(36, 151, 105));
            choose.Click += delegate { ChooseFolder(); };
            scan.Click += delegate { StartScan(); };
            bar.Controls.Add(folder, 0, 0);
            bar.Controls.Add(choose, 1, 0);
            bar.Controls.Add(scan, 2, 0);
            root.Controls.Add(bar, 0, 2);

            list.Dock = DockStyle.Fill;
            list.View = View.Details; list.FullRowSelect = true;
            list.GridLines = false; list.MultiSelect = false;
            list.BackColor = surface; list.ForeColor = Color.White;
            list.BorderStyle = BorderStyle.None;
            list.Columns.Add("File name", 280);
            list.Columns.Add("Location", 420);
            list.Columns.Add("Size", 100);
            list.Columns.Add("Modified", 150);
            list.DoubleClick += delegate { OpenSelected(false); };
            root.Controls.Add(list, 0, 3);

            status.Dock = DockStyle.Fill;
            status.TextAlign = ContentAlignment.MiddleLeft;
            status.ForeColor = Color.Silver;
            status.Text = "Ready | Choose a folder to start indexing";
            root.Controls.Add(status, 0, 4);

            worker.WorkerReportsProgress = true;
            worker.DoWork += delegate(object s, DoWorkEventArgs e) {
                var found = new List<FileEntry>();
                var stack = new Stack<string>();
                stack.Push((string)e.Argument);
                int count = 0;
                while (stack.Count > 0) {
                    string dir = stack.Pop();
                    try {
                        foreach (string path in Directory.GetFiles(dir)) {
                            try {
                                var f = new FileInfo(path);
                                found.Add(new FileEntry {
                                    Name = f.Name, Path = f