using System;
using System.Drawing;
using System.IO;

namespace SwiftFind.Helpers
{
    public static class AppIcon
    {
        public static Icon TryLoad()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates = new[]
                {
                    Path.Combine(baseDir, "SwiftFind.ico"),
                    Path.Combine(baseDir, "assets", "SwiftFind.ico"),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "assets", "SwiftFind.ico")),
                    Path.GetFullPath(Path.Combine(baseDir, "..", "..", "assets", "SwiftFind.ico"))
                };
                foreach (string path in candidates)
                {
                    if (File.Exists(path))
                        return new Icon(path);
                }
            }
            catch { }
            return null;
        }
    }
}
