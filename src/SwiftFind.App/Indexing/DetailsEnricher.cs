using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    /// <summary>
    /// Fills in size and modified-time for entries that came from the USN journal.
    /// Runs in the background at reduced priority so the app and the PC stay responsive.
    /// </summary>
    public static class DetailsEnricher
    {
        public static int DegreeFor(string priority)
        {
            int cores = Math.Max(1, Environment.ProcessorCount);
            switch (priority)
            {
                case "Gentle": return Math.Max(1, cores / 4);
                case "Fast": return cores;
                default: return Math.Max(2, cores / 2);
            }
        }

        public static void Run(IList<FileEntry> entries, Action<int, int> progress, CancellationToken token, string priority = "Balanced")
        {
            var pending = new List<FileEntry>();
            foreach (var f in entries)
                if ((!f.IsDirectory && !f.HasDetails) || (f.IsDirectory && f.Modified == DateTime.MinValue))
                    pending.Add(f);
            if (pending.Count == 0) return;

            int done = 0, total = pending.Count;
            bool low = priority != "Fast";
            var options = new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = DegreeFor(priority) };

            Parallel.ForEach(System.Collections.Concurrent.Partitioner.Create(0, total, 2048), options, range =>
            {
                ThreadPriority old = Thread.CurrentThread.Priority;
                if (low) Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                try
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        FileEntry f = pending[i];
                        try
                        {
                            if (f.IsDirectory) f.Modified = new DirectoryInfo(f.FullPath).LastWriteTime;
                            else
                            {
                                var fi = new FileInfo(f.FullPath);
                                f.Modified = fi.LastWriteTime;
                                f.Size = fi.Length;
                            }
                        }
                        catch
                        {
                            if (!f.IsDirectory) f.Size = 0; // vanished or inaccessible: don't leave it "unknown" forever
                        }
                    }
                }
                finally { if (low) Thread.CurrentThread.Priority = old; }

                int d = Interlocked.Add(ref done, range.Item2 - range.Item1);
                if (progress != null) progress(d, total);
            });
        }
    }
}
