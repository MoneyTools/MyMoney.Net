using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Utilities;

namespace Walkabout.StockQuotes
{
    public class StockQuoteThrottle
    {
        static StockQuoteThrottle _instance = null;
        DateTime _lastCall = DateTime.MinValue;
        int _callsThisMinute;
        int _callsToday;
        int _callsThisMonth;
        object _sync = new object();

        public StockQuoteThrottle()
        {
            _instance = this;
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

        /// <summary>
        /// Get throttled sleep amount in milliseconds.
        /// </summary>
        /// <returns></returns>
        public int GetSleep()
        {
            int result = 0;
            lock (_sync)
            {
                DateTime now = DateTime.Now;
                if (now.Year == _lastCall.Year && now.Month == _lastCall.Month)
                {
                    _callsThisMonth++;
                    if (Settings.ApiRequestsPerMonthLimit != 0 && _callsThisMonth > Settings.ApiRequestsPerMonthLimit)
                    {
                        throw new Exception(Walkabout.Properties.Resources.StockServiceQuotaExceeded);
                    }
                }
                else
                {
                    _callsThisMonth = 1;
                }
                if (now.Date == _lastCall.Date)
                {
                    _callsToday++;
                    if (Settings.ApiRequestsPerDayLimit != 0 && _callsToday > Settings.ApiRequestsPerDayLimit)
                    {
                        throw new Exception(Walkabout.Properties.Resources.StockServiceQuotaExceeded);
                    }
                    if (now.Hour == _lastCall.Hour && now.Minute == _lastCall.Minute)
                    {
                        _callsThisMinute++;
                        if (Settings.ApiRequestsPerMinuteLimit != 0 && _callsThisMinute >= Settings.ApiRequestsPerMinuteLimit)
                        {
                            result = 60000; // sleep to next minute.
                        }
                    }
                    else
                    {
                        _callsThisMinute = 1;
                    }
                }
                else
                {
                    _callsToday = 1;
                }
                _lastCall = now;
            }
            return result;
        }

        public void Save()
        {
            XmlSerializer s = new XmlSerializer(typeof(StockQuoteThrottle));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            using (XmlWriter w = XmlWriter.Create(FileName, settings))
            {
                s.Serialize(w, this);
            }
        }

        private static StockQuoteThrottle Load()
        {
            if (System.IO.File.Exists(FileName))
            {
                XmlSerializer s = new XmlSerializer(typeof(StockQuoteThrottle));
                using (XmlReader r = XmlReader.Create(FileName))
                {
                    return (StockQuoteThrottle)s.Deserialize(r);
                }
            }
            return new StockQuoteThrottle();
        }

        public static StockQuoteThrottle Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }


        internal static string FileName
        {
            get
            {
                return System.IO.Path.Combine(ProcessHelper.AppDataPath, "throttle.xml");
            }
        }

    }

}
