using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SwiftFind.Core;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    /// <summary>
    /// Everything-style fast indexing by enumerating the NTFS Master File Table (FSCTL_ENUM_USN_DATA).
    /// Needs administrator rights; <see cref="LastFailure"/> explains why it fell back otherwise.
    ///
    /// Correctness notes (these were bugs in earlier versions):
    ///  * The MFT is NOT enumerated parents-first, so paths are resolved in a second pass
    ///    from a complete parent table instead of on the fly.
    ///  * File references are masked to 48 bits (the high 16 bits are a sequence number).
    ///  * Root-folder filtering uses a path-boundary check ("C:\Users" must not match "C:\Users2").
    ///  * Size/modified are not part of the USN record; entries are returned with
    ///    <see cref="FileEntry.UnknownSize"/> and filled in later by <see cref="DetailsEnricher"/>.
    /// </summary>
    public static class UsnIndexer
    {
        const uint FSCTL_ENUM_USN_DATA = 0x000900B3;
        const uint GENERIC_READ = 0x80000000;
        const uint FILE_SHARE_ALL = 0x00000007;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
        const ulong FrnMask = 0x0000FFFFFFFFFFFFUL;
        const ulong RootFrn = 5;
        const int ERROR_INVALID_PARAMETER = 87;
        const int ERROR_HANDLE_EOF = 38;

        [StructLayout(LayoutKind.Sequential)]
        struct MFT_ENUM_DATA_V1
        {
            public ulong StartFileReferenceNumber;
            public long LowUsn;
            public long HighUsn;
            public ushort MinMajorVersion;
            public ushort MaxMajorVersion;
        }

        struct Rec { public ulong Parent; public string Name; public bool IsDir; public byte Flags; }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr sec, uint disp, uint flags, IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(IntPtr dev, uint code, ref MFT_ENUM_DATA_V1 inBuf, int inSize,
                                           byte[] outBuf, int outSize, out int returned, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr h);

        /// <summary>Why the last TryIndex call returned null (shown to the user).</summary>
        public static string LastFailure;

        public static bool IsSupported(string rootPath)
        {
            try
            {
                string root = Path.GetPathRoot(rootPath);
                if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\")) return false;
                return new DriveInfo(root).DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <returns>Entries under rootPath, or null (with <see cref="LastFailure"/> set) if USN isn't usable.</returns>
        public static List<FileEntry> TryIndex(string rootPath, PathFilter filter,
                                               Action<int> progress = null, Func<bool> isCancelled = null)
        {
            LastFailure = null;
            if (!IsSupported(rootPath)) { LastFailure = "not an NTFS drive"; return null; }

            string volumeRoot = Path.GetPathRoot(rootPath);
            string volumeDevice = @"\\.\" + volumeRoot.TrimEnd('\\');

            IntPtr h = CreateFile(volumeDevice, GENERIC_READ, FILE_SHARE_ALL, IntPtr.Zero,
                                  OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
            if (h == new IntPtr(-1) || h == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                LastFailure = err == 5 ? "administrator rights required" : "cannot open volume (error " + err + ")";
                return null;
            }

            try
            {
                var recs = new Dictionary<ulong, Rec>(500000);
                var buffer = new byte[1024 * 1024];
                var input = new MFT_ENUM_DATA_V1 { StartFileReferenceNumber = 0, LowUsn = 0, HighUsn = long.MaxValue, MinMajorVersion = 2, MaxMajorVersion = 2 };
                int inSize = Marshal.SizeOf(typeof(MFT_ENUM_DATA_V1));
                int total = 0;

                while (true)
                {
                    if (isCancelled != null && isCancelled()) return null;

                    int returned;
                    bool ok = DeviceIoControl(h, FSCTL_ENUM_USN_DATA, ref input, inSize, buffer, buffer.Length, out returned, IntPtr.Zero);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err == ERROR_HANDLE_EOF) break;
                        if (err == ERROR_INVALID_PARAMETER && inSize != 24) { inSize = 24; continue; } // pre-Win8 24-byte struct
                        LastFailure = "volume enumeration failed (error " + err + ")";
                        return null;
                    }
                    if (returned <= 8) break;

                    ulong next = BitConverter.ToUInt64(buffer, 0);
                    int offset = 8;
                    while (offset + 60 <= returned)
                    {
                        uint len = BitConverter.ToUInt32(buffer, offset);
                        if (len < 60 || offset + len > returned) break;

                        ushort major = BitConverter.ToUInt16(buffer, offset + 4);
                        if (major == 2)
                        {
                            ulong frn = BitConverter.ToUInt64(buffer, offset + 8) & FrnMask;
                            ulong parent = BitConverter.ToUInt64(buffer, offset + 16) & FrnMask;
                            uint attrs = BitConverter.ToUInt32(buffer, offset + 52);
                            ushort nameLen = BitConverter.ToUInt16(buffer, offset + 56);
                            ushort nameOff = BitConverter.ToUInt16(buffer, offset + 58);

                            if (nameOff + nameLen <= len && frn >= 24) // skip NTFS metadata files ($MFT, $Bitmap ...)
                            {
                                recs[frn] = new Rec
                                {
                                    Parent = parent,
                                    Name = Encoding.Unicode.GetString(buffer, offset + nameOff, nameLen),
                                    IsDir = (attrs & 0x10) != 0,
                                    Flags = (byte)(attrs & 6)
                                };
                                total++;
                                if (progress != null && (total % 20000) == 0) progress(total);
                            }
                        }
                        offset += (int)len;
                    }

                    if (next == input.StartFileReferenceNumber) break; // no forward progress
                    input.StartFileReferenceNumber = next;
                }

                return BuildEntries(recs, volumeRoot, rootPath, filter, progress, isCancelled);
            }
            catch (Exception ex)
            {
                Log.Error("USN indexing", ex);
                LastFailure = ex.Message;
                return null;
            }
            finally { CloseHandle(h); }
        }

        static List<FileEntry> BuildEntries(Dictionary<ulong, Rec> recs, string volumeRoot, string rootPath,
                                            PathFilter filter, Action<int> progress, Func<bool> isCancelled)
        {
            string volume = volumeRoot.TrimEnd('\\');                 // "C:"
            string scopeRoot = rootPath.TrimEnd('\\');               // "C:\Users" or "C:"
            string scopePrefix = scopeRoot + "\\";
            bool wholeVolume = scopeRoot.Equals(volume, StringComparison.OrdinalIgnoreCase);

            var dirPath = new Dictionary<ulong, string>(recs.Count / 4 + 16);
            dirPath[RootFrn] = volume;
            var chain = new List<ulong>(32);

            // Resolve a directory's full path by walking up to a known ancestor, memoising every step.
            Func<ulong, string> resolveDir = null;
            resolveDir = start =>
            {
                string known;
                if (dirPath.TryGetValue(start, out known)) return known;

                chain.Clear();
                ulong cur = start;
                string basePath = null;
                for (int depth = 0; depth < 600; depth++)
                {
                    if (dirPath.TryGetValue(cur, out basePath)) break;
                    Rec r;
                    if (!recs.TryGetValue(cur, out r)) return null; // orphan: parent not on this volume listing
                    chain.Add(cur);
                    cur = r.Parent;
                    basePath = null;
                }
                if (basePath == null) return null;

                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    basePath = basePath + "\\" + recs[chain[i]].Name;
                    dirPath[chain[i]] = basePath;
                }
                return basePath;
            };

            var results = new List<FileEntry>(recs.Count / 2);
            int n = 0;
            foreach (var kv in recs)
            {
                if ((++n & 0xFFFF) == 0)
                {
                    if (isCancelled != null && isCancelled()) return null;
                    if (progress != null) progress(n);
                }

                Rec r = kv.Value;
                string parentPath = resolveDir(r.Parent);
                if (parentPath == null) continue;

                string full = parentPath + "\\" + r.Name;

                if (!wholeVolume &&
                    !full.StartsWith(scopePrefix, StringComparison.OrdinalIgnoreCase)) continue; // outside scope (boundary-safe)

                if (r.IsDir)
                {
                    if (filter != null && filter.HasFolderRules && filter.IsFolderExcluded(full)) continue;
                }
                else
                {
                    if (filter != null && filter.HasFolderRules && filter.IsFolderExcluded(parentPath)) continue;
                    if (filter != null && filter.HasExtensionRules)
                    {
                        int dot = r.Name.LastIndexOf('.');
                        if (dot >= 0 && filter.IsExtensionExcluded(r.Name.Substring(dot).ToLowerInvariant())) continue;
                    }
                }

                results.Add(new FileEntry
                {
                    Name = r.Name,
                    FullPath = full,
                    Directory = parentPath,
                    Size = r.IsDir ? 0 : FileEntry.UnknownSize,
                    Modified = DateTime.MinValue,
                    IsDirectory = r.IsDir,
                    Flags = r.Flags
                });
            }
            return results;
        }
    }
}
