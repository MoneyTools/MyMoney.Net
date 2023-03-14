using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.WpfConverters
{
    // Extracts the first letter of the Category name.
    public class CategoryTypeLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is CategoryType)
            {
                CategoryType type = (CategoryType)value;
                return type.ToString()[0].ToString();
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool param = parameter == null ? true : bool.Parse(parameter as string);
            bool val = (bool)value;
            return val == param ?
            Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }   
    
    public class BoolToCollapseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            bool param = parameter == null ? true : bool.Parse(parameter as string);
            bool val = (bool)value;
            return val == param ?
            Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                bool rc = (bool)value;
                if (parameter != null)
                {
                    try
                    {
                        var parts = parameter.ToString().Split('+');
                        var name = parts[0];
                        if (rc && parts.Length > 1)
                        {
                            name = parts[1];
                        }
                        Brush brush = App.Current.MainWindow.FindResource(name) as Brush;
                        return brush;
                    }
                    catch
                    {
                    }
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    [ValueConversion(typeof(DateTime), typeof(string))]
    public class MonthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int month = (int)value;
            DateTime date = new DateTime(1900, month, 1);
            return date.ToString("MMMM");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    public class DeletedAliasToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b == true)
            {
                return "This alias has been subsumed by another and will be deleted when you save your changes";
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class StrikeThroughConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b == true)
            {
                return TextDecorations.Strikethrough;
            }

            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }

    public class NullableValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || (value is INullable && value.ToString() == "Null"))
            {
                return string.Empty;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

    }

    public class RoutingLines : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string valueAsString = (string)value;
            if (valueAsString == null)
            {
                return value;
            }

            Grid g = new Grid();
            g.Opacity = 0.66;

            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);

            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);

            g.RowDefinitions.Add(new RowDefinition());
            g.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);

            int column = 0;
            TrackStyle ts = TrackStyle.Empty;
            foreach (char c in valueAsString)
            {
                switch (c)
                {
                    case 'B':
                        ts = TrackStyle.Buy;
                        break;

                    case 'A':
                        ts = TrackStyle.Add;
                        break;

                    case '|':
                        ts = TrackStyle.Full;
                        break;

                    case 'S':
                        ts = TrackStyle.Sell;
                        break;

                    case 'L':
                        ts = TrackStyle.CloseLinked;
                        break;

                    case 'C':
                        ts = TrackStyle.ClosePositive;
                        break;

                    case 'c':
                        ts = TrackStyle.CloseNegative;
                        break;


                }

                AddTrack(g, column, ts);
                column++;
            }

            return g;
        }

        private enum TrackStyle
        {
            Empty,
            Full,
            Buy,
            Add,
            Sell,
            ClosePositive,
            CloseNegative,
            CloseLinked
        }

        private static readonly Brush lineColorBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x44, 0x7F, 0xFF)); // Light semi-transparent blue
        private static CornerRadius shapeAdd = new CornerRadius(0, 5, 5, 0);
        private static CornerRadius shapeSubtract = new CornerRadius(5, 0, 0, 5);
        private static CornerRadius shapeStart = new CornerRadius(5);
        private static CornerRadius shapeStop = new CornerRadius(5);

        /// <summary>
        /// 
        ///       FULL    BUY    ADD     SELL    CLOSE LINKED
        ///   0    │             │       │        │  
        ///        │             │       │        │
        ///   1    │      O      O+     -O        └─ 
        ///        │      │      |
        ///   2    │      │      |
        /// </summary>
        /// <param name="g"></param>
        /// <param name="columnIndex"></param>
        /// <param name="trackStyle"></param>
        private static void AddTrack(Grid g, int columnIndex, TrackStyle trackStyle)
        {
            //-----------------------------------------------------------------
            // Add a new column
            ColumnDefinition cd = new ColumnDefinition();
            cd.Width = new GridLength(18);
            g.ColumnDefinitions.Add(cd);


            //-----------------------------------------------------------------
            // Add a the vertical bar
            // 
            Border b = new Border();
            b.Background = lineColorBrush;
            b.Width = 2;
            b.HorizontalAlignment = HorizontalAlignment.Center;
            b.VerticalAlignment = VerticalAlignment.Stretch;

            if (trackStyle == TrackStyle.ClosePositive || trackStyle == TrackStyle.CloseNegative || trackStyle == TrackStyle.CloseLinked)
            {
                // From top to center
                Grid.SetRow(b, 0);
                Grid.SetRowSpan(b, 2);
            }
            else if (trackStyle == TrackStyle.Buy)
            {
                // From center to bottom
                Grid.SetRow(b, 1);
                Grid.SetRowSpan(b, 2);
            }
            else
            {
                // Full vertical length
                Grid.SetRow(b, 0);
                Grid.SetRowSpan(b, 3);
            }

            Grid.SetColumn(b, columnIndex);
            g.Children.Add(b);


            //-----------------------------------------------------------------
            // Add the Circle
            //
            if (trackStyle != TrackStyle.Full)
            {
                Border spot = new Border();
                spot.BorderThickness = new Thickness(2);
                spot.Height = 10;
                spot.Width = 10;
                Grid.SetRow(spot, 1); // Always in the center row
                Grid.SetColumn(spot, columnIndex);
                g.Children.Add(spot);


                Label label = new Label();
                label.FontSize = 11;
                label.FontWeight = FontWeights.UltraBold;
                label.Height = 16;
                label.Width = 16;
                spot.Child = label;

                switch (trackStyle)
                {
                    case TrackStyle.Buy:
                        spot.BorderBrush = lineColorBrush;
                        spot.Background = Brushes.AliceBlue;
                        spot.CornerRadius = shapeStart;
                        spot.HorizontalAlignment = HorizontalAlignment.Center;
                        spot.VerticalAlignment = VerticalAlignment.Top;

                        label.Content = "+";
                        label.Margin = new Thickness(0, -5, 0, 0);
                        label.HorizontalContentAlignment = spot.HorizontalAlignment;
                        label.VerticalContentAlignment = spot.VerticalAlignment;
                        label.HorizontalAlignment = spot.HorizontalAlignment;
                        label.VerticalAlignment = spot.VerticalAlignment;

                        break;

                    case TrackStyle.Add:
                        spot.BorderBrush = lineColorBrush;
                        spot.Background = Brushes.AliceBlue;
                        spot.CornerRadius = shapeAdd;
                        spot.HorizontalAlignment = HorizontalAlignment.Right;
                        spot.VerticalAlignment = VerticalAlignment.Center;
                        label.Content = "+";
                        label.Margin = new Thickness(-1, -3, 0, 0);
                        label.HorizontalContentAlignment = spot.HorizontalAlignment;
                        label.VerticalContentAlignment = spot.VerticalAlignment;
                        label.HorizontalAlignment = spot.HorizontalAlignment;
                        label.VerticalAlignment = spot.VerticalAlignment;
                        break;

                    case TrackStyle.Sell:
                        spot.BorderBrush = lineColorBrush;
                        spot.Background = lineColorBrush;
                        spot.CornerRadius = shapeSubtract;
                        spot.HorizontalAlignment = HorizontalAlignment.Left;
                        spot.VerticalAlignment = VerticalAlignment.Center;
                        label.Content = "-";
                        label.Margin = new Thickness(1, -4, 0, 0);
                        label.Foreground = new SolidColorBrush(Colors.White);
                        label.HorizontalContentAlignment = spot.HorizontalAlignment;
                        label.VerticalContentAlignment = spot.VerticalAlignment;
                        label.HorizontalAlignment = spot.HorizontalAlignment;
                        label.VerticalAlignment = spot.VerticalAlignment;
                        break;

                    case TrackStyle.CloseNegative:
                        spot.BorderBrush = Brushes.Red;
                        spot.Background = Brushes.Red;
                        spot.CornerRadius = shapeStop;
                        spot.HorizontalAlignment = HorizontalAlignment.Center;
                        spot.VerticalAlignment = VerticalAlignment.Bottom;
                        break;

                    case TrackStyle.ClosePositive:
                        spot.BorderBrush = Brushes.Green;
                        spot.Background = Brushes.Green;
                        spot.CornerRadius = shapeStop;
                        spot.HorizontalAlignment = HorizontalAlignment.Center;
                        spot.VerticalAlignment = VerticalAlignment.Bottom;
                        break;
                }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

    }


    /// <summary>
    /// WHen this class is bound to the control it ensures a minimum of 2 decimal digits
    /// are displayed and a maximum number of digits if there is more precision to be shown
    /// (default 5, which can be overridden in the ConverterParameter).
    /// </summary>
    public class PreserveDecimalDigitsValueConverter : IValueConverter
    {
        public int GetDecimalDigits(decimal d, string stringFormat)
        {
            if (stringFormat == "N2")
            {
                return 2;
            }

            int digits = 0;
            decimal x = d - (int)d;
            while (x != 0)
            {
                digits++;
                x *= 10;
                x -= (int)x;
            }
            return Math.Max(Math.Min(digits, 5), 2);
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(string))
            {
                throw new Exception("Unexpected target type passed to PreserveDecimalDigitsValueConverter.Convert : " + targetType.Name);
            }

            if (value == null || (value is INullable && value.ToString() == "Null"))
            {
                return "";
            }

            string format = "N5";
            if (parameter is string s)
            {
                format = s;
            }

            Type valueType = value.GetType();
            if (valueType == typeof(SqlDecimal))
            {
                SqlDecimal d = (SqlDecimal)value;
                if (d.IsNull)
                {
                    return "";
                }
                return d.Value.ToString("N" + this.GetDecimalDigits(d.Value, format));
            }
            else if (valueType == typeof(decimal))
            {
                decimal d = (decimal)value;
                return d.ToString("N" + this.GetDecimalDigits(d, format));
            }
            else if (valueType == typeof(DateTime))
            {
                return ((DateTime)value).ToString("d");
            }
            else
            {
                return value.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value != null && value.GetType() != typeof(string))
            {
                throw new Exception("Unexpected value type passed to PreserveDecimalDigitsValueConverter.ConvertBack : " + value.GetType().Name);
            }
            string s = (string)value;

            if (targetType == typeof(SqlDecimal))
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return new SqlDecimal();
                }
                return SqlDecimal.Parse(s);
            }
            else if (targetType == typeof(decimal))
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return 0D;
                }

                return decimal.Parse(s);
            }
            else if (targetType == typeof(DateTime))
            {
                if (string.IsNullOrWhiteSpace(s))
                {
                    return DateTime.Now;
                }

                return DateTime.Parse(s);
            }
            else if (targetType == typeof(string))
            {
                return s;
            }
            else
            {
                throw new Exception("Unexpected target type passed to PreserveDecimalDigitsValueConverter.ConvertBack : " + value.GetType().Name);
            }
        }
    }

    public class SqlDecimalToDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SqlDecimal)
            {
                SqlDecimal sqlDecimal = (SqlDecimal)value;
                if (sqlDecimal.IsNull)
                {
                    return string.Empty;
                }
                return sqlDecimal.Value;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string)
            {
                string s = (string)value;
                if (string.IsNullOrWhiteSpace(s))
                {
                    return SqlDecimal.Null;
                }
                return new SqlDecimal(System.Convert.ToDecimal(value));
            }

            if (value is decimal)
            {
                return new SqlDecimal((decimal)value);
            }
            return new SqlDecimal(0);
        }
    }



    public class DecimalToDecimalConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || (value is INullable && value.ToString() == "Null"))
            {
                return string.Empty;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {

            if (value is string)
            {
                string stringVal = value as string;

                decimal d = 0;
                if (!string.IsNullOrWhiteSpace(stringVal) &&
                    false == decimal.TryParse(stringVal, NumberStyles.Currency, CultureInfo.CurrentCulture, out d))
                {
                    d = System.Convert.ToDecimal(stringVal, CultureInfo.GetCultureInfo("en-US"));
                }

                return d;
            }
            return value;
        }
    }

    public class TrueToVisible : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == DependencyProperty.UnsetValue || value == null || ((bool)value) == false)
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }

    public class FalseToVisible : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || ((bool)value) == false)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }

    public class NullOrEmptyStringToVisible : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(value as string))
            {
                return Visibility.Collapsed;
            }

            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }

    public class TrueToVisibleWhenSelected : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || ((bool)value) == false)
            {
                return DataGridRowDetailsVisibilityMode.Collapsed;
            }

            return DataGridRowDetailsVisibilityMode.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }


    public class MoneyColorConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is decimal)
            {
                decimal c = (decimal)value;
                if (c < 0)
                {
                    return AppTheme.Instance.GetThemedBrush("NegativeCurrencyForegroundBrush");
                }
            }
            return AppTheme.Instance.GetThemedBrush("PositiveCurrencyForegroundBrush");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }


    public class NonzeroToFontBoldConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            int flags = (int)value;
            if (flags != 0)
            {
                return FontWeights.Bold;
            }
            return FontWeights.Normal;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        #endregion
    }


    [DebuggerDisplay("{DisplayName}")]
    public class CulturePicker
    {
        public string CultureCode { get; set; }
        public string DisplayName { get; set; }
        public string CurrencySymbol { get; set; }
        public string TwoLetterISORegionName { get; set; }

        public string CountryFlag
        {
            get
            {
                return "/Icons/Flags/" + this.TwoLetterISORegionName.ToLower() + ".png";
            }
        }
    }

    public class CulturePickerConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }

        // Given this object "CulturePicker" return the text representing the country's Locale code "en-US"
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var cp = (CulturePicker)value;
            if (cp != null)
            {
                switch (parameter)
                {
                    case "CurrencySymbol":
                        return cp.CurrencySymbol;
                    case "CultureCode":
                        return cp.CultureCode;
                    case "DisplayName":
                        return cp.DisplayName;
                }
                return cp.CultureCode;
            }
            return value;
        }
    }

    public class CultureHelpers
    {
        public static List<CulturePicker> CurrencyCultures
        {
            get
            {
                if (currencyCultureInfoCache.Count == 0)
                {
                    foreach (var ci in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
                    {
                        var ri = new RegionInfo(ci.Name);

                        if (!string.IsNullOrEmpty(ri.ISOCurrencySymbol) &&
                            ri.ISOCurrencySymbol.Length == 3) // rules out "World" regions that have no single currency.
                        {
                            var cp = new CulturePicker()
                            {
                                CultureCode = ci.Name,
                                DisplayName = ri.CurrencyEnglishName,
                                CurrencySymbol = ri.ISOCurrencySymbol,
                                TwoLetterISORegionName = ri.TwoLetterISORegionName
                            };
                            currencyCultureInfoCache.Add(cp);
                        }
                    }

                    currencyCultureInfoCache.Sort((a, b) => (a.CurrencySymbol + a.DisplayName).CompareTo(b.CurrencySymbol + b.DisplayName));
                    return currencyCultureInfoCache;
                }
                return currencyCultureInfoCache;
            }
        }

        private static List<CulturePicker> currencyCultureInfoCache = new List<CulturePicker>();
    }
}
