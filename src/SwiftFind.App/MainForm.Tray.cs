using System;
using System.Drawing;
using System.Windows.Forms;
using SwiftFind.Core;
using SwiftFind.Helpers;
using SwiftFind.Services;

namespace SwiftFind
{
    /// <summary>Tray icon, global hotkey and "bring to front" (also used by a second launch).</summary>
    public partial class MainForm
    {
        const int HotkeyId = 0x5F1D;

        public static readonly string[] HotkeyNames = { "Ctrl+Alt+Space", "Ctrl+Shift+Space", "Ctrl+Alt+F", "Win+Shift+F", "Ctrl+Alt+S" };
        static readonly uint[] HotkeyMods = { Native.MOD_CONTROL | Native.MOD_ALT, Native.MOD_CONTROL | Native.MOD_SHIFT, Native.MOD_CONTROL | Native.MOD_ALT, Native.MOD_WIN | Native.MOD_SHIFT, Native.MOD_CONTROL | Native.MOD_ALT };
        static readonly Keys[] HotkeyKeys = { Keys.Space, Keys.Space, Keys.F, Keys.F, Keys.S };

        NotifyIcon tray;
        bool exiting, hotkeyRegistered;

        void SetupTrayAndHotkey()
        {
            // Hotkey
            if (hotkeyRegistered) { Native.UnregisterHotKeySafe(Handle, HotkeyId); hotkeyRegistered = false; }
            if (settings.GlobalHotkey)
            {
                int p = Math.Max(0, Math.Min(HotkeyNames.Length - 1, settings.HotkeyPreset));
                hotkeyRegistered = Native.TryRegisterHotKey(Handle, HotkeyId, HotkeyMods[p], HotkeyKeys[p]);
                if (!hotkeyRegistered) { Log.Info("Global hotkey " + HotkeyNames[p] + " is unavailable (used by another program)."); SetStatus("The hotkey " + HotkeyNames[p] + " is already used by another program - pick a different one in Preferences."); }
            }

            // Tray icon: needed when closing to tray; also handy as a launcher, so it exists whenever either feature is on.
            bool wantTray = settings.CloseToTray || settings.StartMinimized || Program.StartHidden;
            if (wantTray && tray == null)
            {
                var menu = new ContextMenuStrip { Renderer = new DarkRenderer(), BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Theme.Ui(9.5f) };
                menu.Items.Add(Item("Show SwiftFind", Keys.None, (s, e) => BringToFront2()));
                menu.Items.Add(Item("Build / rebuild index", Keys.None, (s, e) => ToggleIndexing()));
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(Item("Exit", Keys.None, (s, e) => { exiting = true; Close(); }));

                tray = new NotifyIcon
                {
                    Icon = AppIcon.Load() ?? SystemIcons.Application,
                    Text = "SwiftFind" + (hotkeyRegistered ? "  (" + HotkeyNames[Math.Max(0, Math.Min(HotkeyNames.Length - 1, settings.HotkeyPreset))] + ")" : ""),
                    ContextMenuStrip = menu,
                    Visible = true
                };
                tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) BringToFront2(); };
            }
            else if (!wantTray && tray != null)
            {
                tray.Visible = false;
                tray.Dispose();
                tray = null;
            }
        }

        void TeardownTrayAndHotkey()
        {
            if (hotkeyRegistered) { Native.UnregisterHotKeySafe(Handle, HotkeyId); hotkeyRegistered = false; }
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
        }

        void HideToTray()
        {
            Hide();
            if (tray != null)
                tray.ShowBalloonTip(1500, "SwiftFind is still running", (hotkeyRegistered ? "Press " + HotkeyNames[Math.Max(0, Math.Min(HotkeyNames.Length - 1, settings.HotkeyPreset))] + " or click" : "Click") + " the tray icon to bring it back.", ToolTipIcon.None);
        }

        /// <summary>Shows the window, restores it if minimised and focuses the search box.</summary>
        public void BringToFront2(bool viaHotkey = false)
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            TopMost = true;   // reliably steals focus from other apps, then releases
            TopMost = false;
            searchBox.Input.Focus();
            if (viaHotkey && settings.ClearSearchOnHotkey) searchBox.Input.Clear(); else searchBox.Input.SelectAll();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                BringToFront2(true);
                return;
            }
            if (m.Msg == Native.WM_DEVICECHANGE)
            {
                int ev = m.WParam.ToInt32();
                if (ev == Native.DBT_DEVNODES_CHANGED || ev == Native.DBT_DEVICEARRIVAL || ev == Native.DBT_DEVICEREMOVECOMPLETE)
                    DriveMonitor.Shared.NudgeSoon();   // a drive was plugged in or removed: re-check in the background
            }
            base.WndProc(ref m);
        }
    }
}
