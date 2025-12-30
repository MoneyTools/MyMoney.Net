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
using static Walkabout.Data.CsvStore;

namespace Walkabout.Importers
{
    public class Exporters
    {
        private readonly HashSet<Account> accounts = new HashSet<Account>();

        public bool SupportXml { get; set; }

        public void ExportPrompt(IEnumerable<object> data)
        {
            SaveFileDialog sd = new SaveFileDialog();
            string filter = Properties.Resources.CsvFileFilter;
            if (this.SupportXml)
            {
                filter += "|" + Properties.Resources.XmlFileFilter;
            }
            sd.Filter = filter;

            if (sd.ShowDialog(App.Current.MainWindow) == true)
            {
                this.Export(sd.FileName, data);
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
                        this.ExportToXml(writer, data);
                        writer.WriteEndElement();
                    }
                    InternetExplorer.EditTransform(IntPtr.Zero, fileName);
                }
                else if (ext == ".csv")
                {
                    using (StreamWriter sw = new System.IO.StreamWriter(fileName, false, Encoding.UTF8))
                    {
                        this.ExportToCsv(sw, data);
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

        internal string ExportString(string action, IEnumerable<object> data)
        {
            using (StringWriter sw = new StringWriter())
            {
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(sw, settings))
                {
                    writer.WriteStartElement("root");
                    writer.WriteAttributeString("action", action);
                    this.ExportToXml(writer, data);
                    writer.WriteEndElement();
                }
                return sw.ToString();
            }
        }

        private static readonly DataContractSerializer TransactionSerializer = new DataContractSerializer(typeof(Transaction), MyMoney.GetKnownTypes());
        private static readonly DataContractSerializer InvestmentSerializer = new DataContractSerializer(typeof(Investment), MyMoney.GetKnownTypes());
        private static readonly DataContractSerializer SplitSerializer = new DataContractSerializer(typeof(Split), MyMoney.GetKnownTypes());
        private static readonly DataContractSerializer AccountSerializer = new DataContractSerializer(typeof(Account), MyMoney.GetKnownTypes());

        private void ExportToXml(XmlWriter writer, IEnumerable<object> data)
        {
            // write out the referenced accounts first.
            var ns = "http://schemas.vteam.com/Money/2010";
            writer.WriteStartElement("Accounts", ns);

            HashSet<Account> accounts = new HashSet<Account>();
            // Find the unique accounts associated with the data
            foreach (object row in data)
            {
                Transaction t = null;
                if (row is Account a)
                {
                    accounts.Add(a);
                }
                else if (row is Transaction t2)
                {
                    t = t2;
                }
                else if (row is Investment i)
                {
                    t = i.Transaction;
                }
                else if (row is Split s)
                {
                    t = s.Transaction;
                }
                if (t != null && t.Account != null)
                {
                    accounts.Add(t.Account);
                    if (t.Transfer != null && t.Transfer.Transaction != null && t.Transfer.Transaction.Account != null)
                    {
                        accounts.Add(t.Transfer.Transaction.Account);
                    }
                }
            }

            foreach (var a in accounts)
            {
                this.ExportAccount(writer, a);
            }

            writer.WriteEndElement();

            foreach (object row in data)
            {
                if (row is Account a)
                {
                    // ignore it, already handled.
                }
                else if (row is Transaction t)
                {
                    TransactionSerializer.WriteObject(writer, t);
                    if (t.Investment != null)
                    {
                        InvestmentSerializer.WriteObject(writer, t.Investment);
                    }
                }
                else if (row is Investment i)
                {
                    InvestmentSerializer.WriteObject(writer, i);
                }
                else if (row is Split s)
                {
                    SplitSerializer.WriteObject(writer, s);
                }
                else
                {
                    // Then it must be the placeholder element!
                    writer.WriteStartElement("Placeholder", ns);
                    writer.WriteEndElement();
                }
            }
        }

        private void ExportAccount(XmlWriter writer, Account account)
        {
            if (!this.accounts.Contains(account))
            {
                AccountSerializer.WriteObject(writer, account);
                this.accounts.Add(account);
            }
        }

        private void ExportToCsv(StreamWriter writer, IEnumerable<object> data)
        {
            bool first = true;

            OptionalColumnFlags flags = OptionalColumnFlags.None;
            HashSet<string> currencies = new HashSet<string>();
            foreach (object row in data)
            {
                if (row is Transaction t)
                {
                    if (t.Investment != null)
                    {
                        flags |= OptionalColumnFlags.InvestmentInfo;
                    }
                    if (t.SalesTax != 0)
                    {
                        flags |= OptionalColumnFlags.SalesTax;
                    }
                    currencies.Add(t.GetAccountCurrency().Symbol);
                }
            }
            if (currencies.Count > 1)
            {
                flags |= OptionalColumnFlags.Currency;
            }

            foreach (object row in data)
            {
                Transaction t = row as Transaction;
                if (t != null)
                {
                    if (first)
                    {
                        first = false;
                        CsvStore.WriteTransactionHeader(writer, flags);
                    }
                    CsvStore.WriteTransaction(writer, t, flags);
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
                        CsvStore.WriteInvestment(writer, i);
                    }
                    else
                    {
                        LoanPaymentAggregation l = row as LoanPaymentAggregation;
                        if (l != null)
                        {
                            if (first)
                            {
                                first = false;
                                CsvStore.WriteLoanPaymentHeader(writer);
                            };
                            CsvStore.WriteLoanPayment(writer, l);
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

            sg.Save(fileName, this.styleForGraph_Accounts);

        }

        private readonly string styleForGraph_Accounts =
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
