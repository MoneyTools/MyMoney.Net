using System;
using System.Globalization;

namespace xMoney.UIControls
{
    public class Helpers
    {
        public Helpers()
        {
        }

        public static string ToKMB(decimal value, bool showDecimalIfNeeded = false)
        {
            if (!showDecimalIfNeeded)
            {
                value = Math.Round(value);
            }

            if (value is > 999999999 or < (-999999999))
            {
                return value.ToString("0,,,.###B", CultureInfo.InvariantCulture);
            }
            else
            if (value is > 999999 or < (-999999))
            {
                return value.ToString("0,,.##M", CultureInfo.InvariantCulture);
            }
            else
            if (value is > 999 or < (-999))
            {
                return value.ToString("0,.#K", CultureInfo.InvariantCulture);
            }
            else
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static bool isNarrow(double width)
        {
            return width < 600;
        }
    }

}