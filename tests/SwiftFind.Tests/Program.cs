using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SwiftFind.Indexing;
using SwiftFind.Models;
using SwiftFind.Search;

// Dependency-free test runner so CI needs no NuGet packages. Exit code 0 = all passed.
static class Program
{
    static int passed, failed;

    static FileEntry F(string path, long size = 100, string modified = "2026-06-15", bool dir = false)
    {
        int i = path.LastIndexOf('\\');
        return new FileEntry
        {
            FullPath = path, Name = path.Substring(i + 1), Directory = path.Substring(0, i),
            Size = size, Modified = DateTime.Parse(modified), IsDirectory = dir
        };
    }

    static List<string> Run(IList<FileEntry> data, string query, int max = 1000)
    {
        var q = SearchEngine.Parse(query);
        var r = SearchEngine.Execute(data, q, max, SearchEngine.BuildComparison(0, true, true), CancellationToken.None);
        return r.Items.Select(x => x.Name).ToList();
    }

    static void Check(string name, bool ok, string detail = "")
    {
        if (ok) { passed++; return; }
        failed++; Console.WriteLine("FAIL: " + name + (detail.Length > 0 ? "  -> " + detail : ""));
    }

    static void Expect(string name, IList<FileEntry> data, string query, params string[] expected)
    {
        var got = Run(data, query);
        Check(name + " [" + query + "]", got.SequenceEqual(expected), "got: " + string.Join(", ", got));
    }

    static int Main()
    {
        var data = new List<FileEntry>
        {
            F(@"C:\docs\Report Q1.pdf", 2 * 1024 * 1024, "2026-03-10"),
            F(@"C:\docs\report_final.docx", 50 * 1024, "2026-06-01"),
            F(@"C:\docs\notes.txt", 10, "2025-01-05"),
            F(@"C:\docs\archive", 0, "2024-12-12", true),
            F(@"C:\pics\holiday.JPG", 4 * 1024 * 1024, "2026-07-04"),
            F(@"C:\pics\holiday.png", 300 * 1024, "2026-07-05"),
            F(@"C:\src\app\main.cs", 4000, "2026-10-01"),
            F(@"C:\src\app\main.cs.bak", 3900, "2026-09-30"),
            F(@"C:\unknown\mystery.bin", FileEntry.UnknownSize, "0001-01-01"),
        };

        // plain terms
        Expect("single term", data, "notes", "notes.txt");
        Expect("case-insensitive", data, "REPORT", "Report Q1.pdf", "report_final.docx");
        Expect("all terms must match", data, "report pdf", "Report Q1.pdf");
        Expect("quoted phrase", data, "\"report q1\"", "Report Q1.pdf");
        Expect("path match", data, "pics", "holiday.JPG", "holiday.png");
        Expect("nopath restricts to name", data, "nopath: docs");
        Expect("path: only", data, "path:src", "main.cs", "main.cs.bak");

        // negation
        Expect("negate !", data, "holiday !png", "holiday.JPG");
        Expect("negate -", data, "main -bak", "main.cs");

        // wildcards
        Expect("wildcard ext", data, "*.pdf", "Report Q1.pdf");
        Expect("wildcard ?", data, "holiday.???", "holiday.JPG", "holiday.png");
        Expect("wildcard anchored", data, "main.cs*", "main.cs", "main.cs.bak");

        // regex
        Expect("regex: prefix", data, @"regex:^report_.*\.docx$", "report_final.docx");
        Expect("regex: global", data, "regex: ^holiday", "holiday.JPG", "holiday.png");
        var bad = SearchEngine.Parse("regex:([");
        Check("invalid regex reports error", bad.Error != null);
        Check("invalid regex returns nothing", SearchEngine.Execute(data, bad, 10, null, CancellationToken.None).Items.Count == 0);

        // extension filters
        Expect("ext", data, "ext:pdf", "Report Q1.pdf");
        Expect("ext uppercase file", data, "ext:jpg", "holiday.JPG");
        Expect("ext multi", data, "ext:jpg;png", "holiday.JPG", "holiday.png");
        Expect("ext excludes folders", data, "ext:pdf archive");

        // size filters
        Expect("size >", data, "size:>1mb", "holiday.JPG", "Report Q1.pdf");
        Expect("size <", data, "size:<1kb ext:txt", "notes.txt");
        Expect("size range", data, "size:100kb..1mb", "holiday.png");
        Expect("size shorthand", data, "size:>3m", "holiday.JPG");
        Check("unknown size never matches max filter", !Run(data, "size:<1gb").Contains("mystery.bin"));
        Check("size parse", SearchEngine.ParseSize("1.5kb") == 1536 && SearchEngine.ParseSize("2GB") == 2147483648L && SearchEngine.ParseSize("x") == null);

        // date filters
        Expect("dm exact day", data, "dm:2026-07-04", "holiday.JPG");
        Expect("dm after", data, "dm:>2026-09-01", "main.cs", "main.cs.bak");
        Expect("dm before", data, "dm:<2025-06-01 ext:txt", "notes.txt");
        Expect("dm range", data, "dm:2026-07-01..2026-07-31", "holiday.JPG", "holiday.png");

        // file / folder restrictions
        Expect("folder:", data, "folder:", "archive");
        Check("file: excludes folders", !Run(data, "file:").Contains("archive"));

        // case sensitivity
        Expect("case:", data, "case: Report", "Report Q1.pdf");

        // sorting
        var sorted = SearchEngine.Execute(data, SearchEngine.Parse(""), 100, SearchEngine.BuildComparison(2, false, false), CancellationToken.None);
        Check("sort by size desc", sorted.Items[0].Name == "holiday.JPG");
        var folderFirst = SearchEngine.Execute(data, SearchEngine.Parse(""), 100, SearchEngine.BuildComparison(0, false, true), CancellationToken.None);
        Check("folders first even when descending", folderFirst.Items[0].IsDirectory);

        // result capping / totals
        var capped = SearchEngine.Execute(data, SearchEngine.Parse(""), 3, SearchEngine.BuildComparison(0, true, false), CancellationToken.None);
        Check("cap honours max", capped.Items.Count == 3);
        Check("cap reports total + truncated", capped.TotalMatches == data.Count && capped.Truncated);

        // cancellation
        var cts = new CancellationTokenSource(); cts.Cancel();
        var big = Enumerable.Range(0, 100000).Select(i => F(@"C:\x\file" + i + ".txt")).ToList();
        bool cancelled = false;
        try { SearchEngine.Execute(big, SearchEngine.Parse("file"), 10, null, cts.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Check("cancellation is honoured", cancelled);

        // empty query
        Check("empty query is empty", SearchEngine.Parse("   ").IsEmpty);

        // exclusion filter boundaries (regression: "C:\\Users" must not exclude "C:\\Users2")
        var pf = new PathFilter(new[] { @"C:\Windows\", @"D:\Temp" }, new[] { ".tmp" });
        Check("filter: exact folder", pf.IsFolderExcluded(@"C:\Windows"));
        Check("filter: child folder", pf.IsFolderExcluded(@"c:\windows\system32"));
        Check("filter: sibling with same prefix is NOT excluded", !pf.IsFolderExcluded(@"C:\Windows2\x"));
        Check("filter: unrelated", !pf.IsFolderExcluded(@"C:\Users"));
        Check("filter: extension", pf.IsExtensionExcluded(".TMP") && !pf.IsExtensionExcluded(".pdf"));

        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
