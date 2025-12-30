using ModernWpf.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Utilities;

namespace Walkabout.Importers
{
    public class CsvFieldMap
    {
        public CsvFieldMap() { }
        public string Header { get; set; }
        public string Field { get; set; }
    }

    public class CsvMap
    {
        public CsvMap() { }

        [XmlIgnore]
        public string FileName { get; set; }

        public bool Negate { get; set; }

        public List<CsvFieldMap> Fields { get; set; }

        public static CsvMap Load(string fileName)
        {
            if (File.Exists(fileName))
            {
                using (var reader = XmlReader.Create(fileName))
                {
                    var s = new XmlSerializer(typeof(CsvMap));
                    var map = (CsvMap)s.Deserialize(reader);
                    map.FileName = fileName;
                    return map;
                }
            }
            return new CsvMap() { FileName = fileName };
        }

        public void Save()
        {
            var settings = new XmlWriterSettings() { Indent = true };
            using (var writer = XmlWriter.Create(this.FileName, settings))
            {
                var s = new XmlSerializer(typeof(CsvMap));
                s.Serialize(writer, this);
            }
        }

        internal void CopyFrom(CsvMap mapping)
        {
            this.Fields = mapping.Fields;
            this.Negate = mapping.Negate;
        }
    }

    internal class UserCanceledException : Exception
    {
        public UserCanceledException(string msg) : base(msg) { }
    }

    internal class CsvTransactionImporter
    {
        private readonly MyMoney money;
        private readonly Account account;
        private readonly CsvMap map;
        private readonly List<TBag> typedData = new List<TBag>();
        private readonly Regex numericRegex = new Regex(@"([+-]?[\d,.]+)");
        private readonly string[] fields;
        private readonly DownloadData data;

        // this is what we "can" import...must match the values in the MapField switch statement.
        public static string[] BankAccountFields = new string[] { "Date", "Payee", "Memo", "Amount", "FITID" };
        public static string[] BrokerageAccountFields = new string[] { "Date", "Payee", "Memo", "Action", "Symbol", "TradeType", "UnitPrice", "Quantity", "Amount", "FITID" };

        public CsvTransactionImporter(MyMoney money, Account account, CsvMap map, DownloadData data, string[] fields)
        {
            this.money = money;
            this.account = account;
            this.map = map;
            this.fields = fields;
            this.data = data;
        }

        public static Dictionary<Account, CsvDocument> GroupCsvByAccount(MyMoney money, CsvDocument csv)
        {
            int accountNumberIndex = csv.Headers.IndexOf("Account Number");
            int accountNameIndex = csv.Headers.IndexOf("Account");
            Dictionary<string, string> accounts = new Dictionary<string, string>();
            Dictionary<Account, CsvDocument> groupedByAccount = new Dictionary<Account, CsvDocument>();
            foreach (var row in csv.Rows)
            {
                string accountNumber = null;
                string accountName = null;
                if (accountNumberIndex >= 0 && accountNumberIndex < row.Count)
                {
                    accountNumber = row[accountNumberIndex];
                }
                if (accountNameIndex >= 0 && accountNameIndex < row.Count)
                {
                    accountName = row[accountNameIndex];
                }
                if (!string.IsNullOrEmpty(accountNumber))
                {
                    Account found = null;
                    foreach (Account a in money.Accounts.GetAccounts())
                    {
                        if (a.AccountId == accountNumber || a.OfxAccountId == accountNumber)
                        {
                            found = a;
                            break;
                        }
                    }
                    if (found == null)
                    {
                        var prompt = $"Please select Account to import the CSV transactions from '{accountName}', amd make sure it has account number {accountNumber}";
                        Account template = new Account();
                        template.AccountId = accountNumber;
                        template.Name = accountName;
                        found = AccountHelper.PickAccount(money, template, prompt);
                    }
                    if (found != null)
                    {
                        // Create new account specific CsvDocument that does not contain the Account and AccountNumber columns.
                        if (!groupedByAccount.ContainsKey(found))
                        {
                            var doc = new CsvDocument();
                            groupedByAccount[found] = doc;
                            doc.Headers.AddRange(csv.Headers);
                            doc.Headers.Remove("Account");
                            doc.Headers.Remove("Account Number");
                        }
                        if (accountNumberIndex >= 0)
                        {
                            if (accountNameIndex > accountNumberIndex)
                            {
                                accountNameIndex--;
                            }
                            row.RemoveAt(accountNumberIndex);
                        }
                        if (accountNameIndex >= 0)
                        {
                            row.RemoveAt(accountNameIndex);
                        }
                        groupedByAccount[found].Rows.Add(row);
                    }
                }
            }
            return groupedByAccount;
        }

        public void Commit()
        {
            // ok, we have typed data, no exceptions, so merge with the account.
            if (this.typedData.Count == 0)
            {
                return;
            }
            try
            {
                this.money.BeginUpdate(this);
                var existing = this.money.Transactions.GetTransactionsFrom(this.account);
                var cache = new TransactionCache(existing);
                // CSV doesn't always provide an FITID so each import can create tons of duplicates.
                // So we need constant time lookup by date so that import can quickly match existing matching transactions.
                Dictionary<DateTime, object> indexed = new Dictionary<DateTime, object>();
                

                foreach (var bag in this.typedData)
                {
                    Transaction found = cache.FindMatch(bag);
                    if (found == null)
                    {
                        Transaction t = this.money.Transactions.NewTransaction(this.account);
                        t.Flags = TransactionFlags.Unaccepted;
                        t.Status = TransactionStatus.Electronic;
                        t.Payee = bag.Payee;
                        t.Amount = bag.Amount;
                        t.Memo = bag.Memo;
                        t.Date = bag.Date;
                        t.FITID = bag.FITID;

                        if (this.account.Type == AccountType.Brokerage || this.account.Type == AccountType.Retirement)
                        {
                            this.AddInvestmentInfo(t, bag);
                        }

                        this.money.Transactions.Add(t);
                        found = t;
                    }
                    else if (found.Status == TransactionStatus.None)
                    {
                        found.Status = TransactionStatus.Electronic;
                    }
                    found.IsDownloaded = true;
                    this.data.AddItem(found);
                }
            }
            finally
            {
                this.money.EndUpdate();
            }
        }

        private void AddInvestmentInfo(Transaction t, TBag bag)
        {
            var symbol = bag.Symbol;
            if (!string.IsNullOrEmpty(symbol) || bag.Quantity != 0)
            {
                var i = t.GetOrCreateInvestment();
                i.Security = bag.Security;
                i.UnitPrice = bag.UnitPrice;
                i.Units = bag.Quantity;
                if (bag.Quantity == 0)
                {
                    // then could be dividends
                    string memo = (t.Memo + "").ToLowerInvariant();
                    if (memo.Contains("dividend"))
                    {
                        i.Type = InvestmentType.Dividend;
                        t.Category = this.money.Categories.InvestmentDividends;
                    }
                    else if (memo.Contains("interest"))
                    {
                        t.Category = this.money.Categories.InvestmentInterest;
                    }
                }
                else
                {
                    if (bag.Quantity > 0)
                    {
                        i.Type = bag.TradeType == "Shares" ? InvestmentType.Add : InvestmentType.Buy;
                    }
                    else
                    {
                        i.Type = bag.TradeType == "Shares" ? InvestmentType.Remove : InvestmentType.Sell;
                    }
                }
            }
        }

        public void EditCsvMap(IEnumerable<string> headers)
        {
            CsvImportDialog cd = new CsvImportDialog(this.fields);
            cd.Owner = System.Windows.Application.Current.MainWindow;
            if (headers != null)
            {
                cd.SetHeaders(headers);
            }
            else
            {
                cd.SetMap(this.map);
            }
            if (cd.ShowDialog() == true)
            {
                this.map.CopyFrom(cd.Mapping);
                this.map.Save();
            }
            else
            {
                throw new UserCanceledException("User cancelled");
            }
        }

        internal int Import(CsvDocument csv)
        {
            if (!this.HeadersMatch(csv.Headers))
            {
                this.EditCsvMap(csv.Headers);
            }
            foreach (var row in csv.Rows)
            {
                this.ImportRow(row);
            }
            return csv.Rows.Count;
        }

        private bool HeadersMatch(IEnumerable<string> headers)
        {
            if (this.map.Fields != null)
            {
                int c = 0;
                foreach (var h in headers)
                {
                    if (c >= this.map.Fields.Count)
                    {
                        return false;
                    }
                    else if (this.map.Fields[c].Header != h)
                    {
                        return false;
                    }
                    c++;
                }
                return true;
            }
            return false;
        }

        private void ImportRow(List<string> values)
        {
            // Rows that contain only one value are like comments in a .csv file containing disclaimer info.
            if (this.map == null || values.Count == 0 || values.Count == 1)
            {
                return;
            }

            int col = 0;
            TBag t = new TBag();
            foreach (var s in values)
            {
                if (col < this.map.Fields.Count)
                {
                    var fm = this.map.Fields[col];
                    if (!string.IsNullOrEmpty(fm.Field))
                    {
                        this.MapField(t, fm, s);
                    }
                }
                col++;
            }

            if (!string.IsNullOrEmpty(t.Symbol))
            {
                Security s = this.money.Securities.FindSymbol(t.Symbol.Trim(), false);
                if (s == null)
                {
                    // then this is a new security, so create it and use the payee name.
                    s = this.money.Securities.FindSymbol(t.Symbol.Trim(), true);
                    if (t.Payee != null)
                    {
                        s.Name = t.Payee.Name;
                    }
                }
                else
                {
                    // then the payee is nicer if it just matches the security name.
                    t.Payee = this.money.Payees.FindPayee(s.Name, true);
                }
                t.Security = s;
            }
            this.typedData.Add(t);
        }

        private void MapField(TBag t, CsvFieldMap field, string value)
        {
            string fieldName = field.Field;
            value = value.Trim();
            // matches this.fields.
            switch (fieldName)
            {
                case "Date":
                    if (DateTime.TryParse(value, out DateTime d))
                    {
                        t.Date = d;
                    }
                    break;
                case "Payee":
                    if (value == "No Description" && !string.IsNullOrEmpty(t.Memo))
                    {
                        // this is a hack for the Fidelity .csv files where sometimes the Payee is just "No Description"
                        value = t.Memo;
                    }
                    Alias alias = this.money.Aliases.FindMatchingAlias(value);
                    if (alias != null)
                    {
                        if (alias.Payee.Name != value && string.IsNullOrEmpty(t.Memo))
                        {
                            t.Memo = value;
                        }
                        t.Payee = alias.Payee;
                    }
                    else
                    {
                        t.Payee = this.money.Payees.FindPayee(value, true);
                    }
                    break;
                case "Memo":
                    t.Memo = value;
                    break;
                case "FITID":
                    t.FITID = value;
                    break;
                case "Amount":
                    var match = numericRegex.Match(value);
                    if (match.Success && decimal.TryParse(match.Groups[1].Value, out decimal amount))
                    {
                        if (map.Negate)
                        {
                            t.Amount = -amount;
                        }
                        else
                        {
                            t.Amount = amount;
                        }
                    }
                    break;
                case "Account":
                    t.Account = value;
                    break;
                case "AccountNumber":
                    t.AccountNumber = value;
                    break;
                case "Symbol":
                    t.Symbol = value;
                    break;
                case "TradeType":
                    t.TradeType = value;
                    break;
                case "UnitPrice":
                    if (decimal.TryParse(value, out decimal up))
                    {
                        t.UnitPrice = up;
                    }
                    break;
                case "Quantity":
                    if (decimal.TryParse(value, out decimal q))
                    {
                        t.Quantity = q;
                    }
                    break;
            }
        }

        internal class TBag
        {
            public DateTime Date;
            public string Memo;
            public decimal Amount;
            public Payee Payee;
            public string FITID;
            // Fidelity brokerage .csv fields.
            public string Account;
            public string AccountNumber;
            public string Symbol;
            public Security Security;
            public string TradeType;
            public decimal UnitPrice;
            public decimal Quantity;            
        }


        internal class TransactionCache
        {
            Dictionary<DateTime, object> indexed = new Dictionary<DateTime, object>();

            public TransactionCache(IList<Transaction> existing)
            {
                // CSV doesn't always provide an FITID so each import can create tons of duplicates.
                // So we need constant time lookup by date so that import can quickly match existing matching transactions.
                foreach (var t in existing)
                {
                    if (indexed.TryGetValue(t.Date, out object o))
                    {
                        if (o is List<Transaction> list)
                        {
                            list.Add(t);
                        }
                        else if (o is Transaction u)
                        {
                            var newList = new List<Transaction>();
                            newList.Add(u);
                            newList.Add(t);
                            indexed[t.Date] = newList;
                        }
                    }
                    else
                    {
                        indexed[t.Date] = t;
                    }
                }
            }

            internal Transaction FindMatch(TBag bag)
            {
                Transaction found = null;
                if (indexed.TryGetValue(bag.Date, out object o))
                {
                    if (o is List<Transaction> list)
                    {
                        foreach (var u in list)
                        {
                            if (this.IsMatch(u, bag))
                            {
                                found = u;
                                break;
                            }
                        }
                    }
                    else if (o is Transaction u)
                    {
                        if (this.IsMatch(u, bag))
                        {
                            found = u;
                        }
                    }
                }
                return found;
            }

            public bool IsMatch(Transaction u, TBag bag)
            {
                var i = u.Investment;
                if (i != null)
                {
                    var s = i.Security;
                    if (s != null && s.Symbol != bag.Symbol)
                    {
                        return false; // no match
                    }
                    if (i.Units != bag.Quantity)
                    {
                        return false; // no match
                    }
                }

                if (u.Payee == bag.Payee && u.Amount == bag.Amount && u.Date == bag.Date)
                {
                    // found a perfect match so skip it!
                    return true;
                }
                return false;
            }
        }

    }
}
