using System;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Utilities;

namespace Walkabout.StockQuotes
{
    public class StockQuoteThrottle
    {
        DateTime _lastCall = DateTime.MinValue;
        int _callsThisMinute;
        int _callsToday;
        int _callsThisMonth;
        string _filename;
        object _sync = new object();
        DelayedActions saveActions = new DelayedActions();

        public StockQuoteThrottle()
        {
        }

        public string FileName
        {
            get { return _filename; }
            set { _filename = value; }
        }

        [XmlIgnore]
        public StockServiceSettings Settings { get; set; }

        public DateTime LastCall
        {
            get { return _lastCall; }
            set { _lastCall = value; }
        }

        public int CallsThisMinute
        {
            get { return _callsThisMinute; }
            set { _callsThisMinute = value; }
        }

        public int CallsToday
        {
            get { return _callsToday; }
            set { _callsToday = value; }
        }

        public int CallsThisMonth
        {
            get { return _callsThisMonth; }
            set { _callsThisMonth = value; }
        }

        private void CheckResetCounters()
        {
            lock (_sync)
            {
                bool changed = false;
                var now = DateTime.Now;
                if (!(now.Year == _lastCall.Year && now.Month == _lastCall.Month))
                {
                    _callsThisMonth = 0;
                    _callsToday = 0;
                    _callsThisMinute = 0;
                }
                else if (now.Date != _lastCall.Date)
                {
                    _callsToday = 0;
                    _callsThisMinute = 0;
                }
                else if (now.Hour != _lastCall.Hour || now.Minute != _lastCall.Minute)
                {
                    _callsThisMinute = 0;
                }
                if (changed)
                {
                    saveActions.StartDelayedAction("save", Save, TimeSpan.FromSeconds(1));
                }
            }
        }

        public void RecordCall()
        {
            lock (_sync)
            {
                CheckResetCounters();
                _callsThisMonth++;
                _callsToday++;
                _callsThisMinute++;
                _lastCall = DateTime.Now;
            }
            saveActions.StartDelayedAction("save", Save, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Get throttled sleep amount in milliseconds.
        /// </summary>
        /// <returns></returns>
        public int GetSleep()
        {
            CheckResetCounters();
            int result = 0;
            if (Settings.ApiRequestsPerMonthLimit != 0 && _callsThisMonth > Settings.ApiRequestsPerMonthLimit)
            {
                throw new Exception(Walkabout.Properties.Resources.StockServiceQuotaExceeded);
            }
            else if (Settings.ApiRequestsPerDayLimit != 0 && _callsToday > Settings.ApiRequestsPerDayLimit)
            {
                throw new Exception(Walkabout.Properties.Resources.StockServiceQuotaExceeded);
            }
            else if (Settings.ApiRequestsPerMinuteLimit != 0 && _callsThisMinute >= Settings.ApiRequestsPerMinuteLimit)
            {
                result = 60000; // sleep to next minute.
            }
            return result;
        }

        public void Save()
        {
            var fullPath = System.IO.Path.Combine(ProcessHelper.AppDataPath, _filename);
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
