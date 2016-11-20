using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
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
            InitializeComponent();
            dispatcher = this.Dispatcher;
            HideButton.Content = "Hide";
        }

        public MainWindow(ImageSource src)
            : this()
        {
            ImageContent.Source = src;
        }

        private void OnDetect(object sender, RoutedEventArgs e)
        {
            DebugView.Children.Clear();
            BitmapSource src = ImageContent.Source as BitmapSource;
            if (src != null)
            {
                float min = float.Parse(Min.Text);
                float max = float.Parse(Max.Text);
                int minEdgeLength = int.Parse(EdgeLength.Text);

                DetectButton.IsEnabled = false;
                CannyEdgeDetector canny = new CannyEdgeDetector(src, max, min, minEdgeLength);
                canny.EdgeDetected += OnEdgeDetected;

                Task.Run(new Action(() =>
                {
                    canny.DetectEdges();
                    canny.EdgeDetected -= OnEdgeDetected;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DetectButton.IsEnabled = true;
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
            dispatcher.BeginInvoke(new Action(() =>
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

                    DebugView.Children.Add(dot);
                }


            }), DispatcherPriority.Render);

            Thread.Sleep(10);
        }


        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            ScaleImage();
        }

        private void ScaleImage()
        {
            BitmapSource bitmap = ImageContent.Source as BitmapSource;
            if (bitmap != null)
            {
                double w = bitmap.PixelWidth;
                double h = bitmap.PixelHeight;

                double xScale = Scroller.ActualWidth / w;
                double yScale = Scroller.ActualHeight / h;
                double minScale = Math.Min(xScale, yScale);
                ContentGrid.RenderTransform = new ScaleTransform(minScale, minScale);
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
            DebugView.Children.Clear();
            if (Clipboard.ContainsImage())
            {
                ImageContent.Source = Clipboard.GetImage();
                ScaleImage();
            }
        }

        private void OnOpen(object sender, RoutedEventArgs e)
        {
            DebugView.Children.Clear();
            OpenFileDialog od = new OpenFileDialog();
            if (od.ShowDialog() == true)
            {
                ImageContent.Source = LoadImage(od.FileName);
                ScaleImage();
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
            if (ImageContent.Source is BitmapSource)
            {
                Clipboard.SetImage((BitmapSource)ImageContent.Source);
            }
        }

        private void OnToggleVisibility(object sender, RoutedEventArgs e)
        {

            if (HideButton.Content.ToString() == "Hide")
            {
                HideButton.Content = "Show";
                ImageContent.Visibility = System.Windows.Visibility.Hidden;
            }
            else
            {
                HideButton.Content = "Hide";
                ImageContent.Visibility = System.Windows.Visibility.Visible;
            }
        }
    }
}
