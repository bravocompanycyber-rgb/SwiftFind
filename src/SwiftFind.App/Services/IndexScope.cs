using System;
using System.Collections.Generic;
using System.Linq;
using SwiftFind.Models;

namespace SwiftFind.Services
{
    /// <summary>Decides which locations are indexed. Uses the cached drive list, so it never blocks.</summary>
    public static class IndexScope
    {
        /// <summary>Drives that are connected and allowed by the user's settings.</summary>
        public static List<DriveDetails> Drives(AppSettings s)
        {
            var excluded = new HashSet<string>(s.ExcludedDrives, StringComparer.OrdinalIgnoreCase);
            return DriveMonitor.Shared.Drives.Where(d =>
                !excluded.Contains(d.Root) &&
                (d.Type == System.IO.DriveType.Fixed ||
                 (d.Type == System.IO.DriveType.Removable && s.IncludeRemovable) ||
                 (d.Type == System.IO.DriveType.Network && s.IncludeNetwork))).ToList();
        }

        public static List<string> Roots(AppSettings s)
        {
            if (!s.IndexAllDrives) return new List<string> { s.RootPath };
            var roots = Drives(s).Select(d => d.Root).ToList();
            if (roots.Count == 0 && DriveMonitor.Shared.Drives.Count == 0)
                roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); // drive list not ready yet
            return roots;
        }

        /// <summary>Serial number of the volume holding <paramref name="root"/> (0 when unknown).</summary>
        public static uint SerialOf(string root)
        {
            string vol = System.IO.Path.GetPathRoot(root) ?? "";
            var d = DriveMonitor.Shared.Drives.FirstOrDefault(x => string.Equals(x.Root, vol, StringComparison.OrdinalIgnoreCase));
            return d == null ? 0 : d.Serial;
        }

        /// <summary>True if the drive that holds <paramref name="root"/> is currently connected.</summary>
        public static bool IsOnline(string root)
        {
            string vol = System.IO.Path.GetPathRoot(root) ?? "";
            return DriveMonitor.Shared.Drives.Any(x => string.Equals(x.Root, vol, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Identifies the kind of scope; drives coming and going do NOT change it, so the saved index stays valid.</summary>
        public static string Key(AppSettings s) { return s.IndexAllDrives ? "all" : "dir:" + s.RootPath; }

        public static string Describe(AppSettings s)
        {
            if (!s.IndexAllDrives) return s.RootPath;
            var drives = Drives(s);
            return drives.Count == 0 ? "Everywhere" : "Everywhere  \u2022  " + string.Join(", ", drives.Select(d => d.Root.TrimEnd('\\')));
        }

        public static bool UnderRoot(string root, string path)
        {
            string r = root.TrimEnd('\\');
            return path.Equals(r, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase);
        }

        public static string DefaultFolder(AppSettings s)
        {
            return s.IndexAllDrives ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : s.RootPath;
        }
    }
}
