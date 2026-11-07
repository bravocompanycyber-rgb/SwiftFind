using System;
using System.Collections.Generic;
using System.IO;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    /// <summary>
    /// Portable directory walker. Works on every drive type and without elevated rights.
    /// Uses EnumerateFileSystemInfos so size and timestamps arrive with the directory listing
    /// (one syscall per directory instead of one per file).
    /// </summary>
    public static class RecursiveIndexer
    {
        /// <returns>The entries found, or null when cancelled.</returns>
        public static List<FileEntry> Index(string rootPath, PathFilter filter,
                                            Action<int> progress = null, Func<bool> isCancelled = null)
        {
            var found = new List<FileEntry>(64000);
            var stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(rootPath));
            int count = 0;

            while (stack.Count > 0)
            {
                if (isCancelled != null && isCancelled()) return null;
                DirectoryInfo dir = stack.Pop();

                if (filter != null && filter.HasFolderRules && filter.IsFolderExcluded(dir.FullName))
                    continue;

                try
                {
                    foreach (FileSystemInfo fsi in dir.EnumerateFileSystemInfos())
                    {
                        try
                        {
                            bool isDir = (fsi.Attributes & FileAttributes.Directory) != 0;
                            if (isDir)
                            {
                                // Never follow junctions/symlinks: avoids loops and double-counting.
                                if ((fsi.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                                if (filter != null && filter.HasFolderRules && filter.IsFolderExcluded(fsi.FullName)) continue;

                                found.Add(new FileEntry
                                {
                                    Name = fsi.Name,
                                    FullPath = fsi.FullName,
                                    Directory = dir.FullName,
                                    Size = 0,
                                    Modified = fsi.LastWriteTime,
                                    IsDirectory = true,
                                    Flags = (byte)((int)fsi.Attributes & 6)
                                });
                                stack.Push((DirectoryInfo)fsi);
                            }
                            else
                            {
                                var fi = (FileInfo)fsi;
                                if (filter != null && filter.HasExtensionRules &&
                                    filter.IsExtensionExcluded(Path.GetExtension(fi.Name).ToLowerInvariant()))
                                    continue;

                                found.Add(new FileEntry
                                {
                                    Name = fi.Name,
                                    FullPath = fi.FullName,
                                    Directory = dir.FullName,
                                    Size = fi.Length,
                                    Modified = fi.LastWriteTime,
                                    IsDirectory = false,
                                    Flags = (byte)((int)fi.Attributes & 6)
                                });
                            }

                            count++;
                            if (progress != null && (count % 2500) == 0) progress(count);
                        }
                        catch (Exception) { /* file vanished or is inaccessible: skip it */ }
                    }
                }
                catch (Exception) { /* directory is inaccessible: skip it */ }
            }
            if (progress != null) progress(count);
            return found;
        }
    }
}
