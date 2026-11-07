using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    /// <summary>
    /// Fast indexing for every file system (NTFS without admin rights, FAT, FAT32, exFAT, ReFS, network shares ...).
    ///
    /// Why it is faster than the managed scanner:
    ///  * FindFirstFileEx with FIND_FIRST_EX_LARGE_FETCH pulls big batches per call, and FindExInfoBasic skips
    ///    the 8.3 short-name lookup that is slow on FAT and NTFS;
    ///  * no FileSystemInfo object is allocated per entry;
    ///  * several threads walk different folders at once, with a thread count chosen per drive by
    ///    <see cref="IndexTuning"/> (many for SSDs, few for spinning disks and USB bridges).
    /// If the native API is unavailable it quietly falls back to <see cref="RecursiveIndexer"/>.
    /// </summary>
    public static class FastScanner
    {
        [StructLayout(LayoutKind.Sequential)]
        struct FILETIME { public uint Low, High; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime, ftLastAccessTime, ftLastWriteTime;
            public uint nFileSizeHigh, nFileSizeLow, dwReserved0, dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr FindFirstFileExW(string fileName, int infoLevel, out WIN32_FIND_DATA data, int searchOp, IntPtr filter, int flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool FindNextFileW(IntPtr handle, out WIN32_FIND_DATA data);

        [DllImport("kernel32.dll")]
        static extern bool FindClose(IntPtr handle);

        const int FindExInfoBasic = 1, FindExSearchNameMatch = 0, FIND_FIRST_EX_LARGE_FETCH = 2;
        const uint ATTR_DIRECTORY = 0x10, ATTR_REPARSE = 0x400;
        static readonly IntPtr InvalidHandle = new IntPtr(-1);

        /// <summary>False once we learn the native API can't be used (e.g. not running on Windows).</summary>
        public static volatile bool NativeAvailable = true;

        /// <returns>The entries, or null when cancelled.</returns>
        public static List<FileEntry> Index(string root, PathFilter filter, int threads, bool lowPriority,
                                            Action<int> progress, Func<bool> isCancelled)
        {
            if (NativeAvailable)
            {
                try { return Scan(root, filter, Math.Max(1, threads), lowPriority, progress, isCancelled); }
                catch (DllNotFoundException) { NativeAvailable = false; }
                catch (EntryPointNotFoundException) { NativeAvailable = false; }
                catch (Exception ex) { Log.Error("Fast scan failed, using the standard scanner", ex); }
            }
            return RecursiveIndexer.Index(root, filter, progress, isCancelled);
        }

        static string LongPath(string p)
        {
            if (p.Length < 240 || p.StartsWith(@"\\?\")) return p;
            return p.StartsWith(@"\\") ? @"\\?\UNC\" + p.Substring(2) : @"\\?\" + p;
        }

        static DateTime ToDate(FILETIME ft)
        {
            long v = ((long)ft.High << 32) | ft.Low;
            if (v <= 0 || v > 2650467743999999999L) return DateTime.MinValue;
            try { return DateTime.FromFileTime(v); } catch { return DateTime.MinValue; }
        }

        sealed class Counter { public int Count; }

        static List<FileEntry> Scan(string root, PathFilter filter, int threads, bool lowPriority,
                                    Action<int> progress, Func<bool> isCancelled)
        {
            var counter = new Counter();
            string start = root.Length > 3 ? root.TrimEnd('\\') : root;     // keep "C:\" intact

            List<FileEntry> all = ParallelWalker.Run(start, threads, lowPriority,
                (dir, local, subDirs) => ScanDirectory(dir, filter, local, subDirs, counter, progress, isCancelled),
                isCancelled);

            if (all != null && progress != null) progress(all.Count);
            return all;
        }

        static void ScanDirectory(string dir, PathFilter filter, List<FileEntry> local, List<string> subDirs,
                                  Counter st, Action<int> progress, Func<bool> isCancelled)
        {
            if (filter != null && filter.HasFolderRules && filter.IsFolderExcluded(dir)) return;

            string prefix = dir.EndsWith("\\") ? dir : dir + "\\";
            WIN32_FIND_DATA fd;
            IntPtr h = FindFirstFileExW(LongPath(prefix + "*"), FindExInfoBasic, out fd, FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);
            if (h == InvalidHandle) return;   // access denied, vanished, or an empty/unreadable folder

            int sinceReport = 0;
            try
            {
                do
                {
                    string name = fd.cFileName;
                    if (name == "." || name == "..") continue;

                    uint attrs = fd.dwFileAttributes;
                    string full = prefix + name;

                    if ((attrs & ATTR_DIRECTORY) != 0)
                    {
                        if ((attrs & ATTR_REPARSE) != 0) continue;                       // junctions/symlinks: avoid loops
                        if (filter != null && filter.HasFolderRules && filter.IsFolderExcluded(full)) continue;
                        local.Add(new FileEntry
                        {
                            Name = name, FullPath = full, Directory = dir, IsDirectory = true,
                            Size = 0, Modified = ToDate(fd.ftLastWriteTime), Flags = (byte)(attrs & 6)
                        });
                        subDirs.Add(full);
                    }
                    else
                    {
                        if (filter != null && filter.HasExtensionRules)
                        {
                            int dot = name.LastIndexOf('.');
                            if (dot >= 0 && filter.IsExtensionExcluded(name.Substring(dot).ToLowerInvariant())) continue;
                        }
                        local.Add(new FileEntry
                        {
                            Name = name, FullPath = full, Directory = dir, IsDirectory = false,
                            Size = ((long)fd.nFileSizeHigh << 32) | fd.nFileSizeLow,
                            Modified = ToDate(fd.ftLastWriteTime), Flags = (byte)(attrs & 6)
                        });
                    }

                    if (++sinceReport >= 2000)
                    {
                        int total = Interlocked.Add(ref st.Count, sinceReport);
                        sinceReport = 0;
                        if (progress != null) progress(total);
                        if (isCancelled != null && isCancelled()) return;
                    }
                }
                while (FindNextFileW(h, out fd));
            }
            finally
            {
                FindClose(h);
                if (sinceReport > 0) Interlocked.Add(ref st.Count, sinceReport);
            }
        }
    }
}
