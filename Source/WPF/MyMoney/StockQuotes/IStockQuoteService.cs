using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Utilities;

namespace Walkabout.StockQuotes
{

    public class DownloadCompleteEventArgs : EventArgs
    {
        /// <summary>
        /// A status message to display on completion.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Set this to true if the entire download is complete.  Set it to false if
        /// it is just a batch completion.
        /// </summary>
        public bool Complete { get; set; }
    }

    public interface IStockQuoteService
    {
        /// <summary>
        /// Whether this service is configured with an Api Key
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Fetch updated security information for the given security.
        /// This can be called multiple times so the service needs to keep a queue of pending
        /// downloads.
        /// </summary>
        /// <param name="securities">List of securities to fetch </param>
        void BeginFetchQuote(string symbol);

        /// <summary>
        /// Return true if your service supports batch download of quotes, meaning one http
        /// request retrieves multiple different quotes at once.  This is usually faster 
        /// than using BeginFetchQuote and is preferred, but if your service doesn't support
        /// this then BeginFetchQuotes will not be called.
        /// </summary>
        bool SupportsBatchQuotes { get; }

        /// <summary>
        /// Fetch updated security information for the given securities (most recent closing price).
        /// This can be called multiple times so the service needs to keep a queue of pending
        /// downloads.
        /// </summary>
        /// <param name="securities">List of securities to fetch </param>
        void BeginFetchQuotes(List<string> symbols);

        /// <summary>
        /// Return true if your service supports the UpdateHistory function.
        /// </summary>
        bool SupportsHistory { get; }

        /// <summary>
        /// If the stock quote service supports it, updates the given StockQuoteHistory
        /// with daily quotes back 20 years.
        /// </summary>
        /// <param name="symbol">The stock whose history is to be downloaded</param>
        /// <returns>Returns true if the history was updated or false if history is not found</returns>
        Task<bool> UpdateHistory(StockQuoteHistory history);

        /// <summary>
        /// Return a count of pending downloads.
        /// </summary>
        int PendingCount { get; }


        /// <summary>
        /// For the current session until all downloads are complete this returns the number of
        /// items completed from the batch provided in BeginFetchQuotes.  Once all downloads are
        /// complete this goes back to zero.
        /// </summary>
        int DownloadsCompleted { get; }

        /// <summary>
        /// Each downloaded quote is raised as an event on this interface.  Could be from any thread.
        /// </summary>
        event EventHandler<StockQuote> QuoteAvailable;

        /// <summary>
        /// Event means a given stock quote symbol was not found by the stock quote service.  
        /// </summary>
        event EventHandler<string> SymbolNotFound;

        /// <summary>
        /// If some error happens fetching a quote, this event is raised.
        /// </summary>
        event EventHandler<string> DownloadError;

        /// <summary>
        /// If the service is performing a whole batch at once, this event is raised after each batch is complete.
        /// If there are still more downloads pending the boolean value for the Complete property is false.
        /// This is also raised when the entire pending list is completed with the boolean set to true.
        /// </summary>
        event EventHandler<DownloadCompleteEventArgs> Complete;

        /// <summary>
        /// This event is raised if quota limits are stopping the service from responding right now.
        /// The booling is true when suspended, and false when resuming.
        /// </summary>
        event EventHandler<bool> Suspended;

        /// <summary>
        /// Returns true if the service is currently suspended (sleeping)
        /// </summary>
        bool IsSuspended { get; }

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
        [XmlAttribute]
        public DateTime Downloaded { get; set; }
    }

    /// <summary>
    /// A stock quote log designed for XML serialization
    /// </summary>
    public class StockQuoteHistory
    {
        public StockQuoteHistory() { this.History = new List<StockQuote>(); }

        public string Symbol { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Whether this is a partial or complete history.
        /// </summary>
        public bool Complete { get; set; }

        public List<StockQuote> History { get; set; }

        public DateTime MostRecentDownload
        {
            get
            {
                if (this.History != null && this.History.Count > 0)
                {
                    return this.History.Last().Downloaded;
                }
                return DateTime.MinValue;
            }
        }

        public List<StockQuote> GetSorted()
        {
            var result = new SortedDictionary<DateTime, StockQuote>();
            if (this.History != null)
            {
                foreach (var quote in this.History)
                {
                    result[quote.Date] = quote;
                }
            }
            return new List<StockQuote>(result.Values);
        }

        public bool AddQuote(StockQuote quote, bool replace = true)
        {
            if (this.History == null)
            {
                this.History = new List<StockQuote>();
            }
            quote.Date = quote.Date.Date;
            if (!string.IsNullOrEmpty(quote.Name))
            {
                this.Name = quote.Name;
                quote.Name = null;
            }
            int len = this.History.Count;
            for (int i = 0; i < len; i++)
            {
                var h = this.History[i];
                if (h.Date == quote.Date)
                {
                    // already have this one
                    if (replace)
                    {
                        h.Downloaded = quote.Downloaded;
                        h.Open = quote.Open;
                        h.Close = quote.Close;
                        h.High = quote.High;
                        h.Low = quote.Low;
                        h.Volume = quote.Volume;
                    }
                    return true;
                }
                if (h.Date > quote.Date)
                {
                    // keep it sorted by date
                    this.History.Insert(i, quote);
                    return true;
                }
            }
            this.History.Add(quote);
            return true;
        }

        public static StockQuoteHistory Load(string logFolder, string symbol)
        {
            var filename = GetFileName(logFolder, symbol);
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
            var filename = GetFileName(logFolder, this.Symbol);
            XmlSerializer s = new XmlSerializer(typeof(StockQuoteHistory));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(filename, settings))
            {
                s.Serialize(w, this);
            }
        }

        public static string GetFileName(string logFolder, string symbol)
        {
            return System.IO.Path.Combine(logFolder, symbol + ".xml");
        }

        internal void Merge(StockQuoteHistory newHistory)
        {
            foreach (var item in newHistory.History)
            {
                this.AddQuote(item);
            }
            // promote any stock quote names to the root (to save space)
            foreach (var item in this.History)
            {
                if (!string.IsNullOrEmpty(item.Name))
                {
                    this.Name = item.Name;
                    item.Name = null;
                }
            }
        }

        private static DateTime[] knownClosures = new DateTime[]
        {
            new DateTime(2018, 12, 5), // honor of President George Bush
            new DateTime(2012, 10, 30), // Hurrican Sandy
            new DateTime(2012, 10, 29), // Hurrican Sandy
            new DateTime(2007, 1, 2), // Honor of President Gerald Ford
            new DateTime(2004, 6, 11), // Honor of President Ronald Reagan
            new DateTime(2001, 9, 14), // 9/11
            new DateTime(2001, 9, 13), // 9/11
            new DateTime(2001, 9, 12), // 9/11
            new DateTime(2001, 9, 11), // 9/11
        };

        internal bool IsComplete()
        {
            if (!this.Complete || this.History.Count == 0)
            {
                return false;
            }

            int missing = 0; // in the last 3 months
            var holidays = new UsHolidays();
            DateTime workDay = holidays.GetPreviousWorkDay(DateTime.Today.AddDays(1));
            DateTime stopDate = workDay.AddMonths(-3);
            int count = 0; // work days
            for (int i = this.History.Count - 1; i >= 0; i--)
            {
                count++;
                StockQuote quote = this.History[i];
                DateTime date = quote.Date.Date;
                if (date > workDay)
                {
                    continue; // might have duplicates?
                }
                if (workDay < stopDate)
                {
                    break;
                }
                if (date < workDay)
                {
                    if (!knownClosures.Contains(workDay))
                    {
                        missing++;
                    }
                    i++;
                }
                workDay = holidays.GetPreviousWorkDay(workDay);
            }
            // There are some random stock market closures which we can't keep track of easily, so
            // make sure we are not missing more than 1% of the history.
            return missing < count * 0.01;
        }


        internal bool RemoveDuplicates()
        {
            if (this.History != null)
            {
                StockQuote previous = null;
                List<StockQuote> duplicates = new List<StockQuote>();
                for (int i = 0; i < this.History.Count; i++)
                {
                    StockQuote quote = this.History[i];
                    if (quote.Date == DateTime.MinValue)
                    {
                        duplicates.Add(quote);
                    }
                    else if (previous != null)
                    {
                        if (previous.Date.Date == quote.Date.Date)
                        {
                            duplicates.Add(previous);
                        }
                    }
                    previous = quote;
                }
                foreach (StockQuote dup in duplicates)
                {
                    this.History.Remove(dup);
                }
                return duplicates.Count > 0;
            }
            return false;
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
            get { return this._name; }
            set
            {
                if (this._name != value)
                {
                    this._name = value;
                    this.OnPropertyChanged("Name");
                }
            }
        }

        public string ApiKey
        {
            get { return this._apiKey; }
            set
            {
                if (this._apiKey != value)
                {
                    this._apiKey = value;
                    this.OnPropertyChanged("ApiKey");
                }
            }
        }

        public int ApiRequestsPerMinuteLimit
        {
            get { return this._requestsPerMinute; }
            set
            {
                if (this._requestsPerMinute != value)
                {
                    this._requestsPerMinute = value;
                    this.OnPropertyChanged("ApiRequestsPerMinuteLimit");
                }
            }
        }

        public int ApiRequestsPerDayLimit
        {
            get { return this._requestsPerDay; }
            set
            {
                if (this._requestsPerDay != value)
                {
                    this._requestsPerDay = value;
                    this.OnPropertyChanged("ApiRequestsPerDayLimit");
                }
            }
        }

        public int ApiRequestsPerMonthLimit
        {
            get { return this._requestsPerMonth; }
            set
            {
                if (this._requestsPerMonth != value)
                {
                    this._requestsPerMonth = value;
                    this.OnPropertyChanged("ApiRequestsPerMonthLimit");
                }
            }
        }

        public string OldName { get; internal set; }

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
            if (r.IsEmptyElement)
            {
                return;
            }

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
