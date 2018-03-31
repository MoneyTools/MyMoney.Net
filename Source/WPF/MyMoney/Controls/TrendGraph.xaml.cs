using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Charts;
using Walkabout.Configuration;
using Walkabout.Data;
using Walkabout.Dialogs;

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for TrendGraph.xaml
    /// </summary>
    public partial class TrendGraph : UserControl
    {
        IEnumerable data;
        Account account;
        Category category;
        CalendarRange range = CalendarRange.Annually;
        int years = 1;
        DateTime start;
        DateTime end;
        bool yearToDate;
        int series = 1;

        public readonly static RoutedUICommand CommandYearToDate;
        public readonly static RoutedUICommand CommandNext;
        public readonly static RoutedUICommand CommandPrevious;
        public readonly static RoutedUICommand CommandSetRange;
        public readonly static RoutedUICommand CommandShowAll;
        public readonly static RoutedUICommand CommandZoomIn;
        public readonly static RoutedUICommand CommandZoomOut;
        public readonly static RoutedUICommand CommandAddSeries;
        public readonly static RoutedUICommand CommandRemoveSeries;
        public readonly static RoutedUICommand CommandShowBudget;

        static TrendGraph()
        {
            CommandYearToDate = new RoutedUICommand("Year to date", "CommandYearToDate", typeof(TrendGraph));
            CommandNext = new RoutedUICommand("Next", "CommandNext", typeof(TrendGraph));
            CommandPrevious = new RoutedUICommand("Previous", "CommandPrevious", typeof(TrendGraph));
            CommandSetRange = new RoutedUICommand("Set range", "CommandSetRange", typeof(TrendGraph));
            CommandShowAll = new RoutedUICommand("Show all", "CommandShowAll", typeof(TrendGraph));
            CommandZoomIn = new RoutedUICommand("Zoom in", "CommandZoomIn", typeof(TrendGraph));
            CommandZoomOut = new RoutedUICommand("Zoom out", "CommandZoomOut", typeof(TrendGraph));
            CommandAddSeries = new RoutedUICommand("Add series", "CommandAddSeries", typeof(TrendGraph));
            CommandRemoveSeries = new RoutedUICommand("Remove series", "CommandRemoveSeries", typeof(TrendGraph));
            CommandShowBudget = new RoutedUICommand("Show budget", "CommandShowBudget", typeof(TrendGraph));            
        }

        public TrendGraph()
        {
            this.Focusable = true;
            this.end = DateTime.Now;
            this.start = Step(end, this.range, this.years, -1);
            InitializeComponent();
            this.MouseWheel += new MouseWheelEventHandler(TransactionGraph_MouseWheel);            
        }

        public bool ShowBalance { get; set; }

        public TrendGraphSeries SelectedSeries { get { return selected; } }

        public object SelectedItem
        { 
            get {
                ChartValue v = this.Chart.Selected;
                return v != null ? v.UserData : null;
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            this.Focus();
            base.OnMouseDown(e);
        }

        public GraphState GetGraphState()
        {
            GraphState state = new GraphState();
            state.Range = this.range;
            state.Years = this.years;
            state.Start = this.start;
            state.End = this.end;
            state.YearToDate = this.yearToDate;
            state.Series = this.series;
            state.ShowBalance = this.ShowBalance;
            return state;
        }

        public void SetGraphState(GraphState state)
        {
            this.range = state.Range;
            this.years = state.Years;
            this.start = state.Start;
            this.end = state.End;
            this.yearToDate = state.YearToDate;
            this.menuItemYearToDate.IsChecked = this.yearToDate;
            this.series = state.Series;
            GenerateGraph();
        }

        static DateTime Step(DateTime start, CalendarRange range, int years, int direction)
        {
            switch (range)
            {
                case CalendarRange.Annually:
                    return start.AddYears(years * direction);
                case CalendarRange.BiMonthly:
                    return start.AddMonths(2 * direction);
                case CalendarRange.Daily:
                    return start.AddDays(direction);
                case CalendarRange.Monthly:
                    return start.AddMonths(direction);
                case CalendarRange.Quarterly:
                    return start.AddMonths(4 * direction);
                case CalendarRange.SemiAnnually:
                    return start.AddMonths(6 * direction);
                case CalendarRange.TriMonthly:
                    return start.AddMonths(3 * direction);
                case CalendarRange.Weekly:
                    return start.AddDays(7 * direction);
                case CalendarRange.BiWeekly:
                    return start.AddDays(14 * direction);
            }
            return start;
        }

        CalendarRange[] mouseWheelDateSteps = {
            0, 
            CalendarRange.Monthly, 
            CalendarRange.SemiAnnually,  
            CalendarRange.Annually  
            };

        void TransactionGraph_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta != 0)
            {
                int spinIndex = e.Delta / 120;
                int direction = spinIndex < 0 ? -1 : 1;
                spinIndex = Math.Abs(spinIndex);

                if (spinIndex >= mouseWheelDateSteps.Length)
                {
                    spinIndex = mouseWheelDateSteps.Length - 1;
                }

                this.start = Step(this.start, mouseWheelDateSteps[spinIndex], 1, direction);
                this.end = DateTime.Now;

                Pin();
                GenerateGraph();
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
        }

        public Category Category
        {
            get { return this.category; }
        }

        public DateTime StartDate { get { return this.start; } }

        public DateTime EndDate { get { return this.end; } }

        public void UpdateGraph(IEnumerable data, Account a, Category c)
        {
            this.data = data;
            this.account = a;
            this.category = c;
            GenerateGraph();
        }

        ChartData chartData;
            
        public void GenerateGraph()
        {
            chartData = new ChartData();
            
            if (this.yearToDate)
            {
                this.end = DateTime.Today;
                this.start = Step(this.end, CalendarRange.Annually, 1, -1);
            }
            
            TimeSpan span = (end - start);
            int days = span.Days;
            
            if (data != null)
            {
                if (series > 1)
                {
                    DateTime previous = start.AddDays(-days * (series - 1));
                    for (int i = 1; i < series; i++)
                    {
                        DateTime next = previous.AddDays(days);
                        AddSeries(previous, next, favorites[i]);
                        previous = next;
                    }
                }
                selected = AddSeries(this.start, this.end, favorites[0]);
            }

            Chart.Data = chartData;
        }

        Color[] favorites = new Color[] { 
                                                Colors.SkyBlue,
                                                Colors.Pink,
                                                Color.FromRgb(0xE8,0xE3,0x95),
                                                Colors.LightSalmon,
                                                Colors.DarkSeaGreen,
                                                Colors.Silver,
                                                Colors.PaleGreen,
                                                Colors.Wheat };

        uint ToUint(string s)
        {
            uint v = 0;
            uint.TryParse(s, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out v);
            return v;
        }

        TrendGraphSeries selected;
        NumberFormatInfo nfi = new NumberFormatInfo();

        private TrendGraphSeries AddSeries(DateTime start, DateTime end, Color color)
        {
            ChartCategory cat = new ChartCategory();
            cat.WpfColor = color;

            nfi.NumberDecimalDigits = 2;
            nfi.CurrencyNegativePattern = 0;

            TimeSpan span = (end - start);
            int days = span.Days;
            if (days > 360)
            {

                cat.Name = start.Year.ToString();
            }
            else
            {
                cat.Name = start.ToShortDateString();
            }

            TrendGraphSeries s = new TrendGraphSeries(cat.Name, cat.Name);            
            chartData.AddSeries(s);

            
            if ((this.account != null && this.account.Type == AccountType.Credit) ||
                (this.category != null && this.category.Type == CategoryType.Expense))
            {
                s.Flipped = true;
            }

            IList<ChartValue> timeData = s.Values;
            s.BeginUpdate();

            bool first = true;
            double balance = this.account != null ? (double)this.account.OpeningBalance : 0;
            DateTime last = start;
            Transaction lastt = null;
            bool started = this.account != null;

            foreach (object row in data)
            {
                Transaction t = row as Transaction;
                if (t == null) continue;

                if ( t.Account == this.account || // showing transactions for an account
                    (account == null )) // showing transactions by category // && ((!t.IsBudgeted && t.Account.IsBudgeted))
                {
                    decimal v = t.GetCategorizedAmount(this.category);
                    if (t.Date >= start && t.Date <= end)
                    {
                        started = true;
                        if (first)
                        {
                            string label = GetLabel(balance, t);
                            timeData.Add(new ChartValue(label, (double)balance, t));
                            first = false;
                        }

                        balance = AddDatum(balance, last, t.Date, t, timeData, (double)v);
                        last = t.Date;
                        lastt = t;
                    }
                    if (started)
                    {
                        balance += (double)v;
                    }
                }
            }

            if (last != end && lastt != null)
            {
                balance = AddDatum(balance, last, end, lastt, timeData, 0);
            }
            
            s.EndUpdate();
            //s.Accumulate = false;
            //s.Color = color;
            s.Category = cat;
            s.Start = start;
            s.End = end;
            
            return s;
        }

        string GetLabel(double balance, Transaction t)
        {
            return balance.ToString("n", nfi) + "\r\n" + t.Date.ToShortDateString();
        }

        double AddDatum(double balance, DateTime start, DateTime end, Transaction t, IList<ChartValue> timeData, double v)
        {
            // for this math to work, we have to ignore "time" in the dates.
            start = new DateTime(start.Year, start.Month, start.Day);
            end = new DateTime(end.Year, end.Month, end.Day);
            TimeSpan r = (end - start);
            int d = r.Days;
            if (d == 0)
            {
                // multiple transactions on the same day, just add the data values.
                int i = timeData.Count - 1;
                ChartValue datum = timeData[i];
                if (datum.UserData != t)
                {
                    datum.Value += v;
                    datum.Label = GetLabel(datum.Value, t);
                }
            }
            else
            {
                // spread the transactions across a range to fill the gaps so the graph spans the whole time span.
                DateTime today = DateTime.Today;
                DateTime date = start;
                for (int i = 0; i < d; i++)
                {
                    string label = GetLabel(balance, t);
                    timeData.Add(new ChartValue(label, balance, t));
                    date = date.AddDays(1);
                }
            }
            return balance;
        }

        void OnYearToDate(object sender, RoutedEventArgs e)
        {
            this.yearToDate = !this.yearToDate;
            menuItemYearToDate.IsChecked = this.yearToDate;
            GenerateGraph();
        }

        void OnNext(object sender, RoutedEventArgs e)
        {
            if (this.range == CalendarRange.Annually)
            {
                this.start = Step(this.start, this.range, 1, 1);
                this.end = Step(this.end, this.range, 1, 1);
            }
            else
            {
                this.start = this.end;
                this.end = Step(this.start, this.range, 1, 1);
            }
            Pin();
            GenerateGraph();
        }

        void OnPrevious(object sender, RoutedEventArgs e)
        {
            if (this.range == CalendarRange.Annually)
            {
                this.start = Step(this.start, this.range, 1, -1);
                this.end = Step(this.end, this.range, 1, -1);
            }
            else
            {
                this.end = this.start;
                this.start = Step(this.start, this.range, 1, -1);
            }
            GenerateGraph();
        }

        void OnZoomIn(object sender, RoutedEventArgs e)
        {
            if (range > CalendarRange.Daily)
            {
                if (years > 1)
                {
                    years--;
                }
                else
                {
                    range = (CalendarRange)(range - 1);
                }
                this.end = Step(this.start, this.range, this.years, 1);
                Pin();
                GenerateGraph();
            }
        }

        void OnZoomOut(object sender, RoutedEventArgs e)
        {
            if (range == CalendarRange.Annually)
            {
                this.years++;
            }
            else
            {
                range = (CalendarRange)(range + 1);
            }
            this.end = Step(this.start, this.range, this.years, 1);
            Pin();
            GenerateGraph();
        }

        void OnSetRange(object sender, RoutedEventArgs e)
        {
            ReportRangeDialog frm = new ReportRangeDialog();
            frm.Title = "Graph Range";
            frm.StartDate = this.start;
            frm.EndDate = this.end;
            frm.Owner = App.Current.MainWindow;
            if (frm.ShowDialog() == true)
            {
                DateTime start = frm.StartDate;
                this.start = start;
                this.end = frm.EndDate;
                this.yearToDate = false;
                this.menuItemYearToDate.IsChecked = this.yearToDate;
                GenerateGraph();
            }
        }

        void OnAddSeries(object sender, RoutedEventArgs e)
        {
            series++;
            GenerateGraph();
        }

        void OnRemoveSeries(object sender, RoutedEventArgs e)
        {
            if (series > 1)
            {
                series--;
                GenerateGraph();
            }
        }

        void Pin()
        {
            if (this.end > DateTime.Today)
            {
                TimeSpan span = this.end - this.start;
                this.end = DateTime.Today;
                this.start = this.end - span;
            }
        }

        private void CanExecute_YearToDate(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CanExecute_Next(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CanExecute_Previous(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CanExecute_SetRange(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CanExecute_ZoomIn(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = range > CalendarRange.Daily;
        }

        private void CanExecute_ZoomOut(object sender, CanExecuteRoutedEventArgs e)
        {

            e.CanExecute = true;
        }

        private void CanExecute_AddSeries(object sender, CanExecuteRoutedEventArgs e)
        {

            e.CanExecute = true;
        }

        private void CanExecute_RemoveSeries(object sender, CanExecuteRoutedEventArgs e)
        {

            e.CanExecute = true;
        }

        private void CanExecute_ShowBudget(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CanExecute_ShowAll(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void OnShowAll(object sender, ExecutedRoutedEventArgs e)
        {
            this.yearToDate = false;
            this.menuItemYearToDate.IsChecked = this.yearToDate;

            DateTime minDate = DateTime.MaxValue;
            DateTime maxDate = DateTime.MinValue;
            foreach (object row in data)
            {
                Transaction t = row as Transaction;
                if (t == null) continue;
                if (t.Date < minDate)
                {
                    minDate = t.Date;
                }
                if (t.Date > maxDate)
                {
                    maxDate = t.Date;
                }
            }

            this.start = minDate;
            this.end = maxDate;

            GenerateGraph();
        }
    }


    public class TrendGraphSeries : ChartSeries
    {
        DateTime start;

        public TrendGraphSeries(string title, string key)
            : base(title, key)
        {
        }

        public DateTime Start
        {
            get { return start; }
            set { start = value; }
        }
        DateTime end;

        public DateTime End
        {
            get { return end; }
            set { end = value; }
        }

    }


}
