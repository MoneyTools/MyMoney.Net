using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace Walkabout.Utilities
{
    /// <summary>
    /// Summary description for NativeMethods.
    /// </summary>
    [CLSCompliantAttribute(false)]
    public static class NativeMethods
    {
        public const int SW_SHOWNORMAL = 1;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOW = 5; // winuser.h

        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const uint WS_POPUP = 0x80000000;
        public const int WS_BORDER = 0x00800000;
        public const int WS_EX_TOPMOST = 0x00000008;
        public const short WH_MOUSE = 7;

        public const int WA_INACTIVE = 0;
        public const int WA_CLICKACTIVE = 2;

        public const int WM_MOVE = 0x0003;
        public const int WM_ACTIVATE = 0x0006;
        public const int WM_SETFOCUS = 0x0007;
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_KEYUP = 0x0101;

        public const uint INFINITE = 0xFFFFFFFF;
        public const int QS_KEY = 0x0001;
        public const int QS_MOUSEMOVE = 0x0002;
        public const int QS_MOUSEBUTTON = 0x0004;
        public const int QS_POSTMESSAGE = 0x0008;
        public const int QS_TIMER = 0x0010;
        public const int QS_PAINT = 0x0020;
        public const int QS_SENDMESSAGE = 0x0040;
        public const int QS_HOTKEY = 0x0080;
        public const int QS_ALLPOSTMESSAGE = 0x0100;
        public const int QS_RAWINPUT = 0x0400;
        public const int QS_MOUSE = QS_MOUSEMOVE | QS_MOUSEBUTTON;
        public const int QS_INPUT = QS_MOUSE | QS_KEY | QS_RAWINPUT;
        public const int QS_ALLEVENTS = QS_INPUT | QS_POSTMESSAGE | QS_TIMER |
            QS_PAINT | QS_HOTKEY;
        public const int QS_ALLINPUT = QS_INPUT | QS_POSTMESSAGE | QS_TIMER |
            QS_PAINT | QS_HOTKEY;

        [DllImport("user32.dll", EntryPoint = "SetParent",
             SetLastError = true, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        public static extern int MsgWaitForMultipleObjects(int nCount, IntPtr[] pHandles, bool bWaitAll, int dwMilliseconds, int dwWakeMask);


        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr windowHandle, int msg, IntPtr w, IntPtr l);

        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow",
             SetLastError = true, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "SetParent",
             SetLastError = true, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        public static extern int SetParent(IntPtr child, IntPtr parent);


        [DllImport("user32.dll", EntryPoint = "ShowWindow",
             SetLastError = true, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr handle, int nCmdShow);


        [DllImport("user32.dll", EntryPoint = "SetForegroundWindow",
            SetLastError = true, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);


        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool MouseHookHandler(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowsHookEx",
             SetLastError = true, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        public static extern IntPtr SetWindowsHookEx(short idHook,
            MouseHookHandler hookProc,
            IntPtr hMod, // HINSTANCE
            uint dwThreadId);

        [DllImport("user32.dll", EntryPoint = "UnhookWindowsHookEx",
             SetLastError = true, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk); // HHOOK

        [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId",
             SetLastError = true, ExactSpelling = true,
             CallingConvention = CallingConvention.StdCall)]
        public static extern int GetCurrentThreadId();

        [DllImport("Shell32.dll", EntryPoint = "ShellExecuteA",
            SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true,
            CallingConvention = CallingConvention.StdCall)]


        public static extern int ShellExecute(IntPtr handle, string verb, string file,
            string args, string dir, int show);


        [DllImport("winmm.dll")]
        public static extern long PlaySound(string lpszName, IntPtr hmod, int flags);

        /// <summary>
        /// http://msdn2.microsoft.com/en-us/library/aa251511.aspx
        /// </summary>
        internal const int GWL_EXSTYLE = -20;

        internal const uint WS_EX_LAYERED = 0x00080000;
        internal const uint WS_EX_TRANSPARENT = 0x00000020;
        internal const uint WS_EX_CONTEXTHELP = 0x00000400;
        internal const uint WS_EX_DLGMODALFRAME = 0x00000001;
        internal const uint WS_MINIMIZEBOX = 0x00020000;
        internal const uint WS_MAXIMIZEBOX = 0x00010000;

        internal const int WM_SETICON = 0x0080;
        internal const int ICON_BIG = 1;
        internal const int ICON_SMALL = 0;
        internal const int WM_SYSCOMMAND = 0x0112;

        internal const int SC_CONTEXTHELP = 0xF180;

        internal const int VK_F1 = 0x70;

        public enum GWL
        {
            NONE = 0,
            STYLE = -16,
            EXSTYLE = -20,
        }

        /// <summary>
        /// http://msdn2.microsoft.com/en-us/library/ms633584.aspx
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="nIndex"></param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        internal static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        /// <summary>
        /// http://msdn2.microsoft.com/en-us/library/ms633584.aspx
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="nIndex"></param>
        /// <returns></returns>
        public static uint GetWindowLong(IntPtr hWnd, GWL nIndex)
        {
            return GetWindowLong(hWnd, (int)nIndex);
        }


        /// <summary>
        /// http://msdn2.microsoft.com/en-us/library/ms633591(VS.85).aspx
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="nIndex"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [DllImport("User32.dll")]
        internal static extern uint SetWindowLong(IntPtr hWnd, int nIndex, uint value);


        /// <summary>
        /// http://msdn2.microsoft.com/en-us/library/ms633591(VS.85).aspx
        /// </summary>
        public static uint SetWindowLong(IntPtr hWnd, GWL nIndex, uint dwNewLong)
        {
            return SetWindowLong(hWnd, (int)nIndex, dwNewLong);
        }

        /// <summary>
        /// http://msdn2.microsoft.com/en-us/library/ms648390.aspx
        /// </summary>
        /// <param name="lpPoint"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.U4)]
        public static extern uint GetDoubleClickTime();

        /// <summary>
        /// http://msdn2.microsoft.com/en-us/library/ms536119(VS.85).aspx
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        internal static System.Windows.Point GetMousePosition()
        {
            NativeMethods.POINT p;
            if (!NativeMethods.GetCursorPos(out p))
            {
                return new System.Windows.Point(0, 0);
            }

            // Convert pixels to device independent WPF coordinates
            return new System.Windows.Point(ConvertPixelsToDeviceIndependentPixels(p.X), ConvertPixelsToDeviceIndependentPixels(p.Y));
        }

        [DllImport("User32.dll")]
        private static extern IntPtr GetDC(HandleRef hWnd);

        [DllImport("User32.dll")]
        private static extern int ReleaseDC(HandleRef hWnd, HandleRef hDC);

        [DllImport("GDI32.dll")]
        private static extern int GetDeviceCaps(HandleRef hDC, int nIndex);

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806")]
        private static int DPI
        {
            get
            {
                HandleRef desktopHwnd = new HandleRef(null, IntPtr.Zero);
                HandleRef desktopDC = new HandleRef(null, GetDC(desktopHwnd));
                try
                {
                    return GetDeviceCaps(desktopDC, 88 /*LOGPIXELSX*/);
                }
                finally
                {
                    ReleaseDC(desktopHwnd, desktopDC);
                }
            }
        }

        private static double ConvertPixelsToDeviceIndependentPixels(int pixels)
        {
            return (double)pixels * 96 / DPI;
        }

        internal static string GetFileVersion(string filename)
        {
            string version = null;
            string manifest = Path.Combine(ProcessHelper.StartupPath, "MyMoney.exe.manifest");
            if (File.Exists(manifest))
            {
                try
                {
                    XNamespace ns = XNamespace.Get("urn:schemas-microsoft-com:asm.v1");
                    XDocument doc = XDocument.Load(manifest);
                    XElement id = doc.Root.Element(ns + "assemblyIdentity");
                    if (id != null)
                    {
                        version = (string)id.Attribute("version");
                    }
                }
                catch
                {
                }
            }
            if (version == null)
            {
                version = typeof(NativeMethods).Assembly.GetName().Version.ToString();
            }
            return version;
        }

        private static char[] badChars = null;

        internal static string GetValidFileName(string name)
        {
            if (badChars == null)
            {
                badChars = Path.GetInvalidFileNameChars();
            }
            if (name.IndexOfAny(badChars) >= 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (char c in name)
                {
                    bool bad = false;
                    foreach (char x in badChars)
                    {
                        if (c == x)
                        {
                            bad = true;
                            break;
                        }
                    }
                    if (!bad)
                    {
                        sb.Append(c);
                    }
                }
            }
            return name;
        }

        public static uint TickCount
        {
            get
            {
                return (uint)Environment.TickCount;
            }
        }
    }
}
