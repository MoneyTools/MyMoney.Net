using AppKit;
using Foundation;
using VTeamWidgets;
using Xamarin.Forms;
using Xamarin.Forms.Platform.MacOS;

namespace XMoney
{
    [Register("AppDelegate")]
    public class AppDelegate : FormsApplicationDelegate
    {
        private readonly NSWindow window;

        public override NSWindow MainWindow => window;

        public AppDelegate()
        {
            NSWindowStyle style = NSWindowStyle.Closable | NSWindowStyle.Resizable | NSWindowStyle.Titled;
            var rect = new CoreGraphics.CGRect(200, 200, 800, 800);
            window = new NSWindow(rect, style, NSBackingStore.Buffered, false)
            {
                TitleVisibility = NSWindowTitleVisibility.Hidden
            };
        }

        public override void DidFinishLaunching(NSNotification notification)
        {
            Forms.Init();

            var xApp = new App();

            LoadApplication(xApp);

            MyXPlatform.Current = new MyStorageImplementation_Apple();

            base.DidFinishLaunching(notification);

        }

        public override void WillTerminate(NSNotification notification)
        {
            // Insert code here to tear down your application
        }

        public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
        {
            return true;
        }
    }
}
