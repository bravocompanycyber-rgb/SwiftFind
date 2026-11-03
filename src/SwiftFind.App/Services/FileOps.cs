using System;
using System.IO;
using Microsoft.VisualBasic.FileIO;

namespace SwiftFind.Services
{
    /// <summary>Safe file operations used by the context menu.</summary>
    public static class FileOps
    {
        /// <summary>Moves to the Recycle Bin (recoverable) instead of deleting permanently.</summary>
        public static void Recycle(string path, bool isDirectory)
        {
            if (isDirectory)
                FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
            else
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }

        static readonly char[] Invalid = Path.GetInvalidFileNameChars();

        /// <returns>An error message, or null when the name is acceptable.</returns>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "The name cannot be empty.";
            if (name.IndexOfAny(Invalid) >= 0) return "A name cannot contain any of these characters:  \\ / : * ? \" < > |";
            if (name.EndsWith(".") || name.EndsWith(" ")) return "A name cannot end with a dot or a space.";
            return null;
        }

        public static bool IsSameOrInside(string parent, string child)
        {
            string p = parent.TrimEnd('\\');
            return child.Equals(p, StringComparison.OrdinalIgnoreCase) ||
                   child.StartsWith(p + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>"name - Copy.ext", "name - Copy (2).ext" ... never overwrites.</summary>
        public static string UniquePath(string directory, string name)
        {
            string candidate = Path.Combine(directory, name);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            string stem = Path.GetFileNameWithoutExtension(name), ext = Path.GetExtension(name);
            if (Directory.Exists(candidate)) { stem = name; ext = ""; }
            for (int i = 1; i < 10000; i++)
            {
                string suffix = i == 1 ? " - Copy" : " - Copy (" + i + ")";
                candidate = Path.Combine(directory, stem + suffix + ext);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not find a free file name.");
        }

        public static void CopyDirectory(string source, string destination)
        {
            if (IsSameOrInside(source, destination))
                throw new IOException("A folder cannot be copied into itself.");
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), false);
            foreach (string dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
        }

        /// <summary>Real move on the same volume; copy-then-delete across volumes.</summary>
        public static void MoveItem(string source, string destination, bool isDirectory)
        {
            if (isDirectory && IsSameOrInside(source, destination))
                throw new IOException("A folder cannot be moved into itself.");
            bool sameVolume = string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase);
            if (isDirectory)
            {
                if (sameVolume) Directory.Move(source, destination);
                else { CopyDirectory(source, destination); Directory.Delete(source, true); }
            }
            else
            {
                if (sameVolume) File.Move(source, destination);
                else { File.Copy(source, destination, false); File.Delete(source); }
            }
        }

        public static void CopyItem(string source, string destination, bool isDirectory)
        {
            if (isDirectory) CopyDirectory(source, destination);
            else File.Copy(source, destination, false);
        }
    }
}
