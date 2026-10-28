using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SwiftFind.Models
{
    /// <summary>
    /// Persistent user preferences. Designed to be simple, readable and future-proof.
    /// </summary>
    public class AppSettings
    {
        public string RootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public List<string> ExcludeFolders = new List<string>();
        public List<string> ExcludeExtensions = new List<string>(); // lowercase, with dot e.g. ".tmp"
        public int MaxResults = 10000;
        public bool AutoIndexOnStartup = false;
        public bool ShowFoldersFirst = true;
        public string GroupBy = "None"; // None | Extension | Folder | Modified | Size
        public bool ColorCode = true;
        public bool MatchPath = true;
        public string LastIndexMethod = "none";
        public long LastIndexTicks = 0;

        static string SettingsPath
        {
            get
            {
                // Portable mode takes priority
                try
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    if (File.Exists(Path.Combine(exeDir, "SwiftFind.portable")))
                        return Path.Combine(exeDir, "settings.ini");
                }
                catch { }

                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SwiftFind");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.ini");
            }
        }

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return s;
                foreach (string line in File.ReadAllLines(SettingsPath))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || !line.Contains("="))
                        continue;
                    int eq = line.IndexOf('=');
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();

                    switch (key.ToLowerInvariant())
                    {
                        case "rootpath": s.RootPath = val; break;
                        case "maxresults": int.TryParse(val, out s.MaxResults); break;
                        case "autoindex": s.AutoIndexOnStartup = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                        case "foldersfirst": s.ShowFoldersFirst = val != "0" && !val.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                        case "groupby": s.GroupBy = val; break;
                        case "colorcode": s.ColorCode = val != "0" && !val.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                        case "matchpath": s.MatchPath = val != "0" && !val.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                        case "lastmethod": s.LastIndexMethod = val; break;
                        case "lastindex": long.TryParse(val, out s.LastIndexTicks); break;
                        case "excludefolder": if (!string.IsNullOrWhiteSpace(val)) s.ExcludeFolders.Add(val); break;
                        case "excludeext":
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                string e = val.StartsWith(".") ? val.ToLowerInvariant() : "." + val.ToLowerInvariant();
                                if (!s.ExcludeExtensions.Contains(e)) s.ExcludeExtensions.Add(e);
                            }
                            break;
                    }
                }
            }
            catch { /* never crash on settings */ }
            return s;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# SwiftFind settings – edit carefully or use Preferences dialog");
                sb.AppendLine("RootPath=" + RootPath);
                sb.AppendLine("MaxResults=" + MaxResults);
                sb.AppendLine("AutoIndex=" + (AutoIndexOnStartup ? "1" : "0"));
                sb.AppendLine("FoldersFirst=" + (ShowFoldersFirst ? "1" : "0"));
                sb.AppendLine("GroupBy=" + GroupBy);
                sb.AppendLine("ColorCode=" + (ColorCode ? "1" : "0"));
                sb.AppendLine("MatchPath=" + (MatchPath ? "1" : "0"));
                sb.AppendLine("LastMethod=" + LastIndexMethod);
                sb.AppendLine("LastIndex=" + LastIndexTicks);
                foreach (string f in ExcludeFolders.Distinct(StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine("ExcludeFolder=" + f);
                foreach (string e in ExcludeExtensions.Distinct(StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine("ExcludeExt=" + e);
                File.WriteAllText(SettingsPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public HashSet<string> GetExcludeFolderSet()
        {
            return new HashSet<string>(ExcludeFolders, StringComparer.OrdinalIgnoreCase);
        }

        public HashSet<string> GetExcludeExtSet()
        {
            return new HashSet<string>(ExcludeExtensions, StringComparer.OrdinalIgnoreCase);
        }
    }
}
