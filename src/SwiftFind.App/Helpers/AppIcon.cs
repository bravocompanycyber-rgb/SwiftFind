using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SwiftFind.Helpers
{
    /// <summary>
    /// Premium runtime application icon for SwiftFind.
    /// Geometric engineered mark: speed + precision aperture.
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
                    g.Clear(Color.Transparent);

                    // Blue rounded plate
                    using (var path = RoundedRect(14, 14, 228, 228, 52))
                    using (var brush = new LinearGradientBrush(
                        new Rectangle(14, 14, 228, 228),
                        Color.FromArgb(48, 110, 240),
                        Color.FromArgb(28, 78, 200),
                        145f))
                    {
                        g.FillPath(brush, path);
                    }

                    // Soft top glass
                    using (var path = RoundedRect(26, 26, 204, 90, 40))
                    using (var brush = new SolidBrush(Color.FromArgb(38, 255, 255, 255)))
                    {
                        g.FillPath(brush, path);
                    }

                    // Symbol
                    DrawSymbol(g, 128, 128, 78, Color.White);

                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        using (Icon temp = Icon.FromHandle(hIcon))
                            return (Icon)temp.Clone();
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

        static void DrawSymbol(Graphics g, float cx, float cy, float scale, Color color)
        {
            using (var pen = new Pen(color, Math.Max(2f, scale * 0.24f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                // Geometric S
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - scale*0.70f, cy - scale*0.50f),
                    new PointF(cx + scale*0.32f, cy - scale*0.50f),
                    new PointF(cx + scale*0.52f, cy - scale*0.18f),
                    new PointF(cx - scale*0.48f, cy + scale*0.20f),
                    new PointF(cx - scale*0.28f, cy + scale*0.50f),
                    new PointF(cx + scale*0.72f, cy + scale*0.50f),
                });
            }

            // Aperture
            float ax = cx + scale * 0.55f;
            float ay = cy - scale * 0.02f;
            float outer = scale * 0.34f;
            float inner = scale * 0.12f;

            using (var pen = new Pen(color, Math.Max(2f, scale * 0.20f)))
            {
                g.DrawEllipse(pen, ax - outer, ay - outer, outer * 2, outer * 2);
            }
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, ax - inner, ay - inner, inner * 2, inner * 2);
            }

            // Speed notch
            float ch = scale * 0.16f;
            float cx0 = cx - scale * 0.88f;
            using (var brush = new SolidBrush(color))
            {
                g.FillPolygon(brush, new[]
                {
                    new PointF(cx0, cy),
                    new PointF(cx0 + ch, cy - ch*0.85f),
                    new PointF(cx0 + ch*0.5f, cy),
                    new PointF(cx0 + ch, cy + ch*0.85f),
                });
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
