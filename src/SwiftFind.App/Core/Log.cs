using System;
using System.IO;

namespace SwiftFind.Core
{
    /// <summary>Tiny, failure-proof file logger. Logging must never be the reason the app crashes.</summary>
    public static class Log
    {
        static readonly object gate = new object();
        const long MaxBytes = 512 * 1024;

        public static void Info(string message) { Write("INFO ", message); }
        public static void Error(string context, Exception ex) { Write("ERROR", context + ": " + ex); }

        static void Write(string level, string message)
        {
            try
            {
                lock (gate)
                {
                    string path = AppPaths.LogFile;
                    try
                    {
                        var fi = new FileInfo(path);
                        if (fi.Exists && fi.Length > MaxBytes)
                        {
                            string old = path + ".old";
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(path, old);
                        }
                    }
                    catch { }
                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
