using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace Walkabout.Utilities
{


    /// <summary>
    /// Interaction logic for MessageBoxEx.xaml
    /// </summary>
    public partial class MessageBoxEx : Window
    {
        //        public string Title { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
        public TextBox DetailsTextBox { get { return this.DetailsText; } }
        public MessageBoxButton Buttons { get; set; }
        public MessageBoxImage MessageImageSource { get; set; }
        public MessageBoxResult Result { get; set; }

        public MessageBoxEx()
        {
            this.InitializeComponent();
            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                this.Owner = Application.Current.MainWindow;
            }
            PreviewKeyDown += new KeyEventHandler(this.MessageBoxEx_PreviewKeyDown);
        }

        private void MessageBoxEx_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C || e.Key == Key.Insert)
            {
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    string content = string.Format("[{0}]{1}{2}", this.Title, Environment.NewLine, this.Message);
                    System.Windows.Clipboard.SetDataObject(content, true);
                }
            }
            else if (e.Key == Key.Escape)
            {
                this.Result = MessageBoxResult.Cancel;
                this.Close();
            }
        }

        public void DisplayWindow()
        {
            this.SetButtonVisibility(this.Buttons);

            if ((this.MessageImageSource == MessageBoxImage.None && this.Buttons == MessageBoxButton.YesNo) || this.Buttons == MessageBoxButton.YesNoCancel)
            {
                // if no Image is supplied but yet this looks like a question then lets use a Question image
                this.MessageImageSource = MessageBoxImage.Question;
            }

            this.SetImageStyle();

            this.CreateMessage();

            if (string.IsNullOrWhiteSpace(this.Details) == false)
            {
                this.ShowDetails.Visibility = Visibility.Visible;
            }

            this.DataContext = this;
            Grid mainShield = null;
            if (Application.Current != null && Application.Current.MainWindow != null)
            {
                mainShield = Application.Current.MainWindow.FindName("Shield") as Grid;
                if (mainShield != null)
                {
                    mainShield.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
                    mainShield.Visibility = System.Windows.Visibility.Visible;
                }
            }

            bool? b = this.ShowDialog();

            if (mainShield != null)
            {
                mainShield.Visibility = System.Windows.Visibility.Collapsed;
            }
        }

        private void CreateMessage()
        {
            if (this.Message.Contains("http://schemas.microsoft.com/winfx/2006/xaml/presentation"))
            {
                FlowDocument doc = (FlowDocument)XamlReader.Parse(@"<FlowDocument xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" + this.Message + "</FlowDocument>");
                foreach (Block block in doc.Blocks.ToArray())
                {
                    Paragraph p = block as Paragraph;
                    if (p != null)
                    {
                        foreach (Inline i in p.Inlines)
                        {
                            Hyperlink hyper = i as Hyperlink;
                            if (hyper != null)
                            {
                                hyper.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(this.OnRequestNavigate);
                            }
                        }
                    }
                    doc.Blocks.Remove(block);
                    this.FlowDocument.Blocks.Add(block);
                }
            }
            else
            {
                this.FlowDocument.Blocks.Add(new Paragraph(new Run(this.Message)));
            }
        }

        private void OnRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                InternetExplorer.OpenUrl(IntPtr.Zero, e.Uri);
            }
        }

        private void SetImageStyle()
        {
            // These are blending colors so they don't need to be themed.
            byte alpha = 0x80;
            if (this.MessageImageSource == MessageBoxImage.Exclamation) // same value as as MessageBoxImage.Warning
            {
                this.ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B));
                this.TextContent.Text = "!";
            }
            else if (this.MessageImageSource == MessageBoxImage.Question)
            {
                this.ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Orange.R, Colors.Orange.G, Colors.Orange.B));
                this.TextContent.Text = "?";
            }
            else if (this.MessageImageSource == MessageBoxImage.Asterisk) // same value as MessageBoxImage.Information
            {
                this.ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Green.R, Colors.Green.G, Colors.Green.B));
                this.TextContent.Text = "*";
            }
            else if (this.MessageImageSource == MessageBoxImage.Error) // Sane value as MessageBoxImage.Hand & MessageBoxImage.Stop
            {
                this.ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Red.R, Colors.Red.G, Colors.Red.B));
                this.TextContent.Text = "*";
            }
            else
            {
                this.ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Blue.R, Colors.Blue.G, Colors.Blue.B));
                this.TextContent.Text = "i";
            }

        }



        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            this.DragMove();
        }

        public static MessageBoxResult Show(string message, string title, string details, MessageBoxButton buttonOption, MessageBoxImage image)
        {
            MessageBoxResult result = MessageBoxResult.None;

            if (title == null)
            {
                // No title was supplied build one for them
                title = GetProductName();
            }

            UiDispatcher.BeginInvoke(new Action(() =>
            {
                MessageBoxEx mb = new MessageBoxEx();
                mb.MaxWidth = SystemParameters.PrimaryScreenWidth * 2 / 3;
                mb.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                mb.SizeToContent = SizeToContent.WidthAndHeight;
                mb.Title = title;
                mb.Message = message;
                mb.Buttons = buttonOption;
                mb.Details = details;
                mb.MessageImageSource = image;

                mb.DisplayWindow();
                result = mb.Result;
            }));
            return result;
        }

        public static MessageBoxResult Show(string message, string title, string details)
        {
            return Show(message, title, details, MessageBoxButton.OK, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
        {
            return Show(message, title, null, buttons, image);
        }

        public static MessageBoxResult Show(string message, string title, MessageBoxButton buttons)
        {
            return Show(message, title, null, buttons, MessageBoxImage.None);
        }

        public static MessageBoxResult Show(string message, string title)
        {
            return Show(message, title, MessageBoxButton.OK);
        }

        public static MessageBoxResult Show(string message)
        {
            return Show(message, null);
        }

        private static string GetProductName()
        {
            Assembly a = Assembly.GetEntryAssembly();

            object[] customAttributes = a.GetCustomAttributes(typeof(AssemblyProductAttribute), false);

            if ((customAttributes != null) && (customAttributes.Length > 0))
            {
                return ((AssemblyProductAttribute)customAttributes[0]).Product;
            }
            return string.Empty;
        }

        private void OnButtonYes_ClickEd(object sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.Yes;
            this.Close();
        }

        private void OnButtonNo_Clicked(object sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.No;
            this.Close();
        }

        private void OnButtonCanceled_Clicked(object sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.Cancel;
            this.Close();
        }

        private void OnButtonOk_Clicked(object sender, RoutedEventArgs e)
        {
            this.Result = MessageBoxResult.OK;
            this.Close();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //  this.Result = MessageBoxResult.Yes;
        }

        private void SetButtonVisibility(MessageBoxButton buttonOption)
        {
            Button defaultButton = null;
            switch (buttonOption)
            {
                case MessageBoxButton.YesNo:
                    this.ButtonYes.Visibility = Visibility.Visible;
                    this.ButtonNo.Visibility = Visibility.Visible;
                    defaultButton = this.ButtonYes;
                    break;

                case MessageBoxButton.YesNoCancel:
                    this.ButtonYes.Visibility = Visibility.Visible;
                    this.ButtonNo.Visibility = Visibility.Visible;
                    defaultButton = this.ButtonYes;
                    break;

                case MessageBoxButton.OKCancel:
                    this.ButtonOK.Visibility = Visibility.Visible;
                    this.ButtonCancel.Visibility = Visibility.Visible;
                    defaultButton = this.ButtonOK;
                    break;

                case MessageBoxButton.OK:
                default:
                    this.ButtonOK.Visibility = Visibility.Visible;
                    defaultButton = this.ButtonOK;
                    break;
            }

            if (defaultButton != null)
            {
                this.ButtonYes.IsDefault = true;
                this.ButtonYes.Background = AppTheme.Instance.GetThemedBrush("ListItemSelectedBackgroundBrush");
            }
        }

    }
}
