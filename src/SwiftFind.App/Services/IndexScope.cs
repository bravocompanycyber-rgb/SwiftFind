using System;
using System.Collections.Generic;
using System.IO;
using SwiftFind.Models;

namespace SwiftFind.Services
{
    /// <summary>Decides which locations are indexed: every local drive, or one chosen folder.</summary>
    public static class IndexScope
    {
        public static List<string> Roots(AppSettings s)
        {
            var roots = new List<string>();
            if (!s.IndexAllDrives)
            {
                roots.Add(s.RootPath);
                return roots;
            }

            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    bool wanted = d.DriveType == DriveType.Fixed || (s.IncludeRemovable && d.DriveType == DriveType.Removable);
                    if (wanted) roots.Add(d.RootDirectory.FullName);
                }
                catch (Exception) { /* drive vanished while enumerating */ }
            }
            if (roots.Count == 0) roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            return roots;
        }

        /// <summary>Identifies what an index covers, so a saved index is only reused for the same scope.</summary>
        public static string Key(AppSettings s)
        {
            return s.IndexAllDrives ? "all:" + string.Join("|", Roots(s)) : "dir:" + s.RootPath;
        }

        public static string Describe(AppSettings s)
        {
            if (!s.IndexAllDrives) return s.RootPath;
            var roots = Roots(s);
            return "Everywhere  \u2022  " + string.Join(", ", roots.ConvertAll(r => r.TrimEnd('\\')));
        }

        /// <summary>A sensible place to paste into when nothing is selected.</summary>
        public static string DefaultFolder(AppSettings s)
        {
            return s.IndexAllDrives ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : s.RootPath;
        }
    }
}
