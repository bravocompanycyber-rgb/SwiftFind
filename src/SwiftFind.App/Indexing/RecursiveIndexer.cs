using System;
using System.Collections.Generic;
using System.IO;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    public static class RecursiveIndexer
    {
        public static List<FileEntry> Index(string rootPath, Action<int> progress = null, HashSet<string> excludes = null)
        {
            var found = new List<FileEntry>(64000);
            var stack = new Stack<string>();
            stack.Push(rootPath);
            int count = 0;

            while (stack.Count > 0)
            {
                string dir = stack.Pop();

                if (excludes != null && excludes.Contains(dir))
                    continue;

                try
                {
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        try
                        {
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
                            if (progress != null && count % 2000 == 0)
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

                            if (excludes != null && excludes.Contains(sub))
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
    }
}
