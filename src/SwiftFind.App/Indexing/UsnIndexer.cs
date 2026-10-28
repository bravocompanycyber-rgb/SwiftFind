using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SwiftFind.Models;

namespace SwiftFind.Indexing
{
    /// <summary>
    /// Fast NTFS indexing via USN Journal (FSCTL_ENUM_USN_DATA).
    /// Requires SeBackupPrivilege / admin for best results on system volumes.
    /// Falls back gracefully when not available.
    /// </summary>
    public static class UsnIndexer
    {
        const uint FSCTL_ENUM_USN_DATA = 0x000900B3;
        const uint GENERIC_READ = 0x80000000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint FILE_SHARE_DELETE = 0x00000004;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

        [StructLayout(LayoutKind.Sequential)]
        struct MFT_ENUM_DATA
        {
            public ulong StartFileReferenceNumber;
            public long LowUsn;
            public long HighUsn;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode,
            ref MFT_ENUM_DATA lpInBuffer, int nInBufferSize,
            byte[] lpOutBuffer, int nOutBufferSize,
            out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        public static bool IsSupported(string rootPath)
        {
            try
            {
                string root = Path.GetPathRoot(rootPath);
                if (string.IsNullOrEmpty(root)) return false;
                var drive = new DriveInfo(root);
                return drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// Attempt fast USN enumeration. Returns null on failure so caller can fall back.
        /// </summary>
        public static List<FileEntry> TryIndex(string rootPath, Action<int> progress = null)
        {
            if (!IsSupported(rootPath)) return null;

            string volumeRoot = Path.GetPathRoot(rootPath);
            if (string.IsNullOrEmpty(volumeRoot)) return null;

            string volumePath = @"\\.\" + volumeRoot.TrimEnd('\\');

            IntPtr hVolume = CreateFile(
                volumePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_FLAG_BACKUP_SEMANTICS,
                IntPtr.Zero);

            if (hVolume == new IntPtr(-1) || hVolume == IntPtr.Zero)
                return null;

            var results = new List<FileEntry>(256000);
            var frnToPath = new Dictionary<ulong, string>(256000);

            try
            {
                // Seed the volume root
                frnToPath[0] = volumeRoot.TrimEnd('\\');

                byte[] buffer = new byte[1024 * 1024]; // 1 MB
                var input = new MFT_ENUM_DATA
                {
                    StartFileReferenceNumber = 0,
                    LowUsn = 0,
                    HighUsn = long.MaxValue
                };

                int total = 0;
                while (true)
                {
                    int bytesReturned;
                    bool ok = DeviceIoControl(
                        hVolume, FSCTL_ENUM_USN_DATA,
                        ref input, Marshal.SizeOf(typeof(MFT_ENUM_DATA)),
                        buffer, buffer.Length,
                        out bytesReturned, IntPtr.Zero);

                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        // ERROR_HANDLE_EOF (38) or ERROR_NO_MORE_ITEMS (259) = done
                        if (err == 38 || err == 259) break;
                        return null; // real failure → fall back
                    }

                    if (bytesReturned < 8) break;

                    ulong nextUsn = BitConverter.ToUInt64(buffer, 0);
                    int offset = 8;

                    while (offset + 60 <= bytesReturned)
                    {
                        uint recordLength = BitConverter.ToUInt32(buffer, offset);
                        if (recordLength < 60 || offset + recordLength > bytesReturned)
                            break;

                        // USN_RECORD_V2 layout (simplified)
                        // 0: RecordLength
                        // 8: FileReferenceNumber
                        // 16: ParentFileReferenceNumber
                        // 48: FileAttributes
                        // 56: FileNameLength
                        // 58: FileNameOffset
                        // 60: FileName

                        ulong fileRef = BitConverter.ToUInt64(buffer, offset + 8);
                        ulong parentRef = BitConverter.ToUInt64(buffer, offset + 16);
                        uint attrs = BitConverter.ToUInt32(buffer, offset + 48);
                        ushort nameLen = BitConverter.ToUInt16(buffer, offset + 56);
                        ushort nameOff = BitConverter.ToUInt16(buffer, offset + 58);

                        if (nameOff + nameLen > recordLength)
                        {
                            offset += (int)recordLength;
                            continue;
                        }

                        string name = Encoding.Unicode.GetString(buffer, offset + nameOff, nameLen);
                        bool isDir = (attrs & 0x10) != 0; // FILE_ATTRIBUTE_DIRECTORY

                        string parentPath;
                        if (!frnToPath.TryGetValue(parentRef, out parentPath))
                            parentPath = volumeRoot.TrimEnd('\\');

                        string fullPath = parentPath.EndsWith("\\")
                            ? parentPath + name
                            : parentPath + "\\" + name;

                        frnToPath[fileRef] = fullPath;

                        // Only keep entries under the requested root
                        if (fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase) ||
                            rootPath.Equals(volumeRoot, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new FileEntry
                            {
                                Name = name,
                                FullPath = fullPath,
                                Directory = parentPath,
                                Size = 0, // size not in basic USN record; filled later if needed
                                Modified = DateTime.MinValue,
                                IsDirectory = isDir
                            });
                            total++;
                            if (progress != null && total % 5000 == 0)
                                progress(total);
                        }

                        offset += (int)recordLength;
                    }

                    if (nextUsn <= input.StartFileReferenceNumber)
                        break;
                    input.StartFileReferenceNumber = nextUsn;
                }

                return results.Count > 0 ? results : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                CloseHandle(hVolume);
            }
        }
    }
}
