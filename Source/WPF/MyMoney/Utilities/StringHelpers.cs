using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;

namespace Walkabout.Utilities
{
    public static class StringHelpers
    {
        public static string CreateFileFilter(params string[] filters)
        {
            List<string> fileTypes = new List<string>(filters);
            return string.Join("|", fileTypes.ToArray());
        }

        public static DateTime SafeGetDateTime(this IDataReader reader, int column)
        {
            try
            {
                return reader.GetDateTime(column);
            }
            catch
            {
                // the DateTime.MinValue does not round trip to SQL and back!!
                return DateTime.MinValue;
            }
        }

        // return a string (never returns null) so it is safe to operate directly on the result.
        public static string SafeLower(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            return s.ToLowerInvariant();
        }

        public static bool Matches(string a, string b)
        {
            if (a == b)
            {
                return true;
            }

            if (a == null && b.Length == 0)
            {
                return true;
            }

            if (b == null && a.Length == 0)
            {
                return true;
            }

            if (string.Compare(a, b, true) == 0)
            {
                return true;
            }

            return false;
        }

        public static int ParseEnum(Type t, string value, int defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            try
            {
                return (int)Enum.Parse(t, value);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static decimal ParseDecimal(string value, decimal defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            else
            {
                try
                {
                    return decimal.Parse(value);
                }
                catch
                {
                    return defaultValue;
                }
            }
        }

        public static bool ParseBoolean(string value, bool defaultValue)
        {
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }
            else
            {
                try
                {
                    return bool.Parse(value);
                }
                catch
                {
                    return defaultValue;
                }
            }
        }

        /// <summary>
        /// Camel case each word in the given input string.  For example, if the input
        /// is "MICROSOFT COMPANY STORE" then the result will be "Microsoft Company Store".
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string CamelCase(this string input)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string word in input.Split(' ', '\t'))
            {
                if (word.Length > 0)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(' ');
                    }
                    char first = word[0];
                    string rest = word.Length > 1 ? word.Substring(1) : string.Empty;
                    sb.Append(char.ToUpper(first));
                    sb.Append(rest.ToLower());
                }
            }
            return sb.ToString();
        }

        public static CultureInfo GetDefaultCultureInfo(string cultureName, string fallback = "en-US")
        {
            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(cultureName);
            }
            catch
            {
                // If all fails default back to USD
                culture = CultureInfo.GetCultureInfo(fallback);
            }
            return culture;
        }

        public static string GetFormattedAmount(decimal amount, CultureInfo currencyCulture = null, int decimalPlace = 2)
        {
            if (currencyCulture == null)
            {
                currencyCulture = new CultureInfo("en-US");
            }
            return string.Format(currencyCulture, "{0:C" + decimalPlace.ToString() + "}", amount);
        }
    }


    // IntelliComboBox can take any type of item, but this item allows
    // you to specify a different display string in the list than
    // the one in the edit box.
    public class IntelliComboBoxItem
    {
        public object ListValue;
        public object EditValue;
        public object Tag;

        public IntelliComboBoxItem(object listValue, object editValue, object tag)
        {
            this.ListValue = listValue;
            this.EditValue = editValue;
            this.Tag = tag;
        }
        public override string ToString()
        {
            return this.ListValue.ToString();
        }
    }

}
