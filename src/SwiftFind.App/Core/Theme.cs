using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SwiftFind.Core
{
    /// <summary>Design tokens and small styling helpers shared by every window.</summary>
    public static class Theme
    {
        // Surfaces
        public static readonly Color Bg = Color.FromArgb(14, 17, 27);
        public static readonly Color Panel = Color.FromArgb(21, 25, 38);
        public static readonly Color Surface = Color.FromArgb(27, 32, 48);
        public static readonly Color SurfaceHi = Color.FromArgb(37, 44, 64);
        public static readonly Color Border = Color.FromArgb(46, 54, 76);

        // Brand
        public static readonly Color Accent = Color.FromArgb(37, 118, 208);
        public static readonly Color AccentHover = Color.FromArgb(62, 140, 230);
        public static readonly Color AccentPressed = Color.FromArgb(28, 98, 178);
        public static readonly Color Orange = Color.FromArgb(242, 138, 36);
        public static readonly Color OrangeHover = Color.FromArgb(255, 158, 62);
        public static readonly Color Success = Color.FromArgb(34, 160, 107);
        public static readonly Color SuccessHover = Color.FromArgb(46, 184, 125);
        public static readonly Color Danger = Color.FromArgb(220, 76, 76);

        // Text
        public static readonly Color Text = Color.FromArgb(232, 235, 245);
        public static readonly Color TextMuted = Color.FromArgb(150, 158, 180);
        public static readonly Color TextDim = Color.FromArgb(104, 112, 134);

        // Result colour coding
        public static readonly Color ColorFolder = Color.FromArgb(130, 176, 255);
        public static readonly Color ColorFav = Color.FromArgb(250, 204, 21);
        public static readonly Color ColorLarge = Color.FromArgb(251, 146, 100);
        public static readonly Color ColorRecent = Color.FromArgb(110, 220, 150);

        public static Font Ui(float size = 9.5f, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        public static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = Math.Max(1, radius * 2);
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>Applies the dark look to a dialog-style form.</summary>
        public static void StyleForm(Form f)
        {
            f.BackColor = Bg;
            f.ForeColor = Text;
            f.Font = Ui();
            Native.UseDarkTitleBar(f);
        }

        /// <summary>Owner-drawn dark combo box so the drop-down list matches the theme.</summary>
        public static void StyleCombo(ComboBox c)
        {
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Surface;
            c.ForeColor = Text;
            c.DrawMode = DrawMode.OwnerDrawFixed;
            c.ItemHeight = 22;
            c.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool hot = (e.State & DrawItemState.Selected) != 0;
                using (var b = new SolidBrush(hot ? Accent : Surface))
                    e.Graphics.FillRectangle(b, e.Bounds);
                string text = Convert.ToString(c.Items[e.Index]);
                TextRenderer.DrawText(e.Graphics, text, c.Font,
                    new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                    Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
        }
    }
}
