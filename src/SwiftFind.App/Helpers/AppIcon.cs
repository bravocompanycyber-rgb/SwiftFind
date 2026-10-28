using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace SwiftFind.Helpers
{
    /// <summary>
    /// Generates a clean, original professional application icon at runtime.
    /// No external assets, no copyright issues.
    /// </summary>
    public static class AppIcon
    {
        public static Icon Create()
        {
            try
            {
                // Create multi-resolution feel by drawing at 256 then letting system scale
                using (var bmp = new Bitmap(256, 256, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);

                    // Background rounded square (modern app icon style)
                    using (var path = RoundedRect(16, 16, 224, 224, 48))
                    using (var brush = new LinearGradientBrush(
                        new Rectangle(16, 16, 224, 224),
                        Color.FromArgb(45, 95, 220),
                        Color.FromArgb(30, 60, 160),
                        135f))
                    {
                        g.FillPath(brush, path);
                    }

                    // Soft inner highlight
                    using (var path = RoundedRect(28, 28, 200, 90, 36))
                    using (var brush = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
                    {
                        g.FillPath(brush, path);
                    }

                    // Magnifying glass circle
                    using (var pen = new Pen(Color.White, 14f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawEllipse(pen, 68, 58, 100, 100);
                    }

                    // Magnifying glass handle
                    using (var pen = new Pen(Color.White, 14f))
                    {
                        pen.StartCap = LineCap.Round;
                        pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 148, 148, 190, 190);
                    }

                    // Small "S" accent in the lens for brand
                    using (var font = new Font("Segoe UI", 42f, FontStyle.Bold))
                    using (var brush = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
                    {
                        var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString("S", font, brush, new RectangleF(68, 58, 100, 100), sf);
                    }

                    // Convert to Icon
                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        Icon temp = Icon.FromHandle(hIcon);
                        // Clone so we can destroy the handle
                        Icon result = (Icon)temp.Clone();
                        return result;
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch
            {
                // Fallback: simple system icon
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
