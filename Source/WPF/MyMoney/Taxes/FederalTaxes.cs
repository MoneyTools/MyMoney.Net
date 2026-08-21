using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Walkabout.Taxes
{
    public enum TaxFilingStatus
    {
        Single,
        Married,
        MarriedSeparately,
        HeadOfHousehold
    }

    public class FederalTaxes
    {
        [JsonProperty("standardDeduction")]
        public StandardDeduction StandardDeduction { get; set; }

        [JsonProperty("incomeBrackets")]
        public Brackets IncomeBrackets { get; set; }

        [JsonProperty("capitalGainsBrackets")]
        public Brackets CapitalGainsBrackets { get; set; }

        public FederalTaxes() {
        }

        public static FederalTaxes Load()
        {
            var location = typeof(FederalTaxes).Assembly.Location;
            var path = Path.GetDirectoryName(location);
            var fileName = Path.Combine(path, "Taxes", "FederalTaxes.json");
            // load the json file
            var json = File.ReadAllText(fileName);
            return Newtonsoft.Json.JsonConvert.DeserializeObject<FederalTaxes>(json);
        }

        /// <summary>
        /// Compute additional income tax to be paid for paycheck over and above what we have 
        /// already paid for the accumulated baseIncome.
        /// </summary>
        /// <param name="baseIncome">The amount we've already paid tax on.</param>
        /// <param name="paycheck">The new amount we need to pay tax on.</param>
        public decimal GetIncomeTax(TaxFilingStatus status, decimal baseIncome, decimal paycheck)
        {
            // Brackets for Married Filing Jointly (thresholds are upper bounds for lower brackets)
            // Ordered from highest threshold to lowest.
            var brackets = status switch
            {
                TaxFilingStatus.Single => this.IncomeBrackets.Single,
                TaxFilingStatus.Married => this.IncomeBrackets.Married,
                TaxFilingStatus.MarriedSeparately => this.IncomeBrackets.Single, // are there income brackets for this?
                TaxFilingStatus.HeadOfHousehold => this.IncomeBrackets.Head,
                _ => this.IncomeBrackets.Single
            };
            var standardDeduction = status switch
            {
                TaxFilingStatus.Single => this.StandardDeduction.Single,
                TaxFilingStatus.Married => this.StandardDeduction.Married,
                TaxFilingStatus.MarriedSeparately => this.StandardDeduction.Single, // todo
                TaxFilingStatus.HeadOfHousehold => this.StandardDeduction.Head,
                _ => this.StandardDeduction.Single
            };

            if (paycheck < 0)
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

        public void InflateBrackets(decimal taxBracketInflation)
        {
            // apply tax bracket inflation.
            var increase = (1 + taxBracketInflation);
            foreach (var brackets in new[] { this.IncomeBrackets.Single, this.IncomeBrackets.Married, this.IncomeBrackets.Head })
            {
                for (int i = 0; i < brackets.Length; i++)
                {
                    brackets[i].Min *= increase;
                    brackets[i].Max *= increase;
                }
            }
        }

        /// <summary>
        /// Calculate the incremental taxes we need to pay on the new gains over and above
        /// what we've already paid tax on in the baseGains.
        /// </summary>
        public decimal GetCapitalGainsTax(TaxFilingStatus status, decimal baseGains, decimal gains)
        {
            if (gains < 0)
            {
                return 0;
            }
            var brackets = status switch
            {
                TaxFilingStatus.Single => this.CapitalGainsBrackets.Single,
                TaxFilingStatus.Married => this.CapitalGainsBrackets.Married,
                TaxFilingStatus.MarriedSeparately => this.CapitalGainsBrackets.Separate, // are there income brackets for this?
                TaxFilingStatus.HeadOfHousehold => this.CapitalGainsBrackets.Head,
                _ => this.IncomeBrackets.Single
            };

            var total = baseGains + gains;
            decimal tax = 0;
            // if we visit in reverse order then 
            for (int i = brackets.Length - 1; i >= 0; i--)
            {
                var bracket = brackets[i];
                if (total >= bracket.Min)
                {
                    decimal taxableAtThisRate = total - bracket.Min;
                    if (taxableAtThisRate > gains)
                    {
                        taxableAtThisRate = gains;
                    }
                    if (taxableAtThisRate > 0)
                    {
                        tax += taxableAtThisRate * (bracket.Rate / 100);
                    }
                    gains -= taxableAtThisRate;
                    total = bracket.Min == 0 ? 0 : bracket.Min - 0.01M;
                }
            }
            
            return tax;
        }

    }

    public class StandardDeduction
    {
        public StandardDeduction() { }

        [JsonProperty("single")]
        public decimal Single { get; set; }

        [JsonProperty("married")]
        public decimal Married { get; set; }

        [JsonProperty("head")]
        public decimal Head { get; set; }
    }

    public class Brackets
    {
        public Brackets() { }

        [JsonProperty("single")]
        public Bracket[] Single { get; set; }

        [JsonProperty("married")]
        public Bracket[] Married { get; set; }

        [JsonProperty("separate")]
        public Bracket[] Separate { get; set; }

        [JsonProperty("head")]
        public Bracket[] Head { get; set; }
    }

    public class Bracket
    {
        public Bracket() { }

        [JsonProperty("min")]
        public decimal Min { get; set; } = 0;

        [JsonProperty("max")]
        public decimal Max{ get; set; } = 0;

        [JsonProperty("rate")]
        public decimal Rate { get; set; } = 0;
    }
}
