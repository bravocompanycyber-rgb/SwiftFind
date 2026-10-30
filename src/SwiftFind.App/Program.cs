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
            try
            {
                var icon = AppIcon.TryLoad();
                if (icon != null) form.Icon = icon;
            }
            catch { }

            Application.Run(form);
        }
    }
}
