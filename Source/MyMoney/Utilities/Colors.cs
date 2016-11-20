using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Data;
using Walkabout.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace Walkabout.Utilities
{
    static class ColorAndBrushGenerator
    {
        static Dictionary<string, Color> cachedColors = new Dictionary<string, Color>();
        static Dictionary<string, Brush> cachedBrushes = new Dictionary<string, Brush>();
        static ColorConverter cc = new ColorConverter();

        public static Color GenerateNamedColor(string name)
        {
            lock (cachedColors)
            {                
                Color color;
                if (!cachedColors.TryGetValue(name, out color))
                {

                    if (!string.IsNullOrWhiteSpace(name) && name[0] == '#')
                    {
                        try
                        {
                            color = (Color)cc.ConvertFrom(name);
                        }
                        catch
                        {
                        }
                    }
                    else
                    {

                        long uniqueValue = (long)name.GetHashCode();
                        long a = uniqueValue & 0xFF000000;
                        a = a >> 24;
                        long r = uniqueValue & 0x00FF0000;
                        r = r >> 16;
                        long g = uniqueValue & 0x0000FF00;
                        g = g >> 8;
                        long b = uniqueValue & 0x000000FF;

                        color = System.Windows.Media.Color.FromRgb((byte)r, (byte)g, (byte)b);
                    }
                    cachedColors[name] = color;
                }
                return color;
            }
        }

        public static Brush CreateLinearBrushFromSolidColor(Color color, double angle)
        {
            var lighter = new HlsColor(color);
            lighter.Lighten(0.33f);

            var darker = new HlsColor(color);
            darker.Darken(0.33f);

            return new LinearGradientBrush(darker.Color, lighter.Color, angle);
        }

        public static void SetNamedColor(string name, Color color)
        {
            lock (cachedColors)
            {
                cachedColors[name] = color;
            }
            lock (cachedBrushes)
            {
                if (cachedBrushes.ContainsKey(name))
                {
                    cachedBrushes.Remove(name);
                }
            }
        }

        public static Brush GetOrCreateNamedGradientBrush(string name, double angle)
        {
            lock (cachedBrushes)
            {
                Brush brush;
                if (!cachedBrushes.TryGetValue(name, out brush))
                {
                    if (name == "Split")
                    {
                        // this indicates a Split
                        brush = GetBrushForSplit();
                    }
                    else
                    {

                        Color color = GenerateNamedColor(name);
                        brush = CreateLinearBrushFromSolidColor(color, angle);
                    }
                    cachedBrushes[name] = brush;
                }
                return brush;
            }
        }

        private static Brush GetBrushForSplit()
        {
            VisualBrush vb = new VisualBrush();
            Grid g = new Grid();
            g.Height = 10;
            g.Width = 10;
            g.Background = Brushes.LightGray;
            Polygon p = new Polygon();
            p.Points.Add(new Point(0, 10));
            p.Points.Add(new Point(10, 0));
            p.Points.Add(new Point(10, 10));
            p.Points.Add(new Point(0, 10));
            g.Children.Add(p);
            vb.Visual = g;
            p.Fill = Brushes.Black;
            return vb;
        }

    }


    #region CONVERTERS

    public class CategoryToBrush : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ( value is string )
            {
                return GetOrCreateBrushFromCategory((string)value);
            }

            return Brushes.Transparent;
        }


        public static object GetOrCreateBrushFromCategory(string colorString)
        {
            Color color = ColorAndBrushGenerator.GenerateNamedColor(colorString);
            if (color.A == 0)
            {
                return Brushes.Transparent;
            }
            return ColorAndBrushGenerator.GetOrCreateNamedGradientBrush(colorString, 45);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            //if (string.IsNullOrEmpty(value.ToString()))
            //    return null;
            return value;
        }

    }

    #endregion
}
