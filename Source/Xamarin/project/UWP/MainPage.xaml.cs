using VTeamWidgets;

namespace XMoney.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public MainPage()
        {
            this.InitializeComponent();

            XMoney.App app = new XMoney.App();

            var fpi = new MyStorageImplementation_UWP();
            MyXPlatform.Current = fpi;

            this.LoadApplication(app);
        }
    }
}
