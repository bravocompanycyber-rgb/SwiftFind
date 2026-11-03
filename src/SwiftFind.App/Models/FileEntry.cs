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
