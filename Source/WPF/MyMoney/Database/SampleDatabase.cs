using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.StockQuotes;
using Walkabout.Utilities;

namespace Walkabout.Assitance
{
    /// <summary>
    /// This class creates a sample database, creating ficticious accounts and transactions.
    /// </summary>
    public class SampleDatabase
    {
        private MyMoney money;
        private const int Years = 10;
        private Account checking;
        private string stockQuotePath;
        private StockQuoteManager manager;
        Random rand = new Random();
        Dictionary<string, StockQuoteHistory> quotes = new Dictionary<string, StockQuoteHistory>();

        public SampleDatabase(MyMoney money, StockQuoteManager manager, string stockQuotePath)
        {
            this.manager = manager;
            this.money = money;
            this.stockQuotePath = stockQuotePath;
        }

        public void Create()
        {
            string temp = Path.Combine(Path.GetTempPath(), "MyMoney");
            Directory.CreateDirectory(temp);

            string path = Path.Combine(temp, "SampleData.xml");
            ProcessHelper.ExtractEmbeddedResourceAsFile("Walkabout.Database.SampleData.xml", path);

            SampleDatabaseOptions options = new SampleDatabaseOptions();
            options.Owner = Application.Current.MainWindow;
            options.SampleData = path;
            if (options.ShowDialog() == false)
            {
                return;
            }

            string zipPath = Path.Combine(temp, "SampleStockQuotes.zip");
            ProcessHelper.ExtractEmbeddedResourceAsFile("Walkabout.Database.SampleStockQuotes.zip", zipPath);

            string quoteFolder = Path.Combine(temp, "StockQuotes");
            if (Directory.Exists(quoteFolder))
            {
                Directory.Delete(quoteFolder, true);
            }
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, temp);
            foreach (var file in Directory.GetFiles(quoteFolder))
            {
                var target = Path.Combine(this.stockQuotePath, Path.GetFileName(file));
                if (!File.Exists(target))
                {
                    File.Copy(file, target, true);
                }
            }

            path = options.SampleData;

            double inflation = options.Inflation;

            SampleData data = null;
            XmlSerializer s = new XmlSerializer(typeof(SampleData));
            using (XmlReader reader = XmlReader.Create(path))
            {
                data = (SampleData)s.Deserialize(reader);
            }

            foreach (SampleSecurity ss in data.Securities)
            {
                var history = StockQuoteHistory.Load(quoteFolder, ss.Symbol);
                if (history != null)
                {
                    this.quotes[ss.Symbol] = history;
                    this.manager.DownloadLog.AddHistory(history);
                }
            }
            int totalFrequency = data.GetTotalFrequency();

            List<SampleTransaction> list = new List<SampleTransaction>();
            List<Account> brokerageAccounts = new List<Account>();

            foreach (SampleAccount sa in data.Accounts)
            {
                // Create all the accounts.
                Accounts accounts = this.money.Accounts;
                Account a = accounts.FindAccount(sa.Name);
                if (a == null)
                {
                    a = accounts.AddAccount(sa.Name);
                }
                a.Type = sa.Type;
                if (a.Type == AccountType.Checking)
                {
                    this.checking = a;
                }
                a.TaxStatus = sa.TaxStatus;

                // Create this many transactions
                int count = sa.Frequency;

                // by scaling the payee frequencies to match the above desired count.
                double ratio = count / (double)totalFrequency;

                if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                {
                    brokerageAccounts.Add(a);
                }
                else
                {
                    // create flat list of all payees to choose from so it fits the histogram
                    List<SamplePayee> payees = new List<SamplePayee>();
                    foreach (SamplePayee payee in data.Payees)
                    {
                        switch (payee.Type)
                        {
                            case PaymentType.Debit:
                            case PaymentType.Check:
                                if (sa.Type != AccountType.Checking)
                                {
                                    continue;
                                }
                                break;
                            case PaymentType.Credit:
                                if (sa.Type != AccountType.Credit)
                                {
                                    continue;
                                }
                                break;
                        }

                        foreach (SampleCategory sc in payee.Categories)
                        {
                            int newFrequency = (int)(sc.Frequency * ratio);
                            for (int i = 0; i < newFrequency; i++)
                            {
                                list.Add(new SampleTransaction()
                                {
                                    Account = sa,
                                    Payee = payee,
                                    Category = sc
                                });
                            }
                        }
                    }
                }
            }

            this.money.BeginUpdate(this);
            // create the securities and stock splits
            foreach (var sec in data.Securities)
            {
                Security stock = this.money.Securities.FindSecurity(sec.Name, true);
                if (sec.Splits != null)
                {
                    foreach (var split in sec.Splits)
                    {
                        var exists = this.money.StockSplits.FindStockSplitByDate(stock, split.Date);
                        if (exists == null)
                        {
                            StockSplit stockSplit = this.money.StockSplits.NewStockSplit();
                            stockSplit.Security = stock;
                            stockSplit.Date = split.Date;
                            stockSplit.Numerator = split.Numerator;
                            stockSplit.Denominator = split.Denominator;
                        }
                    }
                }
            }
            this.money.EndUpdate();

            this.CreateRandomTransactions(list, inflation);

            this.AddPaychecks(options.Employer, options.PayCheck, inflation);

            // now with any spare cash we can buy stocks.
            this.CreateInvestmentSamples(data, brokerageAccounts);

            // only have to do this because we hid all update events earlier by doing BeginUpdate/EndUpdate on money object.
            // trigger payee update 
            this.money.Payees.BeginUpdate(false);
            this.money.Payees.EndUpdate();

            // trigger category update
            this.money.Categories.BeginUpdate(false);
            this.money.Categories.EndUpdate();

            this.money.OnLoaded();
        }

        private decimal GetStockMoney(decimal balance)
        {
            if (balance < 0)
            {
                return 0;
            }
            decimal hundreds = Math.Floor(balance / 100);
            return hundreds * 100;
        }

        private decimal GetClosingPrice(string symbol, DateTime date)
        {
            if (this.quotes.TryGetValue(symbol, out StockQuoteHistory history))
            {
                foreach (var quote in history.History)
                {
                    if (quote.Date > date)
                    {
                        return quote.Close;
                    }
                }
            }
            return this.rand.Next(100);
        }

        class Ownership
        {
            // how much of each stock is owned in each account.
            Dictionary<Account, Dictionary<string, decimal>> owned = new Dictionary<Account, Dictionary<string, decimal>>();

            public void AddUnits(Account a, string symbol, decimal units)
            {
                if (!this.owned.TryGetValue(a, out Dictionary<string, decimal> map))
                {
                    map = new Dictionary<string, decimal>();
                    this.owned[a] = map;
                }
                decimal tally = 0;
                map.TryGetValue(symbol, out tally);
                map[symbol] = tally + units;
            }

            public decimal GetUnits(Account a, string symbol)
            {
                decimal result = 0;
                if (this.owned.TryGetValue(a, out Dictionary<string, decimal> map))
                {
                    map.TryGetValue(symbol, out result);
                }
                return result;
            }
        }

        private void CreateInvestmentSamples(SampleData data, List<Account> brokerageAccounts)
        {
            if (brokerageAccounts.Count == 0)
            {
                return;
            }

            // now balance the accounts.
            foreach (Account a in this.money.Accounts.GetAccounts())
            {
                this.money.Rebalance(a);
            }

            // first figure out how much we can spend each year.
            int year = DateTime.Now.Year - 10;
            DateTime start = new DateTime(year, 1, 1); // start in January
            DateTime end = start.AddYears(1);
            Dictionary<int, decimal> cash = new Dictionary<int, decimal>();
            Ownership ownership = new Ownership();
            decimal removed = 0;

            foreach (var t in this.money.Transactions.GetTransactionsFrom(this.checking))
            {
                if (t.Date > end)
                {
                    cash[year] = this.GetStockMoney(t.Balance);
                    end = end.AddYears(1);
                    year++;
                }
            }

            this.money.BeginUpdate(this);

            Dictionary<Account, decimal> cashBalance = new Dictionary<Account, decimal>();
            foreach (var a in brokerageAccounts)
            {
                cashBalance[a] = 0;
            }

            Transactions transactions = this.money.Transactions;
            for (year = DateTime.Now.Year - 10; year <= DateTime.Now.Year; year++)
            {
                cash.TryGetValue(year, out decimal balance);
                balance -= removed;
                if (balance < 100)
                {
                    continue; // not enough.
                }
                decimal startBalance = balance;

                int numberOfTransactionForThatYear = this.rand.Next(5, 100); // up to 100 transactions max per year.

                // keep track of how much we own so we never go negative.
                IList<int> selectedDays = this.GetRandomDaysInTheYearForTransactions(numberOfTransactionForThatYear);

                foreach (var day in selectedDays)
                {
                    // select random security.
                    SampleSecurity ss = data.Securities[this.rand.Next(0, data.Securities.Count)];
                    // Get or Create the security
                    Security stock = this.money.Securities.FindSecurity(ss.Name, true);
                    stock.SecurityType = ss.SecurityType;
                    stock.Symbol = ss.Symbol;

                    // select a random brokerage.
                    Account a = brokerageAccounts[this.rand.Next(brokerageAccounts.Count)];

                    var canSpend = balance + cashBalance[a];
                    if (canSpend < 100)
                    {
                        break;
                    }

                    // the date the purchase or sale was done
                    var date = new DateTime(year, 1, 1).AddDays(day);

                    // How many unit bought or sold
                    var quote = this.GetClosingPrice(ss.Symbol, date);

                    Transaction t = null;
                    decimal owned = ownership.GetUnits(a, ss.Symbol);
                    if (owned > 4 && this.rand.Next(3) == 1)
                    {
                        // make this a sell transaction.
                        // Create a new Transaction
                        t = this.money.Transactions.NewTransaction(a);
                        t.Date = date;
                        //-----------------------------------------------------
                        // Create the matching Investment transaction
                        Investment i = t.GetOrCreateInvestment();
                        i.Transaction = t;
                        i.Security = stock;
                        i.UnitPrice = quote;
                        i.Price = quote;
                        i.Type = InvestmentType.Sell;
                        // Create the a SELL transaction
                        i.Units = this.rand.Next(1, (int)(owned / 2));
                        ownership.AddUnits(a, ss.Symbol, -i.Units);
                        // Calculate the Payment or Deposit amount
                        t.Amount = this.RoundCents(i.Units * i.UnitPrice);
                    }
                    else
                    {
                        int max = (int)(canSpend / quote);
                        if (max > 0)
                        {
                            // Create a new Transaction
                            t = this.money.Transactions.NewTransaction(a);
                            t.Date = date;

                            //-----------------------------------------------------
                            // Create the matching Investment transaction
                            Investment i = t.GetOrCreateInvestment();
                            i.Transaction = t;
                            i.Security = stock;
                            i.UnitPrice = quote;
                            i.Price = quote;
                            i.Type = InvestmentType.Buy;
                            i.Units = this.rand.Next(1, max);
                            ownership.AddUnits(a, ss.Symbol, i.Units);
                            // Calculate the Payment or Deposit amount
                            t.Amount = this.RoundCents(i.Units * i.UnitPrice * -1);
                        }
                    }

                    if (t != null)
                    {
                        t.Payee = this.money.Payees.FindPayee(ss.Name, true);
                        t.Category = this.money.Categories.InvestmentStocks;
                        balance += t.Amount;
                        cashBalance[a] += t.Amount;
                        // Finally add the new transaction
                        this.money.Transactions.AddTransaction(t);
                    }
                }

                foreach (var acct in brokerageAccounts)
                {
                    var amount = cashBalance[acct];
                    if (amount < 0)
                    {
                        // then we need to transfer money to cover the cost of the stock purchases.
                        Transaction payment = transactions.NewTransaction(this.checking);
                        payment.Date = new DateTime(year, 1, 1);
                        payment.Amount = this.RoundCents(amount);
                        transactions.AddTransaction(payment);
                        this.money.Transfer(payment, acct);
                        removed += -amount;
                        cashBalance[acct] += -amount;
                    }
                }
            }
            this.money.EndUpdate();

            // now balance the accounts again!
            foreach (Account a in this.money.Accounts.GetAccounts())
            {
                this.money.Rebalance(a);
            }
        }

        private decimal RoundCents(decimal value)
        {
            return Math.Round(value, 2);
        }

        private IList<int> GetRandomDaysInTheYearForTransactions(int count)
        {
            List<int> allDaysInYear = new List<int>();
            for (int day = 0; day < 365; day++)
            {
                allDaysInYear.Add(day);
            }

            SortedList<int, int> selectedDays = new SortedList<int, int>();

            for (int populateCount = 0; populateCount < count; populateCount++)
            {
                int takeThisDay = this.rand.Next(allDaysInYear.Count - 1);

                int dayofTheYear = allDaysInYear[takeThisDay];
                selectedDays.Add(dayofTheYear, dayofTheYear);
                allDaysInYear.RemoveAt(takeThisDay);
            }
            return selectedDays.Values;
        }

        private void AddPaychecks(string employer, decimal paycheck, double inflation)
        {
            Debug.Assert(this.checking != null); // the .xml file must have a checking account.
            DateTime today = DateTime.Today;
            DateTime first = today.AddYears(-Years);
            double biMonthlyInfation = (inflation / 24);

            DateTime date = new DateTime(first.Year, 1, 1);
            this.money.BeginUpdate(this);
            Payee payee = this.money.Payees.FindPayee(employer, true);
            Category category = this.money.Categories.GetOrCreateCategory("Wages & Salary:Gross Pay", CategoryType.Income);
            category.Type = CategoryType.Income;
            Transactions transactions = this.money.Transactions;
            for (int paydays = Years * 12 * 2; paydays > 0; paydays--)
            {
                Transaction t = transactions.NewTransaction(this.checking);
                t.Payee = payee;
                t.Category = category;
                t.Date = date;
                t.Amount = this.RoundCents(paycheck);
                transactions.AddTransaction(t);

                // make it a bi-monthly paycheck
                if (date.Day == 1)
                {
                    date = date.AddDays(15);
                }
                else
                {
                    date = date.AddMonths(1);
                    date = new DateTime(date.Year, date.Month, 1);
                }
                paycheck += (paycheck * (decimal)biMonthlyInfation) / 100M;
            }
            this.money.EndUpdate();
        }

        private void CreateRandomTransactions(List<SampleTransaction> list, double inflation)
        {
            // Now pick randomly from the list to mix things up nicely and spread across 10 year range.
            Random rand = new Random();
            DateTime today = DateTime.Today;
            DateTime first = today.AddYears(-Years);
            DateTime start = new DateTime(first.Year, 1, 1); // start in January
            TimeSpan span = today - first;
            int totalDays = (int)span.TotalDays;
            double monthlyInflation = (inflation / 12);

            this.money.BeginUpdate(this);
            Transactions transactions = this.money.Transactions;
            Accounts accounts = this.money.Accounts;
            Payees payees = this.money.Payees;
            Categories categories = this.money.Categories;
            int nextCheck = 2800;

            while (list.Count > 0)
            {
                int i = rand.Next(0, list.Count);
                SampleTransaction st = list[i];
                list.RemoveAt(i);

                SampleAccount sa = st.Account;
                Account a = accounts.FindAccount(sa.Name);
                Payee p = payees.FindPayee(st.Payee.Name, true);
                SampleCategory sc = st.Category;
                Category c = this.money.Categories.GetOrCreateCategory(sc.Name, sc.Type);
                if (c.Type == CategoryType.None)
                {
                    c.Type = sc.Type;
                }
                if (c.Root.Type == CategoryType.None)
                {
                    c.Root.Type = sc.Type;
                }

                int daysFromStart = rand.Next(0, totalDays);
                DateTime date = start + TimeSpan.FromDays(daysFromStart);

                // spread evenly around the average
                decimal amount = 0;
                if (rand.Next(2) == 1)
                {
                    // above average
                    amount = (decimal)rand.Next(sc.Average * 100, sc.Max * 100) / 100;
                }
                else
                {
                    // below average
                    amount = (decimal)rand.Next(sc.Min * 100, sc.Average * 100) / 100;
                }

                // add inflation
                amount = this.Inflate(amount, daysFromStart / 30, (decimal)monthlyInflation);

                Transaction t = transactions.NewTransaction(a);
                t.Payee = p;
                t.Category = c;
                t.Date = date;
                t.Amount = this.RoundCents(amount);
                if (st.Payee.Type == PaymentType.Check)
                {
                    t.Number = nextCheck.ToString();
                    nextCheck++;
                }
                transactions.AddTransaction(t);
            }

            // now pay the credit cards once a month from a checking account.
            Account checking = this.checking;
            if (checking != null)
            {
                foreach (var acct in accounts.GetAccounts())
                {
                    if (acct.Type == AccountType.Credit)
                    {
                        // here we know transactions are sorted by date.
                        DateTime endOfMonth = start.AddMonths(1);
                        decimal balance = 0;
                        foreach (var t in this.money.Transactions.GetTransactionsFrom(acct))
                        {
                            balance += t.Amount;
                            if (t.Date >= endOfMonth)
                            {
                                if (balance != 0)
                                {
                                    Transaction payment = transactions.NewTransaction(checking);
                                    payment.Date = endOfMonth;
                                    payment.Amount = this.RoundCents(balance);
                                    transactions.AddTransaction(payment);
                                    this.money.Transfer(payment, acct);
                                    balance = 0;
                                    endOfMonth = endOfMonth.AddMonths(1);
                                }
                            }
                        }
                    }
                }
            }

            this.money.EndUpdate();
        }

        private decimal Inflate(decimal amount, int months, decimal monthlyInflation)
        {
            while (months-- > 0)
            {
                amount += (amount * monthlyInflation) / 100M;
            }
            return amount;
        }

        public void Export(string path)
        {
            SampleData data = new SampleData();
            List<SampleAccount> accounts = data.Accounts = new List<SampleAccount>();
            Dictionary<Account, SampleAccount> accountMap = new Dictionary<Account, SampleAccount>();

            foreach (Account a in this.money.Accounts.GetAccounts())
            {
                if (!a.IsClosed)
                {
                    var sa = new SampleAccount() { Name = a.Name, Type = a.Type };
                    accounts.Add(sa);
                    accountMap[a] = sa;
                }
            }

            List<SamplePayee> payees = data.Payees = new List<SamplePayee>();
            Dictionary<Payee, SamplePayee> payeeMap = new Dictionary<Payee, SamplePayee>();

            foreach (Transaction t in this.money.Transactions.GetAllTransactions())
            {
                SampleAccount sa;
                if (!accountMap.TryGetValue(t.Account, out sa) || t.Account.Type == AccountType.Brokerage || t.Payee == null || t.IsSplit || t.Category == null || t.Transfer != null)
                {
                    continue;
                }

                Category c = t.Category;
                string catName = c.Name;
                if (catName.Contains("Microsoft") || catName.Contains("Mitzvah") || catName.Contains("Love") || catName.Contains("Loans") || catName.Contains("Chanukah") || catName.Contains("Unknown"))
                {
                    continue;
                }
                catName = catName.Replace("Woodinville", string.Empty).Replace("Redmond", string.Empty).Trim();

                sa.Frequency++;
                Payee p = t.Payee;
                string payeeName = p.Name.Replace("Woodinville", string.Empty).Replace("Redmond", string.Empty).Trim();
                if (payeeName.Contains("Microsoft") || payeeName.Contains("ATM Withdrawal"))
                {
                    continue;
                }
                SamplePayee sp = null;
                if (!payeeMap.TryGetValue(p, out sp))
                {
                    sp = new SamplePayee()
                    {
                        Name = payeeName,
                        Categories = new List<SampleCategory>(),
                        CategoryMap = new Dictionary<Category, SampleCategory>()
                    };
                    payees.Add(sp);
                    payeeMap[p] = sp;
                }
                sp.Frequency++;

                SampleCategory sc;
                if (!sp.CategoryMap.TryGetValue(c, out sc))
                {
                    sc = new SampleCategory() { Name = catName, Type = c.Root.Type };
                    sp.CategoryMap[c] = sc;
                    sp.Categories.Add(sc);
                }
                decimal amount = t.Amount;
                sc.TotalAmount += amount;
                if (sc.Frequency == 0)
                {
                    sc.Min = sc.Max = (int)amount;
                }
                else
                {
                    if (sc.Min > amount) sc.Min = (int)amount;
                    if (sc.Max < amount) sc.Max = (int)amount;
                }
                sc.Frequency++;
            }

            // remove low frequency stuff
            foreach (SampleAccount sa in new List<SampleAccount>(accounts))
            {
                if (sa.Frequency < 10)
                {
                    accounts.Remove(sa);
                }
            }

            foreach (SamplePayee sp in new List<SamplePayee>(payees))
            {
                if (sp.Frequency < 10)
                {
                    payees.Remove(sp);
                }
                else
                {
                    foreach (SampleCategory sc in sp.Categories)
                    {
                        sc.Average = (int)(sc.TotalAmount / sc.Frequency);
                    }
                }
            }

            XmlSerializer s = new XmlSerializer(typeof(SampleData));
            XmlWriterSettings settings = new XmlWriterSettings() { Indent = true };
            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                s.Serialize(writer, data);
            }
        }
    }

    class SampleTransaction
    {
        public SampleAccount Account { get; set; }
        public SamplePayee Payee { get; set; }
        public SampleCategory Category { get; set; }
    }


    public class SampleData
    {
        public SampleData() { }
        public List<SampleAccount> Accounts { get; set; }
        public List<SamplePayee> Payees { get; set; }
        public List<SampleSecurity> Securities { get; set; }

        // Fix frequency totals in case the file was hand edited.
        public int GetTotalFrequency()
        {
            int total = 0;
            foreach (SamplePayee sp in this.Payees)
            {
                int pf = 0;
                foreach (SampleCategory sc in sp.Categories)
                {
                    pf += sc.Frequency;
                }
                sp.Frequency = pf;
                total += pf;
            }
            return total;
        }
    }

    public class SampleAccount
    {
        public SampleAccount() { }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public AccountType Type { get; set; }

        [XmlAttribute]
        public TaxStatus TaxStatus { get; set; }

        [XmlAttribute]
        public int Frequency { get; set; }
    }

    public enum PaymentType
    {
        Debit,
        Check,
        Credit
    }

    public class SamplePayee
    {
        public SamplePayee()
        {
        }
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public int Frequency { get; set; }

        [XmlAttribute]
        public PaymentType Type { get; set; }

        public List<SampleCategory> Categories { get; set; }

        // All the categories found for this payee so far
        [XmlIgnore]
        internal Dictionary<Category, SampleCategory> CategoryMap { get; set; }
    }

    public class SampleSecurity
    {
        public SampleSecurity()
        {
        }

        [XmlAttribute]
        public string Symbol { get; set; }

        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public int PriceRangeLow { get; set; }

        [XmlAttribute]
        public int PriceRangeHight { get; set; }

        [XmlAttribute]
        public SecurityType SecurityType { get; set; }

        [XmlElement("Split")]
        public SampleSplit[] Splits { get; set; }
    }

    public class SampleSplit
    {
        [XmlAttribute]
        public DateTime Date { get; set; }

        [XmlAttribute]
        public int Numerator { get; set; }

        [XmlAttribute]
        public int Denominator { get; set; }
    }


    public class SampleCategory
    {
        public SampleCategory()
        {
        }
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public CategoryType Type { get; set; }

        [XmlAttribute]
        public int Frequency { get; set; }

        [XmlAttribute]
        public int Average { get; set; }

        [XmlAttribute]
        public int Min { get; set; }

        [XmlAttribute]
        public int Max { get; set; }

        [XmlIgnore]
        public decimal TotalAmount { get; set; }
    }
}
