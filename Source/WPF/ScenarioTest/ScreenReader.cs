using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Walkabout.Tests.Interop;
using Color = System.Windows.Media.Color;

namespace ScenarioTest
{
    internal class ScreenReader
    {
        internal static Color GetAverageColor(Rect box)
        {
            var thumbnailWidth = (int)box.Width;
            var thumbnailHeight = (int)box.Height;
            string temp = Path.Combine(Path.GetTempPath(), "thumbnail.png");
            Win32.CaptureScreenRect(temp, System.Drawing.Imaging.ImageFormat.Png, (int)box.Left, (int)box.Top, thumbnailWidth, thumbnailHeight);

            using Stream imageStreamSource = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read);
            PngBitmapDecoder decoder = new PngBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            BitmapSource bitmapSource = decoder.Frames[0];
            int bitsPerPixel = bitmapSource.Format.BitsPerPixel;
            int bytesPerPixel = bitsPerPixel / 8;
            int size = bytesPerPixel * thumbnailWidth * thumbnailHeight;
            var pixels = new byte[size];
            int stride = thumbnailWidth * bytesPerPixel;

            bitmapSource.CopyPixels(new Int32Rect(0, 0, thumbnailWidth, thumbnailHeight), pixels, stride, 0);

            if (bitmapSource.Format == PixelFormats.Bgra32)
            {
                double r = 0, g = 0, b = 0;
                var mask = bitmapSource.Format.Masks.FirstOrDefault();
                for (int i = 0; i < size; i += bytesPerPixel)
                {
                    b += pixels[i];
                    g += pixels[i + 1];
                    r += pixels[i + 2];
                }
                double scale = thumbnailWidth * thumbnailHeight;
                return Color.FromRgb((byte)(r / scale), (byte)(g / scale), (byte)(b / scale));
            }

            throw new Exception("Unexpected image format...");
        }

        internal static Color Blend(Color a, Color b, double opacity)
        {
            var bopacity = 1 - opacity;
            return Color.FromArgb(
                (byte)((a.A * opacity) + (b.A * bopacity)),
                (byte)((a.R * opacity) + (b.R * bopacity)),
                (byte)((a.G * opacity) + (b.G * bopacity)),
                (byte)((a.B * opacity) + (b.B * bopacity)));
        }

        internal static double ColorDistance(Color a, Color b)
        {
            return Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
        }
    }
}
