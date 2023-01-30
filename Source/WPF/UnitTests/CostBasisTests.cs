using NUnit.Framework;
using Walkabout.Data;
using Walkabout.Taxes;
using Walkabout.Utilities;

namespace Walkabout.Tests
{
    public class CostBasisTests
    {
        [Test]
        public void SimpleCostBasis()
        {
            UiDispatcher.CurrentDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            MyMoney m = new MyMoney();
            Security s = m.Securities.NewSecurity();
            s.Name = "MSFT";
            Account a = m.Accounts.AddAccount("Checking");

            AddInvestment(m, s, a, DateTime.Parse("1/1/2000"), 10, 1, InvestmentType.Add);
            m.StockSplits.AddStockSplit(new StockSplit()
            {
                Date = DateTime.Parse("6/1/2000"),
                Numerator = 2,
                Denominator = 1,
                Security = s
            });
            AddInvestment(m, s, a, DateTime.Parse("1/1/2001"), 10, 2, InvestmentType.Sell);
            AddInvestment(m, s, a, DateTime.Parse("1/1/2002"), 20, 2, InvestmentType.Buy);

            m.StockSplits.AddStockSplit(new StockSplit()
            {
                Date = DateTime.Parse("6/1/2002"),
                Numerator = 3,
                Denominator = 1,
                Security = s
            });

            AddInvestment(m, s, a, DateTime.Parse("1/1/2003"), 10, 3, InvestmentType.Buy);
            AddInvestment(m, s, a, DateTime.Parse("1/1/2004"), 20, 3, InvestmentType.Sell);

            Transaction sale = AddInvestment(m, s, a, DateTime.Parse("1/1/2005"), 20, 5, InvestmentType.Sell);


            CapitalGainsTaxCalculator calc = new CapitalGainsTaxCalculator(m, DateTime.Now, false, false);
            List<SecurityPurchase> holdings = new List<SecurityPurchase>(calc.GetHolding(a).GetHoldings());

            Assert.AreEqual(2, holdings.Count); // should have only 2 buys left, the first buy is all used up.
            SecurityPurchase c1 = holdings[0];
            SecurityPurchase c2 = holdings[1];

            Assert.AreEqual(50, c1.UnitsRemaining); // 50 shares left in this one (after stock splits)
            Assert.AreEqual(10, c2.UnitsRemaining); // 10 shares still in this one (has no stock spits)
            Assert.AreEqual(Math.Round(2M / 3M, 5), Math.Round(c1.CostBasisPerUnit, 5)); // a 3:1 split
            Assert.AreEqual(Math.Round(3M, 5), Math.Round(c2.CostBasisPerUnit, 5)); // no splits apply.


        }

        [Test]
        public void CostBasisAcrossTransfers()
        {
            UiDispatcher.CurrentDispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            MyMoney m = new MyMoney();
            Security s = m.Securities.NewSecurity();
            s.Name = "MSFT";
            Account a = m.Accounts.AddAccount("Ameritrade");
            Account a2 = m.Accounts.AddAccount("Fidelity");

            AddInvestment(m, s, a, DateTime.Parse("1/1/2000"), 10, 1, InvestmentType.Buy);
            m.StockSplits.AddStockSplit(new StockSplit()
            {
                Date = DateTime.Parse("6/1/2000"),
                Numerator = 2,
                Denominator = 1,
                Security = s
            });
            AddInvestment(m, s, a, DateTime.Parse("1/1/2001"), 20, 2, InvestmentType.Buy);

            Transaction transfer = AddInvestment(m, s, a, DateTime.Parse("1/1/2002"), 30, 2, InvestmentType.Remove);
            m.Transfer(transfer, a2);

            // now should be able to sell 10 left in this account (after split)
            AddInvestment(m, s, a, DateTime.Parse("1/1/2003"), 10, 3, InvestmentType.Sell);

            // and we should have 30 in the other account
            AddInvestment(m, s, a2, DateTime.Parse("1/1/2004"), 30, 5, InvestmentType.Sell);

            // Ok, now let's if the cost basis is correct!
            CostBasisCalculator calc = new CostBasisCalculator(m, DateTime.Now);
            List<SecurityPurchase> holdings = new List<SecurityPurchase>(calc.GetHolding(a).GetHoldings());

            Assert.AreEqual(0, holdings.Count); // should have nothing left.

            // should have 3 separate cost basis records to cover what we sold.
            List<SecuritySale> sales = new List<SecuritySale>(calc.GetSales());
            Assert.AreEqual(3, sales.Count);

            SecuritySale s1 = sales[0];
            SecuritySale s2 = sales[1];
            SecuritySale s3 = sales[2];

            // since the sale from Ameritrade account happened first it should be returned first
            Assert.AreEqual(2, s1.CostBasisPerUnit); // $2, no splits
            Assert.AreEqual(a, s1.Account); // Ameritrade
            Assert.AreEqual(Math.Round(10M, 5), Math.Round(s1.UnitsSold, 5));

            // Notice here that the Fidelity account inherited the cost basis records correctly
            // from the Ameritrade account as a result of the "Transfer" that happened above.
            Assert.AreEqual(Math.Round(1M / 2M, 5), Math.Round(s2.CostBasisPerUnit, 5)); // $1 after 2:1 split
            Assert.AreEqual(a2, s2.Account); // Fidelity
            Assert.AreEqual(Math.Round(20M, 5), Math.Round(s2.UnitsSold, 5));

            Assert.AreEqual(2, s3.CostBasisPerUnit); // $2, no splits
            Assert.AreEqual(a2, s2.Account); // Fidelity
            Assert.AreEqual(Math.Round(10M, 5), Math.Round(s3.UnitsSold, 5));

        }
        private static Transaction AddInvestment(MyMoney m, Security s, Account a, DateTime date, decimal units, decimal unitPrice, InvestmentType type)
        {
            Transaction t = m.Transactions.NewTransaction(a);
            t.Date = date;
            Investment i = t.GetOrCreateInvestment();
            i.Security = s;
            i.Units = units;
            i.UnitPrice = unitPrice;
            i.Type = type;
            m.Transactions.AddTransaction(t);
            return t;
        }


    }
}
