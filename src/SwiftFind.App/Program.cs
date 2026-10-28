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

            // Set a professional application icon early
            try
            {
                var icon = AppIcon.Create();
                // The main form will also pick it up
            }
            catch { }

            Application.Run(new MainForm());
        }
    }
}
