namespace Walkabout.Tests.Interop
{
    using System.Windows;
    using System.Windows.Automation;
    using System.Windows.Input;
    using System.Security.Permissions;
    using System.ComponentModel;
    using System.Runtime.InteropServices;
    
    using System;

    /// <summary>
    /// Flags for Input.SendMouseInput, indicate whether movement took place,
    /// or whether buttons were pressed or released.
    /// </summary>
    [Flags]
    public enum SendMouseInputFlags {
        /// <summary>Specifies that the pointer moved.</summary>
        Move       = 0x0001,
        /// <summary>Specifies that the left button was pressed.</summary>
        LeftDown   = 0x0002,
        /// <summary>Specifies that the left button was released.</summary>
        LeftUp     = 0x0004,
        /// <summary>Specifies that the right button was pressed.</summary>
        RightDown  = 0x0008,
        /// <summary>Specifies that the right button was released.</summary>
        RightUp    = 0x0010,
        /// <summary>Specifies that the middle button was pressed.</summary>
        MiddleDown = 0x0020,
        /// <summary>Specifies that the middle button was released.</summary>
        MiddleUp   = 0x0040,
        /// <summary>Specifies that the x button was pressed.</summary>
        XDown      = 0x0080,
        /// <summary>Specifies that the x button was released. </summary>
        XUp        = 0x0100,
        /// <summary>Specifies that the wheel was moved</summary>
        Wheel      = 0x0800,
        /// <summary>Specifies that x, y are absolute, not relative</summary>
        Absolute   = 0x8000,
    };


    /// <summary>
    /// Flags for Input.SendMouseInput, for indicating the intention of the mouse wheel rotation
    /// </summary>
    [Flags]
    public enum MouseWheel
    {
        /// <summary>Specifies that the mouse wheel is rotated forward, away from the user</summary>
        Forward_ZoomIn = 1,

        /// <summary>Specifies that the mouse wheel is rotated backward, towards the user</summary>
        Backward_ZoomOut = -1
    }

    /// <summary>
    /// Provides methods for sending mouse and keyboard input
    /// </summary>
    public static class Input {
        /// <summary>The first X mouse button</summary>
        public const int XButton1 = 0x01;

        /// <summary>The second X mouse button</summary>
        public const int XButton2 = 0x02;

        public static void SendMouseInput(int x, int y, int data, SendMouseInputFlags flags) {
            SendMouseInput((double)x, (double)y, data, flags);
        }

        /// <summary>
        /// Call this function between mouse clicks to ensure that it is not interpretted as a double click.
        /// </summary>
        public static void WaitDoubleClickTime()
        {
            int time = Win32.GetDoubleClickTime();
            System.Threading.Thread.Sleep(time * 2);
        }

        /// <summary>
        /// Inject pointer input into the system
        /// </summary>
        /// <param name="x">x coordinate of pointer, if Move flag specified</param>
        /// <param name="y">y coordinate of pointer, if Move flag specified</param>
        /// <param name="data">wheel movement, or mouse X button, depending on flags</param>
        /// <param name="flags">flags to indicate which type of input occurred - move, button press/release, wheel move, etc.</param>
        /// <remarks>x, y are in pixels. If Absolute flag used, are relative to desktop origin.</remarks>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void SendMouseInput(double x, double y, int data, SendMouseInputFlags flags)
        {
            SendMouseInput(x, y, data, flags, 0);
        }

        public static void SendMouseInput(double x, double y, int data, SendMouseInputFlags flags, int mouseData)
        {
            int intflags = (int) flags;

            if((intflags & (int)SendMouseInputFlags.Absolute) != 0) {
                int vscreenWidth = Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN);
                int vscreenHeight = Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN);
                int vscreenLeft = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
                int vscreenTop = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);

                // Absolute input requires that input is in 'normalized' coords - with the entire
                // desktop being (0,0)...(65535,65536). Need to convert input x,y coords to this
                // first.
                //
                // In this normalized world, any pixel on the screen corresponds to a block of values
                // of normalized coords - eg. on a 1024x768 screen,
                // y pixel 0 corresponds to range 0 to 85.333,
                // y pixel 1 corresponds to range 85.333 to 170.666,
                // y pixel 2 correpsonds to range 170.666 to 256 - and so on.
                // Doing basic scaling math - (x-top)*65536/Width - gets us the start of the range.
                // However, because int math is used, this can end up being rounded into the wrong
                // pixel. For example, if we wanted pixel 1, we'd get 85.333, but that comes out as
                // 85 as an int, which falls into pixel 0's range - and that's where the pointer goes.
                // To avoid this, we add on half-a-"screen pixel"'s worth of normalized coords - to
                // push us into the middle of any given pixel's range - that's the 65536/(Width*2)
                // part of the formula. So now pixel 1 maps to 85+42 = 127 - which is comfortably
                // in the middle of that pixel's block.
                // The key ting here is that unlike points in coordinate geometry, pixels take up
                // space, so are often better treated like rectangles - and if you want to target
                // a particular pixel, target its rectangle's midpoint, not its edge.
                x = ((x - vscreenLeft) * 65536) / vscreenWidth + 65536 / (vscreenWidth * 2);
                y = ((y - vscreenTop) * 65536) / vscreenHeight + 65536 / (vscreenHeight * 2);

                intflags |= Win32.MOUSEEVENTF_VIRTUALDESK;
            }

            Win32.INPUT mi = new Win32.INPUT();
            mi.type = Win32.INPUT_MOUSE;
            mi.union.mouseInput.dx = (int) x;
            mi.union.mouseInput.dy = (int)y;
            mi.union.mouseInput.mouseData = data;
            mi.union.mouseInput.dwFlags = intflags;
            mi.union.mouseInput.time = 0;
            mi.union.mouseInput.dwExtraInfo = new IntPtr(0);
            mi.union.mouseInput.mouseData = mouseData;

            if(Win32.SendInput(1, ref mi, Marshal.SizeOf(mi)) == 0) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }


        /// <summary>
        /// Move the mouse to a point and click.  The primary mouse button will be used
        /// this is usually the left button except if the mouse buttons are swaped.
        /// </summary>
        /// <param name="pt">The point to click at</param>
        /// <remarks>pt are in pixels that are relative to desktop origin.</remarks>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void SendMouseScrollWheel(Point pt, MouseWheel mwRotation, System.Windows.Input.Key key)
        {
            Input.SendKeyboardInput(key, true);
            int mouseData = 0;

            if (Walkabout.Tests.Interop.MouseWheel.Forward_ZoomIn == mwRotation)
            {
                mouseData = 120;
            }

            if (Walkabout.Tests.Interop.MouseWheel.Backward_ZoomOut == mwRotation)
            {
                mouseData = -120;
            }
            
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.Wheel | SendMouseInputFlags.Absolute, mouseData);

            Input.SendKeyboardInput(key, false);
        }

        /// <summary>
        /// Taps the specified keyboard key
        /// </summary>
        /// <param name="key">key to tap</param>
        public static void TapKey(Key key)
        {
            SendKeyboardInput(key, true);
            SendKeyboardInput(key, false);
        }

        /// <summary>
        /// Taps the specified keyboard key
        /// </summary>
        /// <param name="key">key to tap</param>
        public static void TapKey(Key key, ModifierKeys mods)
        {
            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                SendKeyboardInput(Key.LeftAlt, true);
            }
            if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SendKeyboardInput(Key.LeftCtrl, true);
            }
            if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                SendKeyboardInput(Key.LeftShift, true);
            }
            if ((mods & ModifierKeys.Windows) == ModifierKeys.Windows)
            {
                SendKeyboardInput(Key.LWin, true);
            }
            SendKeyboardInput(key, true);
            SendKeyboardInput(key, false);

            if ((mods & ModifierKeys.Alt) == ModifierKeys.Alt)
            {
                SendKeyboardInput(Key.LeftAlt, false);
            }
            if ((mods & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SendKeyboardInput(Key.LeftCtrl, false);
            }
            if ((mods & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                SendKeyboardInput(Key.LeftShift, false);
            }
            if ((mods & ModifierKeys.Windows) == ModifierKeys.Windows)
            {
                SendKeyboardInput(Key.LWin, false);
            }
        }

        
        
        /// <summary>
        /// Taps the specified keyboard key
        /// </summary>
        /// <param name="key">key to tap</param>
        public static void TapKeySlow(Key key)
        {
            TapKey(key);
            System.Threading.Thread.Sleep(100);
        }


         /// <summary>
        /// Taps the specified keyboard key
        /// </summary>
        /// <param name="key">key to tap</param>
        public static void TapTabKeyBackward()
        {
            Input.SendKeyboardInput(Key.LeftShift, true);
            TapKeySlow(Key.Tab);
            Input.SendKeyboardInput(Key.LeftShift, false);
        }


        /// <summary>
        /// Inject keyboard input into the system
        /// </summary>
        /// <param name="key">indicates the key pressed or released. Can be one of the constants defined in the Key enum</param>
        /// <param name="press">true to inject a key press, false to inject a key release</param>
        public static void SendKeyboardInput(Key key, bool press) {
            Win32.INPUT ki = new Win32.INPUT();
            ki.type = Win32.INPUT_KEYBOARD;
            ki.union.keyboardInput.wVk = (short) KeyInterop.VirtualKeyFromKey(key);
            ki.union.keyboardInput.wScan = (short) Win32.MapVirtualKey(ki.union.keyboardInput.wVk, 0);
            
            int dwFlags = 0;
            if(ki.union.keyboardInput.wScan > 0) {
                dwFlags |= Win32.KEYEVENTF_SCANCODE;
            }
            if(false == press) {
                dwFlags |= Win32.KEYEVENTF_KEYUP;
            }
            
            ki.union.keyboardInput.dwFlags = dwFlags;
            if(IsExtendedKey(key)) {
                ki.union.keyboardInput.dwFlags |= Win32.KEYEVENTF_EXTENDEDKEY;
            }

            ki.union.keyboardInput.time = Environment.TickCount;
            ki.union.keyboardInput.dwExtraInfo = new IntPtr(0);
            if(0 == Win32.SendInput(1, ref ki, Marshal.SizeOf(ki))) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Injects a unicode character as keyboard input into the system
        /// </summary>
        /// <param name="key">indicates the key to be pressed or released. Can be any unicode character</param>
        /// <param name="press">true to inject a key press, false to inject a key release</param>
        public static void SendUnicodeKeyboardInput(char key, bool press) {
            Win32.INPUT ki = new Win32.INPUT();

            ki.type = Win32.INPUT_KEYBOARD;
            ki.union.keyboardInput.wVk = (short)0;
            ki.union.keyboardInput.wScan = (short)key;
            ki.union.keyboardInput.dwFlags = Win32.KEYEVENTF_UNICODE | (press ? 0 : Win32.KEYEVENTF_KEYUP);
            ki.union.keyboardInput.time = Environment.TickCount;
            ki.union.keyboardInput.dwExtraInfo = new IntPtr(0);
            if (0 == Win32.SendInput(1, ref ki, Marshal.SizeOf(ki))) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        /// <summary>
        /// Injects a string of Unicode characters using simulated keyboard input
        /// It should be noted that this overload just sends the whole string
        /// with no pauses, depending on the recieving applications input processing
        /// it may not be able to keep up with the speed, resulting in corruption or
        /// loss of the input data.
        /// </summary>
        /// <param name="data">The unicode string to be sent</param>
        public static void SendUnicodeString(string data) {
            InternalSendUnicodeString(data, 50);
        }

        /// <summary>
        /// Injects a string of Unicode characters using simulated keyboard input
        /// with user defined timing.
        /// </summary>
        /// <param name="data">The unicode string to be sent</param>
        /// <param name="sleepLength">How long, in milliseconds, to sleep between each character</param>
        public static void SendUnicodeString(string data, int sleepLength) {
            if(sleepLength < 0) {
                throw new ArgumentOutOfRangeException("sleepLength");
            }
            
            InternalSendUnicodeString(data, sleepLength);
        }

        /// <summary>
        /// Checks whether the specified key is currently up or down
        /// </summary>
        /// <param name="key">The Key to check</param>
        /// <returns>true if the specified key is currently down (being pressed), false if it is up</returns>
        public static bool GetAsyncKeyState(Key key) {
            int vKey = KeyInterop.VirtualKeyFromKey(key);
            int resp = Win32.GetAsyncKeyState(vKey);

            if(resp == 0) {
                throw new InvalidOperationException("GetAsyncKeyStateFailed");
            }

            return resp < 0;
        }

        /// <summary>
        /// Move the mouse with a the left button down.
        /// </summary>
        /// <param name="p"></param>
        public static void DragTo(Point p1, Point p2)
        {
            Input.SendMouseInput(p1.X, p1.Y, 0, SendMouseInputFlags.LeftDown | SendMouseInputFlags.Absolute);

            System.Threading.Thread.Sleep(200);

            Input.SlideTo(p2);
            Input.SendMouseInput(p2.X, p2.Y, 0, SendMouseInputFlags.LeftUp | SendMouseInputFlags.Absolute);            
        }

        /// <summary>
        /// Move the mouse with a the right button down.
        /// </summary>
        /// <param name="p"></param>
        public static void DragRightTo(Point p1, Point p2)
        {
            Input.SendMouseInput(p1.X, p1.Y, 0, SendMouseInputFlags.RightDown | SendMouseInputFlags.Absolute);

            System.Threading.Thread.Sleep(200);

            Input.SlideTo(p2);
            Input.SendMouseInput(p2.X, p2.Y, 0, SendMouseInputFlags.RightUp | SendMouseInputFlags.Absolute);
        }

        /// <summary>
        /// Move the mouse to an element. 
        ///
        /// IMPORTANT!
        /// 
        /// Do not call MoveToAndClick (actually, do not make any calls to UIAutomationClient) 
        /// from the UI thread if your test is in the same process as the UI being tested.  
        /// UIAutomation calls back into Avalon core for UI information (e.g. ClickablePoint) 
        /// and must be on the UI thread to get this information.  If your test is making calls 
        /// from the UI thread you are going to deadlock...
        /// 
        /// </summary>
        /// <param name="el">The element that the mouse will move to</param>
        /// <exception cref="NoClickablePointException">If there is not clickable point for the element</exception>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void MoveTo(AutomationElement el) {
            if (el == null) {
                throw new ArgumentNullException("el");
            }
            MoveTo(GetClickablePoint(el));
        }

        /// <summary>
        /// Slide the mouse to an element. 
        /// </summary>
        /// <param name="el">The element that the mouse will move to</param>
        /// <exception cref="NoClickablePointException">If there is not clickable point for the element</exception>
        public static void SlideTo(AutomationElement el)
        {
            if (el == null) {
                throw new ArgumentNullException("el");
            }
            SlideTo(GetClickablePoint(el));
        }

        /// <summary>
        /// Move the mouse to a point. 
        /// </summary>
        /// <param name="pt">The point that the mouse will move to.</param>
        /// <remarks>pt are in pixels that are relative to desktop origin.</remarks>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void MoveTo(Point pt) {
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.Move | SendMouseInputFlags.Absolute);
        }

        /// <summary>
        /// Get the current mouse position.
        /// </summary>
        /// <returns></returns>
        public static Point GetMousePosition()
        {
            Win32.POINT cursorPosition = new Win32.POINT();
            if (Win32.GetCursorPos(out cursorPosition))
            {
                // chart a smooth course from cursor pos to the specified position.
                return new Point(cursorPosition.X, cursorPosition.Y);
            }
            throw new Exception("Win32.GetCursorPos failed");
        }

        /// <summary>
        /// Slide the mouse to a point, simulating a real mouse move with all the points inbetween.
        /// </summary>
        /// <param name="pt">The point that the mouse will move to.</param>
        /// <remarks>pt are in pixels that are relative to desktop origin.</remarks>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void SlideTo(Point pt)
        {
            Win32.POINT cursorPosition = new Win32.POINT();
            if (Win32.GetCursorPos(out cursorPosition))
            {
                // chart a smooth course from cursor pos to the specified position.
                Point start = new Point(cursorPosition.X, cursorPosition.Y);
                Vector v = pt - start;
                int increment = 5;
                int steps = (int)(v.Length / increment);
                v.Normalize();
                for (int i = 0; i < steps; i++)
                {
                    Vector v2 = Vector.Multiply(i * increment, v);
                    Point p = Point.Add(start, v2);
                    Input.SendMouseInput(p.X, p.Y, 0, SendMouseInputFlags.Move | SendMouseInputFlags.Absolute);
                    System.Threading.Thread.Sleep(10);
                }
            }
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.Move | SendMouseInputFlags.Absolute);
        }

        /// <summary>
        /// Move the mouse to an element and click on it.  The primary mouse button will be used
        /// this is usually the left button except if the mouse buttons are swaped.
        ///
        /// IMPORTANT!
        /// 
        /// Do not call MoveToAndClick (actually, do not make any calls to UIAutomationClient) 
        /// from the UI thread if your test is in the same process as the UI being tested.  
        /// UIAutomation calls back into Avalon core for UI information (e.g. ClickablePoint) 
        /// and must be on the UI thread to get this information.  If your test is making calls 
        /// from the UI thread you are going to deadlock...
        /// 
        /// </summary>
        /// <param name="el">The element to click on</param>
        /// <exception cref="NoClickablePointException">If there is not clickable point for the element</exception>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void MoveToAndClick(AutomationElement el) {
            if (el == null) {
                throw new ArgumentNullException("el");
            }
            MoveToAndLeftClick(GetClickablePoint(el));
        }


        /// <summary>
        /// Move to and click in the left side of the element
        /// </summary>
        /// <param name="el">The element to click on</param>
        internal static void MoveToAndClickLeftSide(AutomationElement el)
        {
            if (el == null)
            {
                throw new ArgumentNullException("el");
            }
            Point p = GetClickablePoint(el);
            Rect bounds = el.Current.BoundingRectangle;
            p.X = bounds.Left + (bounds.Width / 10);
            MoveToAndLeftClick(p);
        }

        /// <summary>
        /// Move the mouse to a point and click.  The primary mouse button will be used
        /// this is usually the left button except if the mouse buttons are swaped.
        /// </summary>
        /// <param name="pt">The point to click at</param>
        /// <remarks>pt are in pixels that are relative to desktop origin.</remarks>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void MoveToAndLeftClick(Point pt) {
            MoveTo(pt);

            // send SendMouseInput works in term of the phisical mouse buttons, therefore we need
            // to check to see if the mouse buttons are swapped because this method need to use the primary
            // mouse button.
            if (0 == Win32.GetSystemMetrics(Win32.SM_SWAPBUTTON)) {
                // the mouse buttons are not swaped the primary is the left
                LeftClick(pt);
            }
            else {
                // the mouse buttons are swaped so click the right button which as actually the primary
                RightClick(pt);
            }
        }

        /// <summary>
        /// Click middle button.
        /// </summary>
        /// <param name="pt">Place to click</param>
        public static void MiddleClick(Point pt)
        {
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.MiddleDown | SendMouseInputFlags.Absolute);
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.MiddleUp | SendMouseInputFlags.Absolute);
        }

        /// <summary>
        /// Click right button
        /// </summary>
        /// <param name="pt">Place to click</param>
        public static void RightClick(Point pt)
        {
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.RightDown | SendMouseInputFlags.Absolute);
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.RightUp | SendMouseInputFlags.Absolute);
        }

        /// <summary>
        /// Click left button.
        /// </summary>
        /// <param name="pt">Place to click</param>
        public static void LeftClick(Point pt)
        {
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.LeftDown | SendMouseInputFlags.Absolute);
            Input.SendMouseInput(pt.X, pt.Y, 0, SendMouseInputFlags.LeftUp | SendMouseInputFlags.Absolute);
        }

       
        internal static Point GetClickablePoint(AutomationElement el) {
            int retries = 10;
            while (retries-- > 0)
            {
                try
                {
                    Point pt = el.GetClickablePoint();
                    return pt;
                }
                catch (Exception)
                {
                    // UI is blocked doing something and can't respond right now.
                    System.Threading.Thread.Sleep(1000);
                }
            }
            throw new Exception("Not finding a clickable point!");
        }

        /// <summary>
        /// Move the mouse to a point and right clicks.  The primary mouse button will be used
        /// this is usually the left button except if the mouse buttons are swaped.
        /// </summary>
        /// <param name="pt">The point to click at</param>
        /// <remarks>pt are in pixels that are relative to desktop origin.</remarks>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void MoveToAndRightClick(Point pt)
        {
            MoveTo(pt);

            // send SendMouseInput works in term of the phisical mouse buttons, therefore we need
            // to check to see if the mouse buttons are swapped because this method need to use the primary
            // mouse button.
            if (0 == Win32.GetSystemMetrics(Win32.SM_SWAPBUTTON))
            {
                RightClick(pt);
            }
            else
            {
                // the mouse buttons are swapped so the right click is actually the left button.
                LeftClick(pt);
            }
        }

        /// <summary>
        /// Move the mouse to automation element and click.  The primary mouse button will be used
        /// this is usually the left button except if the mouse buttons are swaped.
        /// </summary>
        /// <param name="pt">The automation element to double click</param>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void MoveToAndDoubleClick(AutomationElement el)
        {
            if (el == null)
            {
                throw new ArgumentNullException("el");
            }
            MoveToAndDoubleClick(GetClickablePoint(el));
        }

        /// <summary>
        /// Move the mouse to a point and click.  The primary mouse button will be used
        /// this is usually the left button except if the mouse buttons are swaped.
        /// </summary>
        /// <param name="pt">The point to click at</param>
        /// <remarks>pt are in pixels that are relative to desktop origin.</remarks>
        /// 
        /// <outside_see conditional="false">
        /// This API does not work inside the secure execution environment.
        /// <exception cref="System.Security.Permissions.SecurityPermission"/>
        /// </outside_see>
        public static void MoveToAndDoubleClick(Point pt)
        {
            MoveTo(pt);
            DoubleLeftClick(pt);
        }

        public static void DoubleLeftClick(Point pt)
        {
            // send SendMouseInput works in term of the phisical mouse buttons, therefore we need
            // to check to see if the mouse buttons are swapped because this method need to use the primary
            // mouse button.
            if (0 == Win32.GetSystemMetrics(Win32.SM_SWAPBUTTON))
            {
                // the mouse buttons are not swaped the primary is the left
                LeftClick(pt);
                LeftClick(pt);
            }
            else
            {
                // the mouse buttons are swaped so click the right button which as actually the primary
                RightClick(pt);
                RightClick(pt);
            }
        }

        // Used internally by the HWND SetFocus code - it sends a hotkey to
        // itself - because it uses a VK that's not on the keyboard, it needs
        // to send the VK directly, not the scan code, which regular
        // SendKeyboardInput does.
        // Note that this method is public, but this class is private, so
        // this is not externally visible.
        internal static void SendKeyboardInputVK(byte vk, bool press) {
            Win32.INPUT ki = new Win32.INPUT();
            ki.type = Win32.INPUT_KEYBOARD;
            ki.union.keyboardInput.wVk = vk;
            ki.union.keyboardInput.wScan = 0;
            ki.union.keyboardInput.dwFlags = press ? 0 : Win32.KEYEVENTF_KEYUP;
            ki.union.keyboardInput.time = 0;
            ki.union.keyboardInput.dwExtraInfo = new IntPtr(0);
            if(0 == Win32.SendInput(1, ref ki, Marshal.SizeOf(ki))) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        internal static bool IsExtendedKey(Key key)
        {
            // From the SDK:
            // The extended-key flag indicates whether the keystroke message originated from one of
            // the additional keys on the enhanced keyboard. The extended keys consist of the ALT and
            // CTRL keys on the right-hand side of the keyboard; the INS, DEL, HOME, END, PAGE UP,
            // PAGE DOWN, and arrow keys in the clusters to the left of the numeric keypad; the NUM LOCK
            // key; the BREAK (CTRL+PAUSE) key; the PRINT SCRN key; and the divide (/) and ENTER keys in
            // the numeric keypad. The extended-key flag is set if the key is an extended key. 
            //
            // - docs appear to be incorrect. Use of Spy++ indicates that break is not an extended key.
            // Also, menu key and windows keys also appear to be extended.
            return key == Key.RightAlt
                || key == Key.RightCtrl
                || key == Key.NumLock
                || key == Key.Insert
                || key == Key.Delete
                || key == Key.Home
                || key == Key.End
                || key == Key.Prior
                || key == Key.Next
                || key == Key.Up
                || key == Key.Down
                || key == Key.Left
                || key == Key.Right
                || key == Key.Apps
                || key == Key.RWin
                || key == Key.LWin;

            // Note that there are no distinct values for the following keys:
            // numpad divide
            // numpad enter
        }

        // Injects a string of Unicode characters using simulated keyboard input
        // with user defined timing
        // <param name="data">The unicode string to be sent</param>
        // <param name="sleepLength">How long, in milliseconds, to sleep between each character</param>
        private static void InternalSendUnicodeString(string data, int sleepLength) {
            char[] chardata = data.ToCharArray();

            foreach (char c in chardata) {
                SendUnicodeKeyboardInput(c, true);
                System.Threading.Thread.Sleep(sleepLength);
                SendUnicodeKeyboardInput(c, false);
                System.Threading.Thread.Sleep(sleepLength);
            }
        }

    }
}
