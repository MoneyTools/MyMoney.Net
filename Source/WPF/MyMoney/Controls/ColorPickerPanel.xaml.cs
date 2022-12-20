using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Walkabout.Utilities;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for ColorPickerPanel.xaml
    /// </summary>
    public partial class ColorPickerPanel : UserControl
    {
        RenderTargetBitmap bitmap;

        public ColorPickerPanel()
        {
            this.InitializeComponent();
            this.DataContext = this;

            this.GrayScale.MouseDown += new MouseButtonEventHandler(this.GrayScale_MouseDown);
            this.GrayScale.MouseMove += new MouseEventHandler(this.GrayScale_MouseMove);
            this.GrayScale.MouseUp += new MouseButtonEventHandler(this.GrayScale_MouseUp);
        }

        public event EventHandler ColorChanged;

        bool isDown;

        void GrayScale_MouseUp(object sender, MouseButtonEventArgs e)
        {
            this.isDown = false;
        }

        void GrayScale_MouseMove(object sender, MouseEventArgs e)
        {
            if (this.isDown)
            {
                this.SetColorAt(e.GetPosition(this.GrayScale));
            }
        }

        void GrayScale_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.isDown = true;
            this.SetColorAt(e.GetPosition(this.GrayScale));
            this.Focus();
        }

        public Color Color
        {
            get { return (Color)this.GetValue(ColorProperty); }
            set { this.SetValue(ColorProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Color.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(Color), typeof(ColorPickerPanel), new UIPropertyMetadata(Colors.Transparent, OnColorChanged, OnCoerceColorValue));

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            Color c = (Color)e.NewValue;
            HlsColor hls = new HlsColor(c);
            ColorPickerPanel panel = (ColorPickerPanel)d;
            panel.LuminanceSlider.Value = hls.Luminance;
            panel.TransparencySlider.Value = (double)c.A / 255.0;
            panel.OnColorChanged();
        }

        void OnColorChanged()
        {
            if (ColorChanged != null)
            {
                ColorChanged(this, EventArgs.Empty);
            }
        }

        private static object OnCoerceColorValue(DependencyObject d, object baseValue)
        {
            Color baseColor = (Color)baseValue;
            return baseValue;
        }

        private void LuminanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Color c = this.Color;
            HlsColor hls = new HlsColor(c);
            hls.Luminance = (float)this.LuminanceSlider.Value;
            Color nc = hls.Color;
            this.Color = Color.FromArgb(c.A, nc.R, nc.G, nc.B);
        }

        private void TransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Color c = this.Color;
            this.Color = Color.FromArgb((byte)(e.NewValue * 255), c.R, c.G, c.B);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            Size result = base.ArrangeOverride(arrangeBounds);
            this.CreateBitmap();
            return result;
        }

        void CreateBitmap()
        {
            this.bitmap = new RenderTargetBitmap((int)this.Rainbow.ActualWidth, (int)this.Rainbow.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            this.bitmap.Render(this.Rainbow);
            this.bitmap.Render(this.GrayScale);
        }

        private void SetColorAt(Point pos)
        {
            byte[] pixels = new byte[4];
            this.bitmap.CopyPixels(new Int32Rect((int)Math.Max(0, Math.Min(this.bitmap.Width - 1, pos.X)), (int)Math.Max(0, Math.Min(this.bitmap.Height - 1, pos.Y)), 1, 1), pixels, 4, 0);

            // there is a premultiply on the alpha value that we have to reverse.
            double alpha = (double)pixels[3];
            double r = (double)pixels[2];
            double g = (double)pixels[1];
            double b = (double)pixels[0];
            if (alpha != 0)
            {
                r = (r * 255) / alpha;
                g = (g * 255) / alpha;
                b = (b * 255) / alpha;
            }
            Color color = Color.FromArgb((byte)alpha, (byte)r, (byte)g, (byte)b);
            this.Color = color;
        }

    }
}
