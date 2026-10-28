using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace SwiftFind
{
    class Entry
    {
        public string Name, Path, Folder;
        public long Size;
        public DateTime Modified;
    }
    class MainForm : Form
    {
        TextBox query = new TextBox();
        TextBox folder = new TextBox();
        ListView results = new ListView();
        Label status = new Label();
        Button index = new Button();
        List<Entry> files = new List<Entry>();
        Timer timer = new Timer();
        bool indexing;
        public MainForm()
        {
            Text = "SwiftFind | Desktop Search";
            Size = new Size(1100, 700);
            MinimumSize = new Size(760, 480);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(22, 28, 40);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10);
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(18),
                ColumnCount = 1, RowCount = 5, BackColor = BackColor
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            Controls.Add(layout);
            var title = new Label {
                Text = "SwiftFind",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                Dock = DockStyle.Fill,
                ForeColor = Color.White
            };
            layout.Controls.Add(title, 0, 0);
            query.Dock = DockStyle.Fill;
            query.Font = new Font("Segoe UI", 14);
            query.Enabled = false;
            query.BackColor = Color.FromArgb(35, 43, 59);
            query.ForeColor = Color.White;
            layout.Controls.Add(query, 0, 1);
            var bar = new FlowLayoutPanel {
                Dock = DockStyle.Fill, WrapContents = false,
                BackColor = BackColor
            };
            folder.Width = 600;
            folder.ReadOnly = true;
            folder.Text = Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile);
            var choose = new Button { Text = "Choose folder", Width = 120 };
            index.Text = "Index files";
            index.Width = 110;
            choose.Click += delegate { ChooseFolder(); };
            index.Click += delegate { StartIndex(); };
            bar.Controls.Add(folder);
            bar.Controls.Add(choose);
            bar.Controls.Add(index);
            layout.Controls.Add(bar, 0, 2);
            results.Dock = DockStyle.Fill;
            results.View = View.Details;
            results.FullRowSelect = true;
            results.MultiSelect = false;
            results.BackColor = Color.FromArgb(35, 43, 59);
            results.ForeColor = Color.White;
            results.Columns.Add("File name", 280);
            results.Columns.Add("Folder", 420);
            results.Columns.Add("Size", 100);
            results.Columns.Add("Modified", 150);
            results.DoubleClick += delegate { OpenSelected(); };
            layout.Controls.Add(results, 0, 3);
            status.Dock = DockStyle.Fill;
            status.Text = "Choose a folder to begin indexing.";
            status.ForeColor = Color.Silver;
            layout.Controls.Add(status, 0, 4);
            timer.Interval = 180;
            timer.Tick += delegate { timer.Stop(); Filter(); };
            query.TextChanged += delegate {
                timer.Stop();
                timer.Start();
            };
            Shown += delegate { ChooseFolder(); };
        }
        void ChooseFolder()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Choose a folder or drive to index";
                if (Directory.Exists(folder.Text))
                    d.SelectedPath = folder.Text;
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    folder.Text = d.SelectedPath;
                    StartIndex();
                }
            }
        }
        async void StartIndex()
        {
            if (indexing || !Directory.Exists(folder.Text)) return;
            indexing = true;
            index.Enabled = false;
            query.Enabled = false;
            files.Clear();
            results.Items.Clear();
            status.Text = "Indexing...";
            string root = folder.Text;
            var found = new List<Entry>();
            await Task.Run(delegate {
                var stack = new Stack<string>();
                stack.Push(root);
                while (stack.Count > 0)
                {
                    string dir = stack.Pop();
                    try
                    {
                        foreach (string path in Directory.GetFiles(dir))
                        {
                            try
                            {
                                var f = new FileInfo(path);
                                found.Add(new Entry {
                                    Name = f.Name, Path = f.FullName,
                                    Folder = f.DirectoryName,
                                    Size = f.Length, Modified = f.LastWriteTime
                                });
                            }
                            catch { }
                        }
                        foreach (string d in Directory.GetDirectories(dir))
                        {
                            try
                            {
                                if ((File.GetAttributes(d) &
                                    FileAttributes.ReparsePoint) == 0)
                                    stack.Push(d);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            });
            files = found;
            indexing = false;
            index.Enabled = true;
            query.Enabled = true;
            Filter();
            status.Text = files.Count.ToString("N0") +
                " files indexed | " + root;
            query.Focus();
        }
        void Filter()
        {
            string[] terms = query.Text.Trim().Split(
                new char[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);
            results.BeginUpdate();
            results.Items.Clear();
            int count = 0;
            foreach (Entry f in files)
            {
                if (!terms.All(t =>
                    f.Name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    f.Path.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                    continue;
                var item = new ListViewItem(f.Name);
                item.SubItems.Add(f.Folder);
                item.SubItems.Add(FormatSize(f.Size));
                item.SubItems.Add(f.Modified.ToString("yyyy-MM-dd HH:mm"));
                item.Tag = f;
                results.Items.Add(item);
                if (++count >= 5000) break;
            }
            results.EndUpdate();
            status.Text = count.ToString("N0") + " results | " +
                files.Count.ToString("N0") + " indexed";
        }
        string FormatSize(long n)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double v = n;
            int i = 0;
            while (v >= 1024 && i < 4) { v /= 1024; i++; }
            return v.ToString("0.##") + " " + units[i];
        }
        void OpenSelected()
        {
            if (results.SelectedItems.Count == 0) return;
            var f = results.SelectedItems[0].Tag as Entry;
            if (f == null) return;
            try { Process.Start(f.Path); }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
