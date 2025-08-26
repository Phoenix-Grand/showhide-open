using System;
using System.Windows.Forms;

namespace ShowHideOpen
{
    internal static class Program
    {
        private static DesktopDoubleClickWatcher? _watcher;
        private static Control? _uiInvoker;

        /// <summary>
        /// Application entry point.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Standard WinForms init (NET 6/7/8 template style)
            ApplicationConfiguration.Initialize();

            // A hidden control to marshal actions back to the UI thread safely.
            _uiInvoker = new Control();
            _uiInvoker.CreateControl();

            // Your existing tray app (Form or ApplicationContext).
            // If your class name differs, adjust this line only.
            var tray = new TrayApp();

            // Start global mouse watcher: double-click on empty desktop toggles.
            _watcher = new DesktopDoubleClickWatcher(() =>
            {
                try
                {
                    // Ensure Toggle runs on the UI thread
                    _uiInvoker?.BeginInvoke(new Action(DesktopToggler.Toggle));
                }
                catch
                {
                    // If anything goes sideways, fall back to direct call
                    DesktopToggler.Toggle();
                }
            });

            // Clean up the hook on exit
            Application.ApplicationExit += (_, __) =>
            {
                _watcher?.Dispose();
                _watcher = null;
                _uiInvoker?.Dispose();
                _uiInvoker = null;
            };

            Application.Run(tray);
        }
    }
}
