using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ShowHideOpen
{
    internal sealed class DesktopIconToggler
    {
        public void Toggle()
        {
            var listView = GetDesktopListViewHandle();
            if (listView == IntPtr.Zero) throw new InvalidOperationException("Could not find desktop icons window.");

            bool visible = Native.IsWindowVisible(listView);
            Native.ShowWindow(listView, visible ? Native.SW_HIDE : Native.SW_SHOW);
        }

        /// <summary>
        /// Finds the handle of the desktop's SysListView32 that hosts the icons.
        /// </summary>
        public static IntPtr GetDesktopListViewHandle()
        {
            // The desktop is hosted either under Progman->SHELLDLL_DefView->SysListView32
            // or WorkerW->SHELLDLL_DefView->SysListView32 (on Windows 8+ / 10 / 11).
            IntPtr progman = Native.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Progman", null);
            IntPtr defView = Native.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (defView != IntPtr.Zero)
            {
                IntPtr list = Native.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
                if (list != IntPtr.Zero) return list;
            }

            // Enum WorkerW windows by walking siblings
            IntPtr workerW = IntPtr.Zero;
            while ((workerW = Native.FindWindowEx(IntPtr.Zero, workerW, "WorkerW", null)) != IntPtr.Zero)
            {
                defView = Native.FindWindowEx(workerW, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero)
                {
                    IntPtr list = Native.FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
                    if (list != IntPtr.Zero) return list;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Returns true if the screen point is over the desktop background (not on an icon).
        /// </summary>
        public static bool IsPointOnDesktopBackground(int x, int y)
        {
            var list = GetDesktopListViewHandle();
            if (list == IntPtr.Zero) return false;

            var pt = new Native.POINT { X = x, Y = y };
            var hWndAtPoint = Native.WindowFromPoint(pt);
            if (hWndAtPoint == IntPtr.Zero) return false;

            // Walk up to root to be safe
            var root = Native.GetAncestor(hWndAtPoint, Native.GA_ROOT);
            // Confirm the click is somewhere within the desktop area (WorkerW/SHELLDLL_DefView/SysListView32)
            var className = GetClassName(root);
            if (className != "Progman" && className != "WorkerW" && className != "WorkerW_Desktop") 
            {
                // Might be the listview itself or its parent defview
                className = GetClassName(hWndAtPoint);
                if (className != "SysListView32" && className != "SHELLDLL_DefView")
                    return false;
            }

            // Use LVM_HITTEST to ensure the point is NOT on an icon
            var info = new Native.LVHITTESTINFO
            {
                pt = new Native.POINT { X = x, Y = y },
                iItem = -1,
                iSubItem = 0,
                iGroup = 0
            };

            int size = Marshal.SizeOf<Native.LVHITTESTINFO>();
            IntPtr pInfo = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(info, pInfo, false);
                var res = Native.SendMessage(list, (uint)Native.LVM_HITTEST, IntPtr.Zero, pInfo);
                // If result is -1, we hit the background, not an item
                info = Marshal.PtrToStructure<Native.LVHITTESTINFO>(pInfo);
                return info.iItem == -1;
            }
            finally
            {
                Marshal.FreeHGlobal(pInfo);
            }
        }

        private static string GetClassName(IntPtr h)
        {
            var sb = new StringBuilder(256);
            Native.GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }
    }
}
