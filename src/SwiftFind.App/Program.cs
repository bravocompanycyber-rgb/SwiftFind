using System;
using System.Threading;
using System.Windows.Forms;
using SwiftFind.Core;

namespace SwiftFind
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // One failure must never take the whole app (and unsaved state) down silently.
            Application.ThreadException += (s, e) => ReportCrash("UI thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash("Unhandled", e.ExceptionObject as Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            bool created;
            using (var mutex = new Mutex(true, "SwiftFind.SingleInstance." + Environment.UserName, out created))
            {
                if (!created)
                {
                    MessageBox.Show("SwiftFind is already running.", "SwiftFind", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }

        static void ReportCrash(string where, Exception ex)
        {
            if (ex == null) return;
            Log.Error(where, ex);
            try
            {
                MessageBox.Show("Something went wrong, but your data is safe.\n\n" + ex.Message +
                    "\n\nDetails were written to:\n" + AppPaths.LogFile, "SwiftFind", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
