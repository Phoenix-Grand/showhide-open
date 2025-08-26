using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShowHideOpen
{
    /// <summary>
    /// Watches for double‑clicks on the empty desktop and invokes a callback.
    /// </summary>
    internal sealed class DesktopDoubleClickWatcher : IDisposable
    {
        private readonly Native.LowLevelMouseProc _proc;
        private IntPtr _hook = IntPtr.Zero;
        private readonly Action _callback;

        public DesktopDoubleClickWatcher(Action callback)
        {
            _callback = callback;
            _proc = HookProc;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule!;
            _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, Native.GetModuleHandle(curModule.ModuleName!), 0);
            if (_hook == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)Native.WM_LBUTTONDBLCLK || wParam == (IntPtr)Native.WM_RBUTTONDBLCLK))
            {
                var data = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);
                if (data.dwExtraInfo == IntPtr.Zero) // filter synthetic
                {
                    // Only fire if double‑click happened on *empty desktop*
                    if (DesktopIconToggler.IsPointOnDesktopBackground(data.pt.X, data.pt.Y))
                    {
                        try { _callback(); } catch { /* ignore */ }
                        // Swallow? No: pass it through to avoid breaking user interaction
                    }
                }
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
