using System;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Utilities;

namespace Walkabout.StockQuotes
{
    public class StockQuoteThrottle
    {
        private DateTime _lastCall = DateTime.MinValue;
        private int _callsThisMinute;
        private int _callsToday;
        private int _callsThisMonth;
        private string _filename;
        private readonly object _sync = new object();
        private readonly DelayedActions saveActions = new DelayedActions();

        public StockQuoteThrottle()
        {
        }

        public string FileName
        {
            get { return this._filename; }
            set { this._filename = value; }
        }

        [XmlIgnore]
        public StockServiceSettings Settings { get; set; }

        public DateTime LastCall
        {
            get { return this._lastCall; }
            set { this._lastCall = value; }
        }

        public int CallsThisMinute
        {
            get { return this._callsThisMinute; }
            set { this._callsThisMinute = value; }
        }

        public int CallsToday
        {
            get { return this._callsToday; }
            set { this._callsToday = value; }
        }

        public int CallsThisMonth
        {
            get { return this._callsThisMonth; }
            set { this._callsThisMonth = value; }
        }

        private void CheckResetCounters()
        {
            lock (this._sync)
            {
                bool changed = false;
                var now = DateTime.Now;
                if (!(now.Year == this._lastCall.Year && now.Month == this._lastCall.Month))
                {
                    this._callsThisMonth = 0;
                    this._callsToday = 0;
                    this._callsThisMinute = 0;
                }
                else if (now.Date != this._lastCall.Date)
                {
                    this._callsToday = 0;
                    this._callsThisMinute = 0;
                }
                else if (now.Hour != this._lastCall.Hour || now.Minute != this._lastCall.Minute)
                {
                    this._callsThisMinute = 0;
                }
                if (changed)
                {
                    this.saveActions.StartDelayedAction("save", this.Save, TimeSpan.FromSeconds(1));
                }
            }
        }

        public void RecordCall()
        {
            lock (this._sync)
            {
                this.CheckResetCounters();
                this._callsThisMonth++;
                this._callsToday++;
                this._callsThisMinute++;
                this._lastCall = DateTime.Now;
            }
            this.saveActions.StartDelayedAction("save", this.Save, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Get throttled sleep amount in milliseconds.
        /// </summary>
        /// <returns></returns>
        public int GetSleep()
        {
            this.CheckResetCounters();
            int result = 0;
            if (this.Settings.ApiRequestsPerMonthLimit != 0 && this._callsThisMonth > this.Settings.ApiRequestsPerMonthLimit)
            {
                throw new Exception(Walkabout.Properties.Resources.StockServiceQuotaExceeded);
            }
            else if (this.Settings.ApiRequestsPerDayLimit != 0 && this._callsToday > this.Settings.ApiRequestsPerDayLimit)
            {
                throw new Exception(Walkabout.Properties.Resources.StockServiceQuotaExceeded);
            }
            else if (this.Settings.ApiRequestsPerMinuteLimit != 0 && this._callsThisMinute >= this.Settings.ApiRequestsPerMinuteLimit)
            {
                result = 61000; // sleep to next minute.
            }
            return result;
        }

        public void Save()
        {
            var fullPath = System.IO.Path.Combine(ProcessHelper.AppDataPath, this._filename);
            XmlSerializer s = new XmlSerializer(typeof(StockQuoteThrottle));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(fullPath, settings))
            {
                s.Serialize(w, this);
            }
        }

        public static StockQuoteThrottle Load(string filename)
        {
            var fullPath = System.IO.Path.Combine(ProcessHelper.AppDataPath, filename);
            if (System.IO.File.Exists(fullPath))
            {
                XmlSerializer s = new XmlSerializer(typeof(StockQuoteThrottle));
                using (XmlReader r = XmlReader.Create(fullPath))
                {
                    var throttle = (StockQuoteThrottle)s.Deserialize(r);
                    throttle.FileName = filename;
                    return throttle;
                }
            }
            return new StockQuoteThrottle() { FileName = filename };
        }
    }

}
