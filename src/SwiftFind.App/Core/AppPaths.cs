using System;
using System.IO;

namespace SwiftFind.Core
{
    /// <summary>Single source of truth for where SwiftFind keeps its data.</summary>
    public static class AppPaths
    {
        static string dataDir;

        /// <summary>Tests point this at a temp folder so they never touch real user data.</summary>
        public static string OverrideDataDir;

        public static bool IsPortable
        {
            get
            {
                try { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SwiftFind.portable")); }
                catch { return false; }
            }
        }

        public static string DataDir
        {
            get
            {
                if (OverrideDataDir != null) { try { Directory.CreateDirectory(OverrideDataDir); } catch { } return OverrideDataDir; }
                if (dataDir != null) return dataDir;
                string dir;
                if (IsPortable)
                    dir = AppDomain.CurrentDomain.BaseDirectory;
                else
                    dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwiftFind");
                try { Directory.CreateDirectory(dir); } catch { }
                dataDir = dir;
                return dir;
            }
        }

        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.ini"); } }
        public static string CacheFile { get { return Path.Combine(DataDir, "index.cache"); } }
        public static string FavoritesFile { get { return Path.Combine(DataDir, "favorites.txt"); } }
        public static string LogFile { get { return Path.Combine(DataDir, "crash.log"); } }
    }
}
