using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.Diagnostics;
using Walkabout.Dialogs;
using Walkabout.Data;
using System.IO;
using Walkabout.Utilities;
using System.Windows;

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
        Random rand = new Random();

        public SampleDatabase(MyMoney money)
        {
            this.money = money;
        }

        public void Create()
        {
            string path = Path.Combine(Path.GetTempPath(), "SampleData.xml");
            ProcessHelper.ExtractEmbeddedResourceAsFile("Walkabout.Database.SampleData.xml", path);

            SampleDatabaseOptions options = new SampleDatabaseOptions();
            options.Owner = Application.Current.MainWindow;
            options.SampleData = path;
            if (options.ShowDialog() == false)
            {
                return;
            }
            path = options.SampleData;

            double inflation = options.Inflation;

            SampleData data = null;
            XmlSerializer s = new XmlSerializer(typeof(SampleData));
            using (XmlReader reader = XmlReader.Create(path))
            {
                data = (SampleData)s.Deserialize(reader);
            }

            int totalFrequency = data.GetTotalFrequency();

            List<SampleTransaction> list = new List<SampleTransaction>();

            foreach (SampleAccount sa in data.Accounts)
            {
                // Create all the accounts.
                Accounts accounts = money.Accounts;
                Account a = accounts.FindAccount(sa.Name);
                if (a == null)
                {
                    a = accounts.AddAccount(sa.Name);
                }
                a.Type = sa.Type;
                a.IsBudgeted = true;
                if (a.Type == AccountType.Checking)
                {
                    this.checking = a;
                }

                // Create this many transactions
                int count = sa.Frequency;

                // by scaling the payee frequencies to match the above desired count.
                double ratio = (double)count / (double)totalFrequency;

                if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                {
                    CreateInvestmentSamples(data, a);
                }
                else
                {
                    // create flat list of all payees to choose from so it fits the histogram
                    List<SamplePayee> payees = new List<SamplePayee>();
                    foreach (SamplePayee payee in data.Payees)
                    {
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


            CreateRandomTransactions(list, inflation);

            AddPaychecks(options.Employer, options.PayCheck, inflation);
        }






        private void CreateInvestmentSamples(SampleData data, Account a)
        {
            money.BeginUpdate(this);
            Transactions transactions = money.Transactions;

            for (int year = DateTime.Now.Year-10; year <= DateTime.Now.Year; year++)
            {
                

                foreach (SampleSecurity ss in data.Securities)
                {
                    // Get or Create the security
                    Security stock = money.Securities.FindSecurity(ss.Name, true);
                    stock.SecurityType = ss.SecurityType;
                    stock.Symbol = ss.Symbol;

                    // keep track of how much we own so we never go negative.
                    decimal owned = 0;


                    IList<int> selectedDays = GetRandomDaysInTheYearForTransactions();

                    foreach (var day in selectedDays)
                    {
                        // Create a new Transaction
                        Transaction t = money.Transactions.NewTransaction(a);


                        // the date the purchase or sale was done
                        t.Date = new DateTime(year,1,1).AddDays(day);


                        //-----------------------------------------------------
                        // Create the matching Investment transaction
                        Investment i = t.GetOrCreateInvestment();
                        i.Transaction = t;
                        i.Security = stock;

                        // Is this a BUY or SELL
                        i.Type = InvestmentType.Buy;
                        // How many unit bought or sold
                        i.Units = rand.Next(1000);

                        if (owned > 0 && rand.Next(2) == 1)
                        {
                            i.Type = InvestmentType.Sell;
                            i.Units = rand.Next((int)owned); // don't sell more than we currently own.
                            owned -= i.Units;
                        }
                        else
                        {
                            owned += i.Units;
                        }

                        
                        // What price
                        i.UnitPrice = Convert.ToDecimal(rand.Next(ss.PriceRangeLow, ss.PriceRangeHight));
                        
                        // add some pennies (decimal value) 
                        decimal penies = rand.Next(99);
                        penies /= 100;
                        i.UnitPrice = i.UnitPrice + penies;

                        // Calculate the Payment or Deposit amount
                        t.Amount = i.Units * i.UnitPrice * (i.Type == InvestmentType.Buy ? -1 : 1);
                        t.Payee = money.Payees.FindPayee(ss.Name, true);
                        t.Category = money.Categories.InvestmentStocks;

                        if (i.Type == InvestmentType.Sell)
                        {
                            t.Amount = t.Amount * 1.10M; // Improve the outcome of profit, sell at the 10% higher random 
                        }

                        //-----------------------------------------------------
                        // Finally add the new transaction
                        money.Transactions.AddTransaction(t);
                    }
                }
            }
            money.EndUpdate();
        }

        private IList<int> GetRandomDaysInTheYearForTransactions()
        {
            List<int> allDaysInYear = new List<int>();
            for (int day = 0; day < 365; day++)
            {
                allDaysInYear.Add(day);
            }

            int numberOfTransactionForThatYear = rand.Next(0, 20); // From 0 to 20

            SortedList<int,int> selectedDays = new SortedList<int,int>();

            for (int populateCount = 0; populateCount < numberOfTransactionForThatYear; populateCount++)
            {
                int takeThisDay = rand.Next(allDaysInYear.Count - 1);

                int dayofTheYear = allDaysInYear[takeThisDay];
                selectedDays.Add(dayofTheYear, dayofTheYear);
                allDaysInYear.RemoveAt(takeThisDay);
            }
            return selectedDays.Values;
        }




        private void AddPaychecks(string employer, decimal paycheck, double inflation)
        {
            Debug.Assert(checking != null); // the .xml file must have a checking account.
            DateTime today = DateTime.Today;
            DateTime first = today.AddYears(-Years);
            double biMonthlyInfation = (inflation / 24);

            DateTime date = new DateTime(first.Year, 1, 1);
            money.BeginUpdate(this);
            Payee payee = money.Payees.FindPayee(employer, true);
            Category category = money.Categories.GetOrCreateCategory("Wages & Salary:Gross Pay", CategoryType.Income);
            category.Type = CategoryType.Income;
            Transactions transactions = money.Transactions;
            for (int paydays = Years * 12 * 2; paydays > 0; paydays--)
            {
                Transaction t = transactions.NewTransaction(this.checking);
                t.Payee = payee;
                t.Category = category;
                t.Date = date;
                t.Amount = paycheck;
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
            money.EndUpdate();
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

            money.BeginUpdate(this);
            Transactions transactions = money.Transactions;
            Accounts accounts = money.Accounts;
            Payees payees = money.Payees;
            Categories categories = money.Categories;

            while (list.Count > 0)
            {
                int i = rand.Next(0, list.Count);
                SampleTransaction st = list[i];
                list.RemoveAt(i);

                SampleAccount sa = st.Account;
                Account a = accounts.FindAccount(sa.Name);
                Payee p = payees.FindPayee(st.Payee.Name, true);
                SampleCategory sc = st.Category;
                Category c = money.Categories.GetOrCreateCategory(sc.Name, sc.Type);
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
                amount = Inflate(amount, (int)(daysFromStart / 30), (decimal)monthlyInflation);

                Transaction t = transactions.NewTransaction(a);
                t.Payee = p;
                t.Category = c;
                t.Date = date;
                t.Amount = amount;
                transactions.AddTransaction(t);
            }
            money.EndUpdate();

            // now trigger UI update
            foreach (Account a in money.Accounts.GetAccounts())
            {
                money.Rebalance(a);
            }

            // only have to do this because we hid all update events earlier by doing BeginUpdate/EndUpdate on money object.
            // trigger payee update 
            money.Payees.BeginUpdate(false);
            money.Payees.EndUpdate();

            // trigger category update
            money.Categories.BeginUpdate(false);
            money.Categories.EndUpdate();
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

            foreach (Account a in money.Accounts.GetAccounts())
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

            foreach (Transaction t in money.Transactions.GetAllTransactions())
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
            foreach (SamplePayee sp in Payees)
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
        public int Frequency { get; set; }
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
