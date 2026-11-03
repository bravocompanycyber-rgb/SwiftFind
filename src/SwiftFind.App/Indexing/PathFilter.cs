using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftFind.Indexing
{
    /// <summary>Fast "is this path inside an excluded folder?" check with correct boundary handling.</summary>
    public sealed class PathFilter
    {
        readonly string[] prefixes;      // "c:\windows\" style, lower-cased
        readonly string[] exact;         // "c:\windows"
        readonly HashSet<string> excludedExtensions;

        public PathFilter(IEnumerable<string> excludeFolders, IEnumerable<string> excludeExtensions)
        {
            var folders = (excludeFolders ?? new string[0])
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim().TrimEnd('\\', '/'))
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            exact = folders;
            prefixes = folders.Select(f => f + "\\").ToArray();
            excludedExtensions = new HashSet<string>(excludeExtensions ?? new string[0], StringComparer.OrdinalIgnoreCase);
        }

        public bool HasFolderRules { get { return exact.Length > 0; } }
        public bool HasExtensionRules { get { return excludedExtensions.Count > 0; } }

        public bool IsFolderExcluded(string path)
        {
            for (int i = 0; i < exact.Length; i++)
            {
                if (path.Equals(exact[i], StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public bool IsExtensionExcluded(string extensionWithDot)
        {
            return excludedExtensions.Count > 0 && excludedExtensions.Contains(extensionWithDot);
        }
    }
}
