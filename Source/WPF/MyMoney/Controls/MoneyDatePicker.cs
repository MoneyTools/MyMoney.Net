using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Walkabout.Controls
{
    /// <summary>
    /// This custom DatePicker causes the picker to auto-complete dates so that they are "prior" to current date.
    /// For example, you might be finishing up some records for December in early January, let's say the year 2012
    /// but you enter december 25th as the date "12/25" since you are still thinking it's 2011, and hit TAB.
    /// You want this to auto-complete to the year 2011 instead of making this a future transaction for next
    /// december in 2012.  So you would rather have the year default to the previous year which is more likely to
    /// be correct.  This class also allows auto-completion of a single "day of month" value, so enter "20" to
    /// get the 20th day of the current month.
    /// </summary>
    public class MoneyDatePicker : DatePicker
    {
        TextBox box;

        public MoneyDatePicker()
        {
            this.SetResourceReference(StyleProperty, "DefaultDatePickerStyle");
            this.Focusable = true;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            Debug.WriteLine("MoneyDatePicker: OnGotFocus");
            base.OnGotFocus(e);
        }

        private void AutoCompleteDate()
        {
            if (box == null)
            {
                return;
            }

            string text = box.Text;
            DateTime dt = DateTime.Now;

            // We don't use DateTime.TryParse because it will insert the current year
            int day = dt.Day;
            int month = dt.Month;
            int year = dt.Year;
            bool adjustYear = true;

            int maxDayOfMonth = DateTime.DaysInMonth(dt.Year, dt.Month);

            // Find the allowable date time separators and allow some flexibility here for user convenience.
            CultureInfo ci = CultureInfo.CurrentCulture;
            DateTimeFormatInfo dtfi = ci.DateTimeFormat;
            string separator = dtfi.DateSeparator;
            List<string> separators = new List<string>();
            separators.Add(separator);
            if (separator != "/")
            {
                separators.Add("/");
            }
            if (separator != "-")
            {
                separators.Add("-");
            }

            string[] parts = text.Trim().Split(separators.ToArray(), StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1)
            {
                // let user enter a day of the month (DateTime doesn't allow this)
                if (!int.TryParse(parts[0], out day))
                {
                    // can't do it.
                    return; // let the base class deal with it.
                }
                else if (day > maxDayOfMonth)
                {
                    day = maxDayOfMonth;
                }
            }
            else if (parts.Length == 2)
            {
                string dayPart = parts[0];
                string monthPart = parts[1];

                string pattern = dtfi.MonthDayPattern;
                if (pattern.ToLowerInvariant()[0] == 'm')
                {
                    // then user is expecting "month/day"
                    string temp = monthPart;
                    monthPart = dayPart;
                    dayPart = temp;
                }

                if (!int.TryParse(monthPart, out month))
                {
                    return;
                }
                else if (month > 12)
                {
                    month = 12;
                }
                
                // reset for new month
                maxDayOfMonth = DateTime.DaysInMonth(dt.Year, month);

                // let user enter a day of the month (DateTime doesn't allow this)
                if (!int.TryParse(dayPart, out day))
                {
                    // can't do it.
                    return; // let the base class deal with it.
                }
                else if (day > maxDayOfMonth)
                {
                    day = maxDayOfMonth;
                }

            }
            else if (DateTime.TryParse(text, out dt))
            {
                year = dt.Year;
                month = dt.Month;
                day = dt.Day;
                // user specified the year, so they must know what they are doing!
                adjustYear = false;
            }
            else
            {
                // then string really doesn't seem to be a valid date.
                return; // let the base class deal with it.
            }

            try
            {
                dt = new DateTime(year, month, day);
                if (adjustYear && dt > DateTime.Now)
                {
                    // then look backwards.
                    dt = dt.AddYears(-1);
                }
                string newText = dt.ToShortDateString();
                if (text != newText)
                {
                    box.Text = newText;
                }
            }
            catch
            {
                // let base class handle the error then.
            }
        }

        protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            if (e.OldFocus == box)
            {
                // we got this before the DatePicker gets it, so now's the time to "auto-complete"
                // any incomplete date with the date we want (looking backwards instead of forwards).
                AutoCompleteDate();
            }
            base.OnLostKeyboardFocus(e);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (this.Template != null)
            {
                box = (TextBox)this.Template.FindName("PART_TextBox", this);
                box.AddHandler(TextBox.KeyDownEvent, new KeyEventHandler(OnTextBoxKeyDown), true);
            }
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {            
            if (e.Key == Key.Enter)
            {
                // The DatePicker set this as handled, which breaks the DataGrid commit model.
                e.Handled = false;
            }
        }

    }
}
