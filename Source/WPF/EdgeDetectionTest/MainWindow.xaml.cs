using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Walkabout.Utilities;

namespace EdgeDetectionTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Dispatcher dispatcher;

        public MainWindow()
        {
            this.InitializeComponent();
            this.dispatcher = this.Dispatcher;
            this.HideButton.Content = "Hide";
        }

        public MainWindow(ImageSource src)
            : this()
        {
            this.ImageContent.Source = src;
        }

        private void OnDetect(object sender, RoutedEventArgs e)
        {
            this.DebugView.Children.Clear();
            BitmapSource src = this.ImageContent.Source as BitmapSource;
            if (src != null)
            {
                float min = float.Parse(this.Min.Text);
                float max = float.Parse(this.Max.Text);
                int minEdgeLength = int.Parse(this.EdgeLength.Text);

                this.DetectButton.IsEnabled = false;
                CannyEdgeDetector canny = new CannyEdgeDetector(src, max, min, minEdgeLength);
                canny.EdgeDetected += this.OnEdgeDetected;

                Task.Run(new Action(() =>
                {
                    canny.DetectEdges();
                    canny.EdgeDetected -= this.OnEdgeDetected;
                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        this.DetectButton.IsEnabled = true;
                    }));
                }));

                //ShowImage(canny.ToImage<int>(canny.FilteredImage), "Filtered");
                //ShowImage(canny.ToImage<float>(canny.DerivativeX), "DerivativeX");
                //ShowImage(canny.ToImage<float>(canny.NonMax), "NonMax");
                //ShowImage(canny.ToImage<float>(canny.Gradient), "Gradient");
                //ShowImage(canny.ToImage<int>(canny.HorizontalEdges), "HorizontalEdges");
                //ShowImage(canny.ToImage<int>(canny.VerticalEdges), "VerticalEdges");
                //ShowImage(canny.ToImage<int>(canny.EdgeMap), "EdgeMap");
            }
        }

        void OnEdgeDetected(object sender, EdgeDetectedEventArgs e)
        {
            this.dispatcher.BeginInvoke(new Action(() =>
            {
                for (int i = 0, n = e.Edge.Count; i < n; i++)
                {
                    Point p = e.Edge[i];

                    Rectangle dot = new Rectangle();
                    dot.Width = 1;
                    dot.Height = 1;
                    dot.Fill = Brushes.Red;
                    Canvas.SetLeft(dot, p.X);
                    Canvas.SetTop(dot, p.Y);

                    this.DebugView.Children.Add(dot);
                }


            }), DispatcherPriority.Render);

            Thread.Sleep(10);
        }


        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            this.ScaleImage();
        }

        private void ScaleImage()
        {
            BitmapSource bitmap = this.ImageContent.Source as BitmapSource;
            if (bitmap != null)
            {
                double w = bitmap.PixelWidth;
                double h = bitmap.PixelHeight;

                double xScale = this.Scroller.ActualWidth / w;
                double yScale = this.Scroller.ActualHeight / h;
                double minScale = Math.Min(xScale, yScale);
                this.ContentGrid.RenderTransform = new ScaleTransform(minScale, minScale);
            }
        }


        private void ShowImage(ImageSource src, string title)
        {
            Window w = new MainWindow(src);
            w.Title = title;
            w.Show();
        }

        private void OnPaste(object sender, ExecutedRoutedEventArgs e)
        {
            this.DebugView.Children.Clear();
            if (Clipboard.ContainsImage())
            {
                this.ImageContent.Source = Clipboard.GetImage();
                this.ScaleImage();
            }
        }

        private void OnOpen(object sender, RoutedEventArgs e)
        {
            this.DebugView.Children.Clear();
            OpenFileDialog od = new OpenFileDialog();
            if (od.ShowDialog() == true)
            {
                this.ImageContent.Source = LoadImage(od.FileName);
                this.ScaleImage();
            }
        }

        private static BitmapFrame LoadImage(string filePath)
        {
            MemoryStream ms = new MemoryStream();
            byte[] buffer = new byte[16000];
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                int len = fs.Read(buffer, 0, buffer.Length);
                while (len > 0)
                {
                    ms.Write(buffer, 0, len);
                    len = fs.Read(buffer, 0, buffer.Length);
                }
            }
            ms.Seek(0, SeekOrigin.Begin);

            BitmapDecoder decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.IgnoreImageCache, BitmapCacheOption.None);
            BitmapFrame frame = decoder.Frames[0];
            return frame;
        }

        private void OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            if (this.ImageContent.Source is BitmapSource)
            {
                Clipboard.SetImage((BitmapSource)this.ImageContent.Source);
            }
        }

        private void OnToggleVisibility(object sender, RoutedEventArgs e)
        {

            if (this.HideButton.Content.ToString() == "Hide")
            {
                this.HideButton.Content = "Show";
                this.ImageContent.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                this.HideButton.Content = "Hide";
                this.ImageContent.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
