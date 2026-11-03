using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SwiftFind.Core
{
    /// <summary>Win32 helpers. Every call degrades silently on older Windows versions.</summary>
    public static class Native
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, string lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SHGetFileInfo(string path, uint attrs, ref SHFILEINFO info, uint size, uint flags);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        const uint SHGFI_ICON = 0x100;
        const uint SHGFI_SMALLICON = 0x1;
        const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        const int EM_SETCUEBANNER = 0x1501;

        /// <summary>Dark title bar on Windows 10 (1809+) and 11. No-op elsewhere.</summary>
        public static void UseDarkTitleBar(Form form)
        {
            try
            {
                Action apply = () =>
                {
                    int on = 1;
                    // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Win10 20H1+), 19 = pre-20H1 value
                    if (DwmSetWindowAttribute(form.Handle, 20, ref on, sizeof(int)) != 0)
                        DwmSetWindowAttribute(form.Handle, 19, ref on, sizeof(int));
                };
                if (form.IsHandleCreated) apply();
                else form.HandleCreated += (s, e) => { try { apply(); } catch { } };
            }
            catch { }
        }

        /// <summary>Dark scrollbars and column headers for list-like controls (Windows 10 1809+).</summary>
        public static void UseDarkControlTheme(Control c)
        {
            try
            {
                Action apply = () => SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
                if (c.IsHandleCreated) apply();
                else c.HandleCreated += (s, e) => { try { apply(); } catch { } };
            }
            catch { }
        }

        /// <summary>Placeholder text for a single-line TextBox.</summary>
        public static void SetCueBanner(TextBox box, string text)
        {
            try
            {
                Action apply = () => SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
                if (box.IsHandleCreated) apply();
                else box.HandleCreated += (s, e) => { try { apply(); } catch { } };
            }
            catch { }
        }

        /// <summary>Gets the shell's small icon for a file type without touching the disk.</summary>
        public static Icon GetShellIcon(string nameOrExtension, bool isDirectory)
        {
            try
            {
                var info = new SHFILEINFO();
                uint attrs = isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL;
                SHGetFileInfo(nameOrExtension, attrs, ref info, (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                    SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                if (info.hIcon == IntPtr.Zero) return null;
                try { return (Icon)Icon.FromHandle(info.hIcon).Clone(); }
                finally { DestroyIcon(info.hIcon); }
            }
            catch { return null; }
        }
    }
}
