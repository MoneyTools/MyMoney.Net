using NUnit.Framework;
using Walkabout.Taxes;
using Walkabout.Utilities;

namespace Walkabout.Tests
{
    public class TaxTests
    {
        [Test]
        public void TestFederalIncomeTaxes()
        {
            var federalTaxes = FederalTaxes.Load();

            // test that incremental works and matches the non-incremental result.
            var paycheck = 250000;
            var tax = federalTaxes.GetIncomeTax(TaxFilingStatus.Single, 0, paycheck);

            // now do the same but in steps.
            decimal baseIncome = 0;
            decimal chunk = 50000;
            decimal totalTax = 0;
            while (baseIncome < paycheck)
            {
                var t = federalTaxes.GetIncomeTax(TaxFilingStatus.Single, baseIncome, chunk);
                totalTax += t;
                baseIncome += chunk;
            }

            Assert.That(Math.Round(totalTax, 0) == Math.Round(tax), "Incremental doesn't match single shot");
        }

        [Test]
        public void TestFederalCapitalGainsTaxes()
        {
            var federalTaxes = FederalTaxes.Load();

            // test that incremental works and matches the non-incremental result.
            var totalGains = 250000;
            var tax = federalTaxes.GetCapitalGainsTax(TaxFilingStatus.Single, 0, totalGains);

            // now do the same but in steps.
            decimal baseGains = 0;
            decimal chunk = 50000;
            decimal totalTax = 0;
            while (baseGains < totalGains)
            {
                var t = federalTaxes.GetCapitalGainsTax(TaxFilingStatus.Single, baseGains, chunk);
                totalTax += t;
                baseGains += chunk;
            }

            Assert.That(Math.Round(totalTax, 0) == Math.Round(tax), "Incremental doesn't match single shot");
        }

        [Test]
        public void TestStateIncomeTaxes()
        {
            var stateTaxes = StateTaxes.Load();

            // find a state with taxes
            var ca = (from s in stateTaxes.Data where s.Abbreviation == "CA" select s).FirstOrDefault();
            Assert.That(ca, Is.Not.Null, "Could not find CA tax data");

            // test that incremental works and matches the non-incremental result.
            var paycheck = 250000;
            var tax = ca.GetIncomeTax(TaxFilingStatus.Single, 0, paycheck);

            // now do the same but in steps.
            decimal baseIncome = 0;
            decimal chunk = 50000;
            decimal totalTax = 0;
            while (baseIncome < paycheck)
            {
                var t = ca.GetIncomeTax(TaxFilingStatus.Single, baseIncome, chunk);
                totalTax += t;
                baseIncome += chunk;
            }

            Assert.That(Math.Round(totalTax ,0) == Math.Round(tax), "Incremental doesn't match single shot");
        }

        [Test]
        public void TestStateCapitalGainsTaxes()
        {
            var stateTaxes = StateTaxes.Load();

            // find a state with custom capital gains 
            var wa = (from s in stateTaxes.Data where s.Abbreviation == "WA" select s).FirstOrDefault();
            Assert.That(wa, Is.Not.Null, "Could not find WA tax data");

            // test that incremental works and matches the non-incremental result.
            var totalGains = 1500000;
            var tax = wa.GetCapitalGainsTax(TaxFilingStatus.Single, 0, 0, totalGains);

            Assert.That(!wa.CapitalGainsTaxedAsIncome, "WA does not tax capital gains as income");

            // now do the same but in steps.
            decimal baseIncome = 0;
            decimal chunk = 50000;
            decimal totalTax = 0;
            decimal baseGains = 0;
            while (baseGains < totalGains)
            {
                var t = wa.GetCapitalGainsTax(TaxFilingStatus.Single, baseIncome, baseGains, chunk);
                totalTax += t;
                baseGains += chunk;
            }

            Assert.That(Math.Round(totalTax, 0) == Math.Round(tax), "Incremental doesn't match single shot");
        }
    }
}
