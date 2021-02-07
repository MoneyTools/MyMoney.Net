using Foundation;
using UIKit;
using VTeamWidgets;

namespace XMoney
{
    [Register("AppDelegate")]
    public partial class AppDelegate : Xamarin.Forms.Platform.iOS.FormsApplicationDelegate
    {
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            Xamarin.Forms.Forms.Init();
            LoadApplication(new App());


            MyXPlatform.Current = new MyStorageImplementation_Apple();

            return base.FinishedLaunching(app, options);
        }
    }
}