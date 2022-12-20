using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Walkabout.Data;
using Walkabout.RestfulWebServices;

namespace Walkabout.StockQuotes
{
    public class ExchangeRates : IDisposable
    {
        MyMoney myMoney;

        Thread quotesThread;
        bool stop;
        List<CurrencyCode> queue = new List<CurrencyCode>(); // list of securities to fetch

        public ExchangeRates()
        {
        }

        public void Dispose()
        {
            this.stop = true;
            lock (this.queue)
            {
                this.queue.Clear();
            }
        }

        public MyMoney MyMoney
        {
            get { return this.myMoney; }
            set { this.myMoney = value; }
        }

        internal void UpdateRates()
        {
            if (this.myMoney != null)
            {
                foreach (var c in this.myMoney.Currencies.GetCurrencies())
                {
                    if (!string.IsNullOrEmpty(c.Symbol))
                    {
                        this.Enqueue(c.Symbol);
                    }
                }
            }
        }

        public void Enqueue(string currency)
        {
            CurrencyCode code;
            if (!Enum.TryParse<RestfulWebServices.CurrencyCode>(currency, out code))
            {
                Debug.WriteLine("ExchangeRates ignoring unknown currency: " + currency);
                return;
            }

            lock (this.queue)
            {
                this.queue.Add(code);
            }

            if (this.quotesThread == null)
            {
                this.quotesThread = new Thread(new ThreadStart(this.GetRates));
                this.quotesThread.Start();
            }

        }

        void GetRates()
        {
            while (!this.stop)
            {
                CurrencyCode code;

                lock (this.queue)
                {
                    if (this.queue.Count > 0)
                    {
                        code = this.queue[0];
                        this.queue.RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }

                decimal d = this.GetExchangeRate(code, CurrencyCode.USD);
                if (d != 0)
                {
                    Walkabout.Data.Currency found = this.myMoney.Currencies.FindCurrency(code.ToString());
                    if (found == null)
                    {
                        found = new Data.Currency();
                        found.Symbol = code.ToString();
                        this.myMoney.Currencies.AddCurrency(found);
                    }

                    found.Ratio = d;
                }
            }
            this.quotesThread = null;
        }

        decimal GetExchangeRate(CurrencyCode fromCurrency, CurrencyCode toCurrency)
        {
            decimal d = 0;
            try
            {
                CurrencyServiceClient csc = new CurrencyServiceClient();
                var result = csc.GetConversionRate(fromCurrency, toCurrency);
                if (result != null)
                {
                    d = (decimal)result.Rate;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("BeginGetExchangeRate for " + fromCurrency.ToString() + " threw exception: " + ex.Message);
            }

            return d;
        }

    }
}
