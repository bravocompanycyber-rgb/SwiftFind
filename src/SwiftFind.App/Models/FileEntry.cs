using System;

namespace SwiftFind.Models
{
    public class FileEntry
    {
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
                if (IsDirectory) return string.Empty;
                int i = Name.LastIndexOf('.');
                return i >= 0 ? Name.Substring(i).ToLowerInvariant() : string.Empty;
            }
        }

        public override string ToString()
        {
            return FullPath;
        }
    }
}
