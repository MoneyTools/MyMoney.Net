using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Walkabout.Utilities
{
    /// <summary>
    /// Sometimes WPF returns bogus mouse positions, this class is a workaround for that.
    /// For example MSDN says "During drag-and-drop operations, the position of the mouse cannot be 
    /// reliably determined through GetPosition.  This is because control of the mouse (possibly 
    /// including capture) is held by the originating element of the drag until the drop is completed, 
    /// with much of the behavior controlled by underlying Win32 calls.         
    /// </summary>
    public class MouseUtilities
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Win32Point
        {
            public Int32 X;
            public Int32 Y;
        };

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref Win32Point pt);

        [DllImport("user32.dll")]
        private static extern bool ScreenToClient(IntPtr hwnd, ref Win32Point pt);

        public static Point GetMousePosition(Visual relativeTo)
        {
            Win32Point mouse = new Win32Point();
            GetCursorPos(ref mouse);

            System.Windows.Interop.HwndSource presentationSource =
                (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(relativeTo);

            ScreenToClient(presentationSource.Handle, ref mouse);

            GeneralTransform transform = relativeTo.TransformToAncestor(presentationSource.RootVisual);

            Point offset = transform.Transform(new Point(0, 0));

            double x = ConvertToDeviceIndependentPixels(mouse.X) - offset.X;
            double y = ConvertToDeviceIndependentPixels(mouse.Y) - offset.Y;
            return new Point(x, y);
        }

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

    }
}
