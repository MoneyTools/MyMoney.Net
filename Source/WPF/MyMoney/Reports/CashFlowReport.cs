using System;
using System.Collections.Generic;
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
    internal class CashFlowCell
    {
        internal List<Transaction> Data; // or splits
        internal decimal Value;
    }

    internal class CashFlowColumns
    {
        Dictionary<string, CashFlowCell> columns = new Dictionary<string, CashFlowCell>();

        public void AddValue(string key, Transaction data, decimal amount)
        {
            CashFlowCell cell;
            columns.TryGetValue(key, out cell);
            if (cell == null)
            {
                cell = new CashFlowCell();
                cell.Data = new List<Transaction>();
                columns[key] = cell;
            }
            cell.Value += amount;
            if (data != null)
            {
                cell.Data.Add(data);
            }
        }

        public CashFlowCell GetCell(String key)
        {
            CashFlowCell cell = null;
            if (!columns.TryGetValue(key, out cell))
            {
                cell = new CashFlowCell();
            }
            return cell;
        }

        public decimal GetValue(String key)
        {
            CashFlowCell cell;
            columns.TryGetValue(key, out cell);
            if (cell != null)
            {
                return cell.Value;
            }
            return 0;
        }

        public List<Transaction> GetData(String key)
        {
            CashFlowCell cell;
            columns.TryGetValue(key, out cell);
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

        public int Count { get { return columns.Count; } }
    }

    //=========================================================================================
    public class CashFlowReport : Report
    {
        FlowDocumentView view;
        MyMoney myMoney;
        bool byYear;
        int fiscalYearStart;
        DateTime startDate;
        DateTime endDate;
        Dictionary<Category, CashFlowColumns> byCategory;
        Dictionary<string, int> monthMap;
        List<string> columns;
        IServiceProvider serviceProvider;

        public CashFlowReport(FlowDocumentView view, MyMoney money, IServiceProvider sp, int fiscalYearStart)
        {
            this.myMoney = money;
            this.fiscalYearStart = fiscalYearStart;
            this.startDate = new DateTime(DateTime.Now.Year, 1, 1);
            this.byYear = true;
            if (this.fiscalYearStart > 0)
            {
                this.startDate = new DateTime(DateTime.Now.Year, this.fiscalYearStart + 1, 1);
                if (this.startDate > DateTime.Today)
                {
                    this.startDate = this.startDate.AddYears(-1); 
                }
            }

            this.endDate = this.startDate.AddYears(1);
            this.startDate = this.startDate.AddYears(-4); // show 5 years by default.
            this.view = view;
            this.serviceProvider = sp;
            view.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            view.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            view.Unloaded += (s, e) =>
            {
                this.view.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            };
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(view);

            if (mouseDownCell != null && Math.Abs(downPos.X - pos.X) < 5 && Math.Abs(downPos.Y - pos.Y) < 5)
            {
                // navigate to show the cell.Data rows.
                IViewNavigator nav = serviceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                nav.ViewTransactions(mouseDownCell.Data);
            }
        }


        private void TallyCategory(Transaction t, Category c, Transaction data, string columnName, decimal amount)
        {
            // let the category bubble up as high as it can go while not flipping from income to expense.
            // For example: the category "Investments" might contain sub-categories of type income (Investments:Dividends)
            // and expenses (like Investments:Fees).  
            if (c.ParentCategory == null || (c.Type != CategoryType.None && c.Type != c.ParentCategory.Type))
            {
                CashFlowColumns columns = null;
                byCategory.TryGetValue(c, out columns);
                if (columns == null)
                {
                    columns = new CashFlowColumns();
                    byCategory[c] = columns;
                }
                columns.AddValue(columnName, data, t.CurrencyNormalizedAmount(amount));
            }
            else
            {
                TallyCategory(t, c.ParentCategory, data, columnName, amount);
            }
        }

        private bool IsExpense(Category c)
        {
            if (c.Type == CategoryType.None && c.ParentCategory != null)
            {
                return IsExpense(c.ParentCategory);
            }
            return (c.Type == CategoryType.Expense);
        }

        private bool IsIncome(Category c)
        {
            if (c.Type == CategoryType.None && c.ParentCategory != null)
            {
                return IsIncome(c.ParentCategory);
            }
            return (c.Type == CategoryType.Income || c.Type == CategoryType.Savings);
        }

        private bool IsUnknown(Category c)
        {
            return (c == null || c.Type == CategoryType.None || c.Name == "Unknown") && 
                (c.ParentCategory == null || IsUnknown(c.ParentCategory));
        }

        private bool IsInvestment(Category c)
        {
            if (c.Type == CategoryType.None && c.ParentCategory != null)
            {
                return IsInvestment(c.ParentCategory);
            }
            return (c.Type == CategoryType.Investments);
        }

        public void Regenerate()
        {
            _ = view.Generate(this);
        }

        public override Task Generate(IReportWriter writer)
        {            
            byCategory = new Dictionary<Category, CashFlowColumns>();

            FlowDocumentReportWriter fwriter = (FlowDocumentReportWriter)writer;
            writer.WriteHeading("Cash Flow Report ");

            ICollection<Transaction> transactions = this.myMoney.Transactions.GetAllTransactionsByTaxDate();

            DateTime firstTransactionDate = DateTime.Now;
            Transaction first = transactions.FirstOrDefault();
            if (first != null)
            {
                firstTransactionDate = first.TaxDate;
            }

            columns = new List<string>();

            DateTime start = this.startDate;
            while(start < this.endDate)
            {
                DateTime end = (byYear) ? start.AddYears(1) : start.AddMonths(1);
                string columnName = start.ToString("MM/yyyy");
                if (byYear)
                {
                    columnName = (this.fiscalYearStart == 0) ? start.Year.ToString() : "FY" + end.Year.ToString();
                }
                columns.Add(columnName);
                GenerateColumn(writer, columnName, transactions, start, end);
                start = end;
            }

            Paragraph heading = fwriter.CurrentParagraph;

            monthMap = new Dictionary<string, int>();
            heading.Inlines.Add(" - from ");

            var previousButton = new Button();
            previousButton.Content = "\uE100";
            previousButton.ToolTip = "Previous year";
            previousButton.FontFamily = new FontFamily("Segoe UI Symbol");
            previousButton.Click += OnPreviousClick;
            previousButton.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            heading.Inlines.Add(new InlineUIContainer(previousButton));

            DatePicker fromPicker = new DatePicker();
            fromPicker.DisplayDateStart = firstTransactionDate;
            fromPicker.SelectedDate = this.startDate;
            fromPicker.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            fromPicker.SelectedDateChanged += OnSelectedFromDateChanged;
            heading.Inlines.Add(new InlineUIContainer(fromPicker));

            heading.Inlines.Add(" to ");

            DatePicker toPicker = new DatePicker();
            toPicker.DisplayDateStart = firstTransactionDate;
            toPicker.SelectedDate = this.endDate;
            toPicker.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            toPicker.SelectedDateChanged += OnSelectedToDateChanged; ;
            heading.Inlines.Add(new InlineUIContainer(toPicker));

            var nextButton = new Button();
            nextButton.Content = "\uE101";
            nextButton.ToolTip = "Next year";
            nextButton.FontFamily = new FontFamily("Segoe UI Symbol");
            nextButton.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            nextButton.Click += OnNextClick;
            heading.Inlines.Add(new InlineUIContainer(nextButton));


            ComboBox byYearMonthCombo = new ComboBox();
            byYearMonthCombo.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            byYearMonthCombo.Items.Add("by years");
            byYearMonthCombo.Items.Add("by month");
            byYearMonthCombo.SelectedIndex = (byYear ? 0 : 1);
            byYearMonthCombo.SelectionChanged += OnByYearMonthChanged;

            heading.Inlines.Add(new InlineUIContainer(byYearMonthCombo));

            heading.Inlines.Add(new InlineUIContainer(CreateExportReportButton()));

            writer.StartTable();
            writer.StartColumnDefinitions();

            writer.WriteColumnDefinition("20", 20, 20); // expander column            
            writer.WriteColumnDefinition("300", 300, 300);

            for (int i = 0; i < columns.Count; i++)
            {
                writer.WriteColumnDefinition("Auto", 100, double.MaxValue);
            }
            writer.EndColumnDefinitions();


            WriteRow(writer, true, true, "", this.columns.ToArray());

            CashFlowColumns columnTotals = new CashFlowColumns();
            
            GenerateGroup(writer, byCategory, columnTotals, "Income", (c) => { return IsIncome(c); });

            GenerateGroup(writer, byCategory, columnTotals, "Expenses", (c) => { return IsExpense(c); });

            GenerateGroup(writer, byCategory, columnTotals, "Investments", (c) => { return IsInvestment(c); });

            GenerateGroup(writer, byCategory, columnTotals, "Unknown", (c) => { return IsUnknown(c); });


            List<decimal> totals = columnTotals.GetOrderedValues(this.columns);
            decimal balance = (from d in totals select d).Sum();

            WriteRow(writer, true, true, "Total", FormatValues(totals).ToArray());

            writer.EndTable();

            writer.WriteParagraph("Net cash flow for this period is " + balance.ToString("C0"));

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);

            return Task.CompletedTask;
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            this.startDate = this.startDate.AddYears(1);
            this.endDate = this.endDate.AddYears(1);
            Regenerate();
        }

        private void OnPreviousClick(object sender, RoutedEventArgs e)
        {
            this.startDate = this.startDate.AddYears(-1);
            this.endDate = this.endDate.AddYears(-1);
            Regenerate();
        }


        private void OnSelectedFromDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker picker && picker.SelectedDate.HasValue)
            {
                this.startDate = picker.SelectedDate.Value;
                Regenerate();
            }
        }

        private void OnSelectedToDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker picker && picker.SelectedDate.HasValue)
            {
                this.endDate = picker.SelectedDate.Value;
                Regenerate();
            }
        }

        private void OnByYearMonthChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            string selected = (string)combo.SelectedItem;
            if (selected == "by years")
            {
                byYear = true;
            }
            else
            {
                byYear = false;
            }

            Regenerate();
        }

        private Button CreateExportReportButton()
        {
            Button button = CreateReportButton("Icons/Excel.png", "Export", "Export .csv spreadsheet file format");

            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(10,0,10,0);
            button.Click += new RoutedEventHandler((s, args) =>
            {
                ExportReportAsCsv();
            });
            return button;
        }

        string GetCategoryCaption(Category c)
        {
            return c.Name;
        }

        private void GenerateGroup(IReportWriter writer, Dictionary<Category, CashFlowColumns> columns, CashFlowColumns columnTotals, string groupName, Func<Category, bool> inGroup)
        {
            List<Category> rootCategories = new List<Category>(columns.Keys);
            rootCategories.Sort(new Comparison<Category>((a, b) =>
            {
                return string.Compare(GetCategoryCaption(a), GetCategoryCaption(b));
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

            WriteRow(writer, true, false, groupName, FormatValues(groupTotals.GetOrderedValues(this.columns)).ToArray());

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

                    WriteRow(writer, false, false, GetCategoryCaption(c), cells);
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
                                TallyCategory(t, c, new Transaction(t, s), columnName, s.Amount);
                            }
                            else if (s.Category == null && s.Amount != 0)
                            {
                                TallyCategory(t, this.myMoney.Categories.Unknown, new Transaction(t, s), columnName, s.Amount);
                            }
                        }
                    }
                    if (t.Splits.Unassigned != 0)
                    {
                        TallyCategory(t, this.myMoney.Categories.UnassignedSplit, t, columnName, t.Splits.Unassigned);
                    }
                }
                else if (t.Category != null)
                {
                    TallyCategory(t, t.Category, t, columnName, t.AmountMinusTax);
                }
                else if (t.Amount != 0)
                {
                    TallyCategory(t, this.myMoney.Categories.Unknown, t, columnName, t.AmountMinusTax);
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

            writer.EndRow();
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

                if (cell.Value != 0)
                {                
                    writer.WriteNumber(cell.Value.ToString("N0"));

                    if (cell.Data.Count > 0)
                    {
                        FlowDocumentReportWriter fw = (FlowDocumentReportWriter)writer;
                        Paragraph p = fw.CurrentParagraph;
                        p.Tag = cell;
                        p.PreviewMouseLeftButtonDown -= OnReportCellMouseDown;
                        p.PreviewMouseLeftButtonDown += OnReportCellMouseDown;
                        p.Cursor = Cursors.Arrow;
                        //p.TextDecorations.Add(TextDecorations.Underline);
                        //p.Foreground = Brushes.DarkSlateBlue;
                        p.SetResourceReference(Paragraph.ForegroundProperty, "HyperlinkForeground");
                    }
                }

                writer.EndCell();
            }

            writer.EndRow();
        }

        CashFlowCell mouseDownCell;
        Point downPos;
        
        private void OnReportCellMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Paragraph p = (Paragraph)sender;
            mouseDownCell = (CashFlowCell)p.Tag;
            downPos = e.GetPosition(this.view);
        }


        public override void Export(string filename)
        {
            using (StreamWriter writer = new StreamWriter(filename, false, Encoding.UTF8))
            {
                GenerateCsvGroup(writer, byCategory, "Income", (c) => { return IsIncome(c); }, (v) => { return v;  });

                GenerateCsvGroup(writer, byCategory, "Expenses", (c) => { return IsExpense(c); }, (v) => { return -v; });

                GenerateCsvGroup(writer, byCategory, "Investments", (c) => { return IsInvestment(c); }, (v) => { return v; });

            }
        }

        private void GenerateCsvGroup(StreamWriter writer, Dictionary<Category, CashFlowColumns> byCategory, string groupTitle, Func<Category, bool> inGroup, Func<decimal,decimal> scaleFunc)
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
                return string.Compare(GetCategoryCaption(a), GetCategoryCaption(b));
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

                    writer.Write(GetCategoryCaption(c));

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
    }

}
