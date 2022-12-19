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
            InitializeComponent();
            if (Application.Current != null && Application.Current.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                this.Owner = Application.Current.MainWindow;
            }
            this.PreviewKeyDown += new KeyEventHandler(MessageBoxEx_PreviewKeyDown);
        }

        void MessageBoxEx_PreviewKeyDown(object sender, KeyEventArgs e)
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
            SetButtonVisibility(this.Buttons);

            if (MessageImageSource == MessageBoxImage.None && this.Buttons == MessageBoxButton.YesNo || this.Buttons == MessageBoxButton.YesNoCancel)
            {
                // if no Image is supplied but yet this looks like a question then lets use a Question image
                MessageImageSource = MessageBoxImage.Question;
            }

            SetImageStyle();

            CreateMessage();

            if (string.IsNullOrWhiteSpace(Details) == false)
            {
                ShowDetails.Visibility = Visibility.Visible;
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
            if (Message.Contains("http://schemas.microsoft.com/winfx/2006/xaml/presentation"))
            {
                FlowDocument doc = (FlowDocument)XamlReader.Parse(@"<FlowDocument xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>" + Message + "</FlowDocument>");
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
                                hyper.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler(OnRequestNavigate);
                            }
                        }
                    }
                    doc.Blocks.Remove(block);
                    FlowDocument.Blocks.Add(block);
                }
            }
            else
            {
                FlowDocument.Blocks.Add(new Paragraph(new Run(Message)));
            }
        }

        void OnRequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            if (e.Uri != null)
            {
                InternetExplorer.OpenUrl(IntPtr.Zero, e.Uri);
            }
        }


        void SetImageStyle()
        {
            // These are blending colors so they don't need to be themed.
            byte alpha = 0x80;
            if (this.MessageImageSource == MessageBoxImage.Exclamation) // same value as as MessageBoxImage.Warning
            {
                ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Yellow.R, Colors.Yellow.G, Colors.Yellow.B));
                TextContent.Text = "!";
            }
            else if (this.MessageImageSource == MessageBoxImage.Question)
            {
                ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Orange.R, Colors.Orange.G, Colors.Orange.B));
                TextContent.Text = "?";
            }
            else if (this.MessageImageSource == MessageBoxImage.Asterisk) // same value as MessageBoxImage.Information
            {
                ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Green.R, Colors.Green.G, Colors.Green.B));
                TextContent.Text = "*";
            }
            else if (this.MessageImageSource == MessageBoxImage.Error) // Sane value as MessageBoxImage.Hand & MessageBoxImage.Stop
            {
                ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Red.R, Colors.Red.G, Colors.Red.B));
                TextContent.Text = "*";
            }
            else
            {
                ImageHolder.Background = new SolidColorBrush(Color.FromArgb(alpha, Colors.Blue.R, Colors.Blue.G, Colors.Blue.B));
                TextContent.Text = "i";
            }

        }



        public void DragWindow(object sender, MouseButtonEventArgs args)
        {
            DragMove();
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
                mb.MaxWidth = (SystemParameters.PrimaryScreenWidth * 2) / 3;
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


        void SetButtonVisibility(MessageBoxButton buttonOption)
        {
            Button defaultButton = null;
            switch (buttonOption)
            {
                case MessageBoxButton.YesNo:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    defaultButton = ButtonYes;
                    break;

                case MessageBoxButton.YesNoCancel:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    defaultButton = ButtonYes;
                    break;

                case MessageBoxButton.OKCancel:
                    ButtonOK.Visibility = Visibility.Visible;
                    ButtonCancel.Visibility = Visibility.Visible;
                    defaultButton = ButtonOK;
                    break;

                case MessageBoxButton.OK:
                default:
                    ButtonOK.Visibility = Visibility.Visible;
                    defaultButton = ButtonOK;
                    break;
            }

            if (defaultButton != null)
            {
                ButtonYes.IsDefault = true;
                ButtonYes.Background = AppTheme.Instance.GetThemedBrush("ListItemSelectedBackgroundBrush");
            }
        }

    }
}
