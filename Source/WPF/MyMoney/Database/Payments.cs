using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    public class Payments
    {
        private const int AllowedMissedPayments = 2;
        internal const double AmountSensitivity = 0.5; // % stderr on amount variation (taking inflation into account).
        internal const double TimeSensitivity = 0.5; // % stderr on date

        public Payee Payee { get; internal set; }
        public Category Category { get; internal set; }

        public List<Transaction> Transactions { get; set; }
        public double Amount { get; set; }
        public TimeSpan Interval { get; set; }
        public DateTime NextDate { get; set; }
        public double MeanDays { get; internal set; }
        private CalendarRange Frequency { get; set; }

        private double GetPredictedAmount()
        {
            return this.Amount;
        }

        internal struct PaymentKey
        {
            private Payee payee;
            private Category category;
            private int hashCode;

            public Payee Payee
            {
                get => payee;
                set
                {
                    payee = value;
                    hashCode = 0;
                }
            }
            public Category Category
            {
                get => category;
                set
                {
                    category = value;
                    hashCode = 0;
                }
            }

            public override bool Equals(object obj)
            {
                // return equals if payment and category pair are the same
                if (obj == null) return false;
                if (obj is PaymentKey p)
                {
                    return p.Payee == this.Payee && p.Category == this.Category;
                }
                return false;
            }

            public override int GetHashCode()
            {
                // return equals if payment and category pair are the same
                if (this.hashCode == 0)
                {
                    this.hashCode = this.Payee.Name.GetHashCode() ^ this.Category.GetFullName().GetHashCode();
                }
                return this.hashCode;
            }
        }


        public static IDictionary<string, Payments> FindRecurringPayments(List<Transaction> view)
        {
            Dictionary<PaymentKey, Payments> groupedByPayeeCategory = new Dictionary<PaymentKey, Payments>();

            PaymentKey temp = new PaymentKey();
            foreach (Transaction t in view)
            {
                temp.Payee = t.Payee;
                temp.Category = t.Category;
                if (!groupedByPayeeCategory.TryGetValue(temp, out Payments payments))
                {
                    payments = new Payments()
                    {
                        Payee = t.Payee,
                        Category = t.Category
                    };
                    groupedByPayeeCategory[temp] = payments;
                }
                payments.Transactions.Add(t);
            }

            SortedDictionary<string, Payments> recurring = new SortedDictionary<string, Payments>();
            // ok, now figure out if the list has a recurring smell to it...
            foreach (var pair in groupedByPayeeCategory)
            {
                var key = pair.Key;
                var payments = pair.Value;
                if (payments.ComputeRecurrence())
                {
                    string sortName = key.Payee.Name + ":" + key.Category.GetFullName();
                    recurring[sortName] = payments;
                }
            }
            return recurring;
        }

        public double GetNextPrediction()
        {
            // Return the next prediction and advance the NextDate according to the 
            // calculated payment Interval.
            if (this.Frequency != CalendarRange.None)
            {
                switch (this.Frequency)
                {
                    case CalendarRange.Daily:
                        this.NextDate = this.NextDate.AddDays(1);
                        break;
                    case CalendarRange.Weekly:
                        this.NextDate = this.NextDate.AddDays(7);
                        break;
                    case CalendarRange.BiWeekly:
                        this.NextDate = this.NextDate.AddDays(14);
                        break;
                    case CalendarRange.Monthly:
                        this.NextDate = this.NextDate.AddMonths(1);
                        break;
                    case CalendarRange.BiMonthly:
                        this.NextDate = this.NextDate.AddMonths(2);
                        break;
                    case CalendarRange.TriMonthly:
                        this.NextDate = this.NextDate.AddMonths(3);
                        break;
                    case CalendarRange.Quarterly:
                        this.NextDate = this.NextDate.AddMonths(4);
                        break;
                    case CalendarRange.SemiAnnually:
                        this.NextDate = this.NextDate.AddMonths(6);
                        break;
                    case CalendarRange.Annually:
                        this.NextDate = this.NextDate.AddYears(1);
                        break;
                    case CalendarRange.BiAnnually:
                        this.NextDate = this.NextDate.AddYears(2);
                        break;
                }
            }
            else
            {
                this.NextDate += this.Interval;
            }
            return this.GetPredictedAmount();
        }

        public bool ComputeRecurrence()
        {
            this.Frequency = this.Category.Frequency;
            if (this.Transactions.Count < 3 && this.Frequency == CalendarRange.None) return false;
            if (this.Frequency == CalendarRange.Never) return false;

            if (this.Frequency == CalendarRange.None)
            {
                // try and compute the frequency.
                var account = this.Transactions[0].Account;
                List<double> amounts = new List<double>(from t in this.Transactions select (double)account.GetNormalizedAmount(t.Amount));
                var sumAmount = amounts.Sum();
                if (sumAmount > 0)
                {
                    // not a bill if the amount is positive!
                    return false;
                }

                // Use a linear regression instead of Mean to allow for inflation where we 
                // compute the distance to the line to calculate how similar the amounts are
                // instead of using the actual amounts.
                MathHelpers.LinearRegression(amounts, out double a, out double b);
                var distances = new List<double>(MathHelpers.DistancesToLine(amounts, a, b));
                var sumDistances = distances.Sum();
                var stdErrAmount = Math.Abs(sumDistances / sumAmount);

                var meanDistance = Math.Floor(MathHelpers.Mean(distances));
                var stdDevDistance = MathHelpers.StandardDeviation(distances);
                var twoStdDevs = stdDevDistance * 2;

                // Now remove outliers that are more than 2 times the standard deviation away from the line.
                List<Transaction> filtered = new List<Transaction>();
                for (int i = 0; i < distances.Count; i++)
                {
                    var distance = distances[i];
                    if (distance >= meanDistance - twoStdDevs && distance <= meanDistance + twoStdDevs)
                    {
                        filtered.Add(this.Transactions[i]);
                    } 
                    else
                    {
                        // removing this transaction.
                    }
                }
                if (filtered.Count < this.Transactions.Count) 
                {
                    // compute the cleaner stddev.
                    amounts = new List<double>(from t in this.Transactions select (double)account.GetNormalizedAmount(t.Amount));
                    MathHelpers.LinearRegression(amounts, out double a1, out double b1);
                    distances = new List<double>(MathHelpers.DistancesToLine(amounts, a1, b1));
                    sumDistances = distances.Sum();
                    stdErrAmount = Math.Abs(sumDistances / sumAmount);
                }

                if (filtered.Count < 3 && this.Frequency == CalendarRange.None)
                {
                    return false;
                }

                List<double> daysBetween = new List<double>();
                Transaction previous = null;
                foreach (var t in filtered)
                {
                    if (previous != null)
                    {
                        var span = previous.Date - t.Date;
                        daysBetween.Add(span.TotalDays);
                    }
                    previous = t;
                }

                var meanDays = Math.Floor(MathHelpers.Mean(daysBetween));
                if (meanDays < 3)
                {
                    return false;
                }
                var stdDevDays = MathHelpers.StandardDeviation(daysBetween);
                var stdErrDays = Math.Abs(stdDevDays / meanDays);

                if (stdErrDays < TimeSensitivity && stdErrAmount < AmountSensitivity)
                {
                    if (meanDays == 1)
                    {
                        this.Frequency = CalendarRange.Daily;
                    }
                    else if (meanDays == 7)
                    {
                        this.Frequency = CalendarRange.Weekly;
                    }
                    else if (meanDays == 14)
                    {
                        this.Frequency = CalendarRange.BiWeekly;
                    }
                    else if (meanDays >= 28 && meanDays <= 31)
                    {
                        this.Frequency = CalendarRange.Monthly;
                    }
                    else if (meanDays >= 58 && meanDays <= 62)
                    {
                        this.Frequency = CalendarRange.BiMonthly;
                    }
                    else if (meanDays >= 88 && meanDays <= 93)
                    {
                        this.Frequency = CalendarRange.TriMonthly;
                    }
                    else if (meanDays >= 120 && meanDays <= 124)
                    {
                        this.Frequency = CalendarRange.Quarterly;
                    }
                    else if (meanDays >= 180 && meanDays <= 185)
                    {
                        this.Frequency = CalendarRange.SemiAnnually;
                    }
                    else if (meanDays >= 720 && meanDays <= 740)
                    {
                        this.Frequency = CalendarRange.BiAnnually;
                    }

                    var today = DateTime.Today;
                    // tranactions are sorted in reverse order, so the most recent one is the expected amount.
                    var nextDate = this.Transactions.First().Date + TimeSpan.FromDays(meanDays);
                    var steps = (today - this.Transactions.First().Date).TotalDays / meanDays;
                    if (steps >= AllowedMissedPayments)
                    {
                        return false; // too far back in time to be a current bill.
                    }
                    // skip ahead so bill is in the future (allow for some missed payments).
                    while (nextDate < today)
                    {
                        nextDate = nextDate + TimeSpan.FromDays(meanDays);
                    }

                    this.Amount = (double)filtered.First().Amount;
                    this.Interval = TimeSpan.FromDays(meanDays);
                    this.NextDate = nextDate;
                    this.MeanDays = meanDays;
                    return true;
                }
            }
            return false;
        }

        public Payments()
        {
            this.Transactions = new List<Transaction>();
        }
    }
}
