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
using Walkabout.Reports;
using Walkabout.Taxes;
using Walkabout.Views;
using Walkabout.Interfaces.Views;

namespace Walkabout.Taxes
{
    //=========================================================================================
    // This class prepares an estimated W2 from the splits found in paycheck deposits.
    public class W2Report : Report
    {
        FlowDocumentView view;
        MyMoney myMoney;
        int year;
        IServiceProvider serviceProvider;
        Point downPos;
        Category selectedCategory;
        TaxCategoryCollection taxCategories;
        Dictionary<Category, List<Transaction>> transactionsByCategory;

        public W2Report(FlowDocumentView view, MyMoney money, IServiceProvider sp)
        {
            this.myMoney = money;
            this.year = DateTime.Now.Year;
            this.view = view;
            this.serviceProvider = sp;
            view.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            this.taxCategories = new TaxCategoryCollection();

        }

        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Point pos = e.GetPosition(view);

            if (Math.Abs(downPos.X - pos.X) < 5 && Math.Abs(downPos.Y - pos.Y) < 5)
            {
                // navigate to show the cell.Data rows.
                IViewNavigator nav = serviceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                List<Transaction> transactions = null;
                if (selectedCategory != null && transactionsByCategory.TryGetValue(selectedCategory, out transactions))
                {
                    nav.ViewTransactions(transactions);
                }
            }
        }

        public void Regenerate()
        {
            view.Generate(this);
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
                        GroupTransactions(t, c);
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
                    GroupTransactions(t, c);
                    found = true;
                }
            }
            return found;
        }

        private void GroupTransactions(Transaction t, Category c)
        {
            List<Transaction> transactions;
            if (!transactionsByCategory.TryGetValue(c, out transactions))
            {
                transactions = new List<Transaction>();
                transactionsByCategory[c] = transactions;
            }
            if (!transactions.Contains(t))
            {
                transactions.Add(t);
            }
        }

        public override void Generate(IReportWriter writer)
        {
            this.transactionsByCategory = new Dictionary<Category, List<Transaction>>();
            FlowDocumentReportWriter fwriter = (FlowDocumentReportWriter)writer;
            writer.WriteHeading("Select year for report: ");

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
            Paragraph heading = fwriter.CurrentParagraph;

            ComboBox byYearCombo = new ComboBox();
            byYearCombo.Margin = new System.Windows.Thickness(5, 0, 0, 0);
            int selected = -1;
            for (int i = startYear; i <= lastYear; i++)
            {
                if (i == this.year)
                {
                    selected = i;
                }
                byYearCombo.Items.Add(i);
            }

            byYearCombo.SelectedItem = selected != -1 ? selected : lastYear;
            byYearCombo.SelectionChanged += OnYearChanged;
            byYearCombo.Margin = new Thickness(10, 0, 0, 0);

            heading.Inlines.Add(new InlineUIContainer(byYearCombo));

            bool empty = true;
            foreach (TaxForm form in taxCategories.GetForms())
            {
                if (GenerateForm(form, writer, transactions))
                {
                    empty = false;
                }
            }

            if (empty)
            {
                writer.WriteParagraph("You have not associated any of your categories with Tax Categories.  See the Category Properties dialog for more information.");
            }

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);

        }

        bool GenerateForm(TaxForm form, IReportWriter writer, ICollection<Transaction> transactions)
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
                if (t.Date.Year == this.year)
                {
                    found |= Summarize(byCategory, t);
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
                                    AddHyperlink(c, writer);
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
                            AddHyperlink(c, writer);
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
            p.PreviewMouseLeftButtonDown += OnReportCellMouseDown;
            p.Cursor = Cursors.Arrow;
            p.SetResourceReference(Paragraph.ForegroundProperty, "HyperlinkForeground");
        }

        private void OnYearChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox box = (ComboBox)sender;
            this.year = (int)box.SelectedItem;
            this.Regenerate();
        }

        string GetCategoryCaption(Category c)
        {
            return c.Name;
        }

        private void OnReportCellMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Paragraph p = (Paragraph)sender;
            selectedCategory = p.Tag as Category;
            downPos = e.GetPosition(this.view);
        }


        public override void Export(string filename)
        {
            throw new NotImplementedException();
        }
    }

}
