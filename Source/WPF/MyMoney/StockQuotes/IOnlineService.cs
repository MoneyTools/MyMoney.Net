using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Walkabout.StockQuotes
{
    public class IOnlineService
    {
        public static List<OnlineServiceSettings> GetDefaultSettingsList()
        {
            List<OnlineServiceSettings> result = new List<OnlineServiceSettings>();
            result.Add(TwelveData.GetDefaultSettings());
            result.Add(YahooFinance.GetDefaultSettings());
            result.Add(ExchangeRateService.GetDefaultSettings());
            return result;
        }

    }


    /// <summary>
    /// </summary>
    public class OnlineServiceSettings : INotifyPropertyChanged
    {
        private string _name;
        private string _address;
        private string _apiKey;
        private string _type;
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

        public string ServiceType
        {
            get { return this._type; }
            set
            {
                if (this._type != value)
                {
                    this._type = value;
                    this.OnPropertyChanged("ServiceType");
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
