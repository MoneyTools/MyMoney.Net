using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Net;
//using Kerr;
using System.Security;
using System.Runtime.InteropServices;
using Walkabout.Utilities;
using Walkabout.Controls;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// You can intercept the ok button and add some validation.
    /// If you want to stop ok from hiding the dialog and display an
    /// error then set Cancel to true and provide an optional error message.
    /// </summary>
    public class OkEventArgs : EventArgs
    {
        public bool Cancel { get; set; }
        public string Error { get; set; }
    }


    /// <summary>
    /// Interaction logic for PasswordWindow.xaml
    /// </summary>
    public partial class PasswordWindow : BaseDialog
    {
        Dictionary<string, TextBox> userDefined = new Dictionary<string, TextBox>();

        public PasswordWindow()
        {
            InitializeComponent();
            this.Loaded += new RoutedEventHandler(OnLoaded);

            UserNamePrompt = "";
            PasswordPrompt = Properties.Resources.PasswordPrompt;

            this.SizeChanged += PasswordWindow_SizeChanged;
        }

        void PasswordWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            TextBlockIntroMessage.Width = Math.Max(0, e.NewSize.Width - 20);
        }

        

        public void AddUserDefinedField(string id, string label)
        {
            //<TextBlock x:Name="TextBlockUserNamePrompt" Text="User Name: " VerticalAlignment="Center" Margin="0,10,0,2"/>
            TextBlock block = new TextBlock() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 0, 2), TextWrapping = TextWrapping.Wrap };
            block.Text = label;
            EntryPanel.Children.Add(block);

            // <TextBox x:Name="TextBoxUserName" TextChanged="OnUserNameChanged" />
            TextBox answerBox = new TextBox();
            answerBox.Name = "TextBox" + id;
            answerBox.TextChanged += OnTextChanged;
            answerBox.GotFocus += OnTextBoxGotFocus;
            EntryPanel.Children.Add(answerBox);

            userDefined[id] = answerBox;
        }

        public string GetUserDefinedField(string id)
        {
            TextBox box = null;
            if (userDefined.TryGetValue(id, out box))
            {
                return box.Text;
            }
            throw new ArgumentException("User defined field was not defined", "id");
        }

        public void SetUserDefinedField(string id, string value)
        {
            TextBox box = null;
            if (userDefined.TryGetValue(id, out box))
            {
                box.Text = value;
                return;
            }
            throw new ArgumentException("User defined field was not defined", "id");
        }

        public RichTextBox IntroMessagePrompt
        {
            get { return TextBlockIntroMessage; }
        }

        public string UserNamePrompt
        {
            get { return TextBlockUserNamePrompt.Text; }
            set
            {
                TextBlockUserNamePrompt.Text = value;
                TextBlockUserNamePrompt.Visibility = TextBoxUserName.Visibility = (string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible);
            }
        }

        public string PasswordPrompt
        {
            get { return TextBlockPasswordPrompt.Text; }
            set
            {
                TextBlockPasswordPrompt.Text = value;
                TextBlockPasswordPrompt.Visibility = PasswordBox.Visibility = (string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible);
            }
        }

        public string RealPassword { get; set; }

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() => {

                UpdateButtonState();
   
                foreach (UIElement child in EntryPanel.Children)
                {
                    TextBox box = child as TextBox;
                    if (box != null && box.Visibility == System.Windows.Visibility.Visible)
                    {
                        box.Focus();
                        box.SelectAll();
                        break;
                    }
                    else
                    {
                        PasswordControl pswd = child as PasswordControl;
                        if (pswd != null && pswd.Visibility == System.Windows.Visibility.Visible)
                        {
                            pswd.SelectAll();
                            break;
                        }
                    }
                }    
            }));
        }

        public string UserName
        {
            get { return TextBoxUserName.Text; }
            set { TextBoxUserName.Text = value; }
        }

        public string PasswordConfirmation
        {
            get { return PasswordBox.Password; }
            set { PasswordBox.Password = value; }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        public event EventHandler<OkEventArgs> OkClicked;

        public void PasswordFailure(string errorMessage)
        {
            KeyImage.Visibility = System.Windows.Visibility.Hidden;
            ShieldImage.Visibility = System.Windows.Visibility.Hidden;
            BrokenImage.Visibility = System.Windows.Visibility.Visible;

            ShowError(errorMessage);
            PasswordBox.SelectAll();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RealPassword))
            {
                if (PasswordBox.Password != RealPassword)
                {
                    PasswordFailure("Password is incorrect");
                    return;
                }
            }

            if (!Optional && PasswordBox.Visibility == System.Windows.Visibility.Visible && string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                PasswordFailure("Password cannot be empty");
                return;
            }

            if (OkClicked != null)
            {
                OkEventArgs args = new OkEventArgs();
                OkClicked(this, args);
                if (!string.IsNullOrWhiteSpace(args.Error))
                {
                    ShowError(args.Error);
                }
                if (args.Cancel)
                {
                    return;
                }
            }

            this.DialogResult = true;
            Hide();
        }

        public void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Visibility = System.Windows.Visibility.Visible;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(RealPassword) && PasswordBox.Password == RealPassword)
            {
                KeyImage.Visibility = System.Windows.Visibility.Hidden;
                ShieldImage.Visibility = System.Windows.Visibility.Visible;
                BrokenImage.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                KeyImage.Visibility = System.Windows.Visibility.Visible;
                ShieldImage.Visibility = System.Windows.Visibility.Hidden;
                BrokenImage.Visibility = System.Windows.Visibility.Hidden;
                if (this.IsVisible)
                {
                    ErrorMessage.Visibility = System.Windows.Visibility.Hidden;
                }
            }
        }

        public bool Optional { get; set; }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateButtonState();
            if (this.IsVisible)
            {
                ErrorMessage.Text = "";
            }
        }

        private void UpdateButtonState()
        {
            bool empty = false;

            foreach (UIElement child in EntryPanel.Children)
            {
                TextBox box = child as TextBox;
                if (box != null && box.Visibility == System.Windows.Visibility.Visible)
                {
                    string value = "" + box.Text;
                    if (string.IsNullOrEmpty(value.Trim()))
                    {
                        empty = true;
                        break;
                    }
                }
            }

            ButtonOk.IsEnabled = !empty && !buttonsDisabled;

            ButtonCancel.IsEnabled = !buttonsDisabled;
        }

        bool buttonsDisabled;

        public void DisableButtons()
        {
            buttonsDisabled = true;
            UpdateButtonState();
        }

        public void EnableButtons()
        {
            buttonsDisabled = false;
            UpdateButtonState();
        }


        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            TextBox box = (TextBox)sender;
            box.SelectAll();
        }

        private void OnPasswordBoxGotFocus(object sender, RoutedEventArgs e)
        {
            PasswordControl box = (PasswordControl)sender;
            box.SelectAll();
        }


    }
}
