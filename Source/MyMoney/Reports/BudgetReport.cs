using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Walkabout.Interfaces.Reports;
using Walkabout.Data;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows.Controls;
using Walkabout.Views;
using System.Windows.Documents;
using Walkabout.Utilities;
using Microsoft.Win32;
using Walkabout.Controls;

namespace Walkabout.Reports
{
    class BudgetReport : Report
    {
        MyMoney money;
        Category filter;
        double sum;
        Dictionary<string, BudgetData> map;
        Dictionary<string, BudgetRow> rows;
        Dictionary<DateTime, BudgetColumn> columns;

        public BudgetReport(FlowDocumentView view, MyMoney money)
        {
            this.money = money;

            if (view != null)
            {
                var doc = view.DocumentViewer.Document;
                doc.Blocks.InsertAfter(doc.Blocks.FirstBlock, new BlockUIContainer(CreateExportReportButton()));
            }
        }

        private Button CreateExportReportButton()
        {
            Button button = CreateReportButton("Icons/Excel.png", "Export", "Export .csv spreadsheet file format");

            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(10);
            button.Click += new RoutedEventHandler((s, args) =>
            {
                ExportReportAsCsv();
            });
            return button;
        }

        public Category CategoryFilter 
        { 
            get { return this.filter; }
            set { this.filter = value; }        
        }

        private class BudgetRow
        {
            public BudgetRow()
            {
                Actuals = new List<decimal>();
                Headers = new List<string>();
            }
            public string Name { get; set; }
            public decimal Budget { get; set; }
            public List<decimal> Actuals { get; set; }
            public List<string> Headers { get; set; }
        }

        private class BudgetColumn
        {
            public string Name;
            public DateTime Date;
            public decimal Total;
        }

        const string BudgetColumnHeader = "Budget";

        public override void Generate(IReportWriter writer)
        {
            writer.WriteHeading("Budget Report");

            this.rows = new Dictionary<string, BudgetRow>();

            this.columns = new Dictionary<DateTime, BudgetColumn>();
            
            foreach (Category rc in money.Categories.GetRootCategories())
            {
                if (rc.Type != CategoryType.Expense) 
                {
                    continue;
                }
                string rowHeader = rc.Name;
                this.CategoryFilter = rc;

                
                // Add column for the budget itself.
                BudgetColumn budgetColumn;
                if (!columns.TryGetValue(DateTime.MinValue, out budgetColumn))
                {
                    budgetColumn = new BudgetColumn()
                    {
                        Name = BudgetColumnHeader,                        
                    };
                    columns[DateTime.MinValue] = budgetColumn;
                }
                budgetColumn.Total += rc.Budget;

                foreach (BudgetData b in this.Compute())
                {
                    BudgetRow row = null;
                    if (!rows.TryGetValue(rowHeader, out row))
                    {
                        row = new BudgetRow()
                                 {
                                     Name = rowHeader,
                                     Budget = rc.Budget
                                 };
                        rows[rowHeader] = row;
                    }

                    DateTime budgetDate = new DateTime(b.BudgetDate.Year, b.BudgetDate.Month, 1);
                    row.Actuals.Add((decimal)b.Actual);
                    row.Headers.Add(b.Name);
                    
                    // Add column for this BudgetData
                    BudgetColumn col;
                    if (!columns.TryGetValue(budgetDate, out col))
                    {
                        col = new BudgetColumn() {
                            Date = budgetDate,
                            Name = b.Name                  
                        };
                        columns[budgetDate] = col;
                    }
                    col.Total += (decimal)b.Actual;
                }
            }

            writer.StartTable();

            writer.StartColumnDefinitions();
            writer.WriteColumnDefinition("300", 300, 300); // category names
            foreach (var pair in from p in columns orderby p.Key select p)
            {
                writer.WriteColumnDefinition("Auto", 100, double.MaxValue); // decimal values.
            }
            writer.WriteColumnDefinition("Auto", 100, double.MaxValue); // total.
            writer.EndColumnDefinitions();

            // write table headers
            List<string> master = new List<string>();
            writer.StartHeaderRow();
            writer.StartCell();
            writer.EndCell();
            foreach (var pair in from p in columns orderby p.Key select p)
            {
                BudgetColumn c = pair.Value;
                string name = c.Name;
                if (name != BudgetColumnHeader) 
                {
                    master.Add(name); // list of all columns we have found (not including "Budget" column)                                
                }
                writer.StartCell();
                writer.WriteNumber(name);
                writer.EndCell();
            }
            writer.StartCell();
            writer.WriteNumber("Balance");
            writer.EndCell();
            writer.EndRow();

            Brush overBudgetBrush = Brushes.Red;
            decimal totalBalance = 0;

            // Now write out the rows.
            foreach (BudgetRow row in from r in rows.Values orderby r.Name select r)
            {
                writer.StartRow();
                writer.StartCell();
                writer.WriteParagraph(row.Name);
                writer.EndCell();
                
                writer.StartCell();
                decimal budget = row.Budget;
                writer.WriteNumber(budget.ToString("C"));
                writer.EndCell();

                decimal balance = 0;
                
                foreach (string col in master)
                {
                    writer.StartCell();              
                    int i = row.Headers.IndexOf(col);
                    if (i >= 0)
                    {
                        decimal actual = row.Actuals[i];          
                        writer.WriteNumber(actual.ToString("C"), FontStyles.Normal, FontWeights.Normal,
                            actual > budget ? overBudgetBrush : null);

                        balance += (row.Budget - actual);
                    }
                    writer.EndCell();
                }

                totalBalance += balance;

                writer.StartCell();
                writer.WriteNumber(balance.ToString("C"));
                writer.EndCell();

                writer.EndRow();
            }

            // Now write out the totals.
            writer.StartHeaderRow();
            writer.StartCell();
            writer.EndCell();
            foreach (var pair in from p in columns orderby p.Key select p)
            {
                BudgetColumn c = pair.Value;
                writer.StartCell();
                writer.WriteNumber(c.Total.ToString("C"));
                writer.EndCell();
            }
            writer.StartCell();
            writer.WriteNumber(totalBalance.ToString("C"));
            writer.EndCell();

            writer.EndRow();

            writer.EndTable();

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);
        }

        public IEnumerable<BudgetData> Compute()
        {
            map = new Dictionary<string, BudgetData>();            

            foreach (Transaction t in this.money.Transactions.GetAllTransactionsByDate())
            {
                bool isCategoryTransfer = IsCategoryTransfer(t);

                if (!t.Account.IsBudgeted && !isCategoryTransfer)
                {
                    continue;
                }

                if (!t.BudgetBalanceDate.HasValue)
                {
                    continue;
                }
                AddTransaction(t, isCategoryTransfer);
            }

            return map.Values;
        }
        
        public bool IsCategoryTransfer(Transaction t)
        {
            if (t.Account != null && t.Account.IsCategoryFund)
            {
                Category c = t.Account.GetFundCategory();
                if (filter == null)
                {
                    // only include expense categories.
                    return c.Type == CategoryType.Expense;
                }
                return c == filter;
            }
            return false;
        }

        private BudgetData AddTransaction(Transaction t, bool isCategoryTransfer)
        {
            // We do not use "transaction.Date" because sometimes a transaction can be pulled into a different month's budget because it makes more 
            // sense depending on what the transaction is about.  For example you might buy an expensive item on the 31st of the month and return it 
            // on the 3rd of the next month because it was defective. This would normally create a spike in your budget graph and a weird profit in the 
            // next month, so instead of that you can just move the transaction from the 3rd into the previous month's budget balance and so it disappears 
            // completely rather than creating noise in your budget.                    
            DateTime date = t.BudgetBalanceDate.HasValue ? t.BudgetBalanceDate.Value : new DateTime(t.Date.Year, t.Date.Month, 1);
            string name = date.ToString("MM/yyyy");
            
            BudgetData data = null;

            if (isCategoryTransfer)
            {
                // then this was a budget transfer into the category
                data = GetBudgetData(date, name);
                data.AddBudget(t); 
            }
            else if (t.IsSplit)
            {
                bool added = false;
                foreach (Split s in t.Splits)
                {
                    if (!(s.IsBudgeted))
                    {
                        continue;
                    }
                    if (ExpenseCategoryMatchesFilter(s.Category))
                    {
                        data = GetBudgetData(date, name);
                        if (!added)
                        {
                            data.Add(t);
                            added = true;
                        }
                        data.Actual -= (double)s.amount;
                        sum += (double)s.amount;
                    }
                }
            }
            else if (ExpenseCategoryMatchesFilter(t.Category))
            {
                data = GetBudgetData(date, name);
                data.AddActual(t);
                sum -= (double)t.amount;
            }
            return data;
        }

        public bool ExpenseCategoryMatchesFilter(Category c) 
        {
            if (c == null) return false;
            if (filter == null) return c.Type == CategoryType.Expense;
            return filter.Contains(c);
        }

        public BudgetData GetBudgetData(DateTime date, string name)
        {
            BudgetData data;
            if (!map.TryGetValue(name, out data))
            {
                // starting a new budget month                
                data = new BudgetData(name);
                data.BudgetDate = date;
                map[name] = data;
            }
            return data;
        }

        public override void Export(string filename)
        {
            if (this.columns == null)
            {
                return;
            }

            using (StreamWriter writer = new StreamWriter(filename, false, Encoding.UTF8))
            {

                List<string> master = new List<string>();

                foreach (var pair in from p in columns orderby p.Key select p)
                {
                    BudgetColumn c = pair.Value;
                    string name = c.Name;
                    writer.Write(",");  
                    if (name == BudgetColumnHeader)
                    {
                        writer.Write("Budget");
                    }
                    else
                    {
                        master.Add(name); // list of all columns we have found (not including "Budget" column)
                        writer.Write(name);        
                    }                    
                }
                writer.WriteLine(", Balance");

                // rows.
                foreach (BudgetRow row in from r in rows.Values orderby r.Name select r)
                {
                    decimal balance = 0;
                    writer.Write(row.Name);
                    writer.Write(", ");
                    writer.Write(row.Budget.ToString());

                    foreach (string col in master)
                    {
                        writer.Write(", ");
                        
                        int i = row.Headers.IndexOf(col);
                        if (i >= 0)
                        {
                            decimal actual = row.Actuals[i];
                            writer.Write(actual.ToString());
                            balance += (row.Budget - actual);
                        }
                        else
                        {
                            writer.Write("0");
                        }                        
                    }

                    writer.Write(", ");
                    writer.WriteLine(balance.ToString());
                }
            }
        }


    }


}
