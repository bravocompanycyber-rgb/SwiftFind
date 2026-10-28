using System;
using System.Collections.Generic;
using System.IO;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    public static class RecursiveIndexer
    {
        public static List<FileEntry> Index(
            string rootPath,
            Action<int> progress = null,
            HashSet<string> excludeFolders = null,
            HashSet<string> excludeExtensions = null)
        {
            var found = new List<FileEntry>(128000);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            stack.Push(rootPath);
            int count = 0;

            while (stack.Count > 0)
            {
                string dir = stack.Pop();

                if (excludeFolders != null && (excludeFolders.Contains(dir) || IsUnderExcluded(dir, excludeFolders)))
                    continue;

                try
                {
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        try
                        {
                            string ext = Path.GetExtension(file).ToLowerInvariant();
                            if (excludeExtensions != null && excludeExtensions.Contains(ext))
                                continue;

                            // Hard deduplication
                            if (!seen.Add(file))
                                continue;

                            var fi = new FileInfo(file);
                            found.Add(new FileEntry
                            {
                                Name = fi.Name,
                                FullPath = fi.FullName,
                                Directory = fi.DirectoryName,
                                Size = fi.Length,
                                Modified = fi.LastWriteTime,
                                IsDirectory = false
                            });
                            count++;
                            if (progress != null && count % 2500 == 0)
                                progress(count);
                        }
                        catch { }
                    }

                    foreach (string sub in Directory.GetDirectories(dir))
                    {
                        try
                        {
                            var di = new DirectoryInfo(sub);
                            if ((di.Attributes & FileAttributes.ReparsePoint) != 0)
                                continue;

                            if (excludeFolders != null &&
                                (excludeFolders.Contains(sub) || IsUnderExcluded(sub, excludeFolders)))
                                continue;

                            if (!seen.Add(sub))
                                continue;

                            found.Add(new FileEntry
                            {
                                Name = di.Name,
                                FullPath = di.FullName,
                                Directory = di.Parent != null ? di.Parent.FullName : "",
                                Size = 0,
                                Modified = di.LastWriteTime,
                                IsDirectory = true
                            });

                            stack.Push(sub);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            return found;
        }

        static bool IsUnderExcluded(string path, HashSet<string> excludes)
        {
            foreach (string ex in excludes)
            {
                if (path.StartsWith(ex.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) ||
                    path.Equals(ex, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
