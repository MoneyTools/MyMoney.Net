using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for PasswordControl.xaml
    /// </summary>
    public partial class PasswordControl : UserControl
    {
        public PasswordControl()
        {
            this.InitializeComponent();
            Loaded += this.PasswordControl_Loaded;
        }

        private void PasswordControl_Loaded(object sender, RoutedEventArgs e)
        {
            this.PasswordField.Name = this.Name;
            this.Name += "Control";
        }

        public event RoutedEventHandler PasswordChanged;

        public string Password
        {
            get { return this.PasswordField.Password; }
            set { this.PasswordField.Password = value; }
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordChanged != null)
            {
                PasswordChanged(sender, e);
            }
        }

        private void OnEyeButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.PasswordTextBox.Text = this.PasswordField.Password;
            this.PasswordTextBox.Visibility = System.Windows.Visibility.Visible;
            this.PasswordField.Visibility = System.Windows.Visibility.Collapsed;
            e.Handled = true;
        }

        private void OnEyeButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.PasswordTextBox.Visibility = System.Windows.Visibility.Collapsed;
            this.PasswordField.Visibility = System.Windows.Visibility.Visible;
            e.Handled = true;
        }

        public void SelectAll()
        {
            this.PasswordField.SelectAll();
            this.PasswordField.Focus();
        }
    }
}
