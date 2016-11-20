using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Views;
using Walkabout.Interfaces.Views;

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
        int year;
        int month;
        bool byYear;
        int columnCount;
        Dictionary<Category, CashFlowColumns> byCategory;
        Dictionary<string, int> monthMap;
        List<string> columns;
        IServiceProvider serviceProvider;

        public CashFlowReport(FlowDocumentView view, MyMoney money, IServiceProvider sp)
        {
            this.myMoney = money;
            this.year = DateTime.Now.Year;
            this.month = DateTime.Now.Month;
            this.byYear = true;
            this.columnCount = 3;
            this.view = view;
            this.serviceProvider = sp;

            view.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
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
                columns.AddValue(columnName, data, amount);
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

        public void Regenerate()
        {
            view.Generate(this);
        }

        public override void Generate(IReportWriter writer)
        {            
            byCategory = new Dictionary<Category, CashFlowColumns>();

            FlowDocumentReportWriter fwriter = (FlowDocumentReportWriter)writer;
            writer.WriteHeading("Cash Flow Report ");

            ICollection<Transaction> transactions = this.myMoney.Transactions.GetAllTransactionsByDate();

            int startYear = year;
            int lastYear = year;

            Transaction first = transactions.FirstOrDefault();
            if (first != null)
            {
                startYear = first.Date.Year;
            }
            Transaction last = transactions.LastOrDefault();
            if (last != null)
            {
                lastYear = last.Date.Year;
            }

            columns = new List<string>();

            DateTime date = new DateTime(year, month, 1);

            for (int i = columnCount - 1; i >= 0; i--)
            {
                if (byYear)
                {
                    int y = year - i;
                    string columnName = y.ToString();
                    columns.Add(columnName);
                    GenerateColumn(writer, columnName, transactions, 0, y);
                }
                else
                {
                    int m = month - i;
                    DateTime md = date.AddMonths(-i);
                    string columnName = md.ToString("MM/yyyy");
                    columns.Add(columnName);
                    GenerateColumn(writer, columnName, transactions, md.Month, md.Year);
                }
            }


            Paragraph heading = fwriter.CurrentParagraph;

            monthMap = new Dictionary<string, int>();

            heading.Inlines.Add(" including ");

            
            ComboBox countCombo = new ComboBox();
            countCombo.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            for (int i = 1; i <= 12; i++)
            {
                countCombo.Items.Add(i.ToString());
            }
            countCombo.SelectedIndex = this.columnCount - 1;
            countCombo.SelectionChanged += OnColumnCountChanged;
            heading.Inlines.Add(new InlineUIContainer(countCombo));

            ComboBox byYearMonthCombo = new ComboBox();
            byYearMonthCombo.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            byYearMonthCombo.Items.Add("Years");
            byYearMonthCombo.Items.Add("Months");

            byYearMonthCombo.SelectedIndex = (byYear ? 0 : 1);
            byYearMonthCombo.SelectionChanged += OnByYearMonthChanged;

            heading.Inlines.Add(new InlineUIContainer(byYearMonthCombo));

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
            
            List<decimal> totals = columnTotals.GetOrderedValues(this.columns);
            decimal balance = (from d in totals select d).Sum();

            WriteRow(writer, true, true, "Total", FormatValues(totals).ToArray());

            writer.EndTable();

            writer.WriteParagraph("Net cash flow for this period is " + balance.ToString("C0"));

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);

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


                    foreach (string columnName in this.columns)
                    {
                        CashFlowColumns cc = columns[c];
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

        private void GenerateColumn(IReportWriter writer, string columnName, ICollection<Transaction> transactions, int month, int year)
        {
            foreach (Transaction t in transactions)
            {
                if (t.Status == TransactionStatus.Void || t.IsDeleted || t.Date.Year != year || t.Transfer != null || t.Account == null || t.Account.Type == AccountType.Asset)
                {
                    continue;
                }

                if (!this.byYear && (t.Date.Month != month))
                {
                    continue;
                }

                if (t.IsSplit)
                {                    
                    foreach (Split s in t.Splits)
                    {                        
                        if (s.Category != null && s.Transfer == null)
                        {
                            Category c = s.Category.Root;
                            TallyCategory(t, c, new Transaction(t,s), columnName, s.Amount);
                        }
                    }
                    if (t.Splits.Unassigned != 0)
                    {
                        TallyCategory(t, this.myMoney.Categories.Unknown, t, columnName, t.Splits.Unassigned);
                    }
                }
                else if (t.Category != null)
                {
                    TallyCategory(t, t.Category, t, columnName, t.amount);
                }
                else if (t.Amount != 0)
                {
                    TallyCategory(t, this.myMoney.Categories.Unknown, t, columnName, t.Amount);
                }
            }
        }

        private void OnColumnCountChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            string selected = (string)combo.SelectedItem;
            int c = 0;
            if (int.TryParse(selected, out c))
            {
                this.columnCount = c;
            }
            Regenerate();
        }

        private void OnByYearMonthChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox combo = (ComboBox)sender;
            string selected = (string)combo.SelectedItem;
            if (selected == "Years")
            {
                byYear = true;
            }
            else
            {
                byYear = false;
            }

            Regenerate();
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
            throw new NotImplementedException();
        }
    }

}
