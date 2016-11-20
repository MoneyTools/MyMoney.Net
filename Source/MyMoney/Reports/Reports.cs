#define ALL
#define XAML

using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Walkabout.Data;
using Walkabout.Utilities;
using Walkabout.Configuration;
using Walkabout.Taxes;
using System.Windows;
using System.Windows.Controls.DataVisualization.Charting;
using System.Windows.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Controls;
using Microsoft.Win32;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Walkabout.Reports
{

    public abstract class Report : IReport
    {
        public abstract void Generate(IReportWriter writer);

        public abstract void Export(string filename);

        protected void ExportReportAsCsv()
        {
            SaveFileDialog fd = new SaveFileDialog();
            fd.CheckPathExists = true;
            fd.AddExtension = true;
            fd.Filter = "CSV File (.csv)|*.csv";
            fd.FileName = this.GetType().Name;

            if (fd.ShowDialog(App.Current.MainWindow) == true)
            {
                try
                {
                    string filename = fd.FileName;
                    this.Export(filename);
                    InternetExplorer.OpenUrl(IntPtr.Zero, filename);
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error Exporting .txf", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public Button CreateReportButton(string relativeIconPath, string caption, string toolTip)
        {
            Button button = new Button();
            button.ToolTip = toolTip;

            StackPanel panel = new StackPanel()
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(2)
            };

            Image img = new Image();
            BitmapFrame frame = BitmapFrame.Create(new Uri("pack://application:,,,/MyMoney;component/" + relativeIconPath));
            img.Source = frame;
            img.Width = frame.Width;
            img.Height = frame.Height;
            panel.Children.Add(img);

            panel.Children.Add(new TextBlock()
            {
                Text = caption,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });

            button.Content = panel;
            return button;
        }

    }

    //=========================================================================================
    public class UnacceptedReport : IReport
    {

        MyMoney myMoney;

        public UnacceptedReport(MyMoney money)
        {
            this.myMoney = money;
        }

        public void Generate(IReportWriter writer)
        {
            writer.WriteHeading("Unaccepted Transactions");

            Transactions transactions = myMoney.Transactions;
            foreach (Account a in this.myMoney.Accounts.GetAccounts(true))
            {
                if (a.IsClosed)
                {
                    continue;
                }

                bool first = true;

                foreach (Transaction t in this.myMoney.Transactions.GetTransactionsFrom(a))
                {
                    if (t.Unaccepted)
                    {
                        if (first)
                        {
                            writer.EndTable();
                            writer.WriteHeading(a.Name);
                            writer.StartTable();

                            writer.StartColumnDefinitions();
                            foreach (double minWidth in new double[] { 100, 300, 120 })
                            {
                                writer.WriteColumnDefinition(minWidth.ToString(), minWidth, double.MaxValue);
                            }
                            writer.EndColumnDefinitions();

                            writer.StartHeaderRow();
                            foreach (string header in new string[] { "Date", "Payee/Category/Memo", "Amount", })
                            {
                                writer.StartCell();
                                writer.WriteParagraph(header);
                                writer.EndCell();
                            }
                            writer.EndRow();

                            first = false;
                        }
                        WriteRow(writer, t.Date.ToShortDateString(), t.PayeeName ?? string.Empty, t.Amount.ToString("C"));
                        WriteRow(writer, string.Empty, t.CategoryName ?? string.Empty, string.Empty);
                        WriteRow(writer, string.Empty, t.Memo ?? string.Empty, string.Empty);
                    }
                }

            }

            writer.EndTable();

            writer.WriteParagraph("Generated on " + DateTime.Today.ToLongDateString(), System.Windows.FontStyles.Italic, System.Windows.FontWeights.Normal, System.Windows.Media.Brushes.Gray);
        }

        private static void WriteRow(IReportWriter writer, string col1, string col2, string col3)
        {
            writer.StartRow();
            writer.StartCell();
            writer.WriteParagraph(col1);
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph(col2);
            writer.EndCell();

            writer.StartCell();
            writer.WriteParagraph(col3);
            writer.EndCell();

            writer.EndRow();
        }



        public void Export(string filename)
        {
            throw new NotImplementedException();
        }
    }


}
