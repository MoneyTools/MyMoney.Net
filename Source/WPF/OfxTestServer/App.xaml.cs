using System.Windows;

namespace OfxTestServer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public string[] CommandLineArgs;

        protected override void OnStartup(StartupEventArgs e)
        {
            CommandLineArgs = e.Args;
            base.OnStartup(e);
        }
    }
}
