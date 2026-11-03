using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Services
{
    /// <summary>Per-extension shell icons for the result list (UI thread only).</summary>
    public sealed class IconCache : IDisposable
    {
        public readonly ImageList Images;
        readonly HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly int iconSize;

        public IconCache()
        {
            iconSize = Math.Max(16, SystemInformation.SmallIconSize.Width);
            // Slightly taller than wide gives list rows comfortable breathing room.
            Images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(iconSize + 4, iconSize + 8) };
            Add("<dir>", "folder", true);
            Add("<file>", "file", false);
        }

        public string KeyFor(FileEntry f)
        {
            string key = f.IsDirectory ? "<dir>" : (f.Extension.Length == 0 ? "<file>" : f.Extension);
            if (!known.Contains(key)) Add(key, key, false);
            return key;
        }

        void Add(string key, string shellName, bool dir)
        {
            known.Add(key);
            using (Icon ico = Native.GetShellIcon(shellName.StartsWith(".") ? "x" + shellName : shellName, dir))
            {
                var bmp = new Bitmap(Images.ImageSize.Width, Images.ImageSize.Height);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    if (ico != null)
                        g.DrawIcon(ico, new Rectangle(2, 4, iconSize, iconSize));
                }
                Images.Images.Add(key, bmp);
            }
        }

        public void Dispose() { Images.Dispose(); }
    }
}
