using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Interfaces.Views;
using Walkabout.Views;

namespace Walkabout.Reports
{
    //=========================================================================================
    public class CashFlowReport : Report
    {
        private MyMoney myMoney;
        private bool byYear;
        private int fiscalYearStart;
        private DateTime startDate;
        private DateTime endDate;
        private Dictionary<Category, CashFlowColumns> byCategory;
        private Dictionary<string, int> monthMap;
        private List<string> columns;

        public CashFlowReport()
        {
            this.startDate = new DateTime(DateTime.Now.Year, 1, 1);
            this.byYear = true;
            this.endDate = this.startDate.AddYears(1);
            this.startDate = this.startDate.AddYears(-4); // show 5 years by default.
        }

        ~CashFlowReport()
        {
            Debug.WriteLine("CashFlowReport disposed!");
        }

        public int FiscalYearStart
        {
            get => this.fiscalYearStart;
            set
            {
                this.fiscalYearStart = value;
                this.startDate = new DateTime(DateTime.Now.Year, this.fiscalYearStart + 1, 1);
                if (this.startDate > DateTime.Today)
                {
                    this.startDate = this.startDate.AddYears(-1);
                }
            }
        }

        public override void OnSiteChanged()
        {
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
        }

        class CashFlowReportState : IReportState
        {
            public int FiscalYearStart { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }

            public CashFlowReportState()
            {
            }

            public Type GetReportType()
            {
                return typeof(CashFlowReport);
            }
        }

        public override IReportState GetState()
        {
            return new CashFlowReportState()
            {
                FiscalYearStart = this.fiscalYearStart,
                StartDate = this.startDate,
                EndDate = this.endDate
            };
        }

        public override void ApplyState(IReportState state)
        {
            if (state is CashFlowReportState cashFlowReportState)
            {
                this.FiscalYearStart = cashFlowReportState.FiscalYearStart;
                this.startDate = cashFlowReportState.StartDate;
                this.endDate = cashFlowReportState.EndDate;
            }
        }


        public override void OnMouseLeftButtonClick(object sender, MouseButtonEventArgs e)
        {
            var view = (FlowDocumentView)sender;
            Point pos = e.GetPosition(view);

            if (this.mouseDownCell != null && Math.Abs(this.downPos.X - pos.X) < 5 && Math.Abs(this.downPos.Y - pos.Y) < 5)
            {
                // navigate to show the cell.Data rows.
                IViewNavigator nav = this.ServiceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                nav.ViewTransactions(this.mouseDownCell.Data);
            }
        }

        bool IsSimilarCategory(CategoryType a, CategoryType b)
        {
            switch (a)
            {
                case CategoryType.None:
                    return true;
                case CategoryType.Income:
                case CategoryType.Savings:
                    return b == CategoryType.Income || b == CategoryType.Savings;
                case CategoryType.RecurringExpense:
                case CategoryType.Expense:
                    return b == CategoryType.Expense || b == CategoryType.RecurringExpense;
                case CategoryType.Reserved:
                    return false;
                case CategoryType.Transfer:
                    return b == CategoryType.Transfer;
                case CategoryType.Investments:
                    return b == CategoryType.Investments;
            }
            return true;
        }


        private void TallyCategory(Transaction t, Category c, Transaction data, string columnName, decimal amount)
        {
            // let the category bubble up as high as it can go while not flipping from income to expense.
            // For example: the category "Investments" might contain sub-categories of type income (Investments:Dividends)
            // and expenses (like Investments:Fees).  
            if (c.ParentCategory == null || !this.IsSimilarCategory(c.Type, c.ParentCategory.Type))
            {
                CashFlowColumns columns = null;
                this.byCategory.TryGetValue(c, out columns);
                if (columns == null)
                {
                    columns = new CashFlowColumns();
                    this.byCategory[c] = columns;
                }
                columns.AddValue(columnName, data, t.CurrencyNormalizedAmount(amount));
            }
            else
            {
                this.TallyCategory(t, c.ParentCategory, data, columnName, amount);
            }
        }

        private bool IsExpense(Category c)
        {
            if (c.Type == CategoryType.None && c.ParentCategory != null)
            {
                return this.IsExpense(c.ParentCategory);
            }
            return c.Type == CategoryType.Expense || c.Type == CategoryType.RecurringExpense;
        }

        private bool IsIncome(Category c)
        {
            if (c.Type == CategoryType.None && c.ParentCategory != null)
            {
                return this.IsIncome(c.ParentCategory);
            }
            return c.Type == CategoryType.Income || c.Type == CategoryType.Savings;
        }

        private bool IsUnknown(Category c)
        {
            return (c == null || c.Type == CategoryType.None || c.Name == "Unknown") &&
                (c.ParentCategory == null || this.IsUnknown(c.ParentCategory));
        }

        private bool IsInvestment(Category c)
        {
            if (c.Type == CategoryType.None && c.ParentCategory != null)
            {
                return this.IsInvestment(c.ParentCategory);
            }
            return c.Type == CategoryType.Investments;
        }

        public override Task Generate(IReportWriter writer)
        {
            this.byCategory = new Dictionary<Category, CashFlowColumns>();

            writer.WriteHeading("Cash Flow Report ");

            ICollection<Transaction> transactions = this.myMoney.Transactions.GetAllTransactionsByTaxDate();

            DateTime firstTransactionDate = DateTime.Now;
            Transaction first = transactions.FirstOrDefault();
            if (first != null)
            {
                firstTransactionDate = first.TaxDate;
            }

            this.columns = new List<string>();

            DateTime start = this.startDate;
            while (start < this.endDate)
            {
                DateTime end = this.byYear ? start.AddYears(1) : start.AddMonths(1);
                string columnName = start.ToString("MM/yyyy");
                if (this.byYear)
                {
                    columnName = (this.fiscalYearStart == 0) ? start.Year.ToString() : "FY" + end.Year.ToString();
                }
                this.columns.Add(columnName);
                this.GenerateColumn(writer, columnName, transactions, start, end);
                start = end;
            }

            if (writer is FlowDocumentReportWriter fwriter)
            {
                Paragraph heading = fwriter.CurrentParagraph;

                this.monthMap = new Dictionary<string, int>();
                heading.Inlines.Add(" - from ");

                var previousButton = new Button();
                previousButton.Content = "\uE100";
                previousButton.ToolTip = "Previous year";
                previousButton.FontFamily = new FontFamily("Segoe UI Symbol");
                previousButton.Click += this.OnPreviousClick;
                previousButton.Margin = new System.Windows.Thickness(5, 0, 0, 0);
                this.AddInline(heading, previousButton);

                DatePicker fromPicker = new DatePicker();
                System.Windows.Automation.AutomationProperties.SetName(fromPicker, "ReportDate");
                fromPicker.DisplayDateStart = firstTransactionDate;
                fromPicker.SelectedDate = this.startDate;
                fromPicker.Margin = new System.Windows.Thickness(5, 0, 0, 0);
                fromPicker.SelectedDateChanged += this.OnSelectedFromDateChanged;
                this.AddInline(heading, fromPicker);

                heading.Inlines.Add(" to ");

                DatePicker toPicker = new DatePicker();
                toPicker.DisplayDateStart = firstTransactionDate;
                toPicker.SelectedDate = this.endDate;
                toPicker.Margin = new System.Windows.Thickness(5, 0, 0, 0);
                toPicker.SelectedDateChanged += this.OnSelectedToDateChanged; ;
                this.AddInline(heading, toPicker);

                var nextButton = new Button();
                nextButton.Content = "\uE101";
                nextButton.ToolTip = "Next year";
                nextButton.FontFamily = new FontFamily("Segoe UI Symbol");
                nextButton.Margin = new System.Windows.Thickness(5, 0, 0, 0);
                nextButton.Click += this.OnNextClick;
                this.AddInline(heading, nextButton);


                ComboBox byYearMonthCombo = new ComboBox();
                byYearMonthCombo.Margin = new System.Windows.Thickness(5, 0, 0, 0);
                byYearMonthCombo.Items.Add("by years");
                byYearMonthCombo.Items.Add("by month");
                byYearMonthCombo.SelectedIndex = this.byYear ? 0 : 1;
                byYearMonthCombo.SelectionChanged += this.OnByYearMonthChanged;

                this.AddInline(heading, byYearMonthCombo);

                this.AddInline(heading, this.CreateExportReportButton());
            }

            this.WriteCurrencyHeading(writer, this.DefaultCurrency);

            writer.StartTable();
            writer.StartColumnDefinitions();

            writer.WriteColumnDefinition("20", 20, 20); // expander column            
            writer.WriteColumnDefinition("300", 300, 300);

            for (int i = 0; i < this.columns.Count; i++)
            {
                writer.WriteColumnDefinition("Auto", 100, double.MaxValue);
            }
            writer.EndColumnDefinitions();


            WriteRow(writer, true, true, "", this.columns.ToArray());

            CashFlowColumns columnTotals = new CashFlowColumns();

            this.GenerateGroup(writer, this.byCategory, columnTotals, "Income", (c) => { return this.IsIncome(c); });

            this.GenerateGroup(writer, this.byCategory, columnTotals, "Expenses", (c) => { return this.IsExpense(c); });

            this.GenerateGroup(writer, this.byCategory, columnTotals, "Investments", (c) => { return this.IsInvestment(c); });

            this.GenerateGroup(writer, this.byCategory, columnTotals, "Unknown", (c) => { return this.IsUnknown(c); });


            List<decimal> totals = columnTotals.GetOrderedValues(this.columns);
            decimal balance = (from d in totals select d).Sum();

            WriteRow(writer, true, true, "Total", this.FormatValues(totals).ToArray());

            writer.EndTable();

            writer.WriteParagraph("Net cash flow for this period is " + this.GetFormattedNormalizedAmount(balance, 0));

            this.WriteTrailer(writer, DateTime.Today);

            return Task.CompletedTask;
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            this.startDate = this.startDate.AddYears(1);
            this.endDate = this.endDate.AddYears(1);
            this.Regenerate();
        }

        private void OnPreviousClick(object sender, RoutedEventArgs e)
        {
            this.startDate = this.startDate.AddYears(-1);
            this.endDate = this.endDate.AddYears(-1);
            this.Regenerate();
        }


        private void OnSelectedFromDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker picker && picker.SelectedDate.HasValue)
            {
                this.startDate = picker.SelectedDate.Value;
                this.Regenerate();
            }
        }

        private void OnSelectedToDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker picker && picker.SelectedDate.HasValue)
            {
                this.endDate = picker.SelectedDate.Value;
                this.Regenerate();
            }
        }

        private void OnByYearMonthChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            string selected = (string)combo.SelectedItem;
            if (selected == "by years")
            {
                this.byYear = true;
            }
            else
            {
                this.byYear = false;
            }

            this.Regenerate();
        }

        public void Regenerate()
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            _ = view.Generate(this);
        }

        private Button CreateExportReportButton()
        {
            Button button = this.CreateReportButton("Icons/Excel.png", "Export", "Export .csv spreadsheet file format");

            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(10, 0, 10, 0);
            button.Click += new RoutedEventHandler((s, args) =>
            {
                this.ExportReportAsCsv();
            });
            return button;
        }

        private string GetCategoryCaption(Category c)
        {
            return c.Name;
        }

        private void GenerateGroup(IReportWriter writer, Dictionary<Category, CashFlowColumns> columns, CashFlowColumns columnTotals, string groupName, Func<Category, bool> inGroup)
        {
            List<Category> rootCategories = new List<Category>(columns.Keys);
            rootCategories.Sort(new Comparison<Category>((a, b) =>
            {
                return string.Compare(this.GetCategoryCaption(a), this.GetCategoryCaption(b));
            }));

            // start the group
            writer.StartExpandableRowGroup();

            CashFlowColumns groupTotals = new CashFlowColumns();

            // compute group totals;
            foreach (Category c in rootCategories)
            {
                if (inGroup(c))
                {
                    foreach (string columnName in this.columns)
                    {
                        CashFlowColumns cc = columns[c];
                        decimal amount = cc.GetValue(columnName);
                        columnTotals.AddValue(columnName, null, amount);
                        groupTotals.AddValue(columnName, null, amount);
                    }
                }
            }

            WriteRow(writer, true, false, groupName, this.FormatValues(groupTotals.GetOrderedValues(this.columns)).ToArray());

            // now add the detail rows of the group
            foreach (Category c in rootCategories)
            {
                if (inGroup(c))
                {
                    List<CashFlowCell> cells = new List<CashFlowCell>();
                    CashFlowColumns cc = columns[c];

                    foreach (string columnName in this.columns)
                    {
                        CashFlowCell cell = cc.GetCell(columnName);
                        cells.Add(cell);
                    }

                    this.WriteRow(writer, false, false, this.GetCategoryCaption(c), cells);
                }
            }

            writer.EndExpandableRowGroup();
        }

        private List<string> FormatValues(IEnumerable<decimal> values)
        {
            List<string> result = new List<string>();
            foreach (decimal d in values)
            {
                result.Add(d.ToString("N0"));
            }
            return result;
        }

        private void GenerateColumn(IReportWriter writer, string columnName, ICollection<Transaction> transactions, DateTime startDate, DateTime endDate)
        {
            foreach (Transaction t in transactions)
            {
                if (t.Status == TransactionStatus.Void || t.IsDeleted || t.Transfer != null || t.Account == null || t.Account.Type == AccountType.Asset)
                {
                    continue;
                }

                if (t.TaxDate < startDate || t.TaxDate >= endDate)
                {
                    continue;
                }

                if (t.IsSplit)
                {
                    foreach (Split s in t.Splits)
                    {
                        if (s.Transfer == null)
                        {
                            if (s.Category != null)
                            {
                                Category c = s.Category.Root;
                                this.TallyCategory(t, c, new Transaction(t, s), columnName, s.Amount);
                            }
                            else if (s.Category == null && s.Amount != 0)
                            {
                                this.TallyCategory(t, this.myMoney.Categories.Unknown, new Transaction(t, s), columnName, s.Amount);
                            }
                        }
                    }
                    if (t.Splits.Unassigned != 0)
                    {
                        this.TallyCategory(t, this.myMoney.Categories.UnassignedSplit, t, columnName, t.Splits.Unassigned);
                    }
                }
                else if (t.Category != null)
                {
                    this.TallyCategory(t, t.Category, t, columnName, t.AmountMinusTax);
                }
                else if (t.Amount != 0)
                {
                    this.TallyCategory(t, this.myMoney.Categories.Unknown, t, columnName, t.AmountMinusTax);
                }
            }
        }

        private static void WriteRow(IReportWriter writer, bool header, bool addExpanderCell, string name, IEnumerable<string> values)
        {
            if (header)
            {
                writer.StartHeaderRow();
            }
            else
            {
                writer.StartRow();
            }

            if (addExpanderCell)
            {
                writer.StartCell();
                writer.EndCell();
            }

            writer.StartCell();
            writer.WriteParagraph(name);
            writer.EndCell();

            foreach (string v in values)
            {
                writer.StartCell();

                if (!string.IsNullOrEmpty(v))
                {
                    writer.WriteNumber(v);
                }

                writer.EndCell();
            }

            if (header)
            {
                writer.EndHeaderRow();
            }
            else
            {
                writer.EndRow();
            }
        }

        private void WriteRow(IReportWriter writer, bool header, bool addExpanderCell, string name, IEnumerable<CashFlowCell> cells)
        {
            if (header)
            {
                writer.StartHeaderRow();
            }
            else
            {
                writer.StartRow();
            }

            if (addExpanderCell)
            {
                writer.StartCell();
                writer.EndCell();
            }

            writer.StartCell();
            writer.WriteParagraph(name);
            writer.EndCell();

            foreach (CashFlowCell cell in cells)
            {
                writer.StartCell();

                if (cell.Data?.Count > 0)
                {
                    writer.WriteNumber(cell.Value.ToString("N0"));
                    this.MakeCurrentParagraphHyperlink(writer, cell);
                }

                writer.EndCell();
            }

            if (header)
            {
                writer.EndHeaderRow();
            }
            else
            {
                writer.EndRow();
            }
        }

        private void MakeCurrentParagraphHyperlink(IReportWriter writer, object userData)
        {
            if (writer is FlowDocumentReportWriter fw)
            {
                Paragraph p = fw.CurrentParagraph;
                p.Tag = userData;
                p.PreviewMouseLeftButtonDown -= this.OnReportCellMouseDown;
                p.PreviewMouseLeftButtonDown += this.OnReportCellMouseDown;
                p.Cursor = Cursors.Arrow;
                //p.TextDecorations.Add(TextDecorations.Underline);
                //p.Foreground = Brushes.DarkSlateBlue;
                p.SetResourceReference(Paragraph.ForegroundProperty, "HyperlinkForeground");
            }
        }

        private CashFlowCell mouseDownCell;
        private Point downPos;

        private void OnReportCellMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            Paragraph p = (Paragraph)sender;
            this.mouseDownCell = (CashFlowCell)p.Tag;
            this.downPos = e.GetPosition(view);
        }


        public override void Export(string filename)
        {
            using (StreamWriter writer = new StreamWriter(filename, false, Encoding.UTF8))
            {
                this.GenerateCsvGroup(writer, this.byCategory, "Income", (c) => { return this.IsIncome(c); }, (v) => { return v; });

                this.GenerateCsvGroup(writer, this.byCategory, "Expenses", (c) => { return this.IsExpense(c); }, (v) => { return -v; });

                this.GenerateCsvGroup(writer, this.byCategory, "Investments", (c) => { return this.IsInvestment(c); }, (v) => { return v; });

            }
        }

        private void GenerateCsvGroup(StreamWriter writer, Dictionary<Category, CashFlowColumns> byCategory, string groupTitle, Func<Category, bool> inGroup, Func<decimal, decimal> scaleFunc)
        {
            writer.Write(groupTitle);
            foreach (string columnName in this.columns)
            {
                writer.Write(",");
                writer.Write(columnName);
            }
            writer.WriteLine();

            List<Category> rootCategories = new List<Category>(byCategory.Keys);
            rootCategories.Sort(new Comparison<Category>((a, b) =>
            {
                return string.Compare(this.GetCategoryCaption(a), this.GetCategoryCaption(b));
            }));

            // now add the detail rows of the group
            foreach (Category c in rootCategories)
            {
                if (inGroup(c))
                {
                    List<CashFlowCell> cells = new List<CashFlowCell>();
                    CashFlowColumns cc = byCategory[c];

                    foreach (string columnName in this.columns)
                    {
                        CashFlowCell cell = cc.GetCell(columnName);
                        cells.Add(cell);
                    }

                    writer.Write(this.GetCategoryCaption(c));

                    foreach (CashFlowCell cell in cells)
                    {
                        writer.Write(",");
                        writer.Write(scaleFunc(cell.Value));
                    }
                    writer.WriteLine();
                }
            }
            writer.WriteLine();
        }

        internal class CashFlowCell
        {
            internal List<Transaction> Data; // or splits
            internal decimal Value;
        }

        internal class CashFlowColumns
        {
            private readonly Dictionary<string, CashFlowCell> columns = new Dictionary<string, CashFlowCell>();

            public void AddValue(string key, Transaction data, decimal amount)
            {
                CashFlowCell cell;
                this.columns.TryGetValue(key, out cell);
                if (cell == null)
                {
                    cell = new CashFlowCell();
                    cell.Data = new List<Transaction>();
                    this.columns[key] = cell;
                }
                cell.Value += amount;
                if (data != null)
                {
                    cell.Data.Add(data);
                }
            }

            public CashFlowCell GetCell(string key)
            {
                CashFlowCell cell = null;
                if (!this.columns.TryGetValue(key, out cell))
                {
                    cell = new CashFlowCell();
                }
                return cell;
            }

            public decimal GetValue(string key)
            {
                CashFlowCell cell;
                this.columns.TryGetValue(key, out cell);
                if (cell != null)
                {
                    return cell.Value;
                }
                return 0;
            }

            public List<Transaction> GetData(string key)
            {
                CashFlowCell cell;
                this.columns.TryGetValue(key, out cell);
                if (cell != null)
                {
                    return cell.Data;
                }
                return new List<Transaction>();
            }

            public List<decimal> GetOrderedValues(IEnumerable<string> columnKeys)
            {
                List<decimal> result = new List<decimal>();
                foreach (string name in columnKeys)
                {
                    result.Add(this.GetValue(name));
                }
                return result;
            }

            public int Count { get { return this.columns.Count; } }
        }

    }

}
