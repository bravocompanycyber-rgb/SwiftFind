using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Services
{
    /// <summary>Binary on-disk index cache (fast to load, written atomically). Remembers each drive's identity.</summary>
    public static class IndexStore
    {
        const string Magic = "SFIX";
        const int Version = 4;
        static readonly object saveGate = new object();

        public sealed class Snapshot
        {
            public string Scope;
            public string Method;
            public DateTime BuiltAt;
            /// <summary>Indexed root -> volume serial at the time it was indexed.</summary>
            public Dictionary<string, uint> Roots = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
            public List<FileEntry> Entries;
        }

        public static void Save(string scope, string method, DateTime builtAt, IDictionary<string, uint> roots, IList<FileEntry> entries)
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
                        w.Write(scope ?? "");
                        w.Write(method ?? "");
                        w.Write(builtAt.Ticks);
                        w.Write(roots.Count);
                        foreach (var kv in roots) { w.Write(kv.Key); w.Write(kv.Value); }
                        w.Write(entries.Count);
                        for (int i = 0; i < entries.Count; i++)
                        {
                            FileEntry e = entries[i];
                            w.Write(e.FullPath);
                            w.Write(e.Size);
                            w.Write(e.Modified.Ticks);
                            w.Write((byte)((e.IsDirectory ? 1 : 0) | e.Flags));
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
                    var snap = new Snapshot { Scope = r.ReadString(), Method = r.ReadString(), BuiltAt = new DateTime(r.ReadInt64()) };

                    int rootCount = r.ReadInt32();
                    if (rootCount < 0 || rootCount > 1000) return null;
                    for (int i = 0; i < rootCount; i++) { string root = r.ReadString(); snap.Roots[root] = r.ReadUInt32(); }

                    int count = r.ReadInt32();
                    if (count < 0 || count > 200000000) return null;
                    var list = new List<FileEntry>(count);
                    string prevDir = null;
                    for (int i = 0; i < count; i++)
                    {
                        string full = r.ReadString();
                        long size = r.ReadInt64();
                        long ticks = r.ReadInt64();
                        byte bits = r.ReadByte();
                        int slash = full.LastIndexOf('\\');
                        string dir = "";
                        if (slash > 0)
                        {
                            // Entries are grouped by folder, so reuse the previous folder string instead of allocating millions of copies.
                            if (prevDir != null && prevDir.Length == slash && string.CompareOrdinal(full, 0, prevDir, 0, slash) == 0) dir = prevDir;
                            else { dir = full.Substring(0, slash); prevDir = dir; }
                        }
                        list.Add(new FileEntry
                        {
                            FullPath = full,
                            Name = slash >= 0 ? full.Substring(slash + 1) : full,
                            Directory = dir,
                            Size = size,
                            Modified = new DateTime(ticks),
                            IsDirectory = (bits & 1) != 0,
                            Flags = (byte)(bits & 6)
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
