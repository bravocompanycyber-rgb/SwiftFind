using System;
using System.Windows.Forms;
using Microsoft.Win32;
using SwiftFind.Core;

namespace SwiftFind.Services
{
    /// <summary>"Start with Windows" via the per-user Run key (no admin rights needed).</summary>
    public static class StartupRegistration
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "SwiftFind";

        public static void Apply(bool enable, bool startMinimized)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (enable) k.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"" + (startMinimized ? " --minimized" : ""));
                    else k.DeleteValue(ValueName, false);
                }
            }
            catch (Exception ex) { Log.Error("Start with Windows", ex); }
        }
    }
}
