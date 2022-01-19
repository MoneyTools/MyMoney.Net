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
        OfxServer server;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var url = "http://localhost:3000/ofx/test/";
            int delay = 1000;
            server = new OfxServer();
            server.UserName = "test";
            server.Password = "1234";
            Dispatcher.BeginInvoke(new Action<TextBox>(OnFocusTextBox), UserName);
            this.DataContext = server;
            MFAChallengeGrid.ItemsSource = server.MFAChallenges;
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
                        Int32.TryParse(s, out delay);
                        i++;
                    }
                }
            }
            server.Start(url, delay);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            server.Terminate();
            base.OnClosing(e);
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
            {
                OnFocusTextBox(box);
            }
        }

        private void OnFocusTextBox(TextBox box)
        {
            box.Focus();
            box.SelectAll();
        }

        private void OnShowAdditionalCredentials(object sender, RoutedEventArgs e)
        {
            AdditionalCredentials.Visibility = Visibility.Visible;
        }

        private void OnHideAdditionalCredentials(object sender, RoutedEventArgs e)
        {
            AdditionalCredentials.Visibility = Visibility.Collapsed;
        }

        private void OnShowAuthTokenQuestions(object sender, RoutedEventArgs e)
        {
            AuthTokenQuestions.Visibility = Visibility.Visible;
        }

        private void OnHideAuthTokenQuestions(object sender, RoutedEventArgs e)
        {
            AuthTokenQuestions.Visibility = Visibility.Collapsed;
        }

        private void OnShowMFAChallengeQuestions(object sender, RoutedEventArgs e)
        {
            server.AddStandardChallenges();
            MFAChallengeQuestions.Visibility = Visibility.Visible;
        }

        private void OnHideMFAChallengeQuestions(object sender, RoutedEventArgs e)
        {
            server.RemoveChallenges();
            MFAChallengeQuestions.Visibility = Visibility.Collapsed;
        }

        private void OnShowChangePasswordQuestions(object sender, RoutedEventArgs e)
        {
            NewPasswordQuestions.Visibility = Visibility.Visible;
            server.ChangePassword = true;
        }

        private void OnHideChangePasswordQuestions(object sender, RoutedEventArgs e)
        {
            NewPasswordQuestions.Visibility = Visibility.Collapsed;
        }
    }
}
