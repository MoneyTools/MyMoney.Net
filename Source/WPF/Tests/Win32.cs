namespace Walkabout.Tests.Interop
{

    using System;
    using System.Runtime.InteropServices;
    using System.Collections.Generic;
    using System.Windows;
    using System.Windows.Automation;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.Windows.Forms;
    using System.Threading;

    ///<summary>
    ///This class is used to PInvoke for win32 functionality that I have not been
    ///able to find in the managed platform
    ///Feel free to updated clients to used managed/safe alternatives
    ///</summary>
    internal class Win32
    {
        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        //SendInput related
        public const int VK_SHIFT = 0x10;
        public const int VK_CONTROL = 0x11;
        public const int VK_MENU = 0x12;

        public const int KEYEVENTF_EXTENDEDKEY = 0x0001;
        public const int KEYEVENTF_KEYUP = 0x0002;
        public const int KEYEVENTF_UNICODE = 0x0004;
        public const int KEYEVENTF_SCANCODE = 0x0008;

        public const int MOUSEEVENTF_VIRTUALDESK = 0x4000;

        public const int EM_SETSEL = 0x00B1;
        public const int EM_GETLINECOUNT = 0x00BA;
        public const int EM_LINEFROMCHAR = 0x00C9;

        // GetSystemMetrics
        public const int SM_CXMAXTRACK = 59;
        public const int SM_CYMAXTRACK = 60;
        public const int SM_XVIRTUALSCREEN = 76;
        public const int SM_YVIRTUALSCREEN = 77;
        public const int SM_CXVIRTUALSCREEN = 78;
        public const int SM_CYVIRTUALSCREEN = 79;
        public const int SM_SWAPBUTTON = 23;

        // Window style information
        //public const int GWL_HINSTANCE  = -6;
        //public const int GWL_ID         = -12;
        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int WS_VSCROLL = 0x00200000;
        public const int WS_HSCROLL = 0x00100000;
        public const int ES_MULTILINE = 0x0004;
        public const int ES_AUTOVSCROLL = 0x0040;
        public const int ES_AUTOHSCROLL = 0x0080;

        public const int INPUT_MOUSE = 0;
        public const int INPUT_KEYBOARD = 1;

        public const int WM_SETFOCUS = 0x0007;

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public INPUTUNION union;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct INPUTUNION
        {
            [FieldOffset(0)] public MOUSEINPUT mouseInput;
            [FieldOffset(0)] public KEYBDINPUT keyboardInput;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public int mouseData;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public short wVk;
            public short wScan;
            public int dwFlags;
            public int time;
            public IntPtr dwExtraInfo;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct HWND
        {
            IntPtr _value;

            public static HWND Cast(IntPtr hwnd)
            {
                HWND temp = new HWND();
                temp._value = hwnd;
                return temp;
            }

            public static implicit operator IntPtr(HWND hwnd)
            {
                return hwnd._value;
            }

            public static HWND NULL
            {
                get
                {
                    HWND temp = new HWND();
                    temp._value = IntPtr.Zero;
                    return temp;
                }
            }

            public static bool operator ==(HWND lhs, HWND rhs)
            {
                return lhs._value == rhs._value;
            }

            public static bool operator !=(HWND lhs, HWND rhs)
            {
                return lhs._value != rhs._value;
            }

            override public bool Equals(object oCompare)
            {
                HWND temp = Cast((HWND)oCompare);
                return _value == temp._value;
            }

            public override int GetHashCode()
            {
                return (int)_value;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetDoubleClickTime();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SendInput(int nInputs, ref INPUT mi, int cbSize);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int MapVirtualKey(int nVirtKey, int nMapType);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetAsyncKeyState(int nVirtKey);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetKeyState(int nVirtKey);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        internal static extern int GetKeyboardState(byte[] keystate);

        [DllImport("user32.dll", ExactSpelling = true, EntryPoint = "keybd_event", CharSet = CharSet.Auto)]
        internal static extern void Keybd_event(byte vk, byte scan, int flags, int extrainfo);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        internal static extern int SetKeyboardState(byte[] keystate);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(Win32.HWND hWnd, int nMsg, IntPtr wParam, IntPtr lParam);

        public static void SetFocus(Win32.HWND hWnd)
        {
            SendMessage(hWnd, WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero);
        }

        // Overload for WM_GETTEXT
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(Win32.HWND hWnd, int nMsg, IntPtr wParam, System.Text.StringBuilder lParam);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int metric);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowLong(Win32.HWND hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);


        [DllImport("User32.dll")]
        private static extern IntPtr GetDC(HandleRef hWnd);

        [DllImport("User32.dll")]
        private static extern int ReleaseDC(HandleRef hWnd, HandleRef hDC);

        [DllImport("GDI32.dll")]

        private static extern int GetDeviceCaps(HandleRef hDC, int nIndex);

        private static int _dpi = 0;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806")]
        private static int DPI
        {
            get
            {
                if (_dpi == 0)
                {
                    HandleRef desktopHwnd = new HandleRef(null, IntPtr.Zero);
                    HandleRef desktopDC = new HandleRef(null, GetDC(desktopHwnd));
                    _dpi = GetDeviceCaps(desktopDC, 88 /*LOGPIXELSX*/);
                    ReleaseDC(desktopHwnd, desktopDC);
                }
                return _dpi;
            }
        }

        internal static double ConvertToDeviceIndependentPixels(int pixels)
        {
            return (double)pixels * 96 / (double)DPI;
        }


        internal static int ConvertFromDeviceIndependentPixels(double pixels)
        {
            return (int)(pixels * (double)DPI / 96);
        }

        internal static AutomationElement FindWindow(int processId, string automationId)
        {
            foreach (IntPtr hwnd in SafeNativeMethods.GetDesktopWindows())
            {
                if (SafeNativeMethods.IsWindowVisible(hwnd))
                {
                    int procId;
                    SafeNativeMethods.GetWindowThreadProcessId(hwnd, out procId);
                    if (procId == processId)
                    {
                        try
                        {
                            AutomationElement e = AutomationElement.FromHandle(hwnd);
                            if (e.Current.AutomationId == automationId)
                            {
                                return e;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
            }

            return null;
        }

        internal static AutomationElement FindDesktopWindow(string name, int retries = 10)
        {
            for (int i = 0; i < retries; i++)
            {
                foreach (IntPtr hwnd in SafeNativeMethods.GetDesktopWindows())
                {
                    if (SafeNativeMethods.IsWindowVisible(hwnd))
                    {
                        try
                        {
                            AutomationElement e = AutomationElement.FromHandle(hwnd);
                            System.Diagnostics.Debug.WriteLine("Found window: " + e.Current.Name);
                            if (e.Current.Name == name)
                            {
                                return e;
                            }
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                Thread.Sleep(1000);
            }
            return null;
        }


        public static void CaptureScreen(string filename, ImageFormat format)
        {
            //create a DC of display device
            IntPtr srcDC = SafeNativeMethods.CreateDC("DISPLAY", null, null, IntPtr.Zero);
            //create a memory DC to capture screen to
            IntPtr destDC = SafeNativeMethods.CreateCompatibleDC(srcDC);
            //Create compatible bitmap of the screen
            IntPtr hBitmap = SafeNativeMethods.CreateCompatibleBitmap(srcDC, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
            IntPtr hOldBitmap = SafeNativeMethods.SelectObject(destDC, hBitmap);

            try
            {
                //Copy screen to hBitmap
                SafeNativeMethods.BitBlt(destDC, 0, 0, Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, srcDC, 0, 0, SafeNativeMethods.SRCCOPY);
                hBitmap = SafeNativeMethods.SelectObject(destDC, hOldBitmap);
                //Save the screen bitmap to file
                Bitmap.FromHbitmap(hBitmap).Save(filename, format);
            }
            catch
            {
                return;
            }
            finally
            {
                SafeNativeMethods.DeleteDC(srcDC);
                SafeNativeMethods.DeleteDC(destDC);
            }
        }
    }


    /// <devdoc>
    /// See VsLauncher.cs
    /// </devdoc>
    internal static class SafeNativeMethods
    {

        /// <summary>
        /// Get the foreground window
        /// </summary>
        /// <returns></returns>
        [DllImport("User32")]
        internal static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Get the foreground window
        /// </summary>
        /// <returns></returns>
        [DllImport("User32")]
        internal static extern bool SetForegroundWindow(IntPtr hwnd);

        /// <summary>
        /// Gets the top window
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        [DllImport("User32")]
        internal static extern IntPtr GetTopWindow(IntPtr hwnd);

        [DllImport("User32")]
        internal static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>
        /// Gets the window text length
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        [DllImport("User32", CharSet = CharSet.Unicode)]
        static extern int GetWindowTextLength(IntPtr hwnd);

        /// <summary>
        /// Gets the window text natively
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="lpString"></param>
        /// <param name="nMaxCount"></param>
        /// <returns></returns>
        [DllImport("User32", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, IntPtr lpString, int nMaxCount);

        /// <summary>
        /// Gets the window text in a managed friendly way
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        internal static string GetWindowText(IntPtr hwnd)
        {
            int len = GetWindowTextLength(hwnd);
            if (len <= 0) return "";
            len++; // include space for the null terminator.
            IntPtr buffer = Marshal.AllocCoTaskMem(len * 2);
            GetWindowText(hwnd, buffer, len);
            string s = Marshal.PtrToStringUni(buffer, len - 1);
            Marshal.FreeCoTaskMem(buffer);
            return s;
        }

        /// <summary>
        /// Get the process and thread id of the given window.
        /// </summary>
        /// <param name="hwnd">The window handle to query</param>
        /// <param name="procId">The process that created the window</param>
        /// <returns>The thread id for the window</returns>
        [DllImport("User32", CharSet = CharSet.Unicode)]
        internal static extern int GetWindowThreadProcessId(IntPtr hwnd, out int procId);

        /// <summary>
        /// The WindowFromPoint function retrieves a handle to the window that contains the specified point. 
        /// </summary>
        /// <param name="Point">Location of window</param>
        /// <returns>The found window handle or IntPtr.Zero if not found</returns>
        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(POINT Point);

        /// <summary>
        /// The POINT structure defines the x- and y- coordinates of a point. 
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern int GetWindowRect(IntPtr hwnd, ref RECT bounds);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Return the bounds of the given window
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        internal static Rect GetWindowRect(IntPtr hwnd)
        {
            RECT bounds = new RECT();
            GetWindowRect(hwnd, ref bounds);
            return new Rect(bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top);
        }

        internal delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lParam);

        internal static IEnumerable<IntPtr> GetDesktopWindows()
        {
            IList<IntPtr> result = new List<IntPtr>();
            WindowEnumProc callback = new WindowEnumProc((hwnd, lparam) => { result.Add(hwnd); return true; });
            EnumWindows(callback, IntPtr.Zero);
            return result;
        }

        [DllImport("user32.dll")]
        static extern bool EnumWindows(WindowEnumProc lpEnumFunc, IntPtr lParam);


        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateDC(
        string lpszDriver,
        string lpszDevice,
        string lpszOutput,
        IntPtr lpInitData
        );

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(
        IntPtr hdcDest,
        int nXDest,
        int nYDest,
        int nWidth,
        int nHeight,
        IntPtr hdcSrc,
        int nXSrc,
        int nYSrc,
        int dwrop);

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(
        IntPtr hdc // handle to DC 
        );

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleBitmap(
        IntPtr hdc, // handle to DC 
        int nWidth, // width of bitmap, in pixels 
        int nHeight // height of bitmap, in pixels 
        );

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(
        IntPtr hdc, // handle to DC 
        IntPtr hgdiobj // handle to object 
        );

        [DllImport("gdi32.dll")]
        public static extern int DeleteDC(
        IntPtr hdc // handle to DC 
        );

        public static readonly int SRCCOPY = 13369376;
    }

}

