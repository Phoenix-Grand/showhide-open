using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShowHideOpen
{
    /// <summary>
    /// Global low-level mouse watcher. When the user double-clicks the empty area
    /// of the Windows desktop (the SysListView32 within SHELLDLL_DefView),
    /// invokes the supplied callback.
    /// </summary>
    internal sealed class DesktopDoubleClickWatcher : IDisposable
    {
        // ---- Win32 constants ----
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int LVM_HITTEST = 0x1000 + 18;

        [Flags]
        private enum LVHT : uint
        {
            NOWHERE        = 0x00000001,
            ONITEMICON     = 0x00000002,
            ONITEMLABEL    = 0x00000004,
            ONITEMSTATEICON= 0x00000008,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LVHITTESTINFO
        {
            public POINT pt;       // client coords expected
            public uint flags;     // out
            public int iItem;      // out
            public int iSubItem;   // out
            public int iGroup;     // out (Vista+)
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        // ---- P/Invoke ----
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool   UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT pt);
        [DllImport("user32.dll")] private static extern bool   ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int  SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref LVHITTESTINFO lParam);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern int  GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? lpModuleName);
        [DllImport("user32.dll")]   private static extern uint   GetDoubleClickTime();

        // ---- Fields ----
        private IntPtr _hook = IntPtr.Zero;
        private readonly LowLevelMouseProc _proc;
        private readonly Action _onDesktopDoubleClick;

        // Fallback timing for manual double-click detect when WM_LBUTTONDBLCLK is swallowed
        private POINT _lastDownPt;
        private uint _lastDownTick;
        private const int ProximityPx = 4;

        // ---- Ctor/Dispose ----
        public DesktopDoubleClickWatcher(Action onDesktopDoubleClick)
        {
            _onDesktopDoubleClick = onDesktopDoubleClick ?? throw new ArgumentNullException(nameof(onDesktopDoubleClick));
            _proc = HookCallback;

            using var curProcess = Process.GetCurrentProcess();
            using var curModule  = curProcess.MainModule!;
            _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(curModule.ModuleName), 0);
            if (_hook == IntPtr.Zero)
                throw new InvalidOperationException("Failed to set global mouse hook.");
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        // ---- Helpers ----
        private static string GetWindowClass(IntPtr hwnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static bool IsDesktopListView(IntPtr hwnd)
        {
            // Desktop icons live in a "SysListView32" within "SHELLDLL_DefView"
            return GetWindowClass(hwnd) == "SysListView32";
        }

        private static bool HitTestIsEmptyArea(IntPtr listViewHwnd, POINT screenPt)
        {
            // Convert to client coordinates for LVM_HITTEST
            var clientPt = screenPt;
            ScreenToClient(listViewHwnd, ref clientPt);

            var info = new LVHITTESTINFO { pt = clientPt };
            SendMessage(listViewHwnd, LVM_HITTEST, IntPtr.Zero, ref info);

            return ((LVHT)info.flags & LVHT.NOWHERE) != 0;
        }

        // ---- Hook ----
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var msg = wParam.ToInt32();
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                if (msg == WM_LBUTTONDBLCLK)
                {
                    HandlePotentialDesktopDoubleClick(data.pt);
                }
                else if (msg == WM_LBUTTONDOWN)
                {
                    // Manual double-click detector: time + distance
                    var now = (uint)Environment.TickCount;
                    var dt  = now - _lastDownTick;
                    var dbl = dt <= GetDoubleClickTime() &&
                              Math.Abs(data.pt.X - _lastDownPt.X) <= ProximityPx &&
                              Math.Abs(data.pt.Y - _lastDownPt.Y) <= ProximityPx;

                    _lastDownPt = data.pt;
                    _lastDownTick = now;

                    if (dbl) HandlePotentialDesktopDoubleClick(data.pt);
                }
            }

            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        private void HandlePotentialDesktopDoubleClick(POINT screenPt)
        {
            var hwnd = WindowFromPoint(screenPt);
            if (hwnd != IntPtr.Zero && IsDesktopListView(hwnd) && HitTestIsEmptyArea(hwnd, screenPt))
            {
                try { _onDesktopDoubleClick(); } catch { /* ignore */ }
            }
        }
    }
}
