using System;
using System.Collections.Generic;
using System.Threading;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    /// <summary>
    /// Walks a folder tree with several worker threads sharing one work list. It knows nothing about the file
    /// system (a callback lists each folder), so the threading logic is unit-tested with a fake tree.
    ///
    /// Termination rule: the walk is finished when the work list is empty AND no worker is still scanning
    /// (a scanning worker may yet add more folders). Idle workers wait on the monitor until one of those changes.
    /// </summary>
    public static class ParallelWalker
    {
        /// <param name="scanDirectory">Lists one folder: add its entries to the first list and its sub-folders to the second.</param>
        /// <returns>All entries, or null when cancelled.</returns>
        public static List<FileEntry> Run(string root, int threads, bool lowPriority,
                                          Action<string, List<FileEntry>, List<string>> scanDirectory,
                                          Func<bool> isCancelled)
        {
            var gate = new object();
            var pending = new Stack<string>();
            var results = new List<FileEntry>(100000);
            int active = 0;
            bool stop = false;
            Exception fatal = null;
            pending.Push(root);

            ThreadStart body = () =>
            {
                var local = new List<FileEntry>(8192);
                var found = new List<string>(64);
                try
                {
                    if (lowPriority) Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
                    while (true)
                    {
                        string dir;
                        lock (gate)
                        {
                            while (pending.Count == 0 && active > 0 && !stop) Monitor.Wait(gate);
                            if (stop || pending.Count == 0) { Monitor.PulseAll(gate); break; }
                            dir = pending.Pop();
                            active++;
                        }

                        found.Clear();
                        try { scanDirectory(dir, local, found); }
                        catch (DllNotFoundException) { throw; }
                        catch (EntryPointNotFoundException) { throw; }
                        catch (Exception) { /* one unreadable folder must not stop the rest */ }
                        finally
                        {
                            lock (gate)
                            {
                                for (int i = 0; i < found.Count; i++) pending.Push(found[i]);
                                active--;
                                Monitor.PulseAll(gate);
                            }
                        }

                        if (isCancelled != null && isCancelled())
                        {
                            lock (gate) { stop = true; Monitor.PulseAll(gate); }
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Only "native API missing" style errors reach here: stop everyone so the caller can fall back.
                    fatal = ex;
                    lock (gate) { stop = true; Monitor.PulseAll(gate); }
                }
                finally { lock (gate) { results.AddRange(local); } }
            };

            var workers = new List<Thread>();
            for (int i = 0; i < Math.Max(1, threads); i++)
            {
                var t = new Thread(body) { IsBackground = true, Name = "SwiftFind scan " + i };
                workers.Add(t);
                t.Start();
            }
            foreach (Thread t in workers) t.Join();

            if (fatal != null) throw fatal;
            if (isCancelled != null && isCancelled()) return null;
            return results;
        }
    }
}
