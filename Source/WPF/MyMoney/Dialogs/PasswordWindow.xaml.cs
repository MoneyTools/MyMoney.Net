using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
//using Kerr;
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
        private readonly Dictionary<string, TextBox> userDefined = new Dictionary<string, TextBox>();

        public PasswordWindow()
        {
            this.InitializeComponent();
            Loaded += new RoutedEventHandler(this.OnLoaded);

            this.UserNamePrompt = "";
            this.PasswordPrompt = Properties.Resources.PasswordPrompt;

            SizeChanged += this.PasswordWindow_SizeChanged;
        }

        private void PasswordWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.TextBlockIntroMessage.Width = Math.Max(0, e.NewSize.Width - 20);
        }



        public void AddUserDefinedField(string id, string label)
        {
            //<TextBlock x:Name="TextBlockUserNamePrompt" Text="User Name: " VerticalAlignment="Center" Margin="0,10,0,2"/>
            TextBlock block = new TextBlock() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 10, 0, 2), TextWrapping = TextWrapping.Wrap };
            block.Text = label;
            this.EntryPanel.Children.Add(block);

            // <TextBox x:Name="TextBoxUserName" TextChanged="OnUserNameChanged" />
            TextBox answerBox = new TextBox();
            answerBox.Name = "TextBox" + id;
            answerBox.TextChanged += this.OnTextChanged;
            answerBox.GotFocus += this.OnTextBoxGotFocus;
            this.EntryPanel.Children.Add(answerBox);

            this.userDefined[id] = answerBox;
        }

        public string GetUserDefinedField(string id)
        {
            TextBox box = null;
            if (this.userDefined.TryGetValue(id, out box))
            {
                return box.Text;
            }
            throw new ArgumentException("User defined field was not defined", "id");
        }

        public void SetUserDefinedField(string id, string value)
        {
            TextBox box = null;
            if (this.userDefined.TryGetValue(id, out box))
            {
                box.Text = value;
                return;
            }
            throw new ArgumentException("User defined field was not defined", "id");
        }

        public RichTextBox IntroMessagePrompt
        {
            get { return this.TextBlockIntroMessage; }
        }

        public string UserNamePrompt
        {
            get { return this.TextBlockUserNamePrompt.Text; }
            set
            {
                this.TextBlockUserNamePrompt.Text = value;
                this.TextBlockUserNamePrompt.Visibility = this.TextBoxUserName.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public string PasswordPrompt
        {
            get { return this.TextBlockPasswordPrompt.Text; }
            set
            {
                this.TextBlockPasswordPrompt.Text = value;
                this.TextBlockPasswordPrompt.Visibility = this.PasswordBox.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public string RealPassword { get; set; }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.Dispatcher.BeginInvoke(new Action(() =>
            {

                this.UpdateButtonState();

                foreach (UIElement child in this.EntryPanel.Children)
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
            get { return this.TextBoxUserName.Text; }
            set { this.TextBoxUserName.Text = value; }
        }

        public string PasswordConfirmation
        {
            get { return this.PasswordBox.Password; }
            set { this.PasswordBox.Password = value; }
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        public event EventHandler<OkEventArgs> OkClicked;

        public void PasswordFailure(string errorMessage)
        {
            this.KeyImage.Visibility = System.Windows.Visibility.Hidden;
            this.ShieldImage.Visibility = System.Windows.Visibility.Hidden;
            this.BrokenImage.Visibility = System.Windows.Visibility.Visible;

            this.ShowError(errorMessage);
            this.PasswordBox.SelectAll();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(this.RealPassword))
            {
                if (this.PasswordBox.Password != this.RealPassword)
                {
                    this.PasswordFailure("Password is incorrect");
                    return;
                }
            }

            if (!this.Optional && this.PasswordBox.Visibility == System.Windows.Visibility.Visible && string.IsNullOrWhiteSpace(this.PasswordBox.Password))
            {
                this.PasswordFailure("Password cannot be empty");
                return;
            }

            if (OkClicked != null)
            {
                OkEventArgs args = new OkEventArgs();
                OkClicked(this, args);
                if (!string.IsNullOrWhiteSpace(args.Error))
                {
                    this.ShowError(args.Error);
                }
                if (args.Cancel)
                {
                    return;
                }
            }

            this.DialogResult = true;
            this.Hide();
        }

        public void ShowError(string message)
        {
            this.ErrorMessage.Text = message;
            this.ErrorMessage.Visibility = System.Windows.Visibility.Visible;
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(this.RealPassword) && this.PasswordBox.Password == this.RealPassword)
            {
                this.KeyImage.Visibility = System.Windows.Visibility.Hidden;
                this.ShieldImage.Visibility = System.Windows.Visibility.Visible;
                this.BrokenImage.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                this.KeyImage.Visibility = System.Windows.Visibility.Visible;
                this.ShieldImage.Visibility = System.Windows.Visibility.Hidden;
                this.BrokenImage.Visibility = System.Windows.Visibility.Hidden;
                if (this.IsVisible)
                {
                    this.ErrorMessage.Visibility = System.Windows.Visibility.Hidden;
                }
            }
        }

        public bool Optional { get; set; }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            this.UpdateButtonState();
            if (this.IsVisible)
            {
                this.ErrorMessage.Text = "";
            }
        }

        private void UpdateButtonState()
        {
            bool empty = false;

            foreach (UIElement child in this.EntryPanel.Children)
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

            this.ButtonOk.IsEnabled = !empty && !this.buttonsDisabled;

            this.ButtonCancel.IsEnabled = !this.buttonsDisabled;
        }

        private bool buttonsDisabled;

        public void DisableButtons()
        {
            this.buttonsDisabled = true;
            this.UpdateButtonState();
        }

        public void EnableButtons()
        {
            this.buttonsDisabled = false;
            this.UpdateButtonState();
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
