using SQLite;
using System;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace XMoney
{
    public partial class App : Application
    {
        public SQLiteConnection dbConnection;

        public App()
        {
            InitializeComponent();

            // The root page of your application
            var navPage = new NavigationPage();
            AppTheme appTheme = AppInfo.RequestedTheme;
            if (appTheme == AppTheme.Dark)
            {
                navPage.BarTextColor = Color.LightBlue;
            }

            //NavigationPage.SetTitleView(this, CreateTitleView());

            MainPage = navPage;
            
            MainPage.Navigation.PushAsync(new PageMain());
        }

        //private View CreateTitleView()
        //{
        //    var view = new StackLayout() { Orientation = StackOrientation.Horizontal, BackgroundColor = Color.Pink, HorizontalOptions = LayoutOptions.CenterAndExpand };
        //    view.Children.Add(new Label { Text = "Hello", TextColor = Color.Yellow, HeightRequest = 30, FontSize = 8 });
        //    return view;
        //}

        protected override void OnStart()
        {

        }

        protected override void OnSleep()
        {
            // Handle when your app sleeps
        }

        protected override void OnResume()
        {
            // Handle when your app resumes
        }

        private const int smallWightResolution = 768;
        //private const int smallHeightResolution = 1280;

        public static bool IsSmallDevice()
        {
            // Get Metrics
            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;

            // Width (in pixels)
            var width = mainDisplayInfo.Width / mainDisplayInfo.Density;

            return width <= smallWightResolution;
        }

        public static double GetDeviceScreenDesity()
        {
            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            return mainDisplayInfo.Density == 0 ? 1 : mainDisplayInfo.Density;
        }

        public static bool IsInLandscapeMode()
        {
            var mainDisplayInfo = DeviceDisplay.MainDisplayInfo;
            return mainDisplayInfo.Width > mainDisplayInfo.Height;
        }

        public static bool AlertConfirm(string title, string content, string confirmButton = "Ok", Action<bool> callback = null)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                var thisRunningApp = (XMoney.App)App.Current;
                await thisRunningApp.MainPage.DisplayAlert(title, content, confirmButton);
                callback?.DynamicInvoke();
            });

            return true;
        }
    }
}