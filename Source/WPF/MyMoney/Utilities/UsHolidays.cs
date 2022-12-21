using System;
using System.Collections.Generic;

namespace Walkabout.Utilities
{
    internal class UsHolidays
    {
        private HashSet<DateTime> holidays;
        private int year;

        public UsHolidays() { }

        /// <summary>
        /// Enumerate work days, skipping weekends and holidays.
        /// </summary>
        /// <param name="date">Date to work back from</param>
        public DateTime GetPreviousWorkDay(DateTime date)
        {
            bool moved = true;
            date = date.AddDays(-1);
            do
            {
                moved = false;
                if (date.Year != this.year)
                {
                    this.year = date.Year;
                    this.holidays = GetHolidays(this.year);
                }
                if (this.holidays.Contains(date))
                {
                    date = date.AddDays(-1); // skip holidays
                    moved = true;
                }
                if (date.DayOfWeek == DayOfWeek.Sunday)
                {
                    date = date.AddDays(-1);
                    moved = true;
                }
                if (date.DayOfWeek == DayOfWeek.Saturday)
                {
                    date = date.AddDays(-1);
                    moved = true;
                }
            } while (moved);
            return date;
        }

        public static HashSet<DateTime> GetHolidays(int year)
        {
            return new HashSet<DateTime>(new DateTime[] {
                GetNewYearsHoliday(year),
                GetMartinLutherKingJrHoliday(year),
                GetWashingtonsBirthdayHoliday(year),
                GetGoodFridayHoliday(year),
                GetMemorialDayHoliday(year),
                GetIndependenceDayHoliday(year),
                GetLaborDayHoliday(year),
                GetThanksgivingHoliday(year),
                GetChristmasDayHoliday(year),
            });
        }

        public static DateTime GetNewYearsHoliday(int year)
        {
            DateTime date = new DateTime(year, 1, 1);
            return MoveFordwards(date);
        }


        public static DateTime GetMartinLutherKingJrHoliday(int year)
        {
            // martin luther king Jr day (third Monday of January each year)
            return GetThirdMonday(year, 1);
        }

        public static DateTime GetWashingtonsBirthdayHoliday(int year)
        {
            // washington's birthday (third Monday of February)
            return GetThirdMonday(year, 2);
        }

        public static DateTime GetMemorialDayHoliday(int year)
        {
            // memorial day (The holiday is observed on the last Monday of May)
            return GetLastMonday(year, 5);
        }

        public static DateTime GetIndependenceDayHoliday(int year)
        {
            // July 4th independency day
            return MoveClosestWorkday(new DateTime(year, 7, 4));
        }

        public static DateTime GetLaborDayHoliday(int year)
        {
            // labor day (first Monday in September)
            return GetFirstMonday(year, 9);
        }

        public static DateTime GetThanksgivingHoliday(int year)
        {
            // thanksgiving (the fourth Thursday of November)
            return GetForthThursday(year, 11);
        }

        public static DateTime GetChristmasDayHoliday(int year)
        {
            // christmas day 
            return MoveClosestWorkday(new DateTime(year, 12, 25));
        }

        public static DateTime GetEasterSunday(int year)
        {
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = ((19 * a) + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + (2 * e) + (2 * i) - h - k) % 7;
            int m = (a + (11 * h) + (22 * l)) / 451;
            int month = (h + l - (7 * m) + 114) / 31;
            int day = ((h + l - (7 * m) + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }

        public static DateTime GetGoodFridayHoliday(int year)
        {
            return GetEasterSunday(year).AddDays(-2);
        }

        // Helper methods ============================================================
        private static DateTime MoveFordwards(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                date = date.AddDays(1);
            }
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(1);
            }
            return date;
        }

        private static DateTime MoveClosestWorkday(DateTime date)
        {
            if (date.DayOfWeek == DayOfWeek.Saturday)
            {
                return date.AddDays(-1);
            }
            if (date.DayOfWeek == DayOfWeek.Sunday)
            {
                return date.AddDays(1);
            }
            return date;
        }

        private static DateTime GetFirstMonday(int year, int month)
        {
            /*             
            Sunday = 0,
            Monday = 1,
            Tuesday = 2,
            Wednesday = 3,
            Thursday = 4,
            Friday = 5,
            Saturday = 6
             */
            DateTime date = new DateTime(year, month, 1);
            return date.AddDays((8 - (int)date.DayOfWeek) % 7);
        }

        private static DateTime GetFirstThursday(int year, int month)
        {
            DateTime date = new DateTime(year, month, 1);
            return date.AddDays((11 - (int)date.DayOfWeek) % 7);
        }

        private static DateTime GetThirdMonday(int year, int month)
        {
            return GetFirstMonday(year, month).AddDays(14);
        }

        private static DateTime GetLastMonday(int year, int month)
        {
            DateTime date = GetFirstMonday(year, month).AddDays(14);
            while (true)
            {
                DateTime next = date.AddDays(7);
                if (next.Month != month)
                {
                    break;
                }
                date = next;
            }
            return date;
        }

        private static DateTime GetForthThursday(int year, int month)
        {
            return GetFirstThursday(year, month).AddDays(21);
        }


    }
}
