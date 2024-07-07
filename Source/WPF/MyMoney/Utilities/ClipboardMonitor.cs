using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Walkabout.Utilities
{
    /// <summary>
    /// Clipboard Monitor class to notify if the clipboard content changes
    /// Many thanks to Ralf Beckers at https://github.com/Bassman2/WpfClipboardMonitor.
    /// </summary>
    public class ClipboardMonitor
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private readonly IntPtr windowHandle;

        /// <summary>
        /// Event for clipboard update notification.
        /// </summary>
        public event EventHandler ClipboardUpdate;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="window">Main window of the application.</param>
        /// <param name="start">Enable clipboard notification on startup or not.</param>
        public ClipboardMonitor(Window window, bool start = true)
        {
            windowHandle = new WindowInteropHelper(window).EnsureHandle();
            HwndSource.FromHwnd(windowHandle)?.AddHook(this.HwndHandler);
            if (start) this.Start();
        }

        /// <summary>
        /// Enable clipboard notification.
        /// </summary>
        public void Start()
        {
            NativeMethods.AddClipboardFormatListener(windowHandle);
        }

        /// <summary>
        /// Disable clipboard notification.
        /// </summary>
        public void Stop()
        {
            NativeMethods.RemoveClipboardFormatListener(windowHandle);
        }

        private IntPtr HwndHandler(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                this.ClipboardUpdate?.Invoke(this, new EventArgs());
            }
            handled = false;
            return IntPtr.Zero;
        }

        private static class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AddClipboardFormatListener(IntPtr hwnd);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        }
    }
}