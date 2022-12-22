using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace OfxTestServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OfxServer server;

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var url = "http://localhost:3000/ofx/test/";
            int delay = 1000;
            this.server = new OfxServer();
            this.server.UserName = "test";
            this.server.Password = "1234";
            this.Dispatcher.BeginInvoke(new Action<TextBox>(this.OnFocusTextBox), this.UserName);
            this.DataContext = this.server;
            this.MFAChallengeGrid.ItemsSource = this.server.MFAChallenges;
            var app = Application.Current as App;
            string[] args = app.CommandLineArgs;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("/") || arg.StartsWith("-"))
                {
                    arg = arg.Substring(1);
                    if (arg == "delay" && i < args.Length - 1)
                    {
                        var s = args[i + 1];
                        int.TryParse(s, out delay);
                        i++;
                    }
                }
            }
            this.server.Start(url, delay);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            this.server.Terminate();
            base.OnClosing(e);
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
            {
                this.OnFocusTextBox(box);
            }
        }

        private void OnFocusTextBox(TextBox box)
        {
            box.Focus();
            box.SelectAll();
        }

        private void OnShowAdditionalCredentials(object sender, RoutedEventArgs e)
        {
            this.AdditionalCredentials.Visibility = Visibility.Visible;
        }

        private void OnHideAdditionalCredentials(object sender, RoutedEventArgs e)
        {
            this.AdditionalCredentials.Visibility = Visibility.Collapsed;
        }

        private void OnShowAuthTokenQuestions(object sender, RoutedEventArgs e)
        {
            this.AuthTokenQuestions.Visibility = Visibility.Visible;
        }

        private void OnHideAuthTokenQuestions(object sender, RoutedEventArgs e)
        {
            this.AuthTokenQuestions.Visibility = Visibility.Collapsed;
        }

        private void OnShowMFAChallengeQuestions(object sender, RoutedEventArgs e)
        {
            this.server.AddStandardChallenges();
            this.MFAChallengeQuestions.Visibility = Visibility.Visible;
        }

        private void OnHideMFAChallengeQuestions(object sender, RoutedEventArgs e)
        {
            this.server.RemoveChallenges();
            this.MFAChallengeQuestions.Visibility = Visibility.Collapsed;
        }

        private void OnShowChangePasswordQuestions(object sender, RoutedEventArgs e)
        {
            this.NewPasswordQuestions.Visibility = Visibility.Visible;
            this.server.ChangePassword = true;
        }

        private void OnHideChangePasswordQuestions(object sender, RoutedEventArgs e)
        {
            this.NewPasswordQuestions.Visibility = Visibility.Collapsed;
        }
    }
}
