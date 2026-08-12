using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Interfaces.Views;
using Walkabout.Reports;
using Walkabout.Views;
using Walkabout.Views.Controls;

namespace Walkabout.Taxes
{
    //=========================================================================================
    // This class prepares an estimated W2 from the splits found in paycheck deposits.
    public class W2Report : Report
    {
        private MyMoney myMoney;
        private DatabaseSettings databaseSettings;
        private DateTime startDate;
        private DateTime endDate;
        private Point downPos;
        private int fiscalYearStart;
        private Category selectedCategory;
        private TaxCategoryCollection taxCategories;
        private Dictionary<Category, List<Transaction>> transactionsByCategory;
        private const string FiscalPrefix = "FY ";
        private ReportsControl panel;

        public W2Report()
        {
            this.ReportDate = DateTime.Now;
        }

        ~W2Report()
        {
            Debug.WriteLine("W2Report disposed!");
        }

        protected override void Dispose(bool disposing)
        {
            this.Unregister();
            base.Dispose(disposing);
        }

        private void Unregister()
        {
            if (this.panel != null)
            {
                this.panel.FiscalYearChanged -= this.OnFiscalYearChanged;
            }
        }

        private void Register()
        {
            this.panel.FiscalYearChanged += this.OnFiscalYearChanged;
        }

        public override void OnSiteChanged()
        {
            this.taxCategories = new TaxCategoryCollection();
            this.transactionsByCategory = null;
            this.myMoney = (MyMoney)this.ServiceProvider.GetService(typeof(MyMoney));
            this.databaseSettings = (DatabaseSettings)this.ServiceProvider.GetService(typeof(DatabaseSettings));
            this.fiscalYearStart = this.databaseSettings.FiscalYearStart;
            // this also makes the panel visible.
            this.Unregister();
            var panel = (ReportsControl)this.ServiceProvider.GetService(typeof(ReportsControl));
            this.panel = panel;
            panel.HideReportDateRow();
            panel.HideNormalizeCurrencyRow();
            this.Unregister();
            this.Register();
        }

        public class W2ReportState : IReportState
        {
            public StateSource Source { get; set; }
            public int FiscalYearStart { get; set; }
            public int Year { get; set; }

            public W2ReportState()
            {
            }

            public Type GetReportType()
            {
                return typeof(W2Report);
            }
        }

        public override IReportState GetState()
        {
            return new W2ReportState()
            {
                FiscalYearStart = this.fiscalYearStart,
                Year = this.startDate.Year,
            };
        }

        public override void ApplyState(IReportState state)
        {
            if (state is W2ReportState taxReportState)
            {
                this.fiscalYearStart = taxReportState.FiscalYearStart;
                this.SetStartDate(taxReportState.Year);
            }
        }

        private void SetStartDate(int year)
        {
            this.startDate = new DateTime(year, this.fiscalYearStart + 1, 1);
            if (this.fiscalYearStart > 0)
            {
                // Note: "FY2020" means July 2019 to July 2020, in other words
                // it is the end date that represents the year.
                this.startDate = this.startDate.AddYears(-1);
            }
            if (this.startDate > DateTime.Today)
            {
                this.startDate = this.startDate.AddYears(-1);
            }
            this.endDate = this.startDate.AddYears(1);
        }

        public override void OnMouseLeftButtonClick(object sender, MouseButtonEventArgs e)
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            Point pos = e.GetPosition(view);

            if (Math.Abs(this.downPos.X - pos.X) < 5 && Math.Abs(this.downPos.Y - pos.Y) < 5)
            {
                // navigate to show the cell.Data rows.
                IViewNavigator nav = this.ServiceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                List<Transaction> transactions = null;
                if (this.selectedCategory != null && this.transactionsByCategory.TryGetValue(this.selectedCategory, out transactions))
                {
                    nav.ViewTransactions(transactions);
                }
            }
        }

        public void Regenerate()
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            _ = view.Generate(this);
        }

        private bool Summarize(Dictionary<Category, decimal> byCategory, Transaction t)
        {
            bool found = false;
            if (t.IsSplit)
            {
                foreach (Split s in t.Splits)
                {
                    Category c = s.Category;
                    decimal total = 0;
                    if (c != null && byCategory.TryGetValue(c, out total))
                    {
                        found = true;
                        total += s.Amount;
                        byCategory[c] = total;
                        this.GroupTransactions(t, c);
                    }
                }
            }
            else if (t.Category != null)
            {
                Category c = t.Category;
                decimal total = 0;
                if (byCategory.TryGetValue(c, out total))
                {
                    total += t.Amount;
                    byCategory[c] = total;
                    this.GroupTransactions(t, c);
                    found = true;
                }
            }
            return found;
        }

        private void GroupTransactions(Transaction t, Category c)
        {
            List<Transaction> transactions;
            if (!this.transactionsByCategory.TryGetValue(c, out transactions))
            {
                transactions = new List<Transaction>();
                this.transactionsByCategory[c] = transactions;
            }
            if (!transactions.Contains(t))
            {
                transactions.Add(t);
            }
        }

        public override Task Generate(IReportWriter writer)
        {
            this.DelaySaveState();
            this.fiscalYearStart = this.databaseSettings.FiscalYearStart;
            this.transactionsByCategory = new Dictionary<Category, List<Transaction>>();

            ICollection<Transaction> transactions = this.myMoney.Transactions.GetAllTransactionsByDate();

            var (firstYear, lastYear) = this.myMoney.Transactions.GetTaxYearRange(this.fiscalYearStart);

            if (this.startDate == DateTime.MinValue)
            {
                this.SetStartDate(lastYear);
            }
            writer.WriteHeading("W2 Tax Report For Financial Year " + this.startDate.Year);

            var box = panel.ShowFiscalYearRow();
            this.AddFiscalYearItems(box, firstYear, lastYear);

            this.WriteCurrencyHeading(writer, this.DefaultCurrency);

            bool empty = true;
            foreach (TaxForm form in this.taxCategories.GetForms())
            {
                if (this.GenerateForm(form, writer, transactions))
                {
                    empty = false;
                }
            }

            if (empty)
            {
                writer.WriteParagraph("You have not associated any of your categories with Tax Categories.  See the Category Properties dialog for more information.");
            }

            this.WriteTrailer(writer, DateTime.Today);

            return Task.CompletedTask;
        }

        private void AddFiscalYearItems(ComboBox byYearCombo, int firstYear, int lastYear)
        {
            int selected = -1;
            int index = 0;
            byYearCombo.Items.Clear();
            for (int i = lastYear; i >= firstYear; i--)
            {
                if (this.fiscalYearStart > 0 && i == this.endDate.Year)
                {
                    selected = index;
                }
                else if (this.fiscalYearStart == 0 && i == this.startDate.Year)
                {
                    selected = index;
                }
                if (this.fiscalYearStart > 0)
                {
                    byYearCombo.Items.Add("FY " + i);
                }
                else
                {
                    byYearCombo.Items.Add(i.ToString());
                }
                index++;
            }

            if (selected != -1)
            {
                byYearCombo.SelectedIndex = selected;
            }
        }

        private void OnFiscalYearChanged(object sender, string label)
        {
            if (label.StartsWith(FiscalPrefix))
            {
                label = label.Substring(FiscalPrefix.Length);
            }
            if (int.TryParse(label, out int year))
            {
                this.SetStartDate(year);
                this.Regenerate();
            }
        }

        private bool GenerateForm(TaxForm form, IReportWriter writer, ICollection<Transaction> transactions)
        {
            var byCategory = new Dictionary<Category, decimal>();

            // could be one to many mapping.
            Dictionary<TaxCategory, List<Category>> map = new Dictionary<TaxCategory, List<Category>>();

            // find our matching category 
            foreach (TaxCategory tc in form.Categories)
            {
                foreach (Category c in this.myMoney.Categories.GetCategories())
                {
                    if (c.TaxRefNum == tc.RefNum)
                    {
                        byCategory[c] = 0M;

                        List<Category> list = null;
                        if (!map.TryGetValue(tc, out list))
                        {
                            list = new List<Category>();
                            map[tc] = list;
                        }
                        list.Add(c);
                    }
                }
            }

            bool found = false;

            // summarize the year.
            foreach (Transaction t in transactions)
            {
                if (t.Transfer != null || t.IsDeleted || t.Status == TransactionStatus.Void)
                {
                    continue;
                }
                var date = t.TaxDate;
                bool include = date >= this.startDate && date < this.endDate;
                if (include)
                {
                    found |= this.Summarize(byCategory, t);
                }
            }

            if (!found)
            {
                return false;
            }

            writer.WriteHeading("Form " + form.Name);


            writer.StartTable();
            writer.StartColumnDefinitions();
            writer.WriteColumnDefinition("20", 20, 20); // expander column     
            writer.WriteColumnDefinition("300", 300, 300); // row category name 
            writer.WriteColumnDefinition("100", 100, 00); // row value
            writer.EndColumnDefinitions();

            foreach (TaxCategory tc in form.Categories)
            {
                List<Category> list = null;
                if (map.TryGetValue(tc, out list))
                {
                    if (list.Count > 1)
                    {
                        decimal total = 0;
                        foreach (Category c in list)
                        {
                            total += byCategory[c];
                        }
                        if (total != 0)
                        {
                            writer.StartExpandableRowGroup();

                            // header row for the total.
                            writer.StartRow();
                            writer.StartCell();
                            writer.WriteParagraph(tc.Name);
                            writer.EndCell();
                            writer.StartCell();
                            writer.WriteNumber(total.ToString("N0"));
                            writer.EndCell();
                            writer.EndRow();

                            foreach (Category c in list)
                            {
                                decimal v = byCategory[c];
                                if (v != 0)
                                {
                                    writer.StartRow();
                                    writer.StartCell();
                                    writer.WriteParagraph("    " + c.GetFullName());
                                    writer.EndCell();
                                    writer.StartCell();
                                    writer.WriteNumber(v.ToString("N0"));
                                    this.AddHyperlink(c, writer);
                                    writer.EndCell();
                                    writer.EndRow();
                                }
                            }

                            writer.EndExpandableRowGroup();
                        }

                    }
                    else if (list.Count == 1)
                    {
                        Category c = list[0];
                        decimal v = byCategory[c];
                        if (v != 0)
                        {
                            writer.StartRow();
                            writer.StartCell(); // null expander
                            writer.EndCell();
                            writer.StartCell();
                            writer.WriteParagraph(tc.Name);
                            writer.EndCell();
                            writer.StartCell();
                            writer.WriteNumber(v.ToString("N0"));
                            this.AddHyperlink(c, writer);
                            writer.EndCell();
                            writer.EndRow();
                        }
                    }
                }

            }
            writer.EndTable();

            return true;
        }

        private void AddHyperlink(Category c, IReportWriter writer)
        {
            if (writer is FlowDocumentReportWriter fw)
            {
                Paragraph p = fw.CurrentParagraph;
                p.Tag = c;
                p.PreviewMouseLeftButtonDown += this.OnReportCellMouseDown;
                p.Cursor = Cursors.Arrow;
                p.SetResourceReference(Paragraph.ForegroundProperty, "HyperlinkForeground");
            }
            else
            {
                writer.WriteParagraph(c.Name);
            }
        }

        private string GetCategoryCaption(Category c)
        {
            return c.Name;
        }

        private void OnReportCellMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var view = (FlowDocumentView)this.ServiceProvider.GetService(typeof(FlowDocumentView));
            Paragraph p = (Paragraph)sender;
            this.selectedCategory = p.Tag as Category;
            this.downPos = e.GetPosition(view);
        }


        public override void Export(string filename)
        {
            throw new NotImplementedException();
        }
    }

}
