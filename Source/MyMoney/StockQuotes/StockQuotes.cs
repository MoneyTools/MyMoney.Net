using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Xml.Linq;
using System.Xml;
using Walkabout.Data;
using Walkabout.Sgml;
using Walkabout.Controls;
using Walkabout.Configuration;
using Walkabout.Utilities;
using System.Text;
using System.Windows.Documents;
using System.Windows.Input;
using Newtonsoft.Json;
using Walkabout.Ofx;

namespace Walkabout.Network
{
    /// <summary>
    /// This class tracks changes to Securities and fetches stock quotes from Yahoo
    /// </summary>
    public class StockQuotes : IDisposable
    {
        MyMoney myMoney;
        Thread quotesThread;
        bool busy;
        StringBuilder errorLog;
        bool hasError;
        bool stop;
        List<Security> queue = new List<Security>(); // list of securities to fetch
        HashSet<string> fetched = new HashSet<string>(); // list that we have already fetched.
        XDocument newQuotes;
        IStatusService status;
        IServiceProvider provider;
        HttpWebRequest _current;        
        char[] illegalUrlChars = new char[] { ' ', '\t', '\n', '\r', '/', '+', '=', '&', ':' };
        const string address = "https://www.alphavantage.co/query?apikey={0}&function=TIME_SERIES_DAILY_ADJUSTED&symbol={1}";

        public StockQuotes(IServiceProvider provider)
        {
            this.provider = provider;
            this.myMoney = (MyMoney)provider.GetService(typeof(MyMoney));
            this.status = (IStatusService)provider.GetService(typeof(IStatusService));
            this.myMoney.Changed += new EventHandler<ChangeEventArgs>(OnMoneyChanged);

            // assume we have fetched all securities.
            // call UpdateQuotes to refetch them all again, otherwise this
            // class will track changes and automatically fetch any new securities that it finds.
            foreach (Security s in myMoney.Securities.AllSecurities)
            {
                if (!string.IsNullOrEmpty(s.Symbol))
                {
                    fetched.Add(s.Symbol);
                }
            }
        }

        void OnMoneyChanged(object sender, ChangeEventArgs args)
        {
            List<Security> newSecurities = new List<Security>();
            while (args != null)
            {
                if (args.Item is Security)
                {
                    Security s = (Security)args.Item;
                    string symbol = s.Symbol;

                    if (!string.IsNullOrEmpty(symbol))
                    {
                        switch (args.ChangeType)
                        {
                            case ChangeType.Changed:
                            case ChangeType.Inserted:
                                if (!fetched.Contains(symbol))
                                {
                                    newSecurities.Add(s);
                                }
                                break;
                            case ChangeType.Deleted:
                                if (fetched.Contains(symbol))
                                {
                                    fetched.Remove(symbol);
                                    newSecurities.Remove(s);
                                }
                                break;
                        }
                    }
                }
                args = args.Next;
            }

            Enqueue(newSecurities);            
        }

        private void Enqueue(List<Security> toFetch)
        {
            if (processingResults)
            {
                // avoid triggering our own infinite loop.
                return;
            }
            lock (queue)
            {
                foreach (Security s in toFetch)
                {
                    if (!queue.Contains(s))
                    {
                        queue.Add(s);
                    }
                }
            }
            if (queue.Count > 0)
            {
                BeginGetQuotes();
            }
        }

        public void UpdateQuotes()
        {
            Enqueue(myMoney.GetOwnedSecurities());
        }

        void BeginGetQuotes()
        {
            if (quotesThread == null)
            {
                stop = false;
                this.quotesThread = new Thread(new ThreadStart(GetQuotes));
                this.quotesThread.Start();
            }
        }

        EventHandlerCollection<EventArgs> handlers;

        public event EventHandler<EventArgs> DownloadComplete
        {
            add
            {
                if (handlers == null)
                {
                    handlers = new EventHandlerCollection<EventArgs>();
                }
                handlers.AddHandler(value);
            }
            remove
            {
                if (handlers != null)
                {
                    handlers.RemoveHandler(value);
                }
            }
        }

        void OnDownloadComplete()
        {
            if (handlers != null && handlers.HasListeners)
            {
                handlers.RaiseEvent(this, EventArgs.Empty);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopThread();
            }
        }

        public bool Busy
        {
            get
            {
                return busy;
            }
        }

        void StopThread()
        {
            stop = true;
            if (_current != null)
            {
                _current.Abort();
            }
            if (status != null)
            {
                status.ShowProgress(string.Empty, 0, 0, 0);
            }
        }

        void GetQuotes()
        {
            try
            {
                busy = true;
                hasError = false;
                errorLog = new StringBuilder();
                newQuotes = XDocument.Parse("<StockQuotes/>");

                int max = 0;
                List<Security> localCopy;
                lock (queue)
                {
                    localCopy = new List<Security>(queue);
                    max = localCopy.Count;
                    queue.Clear();
                }

                for (int i = 0; i < max; i++)
                {
                    Security s = localCopy[i];
                    string symbol = s.Symbol;
                    if (stop) break;
                    try
                    {
                        if (!string.IsNullOrEmpty(symbol))
                        {
                            if (symbol.IndexOfAny(illegalUrlChars) >= 0)
                            {
                                AddError(string.Format(Walkabout.Properties.Resources.SkippingSecurityIllegalSymbol, symbol));
                            }
                            else
                            {
                                FetchQuote(s);
                            }
                        }
                        else
                        {
                            AddError(string.Format(Walkabout.Properties.Resources.SkippingSecurityMissingSymbol, s.Name));
                        }
                        if (status != null)
                        {
                            status.ShowProgress(s.Name, 0, max, i);
                        }
                    }
                    catch (System.Net.WebException we)
                    {
                        if (we.Status != WebExceptionStatus.RequestCanceled)
                        {
                            AddError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + we.Message);
                        }
                        else
                        {
                            // we cancelled, so bail.
                            stop = true;
                            break;
                        }

                        HttpWebResponse http = we.Response as HttpWebResponse;
                        if (http != null)
                        {
                            // certain http error codes are fatal.
                            switch (http.StatusCode)
                            {
                                case HttpStatusCode.ServiceUnavailable:
                                case HttpStatusCode.InternalServerError:
                                case HttpStatusCode.Unauthorized:
                                    AddError(http.StatusDescription);
                                    stop = true;
                                    break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        XElement se = new XElement("Query", new XAttribute("Symbols", symbol));
                        se.Add(new XElement("Error", e.Message));
                        newQuotes.Root.Add(se);

                        // continue
                        AddError(string.Format(Walkabout.Properties.Resources.ErrorFetchingSymbols, symbol) + "\r\n" + e.Message);
                    }
                }

                if (!stop)
                {
                    OnDownloadComplete();
                }

            }
            catch (ThreadAbortException)
            {
                // shutting down.            
                quotesThread = null;
                return;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }

            UiDispatcher.BeginInvoke(new Action(() =>
            {
                // must run on the UI thread because some Money changed event handlers change dependency properties and that requires UI thread.
                ProcessResults();
            }));

            this.quotesThread = null;
        }

        bool processingResults;

        private void ProcessResults()
        {
            try
            {
                processingResults = true;  // lock out Enqueue.

                // Now batch update the securities instead of dribbling them in one by one.
                this.myMoney.Securities.BeginUpdate(true);
                try
                {
                    foreach (XElement e in newQuotes.Root.Elements())
                    {
                        ProcessResult(e);
                    }
                }
                finally
                {
                    this.myMoney.Securities.EndUpdate();
                }

                busy = false;

                if (status != null)
                {
                    status.ShowProgress("", 0, 0, 0);
                }
                quotesThread = null;
                if (newQuotes != null)
                {
                    string dir = OfxRequest.OfxLogPath;
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    string path = Path.Combine(dir, "Stocks.xml");
                    newQuotes.Save(path);

                    if (hasError && !stop)
                    {
                        Paragraph p = new Paragraph();
                        p.Inlines.Add(errorLog.ToString());
                        p.Inlines.Add(new LineBreak());
                        p.Inlines.Add(new LineBreak());
                        p.Inlines.Add("See ");
                        var link = new Hyperlink() { NavigateUri = new Uri("file://" + path) };
                        link.Cursor = Cursors.Arrow;
                        link.PreviewMouseLeftButtonDown += OnShowLogFile;
                        link.Inlines.Add("Log File");
                        p.Inlines.Add(link);
                        p.Inlines.Add(" for details");

                        OutputPane output = (OutputPane)provider.GetService(typeof(OutputPane));
                        output.AppendHeading(Walkabout.Properties.Resources.StockQuoteErrorCaption);
                        output.AppendParagraph(p);
                        output.Show();
                    }
                }
            } 
            catch (Exception e)
            {
                MessageBoxEx.Show(e.ToString(), Walkabout.Properties.Resources.StockQuotesException, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                processingResults = false;
            }
        }

        void OnShowLogFile(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            Uri uri = link.NavigateUri;
            InternetExplorer.OpenUrl(IntPtr.Zero, uri.AbsoluteUri);
        }


        static void ReadMetadata(JsonTextReader reader, string symbol)
        {
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "2. Symbol")
                {
                    if (reader.Read() && reader.TokenType == JsonToken.String)
                    {
                        string s = (string)reader.Value;
                        if (string.Compare(s, symbol, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            throw new Exception(string.Format("Expection information on symbol {0}, but got symbol {1} instead", symbol, s));
                        }
                    }
                }
            }
        }

        static XElement ReadDailyValue(JsonTextReader reader)
        {
            //  "Symbol", "Name", "Price", "LastPrice" 
            XElement quote = new XElement("Quote");

            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string name = (string)reader.Value;
                    if (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.StartObject)
                        {
                            quote.Add(new XElement("Date", name));
                        }
                        else if (reader.TokenType == JsonToken.String)
                        {
                            string value = (string)reader.Value;
                            switch (name)
                            {
                                case "1. open":
                                    quote.Add(new XElement("LastPrice", value));
                                    break;
                                case "2. high":
                                    break;
                                case "3. low":
                                    break;
                                case "4. close":
                                    quote.Add(new XElement("Price", value));
                                    break;
                                case "5. adjusted close":
                                    break;
                                case "6. volume":
                                    break;
                                case "7. dividend amount":
                                    break;
                                case "8. split coefficient":
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            return quote;
        }

        static XElement ReadTimeSeries(JsonTextReader reader)
        {
            List<XElement> list = new List<XElement>();
            while (reader.Read() && reader.TokenType != JsonToken.EndObject)
            {
                if (reader.TokenType == JsonToken.StartObject)
                {
                    return ReadDailyValue(reader);
                }
            }
            return null;
        }
        
        XElement ParseStockQuote(JsonTextReader reader, Security s)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.PropertyName)
                {
                    string name = (string)reader.Value;
                    if (name == "Meta Data")
                    {
                        ReadMetadata(reader, s.Symbol);
                    }
                    else if (name == "Time Series (Daily)")
                    {
                        XElement today = ReadTimeSeries(reader);
                        if (today != null)
                        {
                            today.Add(new XElement("Symbol", s.Symbol));
                            today.Add(new XElement("Name", s.Name));
                            return today;
                        }
                    }
                    else if (name == "Information")
                    {
                        if (reader.Read() && reader.TokenType == JsonToken.String)
                        {
                            string reason = (string)reader.Value;
                            throw new Exception(string.Format("Error fetching security '{0}': {1}", s.Name, reason));
                        }
                    }
                }
            }
            return null;
        }


        void FetchQuote(Security s)
        {
            // T7ZS25TB090CWC0Q
            // See https://www.alphavantage.co/documentation/
            string uri = string.Format(address, "T7ZS25TB090CWC0Q", s.Symbol);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.UserAgent = "USER_AGENT=Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.1;)";
            req.Method = "GET";
            req.Timeout = 10000;
            req.UseDefaultCredentials = false;
            _current = req;

            WebResponse resp = req.GetResponse();            
            using (Stream stm = resp.GetResponseStream())
            {
                using (StreamReader sr = new StreamReader(stm, Encoding.UTF8)) 
                {
                    using (var reader = new Newtonsoft.Json.JsonTextReader(sr))
                    {
                        XElement quote = ParseStockQuote(reader, s);
                        if (quote != null)
                        {
                            newQuotes.Root.Add(quote);
                        }
                        else
                        {
                            AddError("Stock quote result format not recognized.");
                        }
                    }
                }
            }

            // this service doesn't want too many calls per second.
            Thread.Sleep(1000);
        }

        void AddError(string msg)
        {
            hasError = true;
            errorLog.AppendLine(msg);
        }

        void ProcessResult(XElement stock)
        {
            string name = GetString(stock, "Symbol");                            
            decimal quote = GetDecimal(stock, "Price");
            if (quote != 0)
            {
                // we want to stop this from adding new Security objects by passing false
                // because the Security objects should already exist as given to Enqueue
                // and we don't even fetch anything of those Security objects don't already
                // have a 'Symbol' to lookup.
                Security s = this.myMoney.Securities.FindSymbol(name, false);
                if (s == null || s.IsDeleted)
                {
                    return;
                }

                // Check to see if the security name has changed and update if needed
                string securityName = GetString(stock, "Name");
                if (!string.IsNullOrEmpty(securityName) && (string.IsNullOrEmpty(s.Name) || s.Name == name))
                {
                    s.Name = securityName;
                }
                string lp = GetString(stock, "LastPrice");
                if (lp == "N/A")
                {
                    AddError(string.Format(Walkabout.Properties.Resources.YahooSymbolNotFound, name));
                }
                decimal last = GetDecimal(stock, "LastPrice");
                s.LastPrice = last;
                s.Price = quote;
            }
        }

        static string GetString(XElement e, string name)
        {
            XElement node = e.Element(name);
            return (node != null) ? node.Value : string.Empty;
        }

        static decimal GetDecimal(XElement e, string name)
        {
            decimal result = 0;
            XElement node = e.Element(name);
            if (node == null)
            {
                return 0;
            }
            string value = node.Value;
            if (!string.IsNullOrEmpty(value))
            {
                decimal.TryParse(value, out result);
            }
            return result;
        }

    }
}
