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
        public string ThemeMode = "Dark";            // Dark | Light | System
        public int TextSizePercent = 100;             // 90 / 100 / 115 / 130
        public string DateFormat = "Iso";             // Iso | System | Relative
        public string DoubleClick = "Open";           // Open | Reveal
        public bool ConfirmDelete = true;
        public bool ShowHidden = true;                // hidden and system items in results
        public bool StartWithWindows = false;
        public bool StartMinimized = false;
        public int SearchDelayMs = 90;
        public int HotkeyPreset = 0;
        public bool ClearSearchOnHotkey = false;
        public string IndexPriority = "Auto";         // Gentle | Auto | Fast
        public int AutoRefreshHours = 6;              // 0 = never
        public bool IncludeNetwork = false;
        public List<string> ExcludedDrives = new List<string>();
        public bool IndexAllDrives = true;      // search everywhere (all local drives) instead of one folder
        public bool IncludeRemovable = true;    // include USB / removable drives when searching everywhere
        public bool GlobalHotkey = true;   // Ctrl+Alt+Space brings SwiftFind to the front
        public bool CloseToTray = false;
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
                        case "theme": if (val == "Dark" || val == "Light" || val == "System") s.ThemeMode = val; break;
                        case "textsize": s.TextSizePercent = Clamp(ParseInt(val, 100), 80, 160); break;
                        case "dateformat": if (val == "Iso" || val == "System" || val == "Relative") s.DateFormat = val; break;
                        case "doubleclick": if (val == "Open" || val == "Reveal") s.DoubleClick = val; break;
                        case "confirmdelete": s.ConfirmDelete = ParseBool(val, s.ConfirmDelete); break;
                        case "showhidden": s.ShowHidden = ParseBool(val, s.ShowHidden); break;
                        case "startwithwindows": s.StartWithWindows = ParseBool(val, false); break;
                        case "startminimized": s.StartMinimized = ParseBool(val, false); break;
                        case "searchdelay": s.SearchDelayMs = Clamp(ParseInt(val, 90), 0, 1000); break;
                        case "hotkeypreset": s.HotkeyPreset = Clamp(ParseInt(val, 0), 0, 4); break;
                        case "clearonhotkey": s.ClearSearchOnHotkey = ParseBool(val, false); break;
                        case "priority": s.IndexPriority = (val == "Gentle" || val == "Fast") ? val : "Auto"; break;
                        case "autorefreshhours": s.AutoRefreshHours = Clamp(ParseInt(val, 6), 0, 168); break;
                        case "includenetwork": s.IncludeNetwork = ParseBool(val, false); break;
                        case "excludeddrive": if (!string.IsNullOrWhiteSpace(val)) s.ExcludedDrives.Add(val); break;
                        case "everywhere": s.IndexAllDrives = ParseBool(val, s.IndexAllDrives); break;
                        case "removable": s.IncludeRemovable = ParseBool(val, s.IncludeRemovable); break;
                        case "hotkey": s.GlobalHotkey = ParseBool(val, s.GlobalHotkey); break;
                        case "closetotray": s.CloseToTray = ParseBool(val, s.CloseToTray); break;
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
                sb.AppendLine("Theme=" + ThemeMode);
                sb.AppendLine("TextSize=" + TextSizePercent);
                sb.AppendLine("DateFormat=" + DateFormat);
                sb.AppendLine("DoubleClick=" + DoubleClick);
                sb.AppendLine("ConfirmDelete=" + (ConfirmDelete ? "1" : "0"));
                sb.AppendLine("ShowHidden=" + (ShowHidden ? "1" : "0"));
                sb.AppendLine("StartWithWindows=" + (StartWithWindows ? "1" : "0"));
                sb.AppendLine("StartMinimized=" + (StartMinimized ? "1" : "0"));
                sb.AppendLine("SearchDelay=" + SearchDelayMs);
                sb.AppendLine("HotkeyPreset=" + HotkeyPreset);
                sb.AppendLine("ClearOnHotkey=" + (ClearSearchOnHotkey ? "1" : "0"));
                sb.AppendLine("Priority=" + IndexPriority);
                sb.AppendLine("AutoRefreshHours=" + AutoRefreshHours);
                sb.AppendLine("IncludeNetwork=" + (IncludeNetwork ? "1" : "0"));
                foreach (string d in ExcludedDrives.Distinct(StringComparer.OrdinalIgnoreCase)) sb.AppendLine("ExcludedDrive=" + d);
                sb.AppendLine("Everywhere=" + (IndexAllDrives ? "1" : "0"));
                sb.AppendLine("Removable=" + (IncludeRemovable ? "1" : "0"));
                sb.AppendLine("Hotkey=" + (GlobalHotkey ? "1" : "0"));
                sb.AppendLine("CloseToTray=" + (CloseToTray ? "1" : "0"));
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
