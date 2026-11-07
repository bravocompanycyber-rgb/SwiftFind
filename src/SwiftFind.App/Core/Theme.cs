using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SwiftFind.Core
{
    /// <summary>Design tokens and small styling helpers shared by every window.</summary>
    public static class Theme
    {
        public static bool IsDark = true;
        public static float FontScale = 1f;

        // Surfaces
        public static Color Bg, Panel, Surface, SurfaceHi, Border;

        // Brand
        public static Color Accent = Color.FromArgb(37, 118, 208);
        public static Color AccentHover = Color.FromArgb(62, 140, 230);
        public static Color AccentPressed = Color.FromArgb(28, 98, 178);
        public static Color Orange = Color.FromArgb(242, 138, 36);
        public static Color OrangeHover = Color.FromArgb(255, 158, 62);
        public static Color Success = Color.FromArgb(34, 160, 107);
        public static Color Danger = Color.FromArgb(220, 76, 76);

        // Text
        public static Color Text, TextMuted, TextDim;

        // Result colour coding
        public static Color ColorFolder, ColorFav, ColorLarge, ColorRecent;

        static Theme() { Apply("Dark", 100); }

        /// <summary>Call once at startup, before any window is created.</summary>
        public static void Apply(string mode, int textSizePercent)
        {
            bool dark = mode != "Light";
            if (mode == "System") dark = !SystemUsesLightTheme();
            IsDark = dark;
            FontScale = Math.Max(0.8f, Math.Min(1.6f, textSizePercent / 100f));

            if (dark)
            {
                Bg = Color.FromArgb(14, 17, 27); Panel = Color.FromArgb(21, 25, 38);
                Surface = Color.FromArgb(27, 32, 48); SurfaceHi = Color.FromArgb(37, 44, 64); Border = Color.FromArgb(46, 54, 76);
                Text = Color.FromArgb(232, 235, 245); TextMuted = Color.FromArgb(150, 158, 180); TextDim = Color.FromArgb(104, 112, 134);
                ColorFolder = Color.FromArgb(130, 176, 255); ColorFav = Color.FromArgb(250, 204, 21);
                ColorLarge = Color.FromArgb(251, 146, 100); ColorRecent = Color.FromArgb(110, 220, 150);
            }
            else
            {
                Bg = Color.FromArgb(244, 246, 250); Panel = Color.FromArgb(255, 255, 255);
                Surface = Color.FromArgb(255, 255, 255); SurfaceHi = Color.FromArgb(228, 233, 243); Border = Color.FromArgb(205, 212, 226);
                Text = Color.FromArgb(28, 32, 46); TextMuted = Color.FromArgb(92, 100, 122); TextDim = Color.FromArgb(128, 136, 156);
                ColorFolder = Color.FromArgb(28, 98, 190); ColorFav = Color.FromArgb(166, 120, 0);
                ColorLarge = Color.FromArgb(190, 84, 24); ColorRecent = Color.FromArgb(18, 132, 76);
            }
        }

        static bool SystemUsesLightTheme()
        {
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1);
                return !(v is int) || (int)v != 0;
            }
            catch { return false; }
        }

        public static Font Ui(float size = 9.5f, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size * FontScale, style, GraphicsUnit.Point);
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
            f.AutoScaleDimensions = new SizeF(96F, 96F);   // dialogs scale with the display, like the main window
            f.AutoScaleMode = AutoScaleMode.Dpi;
            f.BackColor = Bg;
            f.ForeColor = Text;
            f.Font = Ui();
            if (IsDark) Native.UseDarkTitleBar(f);
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
                    hot ? Color.White : Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
        }
    }
}
