using Newtonsoft.Json;
using System.IO;

namespace Walkabout.Taxes
{
    internal class StateTaxes
    {
        public StateTaxes() { }

        [JsonProperty("data")]
        public StateData[] Data { get; set; }


        public static StateTaxes Load()
        {
            var location = typeof(FederalTaxes).Assembly.Location;
            var path = Path.GetDirectoryName(location);
            var fileName = Path.Combine(path, "Taxes", "StateTaxes.json");
            // load the json file
            var json = File.ReadAllText(fileName);

            return JsonConvert.DeserializeObject<StateTaxes>(json);
        }
    }

    internal class CapitalGains
    {
        public CapitalGains() { }

        public bool taxedAsIncome { get; set; }
        public decimal fixedRate { get; set; } // not bracketted.
        public decimal deductionPercentage { get; set; } // percentage of amount excluded
        public decimal deductionAmount { get; set; }
        public string deductionForStateOwnedAssetsOnly { get; set; }
        public decimal surcharge { get; set; }
        public decimal surchargeBracket { get; set; }
    }

    internal class StateData
    {
        public StateData() { }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; }

        [JsonProperty("taxSystem")]
        public string TaxSystem { get; set; }

        [JsonProperty("topMarginalRate")]
        public decimal TopMarginalRate { get; set; }

        [JsonProperty("brackets")]
        public Brackets IncomeBrackets { get; set; }

        [JsonProperty("stateStandardDeduction")]
        public StandardDeduction StandardDeduction { get; set; }

        [JsonProperty("capitalGains")]
        public CapitalGains CapitalGains { get; set; }

        public bool NoCapitalGainsTax
        {
            get { return this.CapitalGains == null || (this.CapitalGains.taxedAsIncome == false && this.CapitalGains.fixedRate == 0); }
        }

        public bool CapitalGainsTaxedAsIncome
        {
            get
            {
                return this.CapitalGains != null && this.CapitalGains.taxedAsIncome;
            }
        }

        public bool CapitalGainsFixedRate
        {
            get
            {
                return this.CapitalGains != null && this.CapitalGains.fixedRate > 0;
            }
        }

        internal decimal GetIncomeTax(TaxFilingStatus status, decimal baseIncome, decimal paycheck)
        {
            if (this.IncomeBrackets == null)
            {
                return 0;
            }
            // Brackets for Married Filing Jointly (thresholds are upper bounds for lower brackets)
            // Ordered from highest threshold to lowest.
            var brackets = status switch
            {
                TaxFilingStatus.Single => this.IncomeBrackets.Single,
                TaxFilingStatus.Married => this.IncomeBrackets.Married,
                TaxFilingStatus.MarriedSeparately => this.IncomeBrackets.Single, // are there income brackets for this?
                TaxFilingStatus.HeadOfHousehold => this.IncomeBrackets.Single, // state data doesn't have this category
                _ => this.IncomeBrackets.Single
            };
            var standardDeduction = status switch
            {
                TaxFilingStatus.Single => this.StandardDeduction.Single,
                TaxFilingStatus.Married => this.StandardDeduction.Married,
                TaxFilingStatus.MarriedSeparately => this.StandardDeduction.Single, // todo
                TaxFilingStatus.HeadOfHousehold => this.StandardDeduction.Single, // state data doesn't have this category
                _ => this.StandardDeduction.Single
            };

            if (paycheck < 0 || brackets.Length == 0)
            {
                return 0;
            }

            decimal income = baseIncome + paycheck - standardDeduction;
            if (income <= 0M)
            {
                return 0M;
            }

            decimal tax = 0M;
            // Now if we traverse them in reverse order we can apply each bracket 
            // in sequence that applies above the given base income.
            for (int i = brackets.Length - 1; i >= 0; i--)
            {
                var bracket = brackets[i];
                if (income >= bracket.Min)
                {
                    decimal taxableAtThisRate = income - bracket.Min;
                    if (taxableAtThisRate > paycheck)
                    {
                        taxableAtThisRate = paycheck;
                    }
                    if (taxableAtThisRate > 0)
                    {
                        tax += taxableAtThisRate * (bracket.Rate / 100);
                    }
                    paycheck -= taxableAtThisRate;
                    income = bracket.Min == 0 ? 0 : bracket.Min - 0.01M;
                }
                if (paycheck <= 0)
                {
                    break; // done!
                }
            }
            return tax;
        }


        internal decimal GetCapitalGainsTax(TaxFilingStatus status, decimal baseIncome, decimal baseGains, decimal gains)
        {
            if (this.NoCapitalGainsTax)
            {
                return 0;
            }

            var totalGain = baseGains + gains - this.CapitalGains.deductionAmount;
            if (totalGain < 0)
            {
                // capital loss or we have not yet met the deduction amount.
                return 0;
            }

            if (this.CapitalGainsFixedRate)
            {
                // amount above deduction?
                if (totalGain < gains)
                {
                    gains = totalGain;
                }

                var tax =  gains * this.CapitalGains.fixedRate / 100.0M;
                if (this.CapitalGains.surchargeBracket > 0 && (baseGains + gains) > this.CapitalGains.surchargeBracket)
                {
                    var extra = (baseGains + gains) - this.CapitalGains.surchargeBracket;
                    if (extra > gains)
                    {
                        extra = gains; // all of it is at surcharge rate.
                    }
                    tax += (extra * this.CapitalGains.surcharge / 100.0M);
                }
                return tax;
            }

            if (this.CapitalGainsTaxedAsIncome)
            {
                return this.GetIncomeTax(status, baseIncome + baseGains, gains);
            }

            return 0;
        }

        internal void InflateBrackets(decimal taxBracketInflation)
        {
            // apply tax bracket inflation.
            var increase = (1 + taxBracketInflation);
            foreach (var brackets in new[] { this.IncomeBrackets.Single, this.IncomeBrackets.Married })
            {
                for (int i = 0; i < brackets.Length; i++)
                {
                    brackets[i].Min *= increase;
                    brackets[i].Max *= increase;
                }
            }

            // todo: will states also adjust capital gains surcharge brackets upwards?
        }
    }
}
