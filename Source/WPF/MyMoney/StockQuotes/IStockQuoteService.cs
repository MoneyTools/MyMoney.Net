using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Data;

namespace Walkabout.StockQuotes
{

    public interface IStockQuoteService
    {
        /// <summary>
        /// If the stock quote service supports it, this method downloads a daily stock price history
        /// for the given symbol.
        /// </summary>
        /// <param name="symbol">The stock whose history is to be downloaded</param>
        /// <returns>The history or null if downloading a history is not supported</returns>
        Task<StockQuoteHistory> DownloadHistory(string symbol);

        /// <summary>
        /// Fetch updated security information for the given securities (most recent closing price).
        /// This can be called multiple times and any pending downloads will be merged automatically
        /// </summary>
        /// <param name="securities">List of securities to fetch </param>
        void BeginFetchQuotes(List<string> symbols);

        /// <summary>
        /// Return a count of pending downloads.
        /// </summary>
        int PendingCount { get; }

        /// <summary>
        /// Each downloaded quote is raised as an event on this interface.  Could be from any thread.
        /// </summary>
        event EventHandler<StockQuote> QuoteAvailable;

        /// <summary>
        /// If some error happens fetching a quote, this event is raised.
        /// </summary>
        event EventHandler<string> DownloadError;

        /// <summary>
        /// If the service is performing a whole batch at once, this event is raised after each batch is complete.
        /// If there are still more downloads pending the boolean value is raised with the value false.
        /// This is also raised when the entire pending list is completed with the boolean set to true.
        /// </summary>
        event EventHandler<bool> Complete;

        /// <summary>
        /// This event is raised if quota limits are stopping the service from responding right now.
        /// The booling is true when suspended, and false when resuming.
        /// </summary>
        event EventHandler<bool> Suspended;

        /// <summary>
        /// Stop all pending requests
        /// </summary>
        void Cancel();
    }

    /// <summary>
    /// This class encapsulates a new stock quote from IStockQuoteService, and is also
    /// designed for XML serialization
    /// </summary>
    public class StockQuote
    {
        public StockQuote() { }
        [XmlAttribute]
        public string Name { get; set; }
        [XmlAttribute]
        public string Symbol { get; set; }
        [XmlAttribute]
        public DateTime Date { get; set; }
        [XmlAttribute]
        public decimal Open { get; set; }
        [XmlAttribute]
        public decimal Close { get; set; }
        [XmlAttribute]
        public decimal High { get; set; }
        [XmlAttribute]
        public decimal Low { get; set; }
        [XmlAttribute]
        public decimal Volume { get; set; }
    }

    /// <summary>
    /// A stock quote log designed for XML serialization
    /// </summary>
    public class StockQuoteHistory
    {
        public StockQuoteHistory() { }

        public string Symbol { get; set; }

        public List<StockQuote> History { get; set; }

        public List<StockQuote> GetSorted()
        {
            var result = new SortedDictionary<DateTime, StockQuote>();
            if (History != null)
            {
                foreach (var quote in History)
                {
                    result[quote.Date] = quote;
                }
            }
            return new List<StockQuote>(result.Values);
        }

        public bool AddQuote(StockQuote quote)
        {
            if (History == null)
            {
                History = new List<StockQuote>();
            }
            var found = (from i in History where i.Date == quote.Date select i).FirstOrDefault();
            if (found == null)
            {
                History.Add(quote);
                return true;
            }
            return false;
        }

        public static StockQuoteHistory Load(string logFolder, string symbol)
        {
            var filename = System.IO.Path.Combine(logFolder, symbol + ".xml");
            if (System.IO.File.Exists(filename))
            {
                XmlSerializer s = new XmlSerializer(typeof(StockQuoteHistory));
                using (XmlReader r = XmlReader.Create(filename))
                {
                    return (StockQuoteHistory)s.Deserialize(r);
                }
            }
            return null;
        }

        public void Save(string logFolder)
        {
            var filename = System.IO.Path.Combine(logFolder, Symbol + ".xml");
            XmlSerializer s = new XmlSerializer(typeof(StockQuoteHistory));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(filename, settings))
            {
                s.Serialize(w, this);
            }
        }

    }

    /// <summary>
    /// </summary>
    public class StockServiceSettings : INotifyPropertyChanged
    {
        private string _name;
        private string _apiKey;
        private int _requestsPerMinute;
        private int _requestsPerDay;
        private int _requestsPerMonth;

        public string Name
        {
            get { return _name; }
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged("Name");
                }
            }
        }

        public string ApiKey
        {
            get { return _apiKey; }
            set
            {
                if (_apiKey != value)
                {
                    _apiKey = value;
                    OnPropertyChanged("ApiKey");
                }
            }
        }

        public int ApiRequestsPerMinuteLimit
        {
            get { return _requestsPerMinute; }
            set
            {
                if (_requestsPerMinute != value)
                {
                    _requestsPerMinute = value;
                    OnPropertyChanged("ApiRequestsPerMinuteLimit");
                }
            }
        }

        public int ApiRequestsPerDayLimit
        {
            get { return _requestsPerDay; }
            set
            {
                if (_requestsPerDay != value)
                {
                    _requestsPerDay = value;
                    OnPropertyChanged("ApiRequestsPerDayLimit");
                }
            }
        }

        public int ApiRequestsPerMonthLimit
        {
            get { return _requestsPerMonth; }
            set
            {
                if (_requestsPerMonth != value)
                {
                    _requestsPerMonth = value;
                    OnPropertyChanged("ApiRequestsPerMonthLimit");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        public void Serialize(XmlWriter w)
        {
            w.WriteElementString("Name", this.Name == null ? "" : this.Name);
            w.WriteElementString("ApiKey", this.ApiKey == null ? "" : this.ApiKey);
            w.WriteElementString("ApiRequestsPerMinuteLimit", this.ApiRequestsPerMinuteLimit.ToString());
            w.WriteElementString("ApiRequestsPerDayLimit", this.ApiRequestsPerDayLimit.ToString());
            w.WriteElementString("ApiRequestsPerMonthLimit", this.ApiRequestsPerMonthLimit.ToString());
        }

        public void Deserialize(XmlReader r)
        {
            if (r.IsEmptyElement) return;
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    if (r.Name == "Name")
                    {
                        this.Name = r.ReadElementContentAsString();
                    }
                    else if (r.Name == "ApiKey")
                    {
                        this.ApiKey = r.ReadElementContentAsString();
                    }
                    else if (r.Name == "ApiRequestsPerMinuteLimit")
                    {
                        this.ApiRequestsPerMinuteLimit = r.ReadElementContentAsInt();
                    }
                    else if (r.Name == "ApiRequestsPerDayLimit")
                    {
                        this.ApiRequestsPerDayLimit = r.ReadElementContentAsInt();
                    }
                    else if (r.Name == "ApiRequestsPerMonthLimit")
                    {
                        this.ApiRequestsPerMonthLimit = r.ReadElementContentAsInt();
                    }
                }
            }
        }

    }


}
