using System;
using System.Drawing;
using System.Windows.Forms;
using SwiftFind.Core;
using SwiftFind.Helpers;

namespace SwiftFind
{
    /// <summary>Tray icon, global hotkey and "bring to front" (also used by a second launch).</summary>
    public partial class MainForm
    {
        const int HotkeyId = 0x5F1D;
        NotifyIcon tray;
        bool exiting, hotkeyRegistered;

        void SetupTrayAndHotkey()
        {
            // Hotkey
            if (hotkeyRegistered) { Native.UnregisterHotKeySafe(Handle, HotkeyId); hotkeyRegistered = false; }
            if (settings.GlobalHotkey)
            {
                hotkeyRegistered = Native.TryRegisterHotKey(Handle, HotkeyId, Native.MOD_CONTROL | Native.MOD_ALT, Keys.Space);
                if (!hotkeyRegistered) Log.Info("Global hotkey Ctrl+Alt+Space is unavailable (already used by another program).");
            }

            // Tray icon: needed when closing to tray; also handy as a launcher, so it exists whenever either feature is on.
            bool wantTray = settings.CloseToTray;
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
                    Text = "SwiftFind" + (hotkeyRegistered ? "  (Ctrl+Alt+Space)" : ""),
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
                tray.ShowBalloonTip(1500, "SwiftFind is still running", "Press Ctrl+Alt+Space or click the tray icon to bring it back.", ToolTipIcon.None);
        }

        /// <summary>Shows the window, restores it if minimised and focuses the search box.</summary>
        public void BringToFront2()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            TopMost = true;   // reliably steals focus from other apps, then releases
            TopMost = false;
            searchBox.Input.Focus();
            searchBox.Input.SelectAll();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                BringToFront2();
                return;
            }
            base.WndProc(ref m);
        }
    }
}
