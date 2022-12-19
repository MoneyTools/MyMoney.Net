using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using System.Xml;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Migrate
{
    public class Exporters
    {
        private HashSet<Account> accounts = new HashSet<Account>();

        public bool SupportXml { get; set; }

        public void ExportPrompt(IEnumerable<object> data)
        {
            SaveFileDialog sd = new SaveFileDialog();
            string filter = Properties.Resources.CsvFileFilter;
            if (SupportXml)
            {
                filter += "|" + Properties.Resources.XmlFileFilter;
            }
            sd.Filter = filter;

            if (sd.ShowDialog(App.Current.MainWindow) == true)
            {
                Export(sd.FileName, data);
            }
        }

        public void Export(string fileName, IEnumerable<object> data)
        {
            try
            {
                string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                if (ext == ".xml")
                {
                    XmlWriterSettings s = new XmlWriterSettings();
                    s.Indent = true;
                    using (XmlWriter writer = XmlWriter.Create(fileName))
                    {
                        writer.WriteStartElement("root");
                        ExportToXml(writer, data);
                        writer.WriteEndElement();
                    }
                    InternetExplorer.EditTransform(IntPtr.Zero, fileName);
                }
                else if (ext == ".csv")
                {
                    using (StreamWriter sw = new System.IO.StreamWriter(fileName, false, Encoding.UTF8))
                    {
                        ExportToCsv(sw, data);
                    }
                    InternetExplorer.OpenUrl(IntPtr.Zero, fileName);
                }
                else
                {
                    throw new Exception("Expecting either .xml or .csv file extension");
                }
            }
            catch (Exception e)
            {
                MessageBoxEx.Show("Error exporting rows\n" + e.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal string ExportString(IEnumerable<object> data)
        {
            using (StringWriter sw = new StringWriter())
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(sw, settings))
                {
                    writer.WriteStartElement("root");
                    ExportToXml(writer, data);
                    writer.WriteEndElement();
                }
                return sw.ToString();
            }
        }


        static DataContractSerializer TransactionSerializer = new DataContractSerializer(typeof(Transaction));
        static DataContractSerializer InvestmentSerializer = new DataContractSerializer(typeof(Investment));
        static DataContractSerializer SplitSerializer = new DataContractSerializer(typeof(Split));
        static DataContractSerializer AccountSerializer = new DataContractSerializer(typeof(Account));

        void ExportToXml(XmlWriter writer, IEnumerable<object> data)
        {
            // write out the referenced accounts first.
            writer.WriteStartElement("Accounts");

            foreach (object row in data)
            {
                Transaction t = row as Transaction;
                Investment i = row as Investment;
                Split s = row as Split;

                if (i != null)
                {
                    t = i.Transaction;
                }
                if (t != null)
                {
                    ExportAccount(writer, t.Account);
                    if (t.Transfer != null)
                    {
                        ExportAccount(writer, t.Transfer.Transaction.Account);
                    }
                }
                else if (s != null)
                {
                    ExportAccount(writer, s.Transaction.Account);
                    if (s.Transfer != null)
                    {
                        ExportAccount(writer, s.Transfer.Transaction.Account);
                    }
                }
            }

            writer.WriteEndElement();

            foreach (object row in data)
            {
                Transaction t = row as Transaction;
                Investment i = row as Investment;
                Split s = row as Split;
                if (t != null)
                {
                    TransactionSerializer.WriteObject(writer, t);
                    i = t.Investment;
                }

                if (i != null)
                {
                    InvestmentSerializer.WriteObject(writer, i);
                }

                if (s != null)
                {
                    SplitSerializer.WriteObject(writer, s);
                }
                else if (t == null && i == null)
                {
                    throw new Exception("Row type " + row.GetType().FullName + " not supported");
                }
            }
        }

        private void ExportAccount(XmlWriter writer, Account account)
        {
            if (!this.accounts.Contains(account))
            {
                AccountSerializer.WriteObject(writer, account);
                accounts.Add(account);
            }
        }

        void ExportToCsv(StreamWriter writer, IEnumerable<object> data)
        {
            bool first = true;
            bool containsInvestmentInfo = false;
            foreach (object row in data)
            {
                if (row is Transaction t && t.Investment != null)
                {
                    containsInvestmentInfo = true;
                }
            }

            foreach (object row in data)
            {
                Transaction t = row as Transaction;
                if (t != null)
                {
                    if (first)
                    {
                        first = false;
                        if (containsInvestmentInfo)
                        {
                            CsvStore.WriteInvestmentHeader(writer);
                        }
                        else
                        {
                            CsvStore.WriteTransactionHeader(writer);
                        }
                    };
                    if (t.Investment != null)
                    {
                        CsvStore.WriteInvestment(writer, t);
                    }
                    else if (containsInvestmentInfo)
                    {
                        CsvStore.WriteInvestmentTransaction(writer, t);
                    }
                    else
                    {
                        CsvStore.WriteTransaction(writer, t);
                    }
                }
                else
                {
                    Investment i = row as Investment;
                    if (i != null)
                    {
                        if (first)
                        {
                            first = false;
                            CsvStore.WriteInvestmentHeader(writer);
                        };
                        CsvStore.WriteInvestment(writer, i.Transaction);
                    }
                    else
                    {
                        LoanPaymentAggregation l = row as LoanPaymentAggregation;
                        if (l != null)
                        {
                            if (first)
                            {
                                first = false;
                                writer.WriteLine("Date,Account,Payment,Percentage,Principal,Interest,Balance");
                            };

                            writer.WriteLine("\"{0}\",\"{1}\",\"{2}\",\"{3}%\",\"{4}\",\"{5}\",\"{6}\"",
                                l.Date.ToShortDateString(),
                                l.Account,
                                l.Payment.ToString("C2"),
                                l.Percentage.ToString("N3"),
                                l.Principal.ToString("C2"),
                                l.Interest.ToString("C2"),
                                l.Balance.ToString("C2")
                                );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Create graph showing how accounts are related via Transfer transactions.
        /// </summary>
        /// <param name="myMoney">The money data to analyze</param>
        /// <param name="fileName">The file name for the DGML graph output</param>
        public void ExportDgmlAccountMap(MyMoney myMoney, string fileName)
        {
            SimpleGraph sg = new SimpleGraph();

            foreach (Transaction t in myMoney.Transactions)
            {
                SimpleGraphNode n = sg.AddOrGetNode(t.AccountName);
                n.Category = t.Account.Type.ToString();


                // Look for Transfer FROM transactions
                if (t.TransferTo != null && t.Amount < 0)
                {
                    // Create a Link that shows a IncomingAccount To This Account
                    SimpleGraphLink link = sg.GetOrAddLink(t.AccountName, t.TransferTo);

                    SimpleGraphProperty sgp = link.GetProperty("Label");

                    if (sgp == null)
                    {
                        sgp = link.AddProperty("Label", 0);
                    }

                    sgp.Value = Convert.ToDecimal(sgp.Value) + Math.Abs(t.Amount);
                }


            }

            //
            // Convert all Label that looks like numeric values into a "Currency" look
            //
            foreach (SimpleGraphLink l in sg.Links)
            {
                try
                {
                    SimpleGraphProperty sgp = l.GetProperty("Label");
                    sgp.Value = Convert.ToDecimal(sgp.Value).ToString("C2");
                }
                catch
                {
                }
            }

            sg.Save(fileName, styleForGraph_Accounts);

        }

        private string styleForGraph_Accounts =
                @"<Styles>
                        <Style TargetType=""Node"" GroupLabel=""Asset"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('Asset')"" />
                            <Setter Property=""Background"" Value=""#FFFF8000"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""Checking"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('Checking')"" />
                            <Setter Property=""Background"" Value=""#FF008000"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""Savings"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('Savings')"" />
                            <Setter Property=""Background"" Value=""#DD008000"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""Credit"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('Credit')"" />
                            <Setter Property=""Background"" Value=""Red"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""Loan"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('Loan')"" />
                            <Setter Property=""Background"" Value=""Pink"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""InvestmentCash"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('InvestmentCash')"" />
                            <Setter Property=""Background"" Value=""#FF400040"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""Investment"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('Investment')"" />
                            <Setter Property=""Background"" Value=""#FF000000"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""MoneyMarket"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('MoneyMarket')"" />
                            <Setter Property=""Background"" Value=""#FF808080"" />
                        </Style>
                        <Style TargetType=""Node"" GroupLabel=""CategoryFund"" ValueLabel=""True"">
                            <Condition Expression=""HasCategory('CategoryFund')"" />
                            <Setter Property=""Background"" Value=""#FF400040"" />
                        </Style>
                  </Styles>";

    }
}
