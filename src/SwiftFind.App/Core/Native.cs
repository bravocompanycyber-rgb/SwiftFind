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

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        public static bool TryRegisterHotKey(IntPtr hwnd, int id, uint modifiers, Keys key)
        {
            try { return RegisterHotKey(hwnd, id, modifiers | MOD_NOREPEAT, (uint)key); }
            catch { return false; }
        }

        public static void UnregisterHotKeySafe(IntPtr hwnd, int id)
        {
            try { UnregisterHotKey(hwnd, id); } catch { }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetVolumeInformation(string root, System.Text.StringBuilder volName, int volNameSize,
            out uint serial, out uint maxComponent, out uint flags, System.Text.StringBuilder fsName, int fsNameSize);

        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr CreateFileRaw(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

        [DllImport("kernel32.dll", EntryPoint = "DeviceIoControl", SetLastError = true)]
        static extern bool DeviceIoControlBytes(IntPtr device, uint code, byte[] input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength, dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);

        /// <summary>Installed RAM in bytes (0 when unknown).</summary>
        public static long TotalRamBytes()
        {
            try
            {
                var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                return GlobalMemoryStatusEx(ref m) ? (long)m.ullTotalPhys : 0;
            }
            catch { return 0; }
        }

        static IntPtr OpenVolumeForQuery(string root)
        {
            // Access 0 = query only, so no administrator rights are needed.
            IntPtr h = CreateFileRaw(@"\\.\" + root.TrimEnd('\\'), 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            return (h == new IntPtr(-1) || h == IntPtr.Zero) ? IntPtr.Zero : h;
        }

        /// <summary>1 = spinning disk (seek penalty), 0 = SSD / flash, -1 = unknown.</summary>
        public static int QuerySeekPenalty(string root)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenVolumeForQuery(root);
                if (h == IntPtr.Zero) return -1;
                var query = new byte[12];
                BitConverter.GetBytes(7).CopyTo(query, 0);   // StorageDeviceSeekPenaltyProperty
                BitConverter.GetBytes(0).CopyTo(query, 4);   // PropertyStandardQuery
                var output = new byte[16];
                int returned;
                if (!DeviceIoControlBytes(h, 0x002D1400, query, query.Length, output, output.Length, out returned, IntPtr.Zero) || returned < 9) return -1;
                return output[8] != 0 ? 1 : 0;
            }
            catch { return -1; }
            finally { if (h != IntPtr.Zero) CloseHandle(h); }
        }

        /// <summary>Storage bus type (7 = USB, 11 = SATA, 17 = NVMe ...), or 0 if unknown.</summary>
        public static int QueryBusType(string root)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenVolumeForQuery(root);
                if (h == IntPtr.Zero) return 0;
                var query = new byte[12];                      // PropertyId 0 = StorageDeviceProperty
                var output = new byte[1024];
                int returned;
                if (!DeviceIoControlBytes(h, 0x002D1400, query, query.Length, output, output.Length, out returned, IntPtr.Zero) || returned < 32) return 0;
                return BitConverter.ToInt32(output, 28);
            }
            catch { return 0; }
            finally { if (h != IntPtr.Zero) CloseHandle(h); }
        }

        public const int WM_DEVICECHANGE = 0x0219;
        public const int DBT_DEVNODES_CHANGED = 0x0007, DBT_DEVICEARRIVAL = 0x8000, DBT_DEVICEREMOVECOMPLETE = 0x8004;

        /// <summary>Volume serial number (changes when a different disk uses the same drive letter). 0 if unknown.</summary>
        public static uint GetVolumeSerial(string root)
        {
            try
            {
                uint serial, max, flags;
                if (GetVolumeInformation(root, null, 0, out serial, out max, out flags, null, 0)) return serial;
            }
            catch { }
            return 0;
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
