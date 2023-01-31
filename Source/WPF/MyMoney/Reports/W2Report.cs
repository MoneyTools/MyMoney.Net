using System;
using System.Collections.Generic;
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

namespace Walkabout.Taxes
{
    //=========================================================================================
    // This class prepares an estimated W2 from the splits found in paycheck deposits.
    public class W2Report : Report
    {
        private readonly FlowDocumentView view;
        private readonly MyMoney myMoney;
        private DateTime startDate;
        private DateTime endDate;
        private readonly IServiceProvider serviceProvider;
        private Point downPos;
        private readonly int fiscalYearStart;
        private Category selectedCategory;
        private readonly TaxCategoryCollection taxCategories;
        private Dictionary<Category, List<Transaction>> transactionsByCategory;
        private const string FiscalPrefix = "FY ";

        public W2Report(FlowDocumentView view, MyMoney money, IServiceProvider sp, int fiscalYearStart)
        {
            this.myMoney = money;
            this.fiscalYearStart = fiscalYearStart;
            this.view = view;
            this.serviceProvider = sp;
            view.PreviewMouseLeftButtonUp -= this.OnPreviewMouseLeftButtonUp;
            view.PreviewMouseLeftButtonUp += this.OnPreviewMouseLeftButtonUp;
            view.Unloaded += (s, e) =>
            {
                view.PreviewMouseLeftButtonUp -= this.OnPreviewMouseLeftButtonUp;
            };
            this.taxCategories = new TaxCategoryCollection();
        }


        private void SetStartDate(DateTime date)
        {
            if (this.fiscalYearStart > 0)
            {
                if (date.Month >= this.fiscalYearStart + 1)
                {
                    this.startDate = new DateTime(date.Year, this.fiscalYearStart + 1, 1);
                }
                else
                {
                    this.startDate = new DateTime(date.Year - 1, this.fiscalYearStart + 1, 1);
                }
            }
            else
            {
                this.startDate = new DateTime(date.Year, 1, 1);
            }
            this.endDate = this.startDate.AddYears(1);
        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(this.view);

            if (Math.Abs(this.downPos.X - pos.X) < 5 && Math.Abs(this.downPos.Y - pos.Y) < 5)
            {
                // navigate to show the cell.Data rows.
                IViewNavigator nav = this.serviceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                List<Transaction> transactions = null;
                if (this.selectedCategory != null && this.transactionsByCategory.TryGetValue(this.selectedCategory, out transactions))
                {
                    nav.ViewTransactions(transactions);
                }
            }
        }

        public void Regenerate()
        {
            _ = this.view.Generate(this);
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
            this.transactionsByCategory = new Dictionary<Category, List<Transaction>>();
            FlowDocumentReportWriter fwriter = (FlowDocumentReportWriter)writer;
            writer.WriteHeading("Select year for report: ");

            ICollection<Transaction> transactions = this.myMoney.Transactions.GetAllTransactionsByDate();

            var (firstYear, lastYear) = this.myMoney.Transactions.GetTaxYearRange(this.fiscalYearStart);

            if (this.startDate == DateTime.MinValue)
            {
                this.SetStartDate(new DateTime(lastYear, 1, 1));
            }

            Paragraph heading = fwriter.CurrentParagraph;

            ComboBox byYearCombo = new ComboBox();
            byYearCombo.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            int selected = -1;
            int index = 0;
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
            byYearCombo.SelectionChanged += this.OnYearChanged;
            byYearCombo.Margin = new Thickness(10, 0, 0, 0);
            this.AddInline(heading, byYearCombo);

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

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);
            return Task.CompletedTask;
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
            FlowDocumentReportWriter fw = (FlowDocumentReportWriter)writer;
            Paragraph p = fw.CurrentParagraph;
            p.Tag = c;
            p.PreviewMouseLeftButtonDown += this.OnReportCellMouseDown;
            p.Cursor = Cursors.Arrow;
            p.SetResourceReference(Paragraph.ForegroundProperty, "HyperlinkForeground");
        }

        private void OnYearChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = (ComboBox)sender;
            string label = (string)box.SelectedItem;
            if (label.StartsWith(FiscalPrefix))
            {
                label = label.Substring(FiscalPrefix.Length);
            }
            if (int.TryParse(label, out int year))
            {
                var start = new DateTime(year, this.fiscalYearStart + 1, 1);
                if (this.fiscalYearStart > 0)
                {
                    start = start.AddYears(-1);
                }
                this.SetStartDate(start);
                this.Regenerate();
            }
        }

        private string GetCategoryCaption(Category c)
        {
            return c.Name;
        }

        private void OnReportCellMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Paragraph p = (Paragraph)sender;
            this.selectedCategory = p.Tag as Category;
            this.downPos = e.GetPosition(this.view);
        }


        public override void Export(string filename)
        {
            throw new NotImplementedException();
        }
    }

}
