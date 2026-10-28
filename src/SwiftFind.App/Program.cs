using System;
using System.Windows.Forms;
using SwiftFind.Helpers;

namespace SwiftFind
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var form = new MainForm();

            // Apply premium icon to the main window (title bar + taskbar)
            try
            {
                form.Icon = AppIcon.Create();
            }
            catch { /* keep default if icon generation fails */ }

            Application.Run(form);
        }
    }
}
