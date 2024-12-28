using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Utilities;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        /// Returns the name of the service.
        /// </summary>
        string FriendlyName { get; }

        /// <summary>
        /// Whether this service is configured with an Api Key
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Fetch latest security information for the given securities (most recent closing price).
        /// This can be called multiple times so the service needs to keep a queue of pending
        /// downloads.
        /// </summary>
        /// <param name="securities">List of securities to fetch </param>
        void BeginFetchQuotes(List<string> symbols);

        /// <summary>
        /// Test that the given api key works.
        /// </summary>
        /// <param name="apiKey"></param>
        /// <returns>Error if something goes wrong, or empty string</returns>
        Task<string> TestApiKeyAsync(string apiKey);

        /// <summary>
        /// Updates the given StockQuoteHistory with daily quotes back as far as the service provides
        /// or until the stock IPO.  
        /// 
        /// The history of stock quotes must be split adjusted, meaning it is showing what the
        /// effective price of 1 share was, so if you paid $100 for a stock in 2010 and 
        /// there was a 2:1 stock split in 2012, then you really only paid $50 for 1 share
        /// in 2010 because that $100 actually paid for 2. So split adjustment means the
        /// stock quote history for 2010 shows the price was $50. This ensures the entire
        /// history is showing the price for 1 share according to today's share count.
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

    public class DateRange: EventArgs
    {
        public DateRange(DateTime start, DateTime end)
        {
            this.Start = start;
            this.End = end;
        }

        public DateTime Start { get; set;  }

        public DateTime End { get; set;  }
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
    /// A stock quote log designed for XML serialization.
    /// </summary>
    public class StockQuoteHistory
    {
        private UsHolidays holidays = new UsHolidays();

        public StockQuoteHistory() { this.History = new List<StockQuote>(); }

        public string Symbol { get; set; }
        public string Name { get; set; }
        public bool NotFound { get; set; }
        public DateTime LastUpdate { get; set; }
        public DateTime? EarliestTime { get; set; }
        public HashSet<DateTime> AdditionalClosures { get; set; }

        /// <summary>
        /// The history of stock quotes, split adjusted, meaning it is showing what the
        /// effective price of 1 share was, so if you paid $100 for a stock in 2010 and 
        /// there was a 2:1 stock split in 2012, then you really only paid $50 for 1 share
        /// in 2010 because that $100 actually paid for 2. So split adjustment means the
        /// stock quote history for 2010 shows the price was $50. This ensures the entire
        /// history is showing the price for 1 share according to today's share count.
        /// </summary>
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

        public bool NeedsUpdating
        {
            get
            {
                if (this.LastUpdate == DateTime.MinValue)
                {
                    return true;
                }
                var workDay = this.holidays.MostRecentWorkDay;
                var daysBehind = (workDay - this.LastUpdate).TotalDays;
                return daysBehind > 0;
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

        public bool MergeQuote(StockQuote quote, bool replace = true)
        {
            this.LastUpdate = DateTime.Today;
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
                this.MergeQuote(item);
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

        private static readonly HashSet<DateTime> knownClosures = new HashSet<DateTime>(new DateTime[]
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
            new DateTime(1994, 4, 27), // Honor of President Richard Nixon
            new DateTime(1985, 9, 27), // Hurrican Gloria
        });

        /// <summary>
        /// Return the days that seem to be missing in our data.
        /// </summary>
        /// <param name="yearsToCheck">How far to go back in time to find missing data.</param>
        /// <returns></returns>
        internal IEnumerable<DateRange> GetMissingDataRanges(int yearsToCheck)
        {
            var holidays = new UsHolidays();
            var workDay = this.holidays.MostRecentWorkDay;
            DateTime stopDate = workDay.AddYears(-yearsToCheck);
            if (!holidays.IsWorkDay(stopDate))
            {
                stopDate = holidays.GetNextWorkDay(stopDate);
            }

            if (this.History.Count == 0)
            {
                yield return new DateRange(stopDate, workDay);
            }
            else
            {
                List<DateRange> ranges = new List<DateRange>();
                DateRange missing = null;

                for (int i = this.History.Count - 1; i >= 0; i--)
                {
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
                        bool skip = knownClosures.Contains(workDay) || (this.AdditionalClosures?.Contains(workDay) == true);
                        if (!skip)
                        {
                            // found a missing date range.
                            if (missing != null)
                            {
                                // can we consolidate ranges?
                                var gap = missing.Start - date;
                                if (gap.TotalDays < 5)
                                {
                                    // consolidate!
                                    missing.Start = date;
                                }
                                else
                                {
                                    ranges.Add(missing);
                                    missing = null;
                                }
                            }
                            if (missing == null)
                            {
                                missing = new DateRange(date, workDay);
                            }
                            workDay = date;
                        }
                    }
                    workDay = holidays.GetPreviousWorkDay(workDay);
                    if (ranges.Count > 10)
                    {
                        break;
                    }
                }

                if (workDay > stopDate)
                {
                    // then our data doesn't go back far enough!
                    // can we consolidate ranges?
                    if (missing != null)
                    {
                        var gap = missing.Start - stopDate;
                        if (gap.TotalDays < 5)
                        {
                            // consolidate!
                            missing.Start = stopDate;
                        }
                        else
                        {
                            ranges.Add(missing);
                            workDay = missing.Start;
                            missing = null;
                        }
                    }

                    if (missing == null)
                    {
                        missing = new DateRange(stopDate, workDay);
                    }
                    else
                    {
                        missing.Start = stopDate;
                    }
                }

                if (missing != null)
                {
                    ranges.Add(missing);
                }

                // Some data seems to be missing about 1 week around the knownClosures.
                int closureFudge = 7; // days
                foreach (var range in ranges.ToArray())
                {
                    foreach (var closure in knownClosures)
                    {
                        var size = (range.End - range.Start).TotalDays;
                        var daysBefore = Math.Abs((range.Start - closure).TotalDays);
                        var daysAfter = Math.Abs((range.End - closure).TotalDays);
                        if (size < closureFudge && (daysBefore < closureFudge || daysAfter < closureFudge))
                        {
                            ranges.Remove(range);
                            break;
                        }
                    }
                }

                if (ranges.Count > 5)
                {
                    // then it's probably more efficient to just do one big call for the whole history.
                    workDay = ranges[0].End;
                    yield return new DateRange(stopDate, workDay);
                }
                else
                {
                    foreach (var result in ranges)
                    {
                        yield return result;
                    }
                }
            }
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
        private string _address;
        private string _apiKey;
        private int _requestsPerMinute;
        private int _requestsPerDay;
        private int _requestsPerMonth;
        private bool _historyEnabled;
        private bool _splitHistoryEnabled;

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

        public string Address
        {
            get { return this._address; }
            set
            {
                if (this._address != value)
                {
                    this._address = value;
                    this.OnPropertyChanged("Address");
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

        public bool HistoryEnabled
        {
            get { return this._historyEnabled; }
            set
            {
                if (this._historyEnabled != value)
                {
                    this._historyEnabled = value;
                    this.OnPropertyChanged("HistoryEnabled");
                }
            }
        }

        /// <summary>
        ///  Can fetch stock splits.
        /// </summary>
        public bool SplitHistoryEnabled
        {
            get { return this._splitHistoryEnabled; }
            set
            {
                if (this._splitHistoryEnabled != value)
                {
                    this._splitHistoryEnabled = value;
                    this.OnPropertyChanged("SplitHistoryEnabled");
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
            w.WriteElementString("HistoryEnabled", XmlConvert.ToString(this.HistoryEnabled));
            w.WriteElementString("SplitHistoryEnabled", XmlConvert.ToString(this.SplitHistoryEnabled));
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
                    else if (r.Name == "HistoryEnabled")
                    {
                        this.HistoryEnabled = r.ReadElementContentAsBoolean();
                    }
                    else if (r.Name == "SplitHistoryEnabled")
                    {
                        this.SplitHistoryEnabled = r.ReadElementContentAsBoolean();
                    }
                }
            }
        }

    }


}
