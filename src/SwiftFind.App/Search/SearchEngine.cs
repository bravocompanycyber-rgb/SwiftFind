using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using SwiftFind.Models;

namespace SwiftFind.Search
{
    /// <summary>
    /// Query parser + matcher. Pure logic (no UI), so it is unit-tested in tests/SwiftFind.Tests.
    ///
    /// Syntax:  words            all words must match (name, or full path when enabled)
    ///          "exact phrase"   phrase containing spaces
    ///          !word / -word    exclude matches
    ///          *.pdf  rep?rt*   wildcards (matched against the whole name)
    ///          ext:pdf;docx     one or more extensions
    ///          size:>10mb  size:&lt;=1kb  size:1mb..5mb
    ///          dm:today|yesterday|week|month|year|2026-03-01|&gt;2026-01-01|2026-01-01..2026-02-01
    ///          path:text        match the full path only
    ///          file: / folder:  restrict to files or folders
    ///          regex:pattern    regular expression (or "regex:" alone to treat all words as regex)
    ///          case:            case-sensitive
    /// </summary>
    public static class SearchEngine
    {
        public sealed class Term
        {
            public string Text;
            public bool Negate;
            public bool PathOnly;
            public bool Wildcard;
            public Regex Rx; // set for wildcard and regex terms
        }

        public class Query
        {
            public List<Term> Terms = new List<Term>();
            public HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public long? MinSize;
            public long? MaxSize;
            public DateTime? ModifiedAfter;
            public DateTime? ModifiedBefore;
            public bool MatchPath = true;
            public bool CaseSensitive;
            public bool FilesOnly;
            public bool FoldersOnly;
            public Func<FileEntry, bool> Extra; // extra UI-level predicate (e.g. favourites only)
            public string Error;                // set when the query itself is invalid

            public bool IsEmpty
            {
                get
                {
                    return Terms.Count == 0 && Extensions.Count == 0 && !MinSize.HasValue && !MaxSize.HasValue &&
                           !ModifiedAfter.HasValue && !ModifiedBefore.HasValue && !FilesOnly && !FoldersOnly && Extra == null;
                }
            }
        }

        public sealed class Result
        {
            public List<FileEntry> Items = new List<FileEntry>();
            public int TotalMatches;
            public bool Truncated;
            public string Error;
        }

        // Above this many matches we skip the global sort (it would stall typing) and sort only the page we return.
        public const int FullSortLimit = 250000;

        // ---------------------------------------------------------------- parsing

        public static Query Parse(string text, bool matchPath = true)
        {
            var q = new Query { MatchPath = matchPath };
            if (string.IsNullOrWhiteSpace(text)) return q;

            bool globalRegex = false;
            var raw = new List<string>();
            foreach (string tok in Tokenize(text))
            {
                string lower = tok.ToLowerInvariant();
                if (lower == "regex:" || lower == "/r") { globalRegex = true; continue; }
                if (lower == "case:") { q.CaseSensitive = true; continue; }
                if (lower == "file:") { q.FilesOnly = true; continue; }
                if (lower == "folder:" || lower == "dir:") { q.FoldersOnly = true; continue; }
                if (lower == "nopath:") { q.MatchPath = false; continue; }

                if (lower.StartsWith("ext:")) { AddExtensions(q, tok.Substring(4)); continue; }
                if (lower.StartsWith("size:")) { ParseSizeExpr(tok.Substring(5), q); continue; }
                if (lower.StartsWith("dm:")) { ParseDateExpr(tok.Substring(3), q); continue; }
                if (lower.StartsWith("modified:")) { ParseDateExpr(tok.Substring(9), q); continue; }
                raw.Add(tok);
            }

            var options = (q.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase) | RegexOptions.CultureInvariant;
            foreach (string tok0 in raw)
            {
                string tok = tok0;
                var term = new Term();

                if ((tok.StartsWith("!") || tok.StartsWith("-")) && tok.Length > 1)
                {
                    term.Negate = true;
                    tok = tok.Substring(1);
                }

                string lower = tok.ToLowerInvariant();
                if (lower.StartsWith("path:") && tok.Length > 5) { term.PathOnly = true; tok = tok.Substring(5); lower = tok.ToLowerInvariant(); }

                try
                {
                    if (lower.StartsWith("regex:") && tok.Length > 6)
                    {
                        term.Text = tok.Substring(6);
                        term.Rx = new Regex(term.Text, options);
                    }
                    else if (globalRegex)
                    {
                        term.Text = tok;
                        term.Rx = new Regex(tok, options);
                    }
                    else if (tok.IndexOf('*') >= 0 || tok.IndexOf('?') >= 0)
                    {
                        term.Text = tok;
                        term.Wildcard = true;
                        term.Rx = new Regex("^" + Regex.Escape(tok).Replace("\\*", ".*").Replace("\\?", ".") + "$", options);
                    }
                    else term.Text = tok;
                }
                catch (ArgumentException)
                {
                    q.Error = "Invalid regular expression: " + tok;
                    continue;
                }
                q.Terms.Add(term);
            }
            return q;
        }

        static IEnumerable<string> Tokenize(string text)
        {
            var sb = new StringBuilder();
            bool inQuote = false, hadQuote = false;
            foreach (char c in text)
            {
                if (c == '"') { inQuote = !inQuote; hadQuote = true; continue; }
                if (char.IsWhiteSpace(c) && !inQuote)
                {
                    if (sb.Length > 0 || hadQuote) { if (sb.Length > 0) yield return sb.ToString(); sb.Clear(); hadQuote = false; }
                    continue;
                }
                sb.Append(c);
            }
            if (sb.Length > 0) yield return sb.ToString();
        }

        static void AddExtensions(Query q, string list)
        {
            foreach (string part in list.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string e = part.Trim().ToLowerInvariant();
                if (e.Length == 0) continue;
                q.Extensions.Add(e.StartsWith(".") ? e : "." + e);
            }
        }

        public static long? ParseSize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().ToUpperInvariant();
            double mult = 1;
            string[] suffixes = { "TB", "GB", "MB", "KB", "T", "G", "M", "K", "B" };
            double[] mults = { 1099511627776.0, 1073741824.0, 1048576.0, 1024.0, 1099511627776.0, 1073741824.0, 1048576.0, 1024.0, 1.0 };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (s.EndsWith(suffixes[i], StringComparison.Ordinal))
                {
                    mult = mults[i];
                    s = s.Substring(0, s.Length - suffixes[i].Length).Trim();
                    break;
                }
            }
            double v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0)
                return (long)(v * mult);
            return null;
        }

        static void ParseSizeExpr(string v, Query q)
        {
            v = v.Trim();
            int dots = v.IndexOf("..", StringComparison.Ordinal);
            if (dots > 0)
            {
                long? lo = ParseSize(v.Substring(0, dots)), hi = ParseSize(v.Substring(dots + 2));
                if (lo.HasValue) q.MinSize = lo;
                if (hi.HasValue) q.MaxSize = hi;
                return;
            }
            if (v.StartsWith(">=")) { q.MinSize = ParseSize(v.Substring(2)); return; }
            if (v.StartsWith("<=")) { q.MaxSize = ParseSize(v.Substring(2)); return; }
            if (v.StartsWith(">")) { long? n = ParseSize(v.Substring(1)); if (n.HasValue) q.MinSize = n.Value + 1; return; }
            if (v.StartsWith("<")) { long? n = ParseSize(v.Substring(1)); if (n.HasValue) q.MaxSize = n.Value - 1; return; }
            if (v.StartsWith("=")) v = v.Substring(1);
            long? exact = ParseSize(v);
            if (exact.HasValue) { q.MinSize = exact; q.MaxSize = exact; }
        }

        static bool TryDate(string s, out DateTime d)
        {
            return DateTime.TryParseExact(s.Trim(), new[] { "yyyy-MM-dd", "yyyy/MM/dd", "yyyy-M-d" },
                CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
        }

        static void ParseDateExpr(string v, Query q)
        {
            v = v.Trim().ToLowerInvariant();
            DateTime today = DateTime.Now.Date, d;
            switch (v)
            {
                case "today": q.ModifiedAfter = today; return;
                case "yesterday": q.ModifiedAfter = today.AddDays(-1); q.ModifiedBefore = today; return;
                case "week": q.ModifiedAfter = today.AddDays(-7); return;
                case "month": q.ModifiedAfter = today.AddDays(-30); return;
                case "year": q.ModifiedAfter = new DateTime(today.Year, 1, 1); return;
            }
            int dots = v.IndexOf("..", StringComparison.Ordinal);
            if (dots > 0)
            {
                DateTime a, b;
                if (TryDate(v.Substring(0, dots), out a) && TryDate(v.Substring(dots + 2), out b))
                { q.ModifiedAfter = a; q.ModifiedBefore = b.AddDays(1); }
                return;
            }
            if (v.StartsWith(">") && TryDate(v.Substring(1), out d)) { q.ModifiedAfter = d.AddDays(1); return; }
            if (v.StartsWith("<") && TryDate(v.Substring(1), out d)) { q.ModifiedBefore = d; return; }
            if (TryDate(v, out d)) { q.ModifiedAfter = d; q.ModifiedBefore = d.AddDays(1); }
        }

        // ---------------------------------------------------------------- matching

        public static bool Matches(FileEntry f, Query q)
        {
            var cmp = q.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            if (q.FilesOnly && f.IsDirectory) return false;
            if (q.FoldersOnly && !f.IsDirectory) return false;

            if (q.Extensions.Count > 0 && (f.IsDirectory || !q.Extensions.Contains(f.Extension))) return false;

            if (q.MinSize.HasValue && (f.IsDirectory || f.Size < q.MinSize.Value)) return false;
            if (q.MaxSize.HasValue && (f.IsDirectory || f.Size < 0 || f.Size > q.MaxSize.Value)) return false;

            if (q.ModifiedAfter.HasValue && f.Modified < q.ModifiedAfter.Value) return false;
            if (q.ModifiedBefore.HasValue && (f.Modified == DateTime.MinValue || f.Modified >= q.ModifiedBefore.Value)) return false;

            for (int i = 0; i < q.Terms.Count; i++)
            {
                Term t = q.Terms[i];
                bool hit = TermHit(t, f, q, cmp);
                if (hit == t.Negate) return false;
            }

            return q.Extra == null || q.Extra(f);
        }

        static bool TermHit(Term t, FileEntry f, Query q, StringComparison cmp)
        {
            if (t.Rx != null)
            {
                if (t.PathOnly) return t.Rx.IsMatch(f.FullPath);
                return t.Rx.IsMatch(f.Name) || (q.MatchPath && t.Rx.IsMatch(f.FullPath));
            }
            if (t.PathOnly) return f.FullPath.IndexOf(t.Text, cmp) >= 0;
            if (f.Name.IndexOf(t.Text, cmp) >= 0) return true;
            return q.MatchPath && f.FullPath.IndexOf(t.Text, cmp) >= 0;
        }

        public static Result Execute(IList<FileEntry> source, Query q, int maxResults,
                                     Comparison<FileEntry> sort, CancellationToken token)
        {
            var res = new Result { Error = q.Error };
            if (q.Error != null) return res;

            var matches = new List<FileEntry>(Math.Min(source.Count, 8192));
            for (int i = 0; i < source.Count; i++)
            {
                if ((i & 0x3FFF) == 0 && token.IsCancellationRequested) token.ThrowIfCancellationRequested();
                FileEntry f = source[i];
                if (Matches(f, q)) matches.Add(f);
            }

            res.TotalMatches = matches.Count;
            res.Truncated = matches.Count > maxResults;

            if (sort != null)
            {
                if (matches.Count <= FullSortLimit) matches.Sort(sort);
                else
                {
                    // Too many to sort globally: keep the first page found, then sort just that page.
                    matches.RemoveRange(maxResults, matches.Count - maxResults);
                    matches.Sort(sort);
                }
            }

            if (matches.Count > maxResults) matches.RemoveRange(maxResults, matches.Count - maxResults);
            res.Items = matches;
            return res;
        }

        // ---------------------------------------------------------------- sorting

        /// <summary>Column: 0 name, 1 location, 2 size, 3 modified, 4 type.</summary>
        public static Comparison<FileEntry> BuildComparison(int column, bool ascending, bool foldersFirst)
        {
            int dir = ascending ? 1 : -1;
            return (a, b) =>
            {
                if (foldersFirst && a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
                int c = 0;
                switch (column)
                {
                    case 1: c = string.Compare(a.Directory, b.Directory, StringComparison.OrdinalIgnoreCase); break;
                    case 2: c = a.Size.CompareTo(b.Size); break;
                    case 3: c = a.Modified.CompareTo(b.Modified); break;
                    case 4: c = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase); break;
                    default: c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); break;
                }
                c *= dir;
                if (c != 0) return c;
                // Stable, deterministic tie-break
                c = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return c != 0 ? c : string.Compare(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase);
            };
        }
    }
}
