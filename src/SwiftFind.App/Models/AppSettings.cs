using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SwiftFind.Core;

namespace SwiftFind.Models
{
    /// <summary>Persistent user preferences stored as a simple, hand-editable INI file.</summary>
    public class AppSettings
    {
        public const int MinResults = 500;
        public const int MaxResultsLimit = 20000;

        public string RootPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public List<string> ExcludeFolders = new List<string>();
        public List<string> ExcludeExtensions = new List<string>(); // lowercase with dot, e.g. ".tmp"
        public int MaxResults = 5000;
        public bool AutoIndexOnStartup = false;
        public bool ShowFoldersFirst = true;
        public string GroupBy = "None"; // None | Extension | Folder | Modified | Size
        public bool ColorCode = true;
        public bool MatchPath = true;
        public string LastIndexMethod = "none";
        public long LastIndexTicks = 0;

        // Window + list state
        public int WindowLeft = -1, WindowTop = -1, WindowWidth = 1280, WindowHeight = 820;
        public bool WindowMaximized = false;
        public int SortColumn = 0;
        public bool SortAscending = true;

        public static AppSettings Load()
        {
            var s = new AppSettings();
            try
            {
                string path = AppPaths.SettingsFile;
                if (!File.Exists(path)) return s;
                foreach (string line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.IndexOf('=') < 0) continue;
                    int eq = line.IndexOf('=');
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "rootpath": if (val.Length > 0) s.RootPath = val; break;
                        case "maxresults": s.MaxResults = Clamp(ParseInt(val, s.MaxResults), MinResults, MaxResultsLimit); break;
                        case "autoindex": s.AutoIndexOnStartup = ParseBool(val, s.AutoIndexOnStartup); break;
                        case "foldersfirst": s.ShowFoldersFirst = ParseBool(val, s.ShowFoldersFirst); break;
                        case "groupby": if (IsValidGroup(val)) s.GroupBy = val; break;
                        case "colorcode": s.ColorCode = ParseBool(val, s.ColorCode); break;
                        case "matchpath": s.MatchPath = ParseBool(val, s.MatchPath); break;
                        case "lastmethod": s.LastIndexMethod = val; break;
                        case "lastindex": long t; if (long.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out t)) s.LastIndexTicks = t; break;
                        case "windowleft": s.WindowLeft = ParseInt(val, s.WindowLeft); break;
                        case "windowtop": s.WindowTop = ParseInt(val, s.WindowTop); break;
                        case "windowwidth": s.WindowWidth = Clamp(ParseInt(val, s.WindowWidth), 600, 10000); break;
                        case "windowheight": s.WindowHeight = Clamp(ParseInt(val, s.WindowHeight), 400, 10000); break;
                        case "maximized": s.WindowMaximized = ParseBool(val, false); break;
                        case "sortcolumn": s.SortColumn = Clamp(ParseInt(val, 0), 0, 4); break;
                        case "sortasc": s.SortAscending = ParseBool(val, true); break;
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
            catch (Exception ex) { Log.Error("Settings load", ex); }
            return s;
        }

        public void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("# SwiftFind settings - edit carefully or use Tools > Preferences");
                sb.AppendLine("RootPath=" + RootPath);
                sb.AppendLine("MaxResults=" + MaxResults.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("AutoIndex=" + (AutoIndexOnStartup ? "1" : "0"));
                sb.AppendLine("FoldersFirst=" + (ShowFoldersFirst ? "1" : "0"));
                sb.AppendLine("GroupBy=" + GroupBy);
                sb.AppendLine("ColorCode=" + (ColorCode ? "1" : "0"));
                sb.AppendLine("MatchPath=" + (MatchPath ? "1" : "0"));
                sb.AppendLine("LastMethod=" + LastIndexMethod);
                sb.AppendLine("LastIndex=" + LastIndexTicks.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine("WindowLeft=" + WindowLeft);
                sb.AppendLine("WindowTop=" + WindowTop);
                sb.AppendLine("WindowWidth=" + WindowWidth);
                sb.AppendLine("WindowHeight=" + WindowHeight);
                sb.AppendLine("Maximized=" + (WindowMaximized ? "1" : "0"));
                sb.AppendLine("SortColumn=" + SortColumn);
                sb.AppendLine("SortAsc=" + (SortAscending ? "1" : "0"));
                foreach (string f in ExcludeFolders.Distinct(StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine("ExcludeFolder=" + f);
                foreach (string e in ExcludeExtensions.Distinct(StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine("ExcludeExt=" + e);

                // Write-then-replace so a crash mid-save can never leave a half-written file.
                string path = AppPaths.SettingsFile;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex) { Log.Error("Settings save", ex); }
        }

        public HashSet<string> GetExcludeFolderSet() { return new HashSet<string>(ExcludeFolders, StringComparer.OrdinalIgnoreCase); }
        public HashSet<string> GetExcludeExtSet() { return new HashSet<string>(ExcludeExtensions, StringComparer.OrdinalIgnoreCase); }

        static bool IsValidGroup(string v)
        {
            return v == "None" || v == "Extension" || v == "Folder" || v == "Modified" || v == "Size";
        }
        static int ParseInt(string v, int fallback)
        {
            int r; return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r) ? r : fallback;
        }
        static bool ParseBool(string v, bool fallback)
        {
            if (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (v == "0" || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }
        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    }
}
