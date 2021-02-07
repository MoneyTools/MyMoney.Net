using Android.App;
using Android.Content.PM;
using Android.OS;
using VTeamWidgets;

namespace XMoney
{
    [Activity(Label = "XMoney.Droid", Icon = "@drawable/icon", Theme = "@style/MainTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation)]
    public class MainActivity : Xamarin.Forms.Platform.Android.FormsAppCompatActivity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            Xamarin.Forms.Forms.Init(this, bundle);
            Xamarin.Essentials.Platform.Init(this, bundle);

            var app = new App();
            MyXPlatform.Current = new MyStorageImplementation_Android();

            LoadApplication(app);

        }
    }
}