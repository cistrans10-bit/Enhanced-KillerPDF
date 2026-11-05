using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KillerPDF.Controls
{
    /// <summary>
    /// A native slot in the WPF layout that WordCraft's window lives in (DOCX mode).
    ///
    /// WPF cannot draw another process's window, so this hosts a plain child window (a STATIC
    /// control) that WPF sizes and positions like any element. WordCraft's own top-level window
    /// is then re-parented into it as a WS_CHILD and kept filling it (<see cref="Embed"/>).
    /// The child belongs to another process; Windows joins the two input queues, so mouse,
    /// keyboard and focus work across the boundary like an ordinary child control.
    /// </summary>
    internal sealed partial class WordHost : HwndHost
    {
        private IntPtr _container;
        private IntPtr _child;
        private uint _childThread;

        /// <summary>True once a WordCraft window has been placed inside.</summary>
        internal bool HasChild => _child != IntPtr.Zero && IsWindow(_child);

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            _container = CreateWindowExW(0, "STATIC", "", (uint)(WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN),
                0, 0, Math.Max(1, (int)ActualWidth), Math.Max(1, (int)ActualHeight),
                hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            return new HandleRef(this, _container);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            // Hand WordCraft's window back to the desktop first: destroying the container would
            // otherwise destroy a window another process still owns.
            Release();
            DestroyWindow(hwnd.Handle);
            _container = IntPtr.Zero;
        }

        /// <summary>Takes WordCraft's top-level window and makes it this slot's only child.</summary>
        internal void Embed(IntPtr window)
        {
            if (_container == IntPtr.Zero || window == IntPtr.Zero) return;
            _child = window;
            long style = GetWindowLongPtrW(window, GWL_STYLE).ToInt64();
            style &= ~(WS_POPUP | WS_CAPTION | WS_THICKFRAME | WS_SYSMENU | WS_MINIMIZEBOX | WS_MAXIMIZEBOX);
            style |= WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN;
            SetWindowLongPtrW(window, GWL_STYLE, new IntPtr(style));
            long ex = GetWindowLongPtrW(window, GWL_EXSTYLE).ToInt64();
            ex &= ~(WS_EX_APPWINDOW | WS_EX_TOOLWINDOW | WS_EX_WINDOWEDGE | WS_EX_DLGMODALFRAME);
            SetWindowLongPtrW(window, GWL_EXSTYLE, new IntPtr(ex));
            SetParent(window, _container);
            // One input queue for both threads, so keyboard focus can move into WordCraft's
            // window (another process) with SetFocus, and back out to KillerPDF's controls.
            _childThread = GetWindowThreadProcessId(window, out _);
            if (_childThread != 0) AttachThreadInput(GetCurrentThreadId(), _childThread, true);
            Fit();
            ShowWindow(window, SW_SHOW);
        }

        /// <summary>Has WordCraft's window paint itself now. It only redraws when something
        /// happens, so after being hidden behind PDF mode it would otherwise show stale pixels.</summary>
        internal void Repaint()
        {
            const uint RDW_INVALIDATE = 0x0001, RDW_ERASE = 0x0004, RDW_ALLCHILDREN = 0x0080, RDW_UPDATENOW = 0x0100;
            if (HasChild) RedrawWindow(_child, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ERASE | RDW_ALLCHILDREN | RDW_UPDATENOW);
        }

        /// <summary>Gives WordCraft's window keyboard focus (after switching to DOCX mode).</summary>
        internal void FocusChild()
        {
            if (HasChild) SetFocus(_child);
        }

        /// <summary>Returns the window to the desktop, hidden, before the slot goes away.</summary>
        internal void Release()
        {
            if (!HasChild) { _child = IntPtr.Zero; return; }
            ShowWindow(_child, SW_HIDE);
            SetParent(_child, IntPtr.Zero);
            if (_childThread != 0) AttachThreadInput(GetCurrentThreadId(), _childThread, false);
            _childThread = 0;
            _child = IntPtr.Zero;
        }

        /// <summary>Focus follows the mouse into WordCraft: its window doesn't take keyboard focus
        /// itself when clicked (a child window only gets it from SetFocus), so a click inside it
        /// (reported to the container as WM_PARENTNOTIFY) or focus landing on the container
        /// passes the focus on.</summary>
        protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_SETFOCUS = 0x0007, WM_PARENTNOTIFY = 0x0210,
                      WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207;
            if (msg == WM_SETFOCUS && HasChild)
            {
                SetFocus(_child);
                handled = true;
                return IntPtr.Zero;
            }
            if (msg == WM_PARENTNOTIFY && HasChild)
            {
                int what = (int)(wParam.ToInt64() & 0xFFFF);
                if (what is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN) SetFocus(_child);
            }
            return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            Fit();
        }

        /// <summary>Keeps the child filling the slot (device pixels, from the container itself).</summary>
        private void Fit()
        {
            if (!HasChild || _container == IntPtr.Zero) return;
            if (!GetClientRect(_container, out RECT r)) return;
            MoveWindow(_child, 0, 0, Math.Max(1, r.Right - r.Left), Math.Max(1, r.Bottom - r.Top), true);
        }

        // ── Win32 ────────────────────────────────────────────────────────
        private const int GWL_STYLE = -16, GWL_EXSTYLE = -20, SW_HIDE = 0, SW_SHOW = 5;
        private const long WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000, WS_POPUP = 0x80000000,
            WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_SYSMENU = 0x00080000,
            WS_MINIMIZEBOX = 0x00020000, WS_MAXIMIZEBOX = 0x00010000,
            WS_CLIPSIBLINGS = 0x04000000, WS_CLIPCHILDREN = 0x02000000;
        private const long WS_EX_APPWINDOW = 0x00040000, WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_WINDOWEDGE = 0x00000100, WS_EX_DLGMODALFRAME = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial IntPtr CreateWindowExW(int exStyle, string className, string windowName, uint style,
            int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyWindow(IntPtr hwnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWindow(IntPtr hwnd);

        [LibraryImport("user32.dll")]
        private static partial IntPtr SetParent(IntPtr child, IntPtr parent);

        [LibraryImport("user32.dll")]
        private static partial IntPtr SetFocus(IntPtr hwnd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool ShowWindow(IntPtr hwnd, int cmd);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, [MarshalAs(UnmanagedType.Bool)] bool repaint);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetClientRect(IntPtr hwnd, out RECT rect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool RedrawWindow(IntPtr hwnd, IntPtr updateRect, IntPtr updateRegion, uint flags);

        [LibraryImport("user32.dll")]
        private static partial uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [LibraryImport("kernel32.dll")]
        private static partial uint GetCurrentThreadId();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool AttachThreadInput(uint attach, uint attachTo, [MarshalAs(UnmanagedType.Bool)] bool doAttach);

        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static partial IntPtr GetWindowLongPtrW(IntPtr hwnd, int index);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial IntPtr SetWindowLongPtrW(IntPtr hwnd, int index, IntPtr value);
    }
}

namespace KillerPDF.Controls
{
    /// <summary>Visible ↔ Hidden. Hidden, not Collapsed, so a hosted native window keeps its
    /// size (and its handle) while the status text shows in its place.</summary>
    internal sealed class InverseVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is System.Windows.Visibility.Visible ? System.Windows.Visibility.Hidden : System.Windows.Visibility.Visible;

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => System.Windows.DependencyProperty.UnsetValue;
    }
}
