using System;

namespace SwiftFind.Models
{
    public class FileEntry
    {
        /// <summary>Size value meaning "not read from disk yet" (fast USN indexing fills details in later).</summary>
        public const long UnknownSize = -1;

        public string Name;
        public string FullPath;
        public string Directory;
        public long Size;
        public DateTime Modified;
        public bool IsDirectory;
        /// <summary>Bit 2 = hidden, bit 4 = system (same values as the Windows file attributes).</summary>
        public byte Flags;

        public const byte FlagHidden = 2, FlagSystem = 4;
        public bool IsHidden { get { return (Flags & (FlagHidden | FlagSystem)) != 0; } }

        public string Extension
        {
            get
            {
                if (IsDirectory || Name == null) return string.Empty;
                int i = Name.LastIndexOf('.');
                return i >= 0 ? Name.Substring(i).ToLowerInvariant() : string.Empty;
            }
        }

        public bool HasDetails { get { return Size != UnknownSize; } }

        public override string ToString() { return FullPath; }
    }
}
