using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ShowHideOpen
{
    internal sealed class TrayApp : Form
    {
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _toggleItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly ToolStripMenuItem _exitItem;
        private readonly DesktopIconToggler _toggler;
        private readonly DesktopDoubleClickWatcher _dcWatcher;

        public TrayApp()
        {
            // Invisible form
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Visible = false;

            _toggler = new DesktopIconToggler();

            _menu = new ContextMenuStrip();
            _toggleItem = new ToolStripMenuItem("Toggle Desktop Icons", null, (_, __) => ToggleNow());
            _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, __) => ToggleStartup()) { Checked = IsStartupEnabled() };
            _exitItem = new ToolStripMenuItem("Exit", null, (_, __) => Application.Exit());

            _menu.Items.AddRange(new ToolStripItem[]
            {
                _toggleItem,
                _startupItem,
                new ToolStripSeparator(),
                _exitItem
            });

            _tray = new NotifyIcon
            {
                Text = "ShowHideOpen",
                Icon = SystemIcons.Application,
                Visible = true,
                ContextMenuStrip = _menu
            };
            _tray.DoubleClick += (_, __) => ToggleNow();

            // Double‑click on empty desktop toggles
            _dcWatcher = new DesktopDoubleClickWatcher(OnDesktopDoubleClick);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Ensure hidden
            Hide();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _dcWatcher?.Dispose();
                _tray.Visible = false;
                _tray.Dispose();
                _menu.Dispose();
            }
            base.Dispose(disposing);
        }

        private void ToggleNow()
        {
            try
            {
                _toggler.Toggle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ShowHideOpen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleStartup()
        {
            try
            {
                var exe = Application.ExecutablePath;
                const string runKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
                using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(runKey, true);
                if (IsStartupEnabled())
                {
                    key.DeleteValue("ShowHideOpen", throwOnMissingValue: false);
                    _startupItem.Checked = false;
                }
                else
                {
                    key.SetValue("ShowHideOpen", exe);
                    _startupItem.Checked = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ShowHideOpen", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool IsStartupEnabled()
        {
            const string runKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
            using var key = Registry.CurrentUser.OpenSubKey(runKey, false);
            var val = key?.GetValue("ShowHideOpen") as string;
            return !string.IsNullOrEmpty(val);
        }

        private void OnDesktopDoubleClick()
        {
            // Toggle only when double‑click happened on empty desktop (watcher enforces this)
            ToggleNow();
        }
    }
}
