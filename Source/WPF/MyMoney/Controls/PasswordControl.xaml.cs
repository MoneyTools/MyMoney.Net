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
            InitializeComponent();
            this.Loaded += PasswordControl_Loaded;
        }

        void PasswordControl_Loaded(object sender, RoutedEventArgs e)
        {
            PasswordField.Name = this.Name;
            this.Name += "Control"; 
        }

        public event RoutedEventHandler PasswordChanged;

        public string Password
        {
            get { return PasswordField.Password; }
            set { PasswordField.Password = value; }
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
            PasswordTextBox.Text = PasswordField.Password;
            PasswordTextBox.Visibility = System.Windows.Visibility.Visible;
            PasswordField.Visibility = System.Windows.Visibility.Collapsed;
            e.Handled = true;
        }

        private void OnEyeButtonUp(object sender, MouseButtonEventArgs e)
        {
            PasswordTextBox.Visibility = System.Windows.Visibility.Collapsed;
            PasswordField.Visibility = System.Windows.Visibility.Visible;
            e.Handled = true;
        }

        public void SelectAll()
        {            
            PasswordField.SelectAll();
            PasswordField.Focus();
        }
    }
}
