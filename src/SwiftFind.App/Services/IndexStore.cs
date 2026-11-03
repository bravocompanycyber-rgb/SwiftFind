using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Services
{
    /// <summary>Binary on-disk index cache (fast to load, written atomically).</summary>
    public static class IndexStore
    {
        const string Magic = "SFIX";
        const int Version = 3;
        static readonly object saveGate = new object();

        public sealed class Snapshot
        {
            public string Root;
            public string Method;
            public DateTime BuiltAt;
            public List<FileEntry> Entries;
        }

        public static void Save(string root, string method, DateTime builtAt, IList<FileEntry> entries)
        {
            try
            {
              lock (saveGate)
              {
                string path = AppPaths.CacheFile;
                string tmp = path + ".tmp";
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16))
                using (var w = new BinaryWriter(new BufferedStream(fs, 1 << 16), Encoding.UTF8))
                {
                    w.Write(Magic);
                    w.Write(Version);
                    w.Write(root ?? "");
                    w.Write(method ?? "");
                    w.Write(builtAt.Ticks);
                    w.Write(entries.Count);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        FileEntry e = entries[i];
                        w.Write(e.FullPath);
                        w.Write(e.Size);
                        w.Write(e.Modified.Ticks);
                        w.Write(e.IsDirectory);
                    }
                }
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
              }
            }
            catch (Exception ex) { Log.Error("Index cache save", ex); }
        }

        /// <returns>The snapshot, or null when missing, corrupt or from an incompatible version.</returns>
        public static Snapshot Load()
        {
            try
            {
                string path = AppPaths.CacheFile;
                if (!File.Exists(path)) return null;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                using (var r = new BinaryReader(new BufferedStream(fs, 1 << 16), Encoding.UTF8))
                {
                    if (r.ReadString() != Magic || r.ReadInt32() != Version) return null;
                    var snap = new Snapshot
                    {
                        Root = r.ReadString(),
                        Method = r.ReadString(),
                        BuiltAt = new DateTime(r.ReadInt64())
                    };
                    int count = r.ReadInt32();
                    if (count < 0 || count > 100000000) return null;
                    var list = new List<FileEntry>(count);
                    for (int i = 0; i < count; i++)
                    {
                        string full = r.ReadString();
                        long size = r.ReadInt64();
                        long ticks = r.ReadInt64();
                        bool dir = r.ReadBoolean();
                        int slash = full.LastIndexOf('\\');
                        list.Add(new FileEntry
                        {
                            FullPath = full,
                            Name = slash >= 0 ? full.Substring(slash + 1) : full,
                            Directory = slash > 0 ? full.Substring(0, slash) : "",
                            Size = size,
                            Modified = new DateTime(ticks),
                            IsDirectory = dir
                        });
                    }
                    snap.Entries = list;
                    return snap;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Index cache load (will rebuild)", ex);
                return null;
            }
        }
    }
}
