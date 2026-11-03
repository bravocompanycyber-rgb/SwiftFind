using System;
using System.Drawing;
using System.Windows.Forms;
using SwiftFind.Controls;
using SwiftFind.Core;
using SwiftFind.Helpers;

namespace SwiftFind.Forms
{
    /// <summary>Small themed dialogs shared by the app.</summary>
    public static class Dialogs
    {
        /// <summary>Single-line text prompt. Returns null if cancelled.</summary>
        public static string Prompt(IWin32Window owner, string title, string label, string initial, Func<string, string> validate = null)
        {
            using (var f = new Form())
            {
                Theme.StyleForm(f);
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.ClientSize = new Size(480, 150);

                var lbl = new Label { Text = label, Left = 20, Top = 18, Width = 440, ForeColor = Theme.TextMuted, AutoSize = false, Height = 20 };
                var box = new TextBox { Left = 20, Top = 44, Width = 440, Text = initial ?? "", BackColor = Theme.Surface, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Ui(10.5f) };
                var err = new Label { Left = 20, Top = 74, Width = 440, Height = 20, ForeColor = Color.FromArgb(255, 130, 130) };
                var ok = new FlatButton("OK", FlatButton.Style.Primary) { Left = 270, Top = 100, Width = 90 };
                var cancel = new FlatButton("Cancel") { Left = 370, Top = 100, Width = 90, DialogResult = DialogResult.Cancel };
                ok.Click += (s, e) =>
                {
                    string problem = validate != null ? validate(box.Text) : null;
                    if (problem != null) { err.Text = problem; return; }
                    f.DialogResult = DialogResult.OK;
                };
                f.AcceptButton = ok; f.CancelButton = cancel;
                f.Controls.AddRange(new Control[] { lbl, box, err, ok, cancel });
                f.Shown += (s, e) =>
                {
                    box.Focus();
                    // Select the name without its extension, like Explorer does.
                    int dot = box.Text.LastIndexOf('.');
                    if (dot > 0) box.Select(0, dot); else box.SelectAll();
                };
                return f.ShowDialog(owner) == DialogResult.OK ? box.Text : null;
            }
        }

        public static void About(IWin32Window owner, string details)
        {
            using (var f = new Form())
            {
                Theme.StyleForm(f);
                f.Text = "About SwiftFind";
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.StartPosition = FormStartPosition.CenterParent;
                f.MinimizeBox = f.MaximizeBox = false;
                f.ShowInTaskbar = false;
                f.ClientSize = new Size(460, 330);

                Image logo = AppIcon.LoadLogo();
                var pic = new PictureBox { Left = 20, Top = 20, Width = 96, Height = 96, SizeMode = PictureBoxSizeMode.Zoom, Image = logo };
                var title = new Label { Text = "SwiftFind", Left = 130, Top = 26, Width = 310, Height = 40, Font = Theme.Ui(22f, FontStyle.Bold), ForeColor = Theme.Text };
                var sub = new Label { UseMnemonic = false, Text = "Fast desktop search & indexing", Left = 132, Top = 70, Width = 310, Height = 22, ForeColor = Theme.Orange, Font = Theme.Ui(10.5f, FontStyle.Bold) };
                var ver = new Label { Text = "Version " + AppVersion(), Left = 132, Top = 94, Width = 310, Height = 20, ForeColor = Theme.TextMuted };
                var info = new TextBox
                {
                    Left = 20, Top = 132, Width = 420, Height = 130, Multiline = true, ReadOnly = true,
                    BackColor = Theme.Surface, ForeColor = Theme.TextMuted, BorderStyle = BorderStyle.None,
                    Text = details, ScrollBars = ScrollBars.Vertical, Font = Theme.Ui(9f)
                };
                var ok = new FlatButton("Close", FlatButton.Style.Primary) { Left = 350, Top = 280, Width = 90, DialogResult = DialogResult.OK };
                f.AcceptButton = ok;
                f.Controls.AddRange(new Control[] { pic, title, sub, ver, info, ok });
                f.ShowDialog(owner);
                if (logo != null) logo.Dispose();
            }
        }

        public static string AppVersion()
        {
            var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return v.Major + "." + v.Minor + "." + v.Build;
        }
    }
}
