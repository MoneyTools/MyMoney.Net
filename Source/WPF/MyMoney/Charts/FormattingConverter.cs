using System;
using System.Globalization;
using System.Windows.Data;

namespace Walkabout.Charts
{

    [ValueConversion(typeof(object), typeof(string))]
    public class NumberConverter : IValueConverter
    {
        private NumberFormatInfo nfi;

        public NumberConverter(NumberFormatInfo nfi)
        {
            this.nfi = nfi;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal)
            {
                decimal v = (decimal)value;
                return v.ToString("C", this.nfi);
            }
            else if (value is double)
            {
                double v = (double)value;
                return v.ToString("C", this.nfi);
            }
            else
            {
                return value.ToString();
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // we don't intend this to ever be called
            return null;
        }
    }


}
