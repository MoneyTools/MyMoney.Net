using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Utilities;
using System.Windows.Documents;
using System.Windows.Markup;

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
            if ( e.Key == Key.C ||  e.Key == Key.Insert) 
            {
                if ( Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl) )
                {
                    string content = string.Format("[{0}]{1}{2}", this.Title, Environment.NewLine, this.Message);
                    System.Windows.Clipboard.SetDataObject(content, true);
                }
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
            if (this.MessageImageSource == MessageBoxImage.Exclamation)
            {
                ImageHolder.Background = Brushes.Yellow;
                TextContent.Text = "!";
            }
            else if (this.MessageImageSource == MessageBoxImage.Question)
            {
                ImageHolder.Background = Brushes.Orange;
                TextContent.Text = "?";
            }
            else if (this.MessageImageSource == MessageBoxImage.Asterisk)
            {
                ImageHolder.Background = Brushes.Green;
                TextContent.Text = "*";
            }
            else if (this.MessageImageSource == MessageBoxImage.Error)
            {
                ImageHolder.Background = Brushes.Red;
                TextContent.Text = "*";
            }
            else if (this.MessageImageSource == MessageBoxImage.None)
            {
                ImageHolder.Background = Brushes.Blue;
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
            switch (buttonOption)
            {
                case MessageBoxButton.YesNo:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    ButtonNo.IsDefault = true;
                    break;

                case MessageBoxButton.YesNoCancel:
                    ButtonYes.Visibility = Visibility.Visible;
                    ButtonNo.Visibility = Visibility.Visible;
                    ButtonCancel.Visibility = Visibility.Visible;
                    ButtonCancel.IsDefault = true;
                    break;

                case MessageBoxButton.OKCancel:
                    ButtonOK.Visibility = Visibility.Visible;
                    ButtonCancel.Visibility = Visibility.Visible;
                    ButtonCancel.IsDefault = true;
                    break;

                case MessageBoxButton.OK:
                default:
                    ButtonOK.Visibility = Visibility.Visible;
                    ButtonOK.IsDefault = true;
                    break;

            }
        }

    }
}
