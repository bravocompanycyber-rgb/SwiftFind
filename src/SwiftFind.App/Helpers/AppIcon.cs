using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SwiftFind.Helpers
{
    /// <summary>
    /// Clean professional application icon — just the letter S.
    /// </summary>
    public static class AppIcon
    {
        public static Icon Create()
        {
            try
            {
                using (var bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);

                    // Modern rounded square
                    using (var path = RoundedRect(12, 12, 232, 232, 52))
                    using (var brush = new LinearGradientBrush(
                        new Rectangle(12, 12, 232, 232),
                        Color.FromArgb(55, 110, 240),
                        Color.FromArgb(30, 70, 190),
                        140f))
                    {
                        g.FillPath(brush, path);
                    }

                    // Subtle highlight
                    using (var path = RoundedRect(24, 24, 208, 80, 40))
                    using (var brush = new SolidBrush(Color.FromArgb(35, 255, 255, 255)))
                    {
                        g.FillPath(brush, path);
                    }

                    // Bold white letter S
                    using (var font = new Font("Segoe UI", 128f, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.White))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString("S", font, brush, new RectangleF(0, 4, 256, 256), sf);
                    }

                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        using (Icon temp = Icon.FromHandle(hIcon))
                        {
                            return (Icon)temp.Clone();
                        }
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        static GraphicsPath RoundedRect(int x, int y, int w, int h, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool DestroyIcon(IntPtr handle);
    }
}
