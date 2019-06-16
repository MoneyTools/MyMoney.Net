using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            stop = true;
            lock (queue)
            {
                queue.Clear();
            }
        }

        public MyMoney MyMoney
        {
            get { return myMoney; }
            set { myMoney = value; }
        }

        internal void UpdateRates()
        {
            if (myMoney != null)
            {
                foreach (var c in myMoney.Currencies.GetCurrencies())
                {
                    if (!string.IsNullOrEmpty(c.Symbol))
                    {
                        Enqueue(c.Symbol);
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

            lock (queue)
            {
                queue.Add(code);
            }

            if (quotesThread == null)
            {
                quotesThread = new Thread(new ThreadStart(GetRates));
                quotesThread.Start();
            }

        }

        void GetRates()
        {
            while (!stop)
            {
                CurrencyCode code;

                lock (queue)
                {
                    if (queue.Count > 0)
                    {
                        code = queue[0];
                        queue.RemoveAt(0);
                    }
                    else
                    {
                        break;
                    }
                }

                decimal d = GetExchangeRate(code, CurrencyCode.USD);
                if (d != 0)
                {
                    Walkabout.Data.Currency found = myMoney.Currencies.FindCurrency(code.ToString());                   
                    if (found == null)
                    {
                        found = new Data.Currency();
                        found.Symbol = code.ToString();
                        myMoney.Currencies.AddCurrency(found);
                    }
                    
                    found.Ratio = d;
                }
            }
            quotesThread = null;
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
