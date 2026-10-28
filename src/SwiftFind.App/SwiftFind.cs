using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace SwiftFind
{
    class Entry
    {
        public string Name, Path;
        public long Size, Ticks;
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
    class MainForm : Form
    {
        TextBox query = new TextBox();
        TextBox folder = new TextBox();
        ListView results = new ListView();
        Label status = new Label();
        Button build = new Button();
        Button choose = new Button();
        Timer debounce = new Timer();
        System.ComponentModel.BackgroundWorker worker =
            new System.ComponentModel.BackgroundWorker();
        List<Entry> files = new List<Entry>();
        string cache, root;
        Color bg = Color.FromArgb(19,24,36);
        Color panel = Color.FromArgb(31,39,55);
        public MainForm()
        {
            Text = "SwiftFind | Desktop Search";
            Width = 1180; Height = 760; MinimumSize = new Size(780,500);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = bg; ForeColor = Color.White;
            Font = new Font("Segoe UI",10);
            string data = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SwiftFind");
            Directory.CreateDirectory(data);
            cache = Path.Combine(data,"index.tsv");
            root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var layout = new TableLayoutPanel {
                Dock=DockStyle.Fill, Padding=new Padding(20),
                ColumnCount=1, RowCount=5, BackColor=bg
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,45));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute,35));
            Controls.Add(layout);
            var title = new Label {
                Text="SwiftFind", Dock=DockStyle.Fill,
                Font=new Font("Segoe UI",25,FontStyle.Bold),
                TextAlign=ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(title,0,0);
            query.Dock=DockStyle.Fill;
            query.Font=new Font("Segoe UI",15);
            query.BackColor=panel; query.ForeColor=Color.White;
            query.BorderStyle=BorderStyle.FixedSingle;
            query.TextChanged += delegate { debounce.Stop(); debounce.Start(); };
            query.KeyDown += delegate(object s, KeyEventArgs e) {
                if(e.KeyCode==Keys.Escape) query.Clear();
                if(e.KeyCode==Keys.Enter && results.Items.Count>0)
                    OpenSelected(false);
            };
            layout.Controls.Add(query,0,1);
            var bar = new TableLayoutPanel {
                Dock=DockStyle.Fill, ColumnCount=3
            };
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));
            bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));
            folder.Dock=DockStyle.Fill; folder.ReadOnly=true;
            folder.Text=root; folder.BackColor=panel; folder.ForeColor=Color.White;
            bar.Controls.Add(folder,0,0);
            choose=MakeButton("Choose folder",Color.FromArgb(65,105,225));
            build=MakeButton("Build / refresh",Color.FromArgb(29,145,103));
            choose.Click += delegate { ChooseFolder(); };
            build.Click += delegate { StartIndex(); };
            bar.Controls.Add(choose,1,0); bar.Controls.Add(build,2,0);
            layout.Controls.Add(bar,0,2);
            results.Dock=DockStyle.Fill;
            results.View=View.Details; results.FullRowSelect=true;
            results.HideSelection=false; results.MultiSelect=false;
            results.BackColor=panel; results.ForeColor=Color.White;
            results.BorderStyle=BorderStyle.None;
            results.Columns.Add("File name",280);
            results.Columns.Add("Location",440);
            results.Columns.Add("Size",100);
            results.Columns.Add("Modified",160);
            results.DoubleClick += delegate { OpenSelected(false); };
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open",null,delegate { OpenSelected(false); });
            menu.Items.Add("Open containing folder",null,delegate { OpenSelected(true); });
            menu.Items.Add("Copy full path",null,delegate {
                if(results.SelectedItems.Count>0)
                    Clipboard.SetText((string)results.SelectedItems[0].Tag);
            });
            results.ContextMenuStrip=menu;
            layout.Controls.Add(results,0,3);
            status.Dock=DockStyle.Fill;
            status.TextAlign=ContentAlignment.MiddleLeft;
            status.ForeColor=Color.Silver;
            layout.Controls.Add(status,0,4);
            debounce.Interval=180;
            debounce.Tick += delegate { debounce.Stop(); Search(); };
            worker.WorkerReportsProgress=true;
            worker.DoWork += delegate(object s, System.ComponentModel.DoWorkEventArgs e) {
                var found = new List<Entry>();
                var stack = new Stack<string>();
                stack.Push((string)e.Argument);
                int count=0;
                while(stack.Count>0) {
                    string dir=stack.Pop();
                    try {
                        foreach(string sub in Directory.GetDirectories(dir)) {
                            try {
                                if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0)
                                    stack.Push(sub);
                            } catch {}
                        }
                    } catch {}
                    try {
                        foreach(string p in Directory.GetFiles(dir)) {
                            try {
                                var f=new FileInfo(p);
                                found.Add(new Entry {
                                    Name=f.Name, Path=f.FullName,
                                    Size=f.Length, Ticks=f.LastWriteTimeUtc.Ticks
                                });
                                count++;
                                if(count%2000==0)
                                    ((System.ComponentModel.BackgroundWorker)s).ReportProgress(count);
                            } catch {}
                        }
                    } catch {}
                }
                e.Result=found;
            };
            worker.ProgressChanged += delegate(object s, System.ComponentModel.ProgressChangedEventArgs e) {
                status.Text="Indexing... "+e.ProgressPercentage.ToString("N0")+" files";
            };
            worker.RunWorkerCompleted += delegate(object s, System.ComponentModel.RunWorkerCompletedEventArgs e) {
                build.Enabled=true; choose.Enabled=true;
                if(e.Error!=null) { status.Text="Index error: "+e.Error.Message; return; }
                files=(List<Entry>)e.Result;
                SaveCache(); Search();
            };
            LoadCache();
            Search();
            status.Text=files.Count.ToString("N0")+" indexed files | Choose Build / refresh to update";
        }
        Button MakeButton(string text, Color color)
        {
            var b=new Button {
                Text=text, Dock=DockStyle.Fill, BackColor=color,
                ForeColor=Color.White, FlatStyle=FlatStyle.Flat,
                Cursor=Cursors.Hand
            };
            b.FlatAppearance.BorderSize=0;
            return b;
        }
        void ChooseFolder()
        {
            using(var d=new FolderBrowserDialog()) {
                d.Description="Choose a folder to index";
                d.SelectedPath=Directory.Exists(root)?root:@"C:\\";
                if(d.ShowDialog(this)==DialogResult.OK) {
                    root=d.SelectedPath; folder.Text=root;
                    files.Clear(); results.Items.Clear();
                    status.Text="Folder selected | Build / refresh to index";
                }
            }
        }
        void StartIndex()
        {
            if(worker.IsBusy) return;
            if(!Directory.Exists(root)) { MessageBox.Show("Choose a valid folder."); return; }
            files.Clear(); results.Items.Clear();
            build.Enabled=false; choose.Enabled=false;
            status.Text="Starting index...";
            worker.RunWorkerAsync(root);
        }
        void SaveCache()
        {
            string temp=cache+".tmp";
            try {
                using(var w=new StreamWriter(temp,false,Encoding.UTF8)) {
                    w.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(root)));
                    foreach(var f in files)
                        w.WriteLine(Convert.ToBase64String(Encoding.UTF8.GetBytes(f.Path))+
                            "\t"+f.Size+"\t"+f.Ticks);
                }
                if(File.Exists(cache)) File.Delete(cache);
                File.Move(temp,cache);
                status.Text="Index saved | "+files.Count.ToString("N0")+" files";
            } catch(Exception ex) {
                status.Text="Cache save failed: "+ex.Message;
            }
        }
        void LoadCache()
        {
            if(!File.Exists(cache)) return;
            try {
                using(var r=new StreamReader(cache,Encoding.UTF8)) {
                    string h=r.ReadLine();
                    if(h==null || Encoding.UTF8.GetString(Convert.FromBase64String(h))!=root) return;
                    string line;
                    while((line=r.ReadLine())!=null) {
                        string[] p=line.Split('\t');
                        long size,ticks;
                        if(p.Length!=3 || !Int64.TryParse(p[1],out size) ||
                            !Int64.TryParse(p[2],out ticks)) continue;
                        string path=Encoding.UTF8.GetString(Convert.FromBase64String(p[0]));
                        files.Add(new Entry { Path=path, Name=System.IO.Path.GetFileName(path),
                            Size=size, Ticks=ticks });
                    }
                }
            } catch { files.Clear(); }
        }
        void Search()
        {
            string q=query.Text.Trim();
            IEnumerable<Entry> matches=files;
            if(q.Length>0) {
                string[] terms=q.Split(new char[]{' '},StringSplitOptions.RemoveEmptyEntries);
                matches=matches.Where(f=>terms.All(t=>
                    f.Name.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0 ||
                    f.Path.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0));
            }
            var items=matches.OrderBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).Take(5000).ToList();
            results.BeginUpdate(); results.Items.Clear();
            foreach(var f in items) {
                var item=new ListViewItem(f.Name);
                item.SubItems.Add(f.Path);
                item.SubItems.Add(FormatSize(f.Size));
                item.SubItems.Add(new DateTime(f.Ticks,DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
                item.Tag=f.Path; results.Items.Add(item);
            }
            results.EndUpdate();
            status.Text="Index: "+files.Count.ToString("N0")+" files | Showing "+
                items.Count.ToString("N0")+" | Type to search instantly";
        }
        string FormatSize(long n)
        {
            string[] u={"B","KB","MB","GB","TB"}; double d=n; int i=0;
            while(d>=1024 && i<u.Length-1) { d/=1024; i++; }
            return d.ToString("0.##")+" "+u[i];
        }
        void OpenSelected(bool containing)
        {
            if(results.SelectedItems.Count==0) return;
            string p=(string)results.SelectedItems[0].Tag;
            try {
                if(!File.Exists(p)) { MessageBox.Show("File no longer exists. Refresh the index."); return; }
                if(containing) Process.Start("explorer.exe","/select,\""+p+"\"");
                else Process.Start(p);
            } catch(Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}
