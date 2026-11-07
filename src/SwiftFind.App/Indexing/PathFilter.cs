using System;
using System.Collections.Generic;
using System.Linq;

namespace SwiftFind.Indexing
{
    /// <summary>
    /// Exclusion rules. A rule that looks like a path ("C:\\Windows\\WinSxS") excludes that folder and everything below it.
    /// A bare name ("node_modules") excludes every folder with that name, wherever it is.
    /// Boundaries are respected: "C:\\Windows" never matches "C:\\Windows2".
    /// </summary>
    public sealed class PathFilter
    {
        readonly string[] exact;        // "c:\\windows"
        readonly string[] prefixes;     // "c:\\windows\\"
        readonly string[] nameMarkers;  // "\\node_modules\\"
        readonly HashSet<string> excludedExtensions;

        public PathFilter(IEnumerable<string> excludeFolders, IEnumerable<string> excludeExtensions)
        {
            var rules = (excludeFolders ?? new string[0])
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f.Trim().TrimEnd('\\', '/'))
                .Where(f => f.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var paths = rules.Where(r => !IsName(r)).ToArray();
            exact = paths;
            prefixes = paths.Select(f => f + "\\").ToArray();
            nameMarkers = rules.Where(IsName).Select(n => "\\" + n + "\\").ToArray();
            excludedExtensions = new HashSet<string>(excludeExtensions ?? new string[0], StringComparer.OrdinalIgnoreCase);
        }

        static bool IsName(string r) { return r.IndexOf('\\') < 0 && r.IndexOf('/') < 0 && r.IndexOf(':') < 0; }

        public bool HasFolderRules { get { return exact.Length > 0 || nameMarkers.Length > 0; } }
        public bool HasExtensionRules { get { return excludedExtensions.Count > 0; } }

        /// <summary>True when the folder at <paramref name="path"/> (or anything above it) is excluded.</summary>
        public bool IsFolderExcluded(string path)
        {
            for (int i = 0; i < exact.Length; i++)
            {
                if (path.Equals(exact[i], StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            if (nameMarkers.Length > 0)
            {
                string withEnd = path.EndsWith("\\") ? path : path + "\\";
                for (int i = 0; i < nameMarkers.Length; i++)
                    if (withEnd.IndexOf(nameMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        public bool IsExtensionExcluded(string extensionWithDot)
        {
            return excludedExtensions.Count > 0 && excludedExtensions.Contains(extensionWithDot);
        }
    }
}
