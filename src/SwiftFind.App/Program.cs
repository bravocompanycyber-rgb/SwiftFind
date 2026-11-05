using System;
using System.Threading;
using System.Windows.Forms;
using SwiftFind.Core;

namespace SwiftFind
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // One failure must never take the whole app (and unsaved state) down silently.
            Application.ThreadException += (s, e) => ReportCrash("UI thread", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash("Unhandled", e.ExceptionObject as Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

            string id = Environment.UserName;
            bool created;
            bool waitForPrevious = Array.IndexOf(args, "--wait-for-previous") >= 0;
            using (var mutex = new Mutex(true, "SwiftFind.SingleInstance." + id, out created))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "SwiftFind.Show." + id))
            {
                if (!created && waitForPrevious)
                {
                    // Restarted by the previous instance (e.g. elevated): wait for it to finish closing.
                    try { created = mutex.WaitOne(10000); }
                    catch (AbandonedMutexException) { created = true; } // previous process ended without releasing: we own it now
                }

                if (!created)
                {
                    // Already running: ask that instance to come to the front instead of showing an error.
                    try { showEvent.Set(); } catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                var form = new MainForm();
                RegisteredWaitHandle wait = null;
                form.Shown += (s, e) =>
                {
                    wait = ThreadPool.RegisterWaitForSingleObject(showEvent, (state, timedOut) =>
                    {
                        try { if (!form.IsDisposed) form.BeginInvoke((Action)form.BringToFront2); } catch { }
                    }, null, Timeout.Infinite, false);
                };
                Application.Run(form);
                if (wait != null) wait.Unregister(null);
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
