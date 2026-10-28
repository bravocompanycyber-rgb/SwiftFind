using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SwiftFind.Models;

namespace SwiftFind.Search
{
    public static class SearchEngine
    {
        public class Query
        {
            public string[] Terms = new string[0];
            public string Extension;          // e.g. ".pdf"
            public long? MinSize;
            public long? MaxSize;
            public DateTime? ModifiedAfter;
            public DateTime? ModifiedBefore;
            public bool RegexMode;
            public bool MatchPath = true;
            public bool CaseSensitive;
        }

        public static Query Parse(string text)
        {
            var q = new Query();
            if (string.IsNullOrWhiteSpace(text)) return q;

            var terms = new List<string>();
            foreach (string raw in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = raw;

                if (t.StartsWith("ext:", StringComparison.OrdinalIgnoreCase))
                {
                    q.Extension = t.Substring(4);
                    if (!q.Extension.StartsWith(".")) q.Extension = "." + q.Extension;
                    q.Extension = q.Extension.ToLowerInvariant();
                    continue;
                }
                if (t.StartsWith("size:>", StringComparison.OrdinalIgnoreCase))
                {
                    q.MinSize = ParseSize(t.Substring(6));
                    continue;
                }
                if (t.StartsWith("size:<", StringComparison.OrdinalIgnoreCase))
                {
                    q.MaxSize = ParseSize(t.Substring(6));
                    continue;
                }
                if (t.StartsWith("dm:", StringComparison.OrdinalIgnoreCase))
                {
                    // dm:today, dm:week, dm:month, dm:year
                    string v = t.Substring(3).ToLowerInvariant();
                    DateTime now = DateTime.Now.Date;
                    if (v == "today") q.ModifiedAfter = now;
                    else if (v == "week") q.ModifiedAfter = now.AddDays(-7);
                    else if (v == "month") q.ModifiedAfter = now.AddDays(-30);
                    else if (v == "year") q.ModifiedAfter = new DateTime(now.Year, 1, 1);
                    continue;
                }
                if (t.Equals("regex:", StringComparison.OrdinalIgnoreCase) || t == "/r")
                {
                    q.RegexMode = true;
                    continue;
                }

                terms.Add(t);
            }

            q.Terms = terms.ToArray();
            return q;
        }

        static long? ParseSize(string s)
        {
            s = s.Trim().ToUpperInvariant();
            double mult = 1;
            if (s.EndsWith("KB")) { mult = 1024; s = s.Substring(0, s.Length - 2); }
            else if (s.EndsWith("MB")) { mult = 1024 * 1024; s = s.Substring(0, s.Length - 2); }
            else if (s.EndsWith("GB")) { mult = 1024L * 1024 * 1024; s = s.Substring(0, s.Length - 2); }
            else if (s.EndsWith("B")) { s = s.Substring(0, s.Length - 1); }

            double v;
            if (double.TryParse(s, out v)) return (long)(v * mult);
            return null;
        }

        public static List<FileEntry> Execute(IList<FileEntry> source, Query q, int maxResults = 8000)
        {
            var comparison = q.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            Regex regex = null;

            if (q.RegexMode && q.Terms.Length > 0)
            {
                try { regex = new Regex(string.Join(" ", q.Terms), q.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase); }
                catch { /* invalid regex → treat as normal */ }
            }

            IEnumerable<FileEntry> query = source;

            if (regex != null)
            {
                query = query.Where(f => regex.IsMatch(f.Name) || (q.MatchPath && regex.IsMatch(f.FullPath)));
            }
            else if (q.Terms.Length > 0)
            {
                query = query.Where(f =>
                {
                    foreach (string t in q.Terms)
                    {
                        bool nameMatch = WildcardMatch(f.Name, t, comparison);
                        bool pathMatch = q.MatchPath && WildcardMatch(f.FullPath, t, comparison);
                        if (!nameMatch && !pathMatch) return false;
                    }
                    return true;
                });
            }

            if (!string.IsNullOrEmpty(q.Extension))
                query = query.Where(f => !f.IsDirectory && f.Extension == q.Extension);

            if (q.MinSize.HasValue)
                query = query.Where(f => !f.IsDirectory && f.Size >= q.MinSize.Value);

            if (q.MaxSize.HasValue)
                query = query.Where(f => !f.IsDirectory && f.Size <= q.MaxSize.Value);

            if (q.ModifiedAfter.HasValue)
                query = query.Where(f => f.Modified >= q.ModifiedAfter.Value);

            if (q.ModifiedBefore.HasValue)
                query = query.Where(f => f.Modified <= q.ModifiedBefore.Value);

            return query
                .OrderBy(f => f.IsDirectory ? 0 : 1)
                .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        // Simple wildcard: * and ?
        static bool WildcardMatch(string input, string pattern, StringComparison comparison)
        {
            if (pattern.IndexOf('*') < 0 && pattern.IndexOf('?') < 0)
                return input.IndexOf(pattern, comparison) >= 0;

            // Convert to regex
            string re = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(input, re, comparison == StringComparison.OrdinalIgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        }
    }
}
