using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SwiftFind.Core;

namespace SwiftFind.Controls
{
    /// <summary>Large rounded search field with a magnifier glyph and a focus ring.</summary>
    public class SearchBox : Panel
    {
        public readonly TextBox Input;
        bool focused;

        public SearchBox()
        {
            DoubleBuffered = true;
            Height = 52;
            BackColor = Theme.Bg;
            Padding = new Padding(52, 14, 18, 8);

            Input = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Surface,
                ForeColor = Theme.Text,
                Font = Theme.Ui(13f),
                Dock = DockStyle.Fill
            };
            Input.GotFocus += (s, e) => { focused = true; Invalidate(); };
            Input.LostFocus += (s, e) => { focused = false; Invalidate(); };
            Controls.Add(Input);
            Native.SetCueBanner(Input, "Search files and folders   \u2022   try  *.pdf   ext:jpg   size:>10mb   dm:today");
            Click += (s, e) => Input.Focus();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(1, 1, Width - 3, Height - 3);
            using (GraphicsPath path = Theme.RoundedRect(r, 14))
            {
                using (var b = new SolidBrush(Theme.Surface)) g.FillPath(b, path);
                using (var p = new Pen(focused ? Theme.Accent : Theme.Border, focused ? 2f : 1f)) g.DrawPath(p, path);
            }

            // Magnifier glyph
            using (var p = new Pen(focused ? Theme.AccentHover : Theme.TextMuted, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                int cx = 30, cy = Height / 2 - 2;
                g.DrawEllipse(p, cx - 7, cy - 7, 13, 13);
                g.DrawLine(p, cx + 4, cy + 4, cx + 10, cy + 10);
            }
        }
    }
}
