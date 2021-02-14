using System;
using Xamarin.Forms;

namespace XMoney
{

    public class Settings : BaseViewModel
    {
        //This is known as early binding or initialization  
        private static readonly Settings _instance = new();

        //Constructor is marked as private   
        //so that the instance cannot be created   
        //from outside of the class  
        private Settings()
        {
        }

        //Static method which allows the instance creation  
        internal static Settings Get()
        {
            //All you need to do it is just return the  
            //already initialized which is thread safe  
            return _instance;
        }

        //override public event PropertyChangedEventHandler PropertyChanged;


        public static void SetValue(string key, string value)
        {
            Application.Current.Properties[key] = value;
            Application.Current.SavePropertiesAsync();
        }


        public static void SetValue(string key, int value)
        {
            Application.Current.Properties[key] = value;
            Application.Current.SavePropertiesAsync();
        }

        public static void SetValue(string key, double value)
        {
            Application.Current.Properties[key] = value;
            Application.Current.SavePropertiesAsync();
        }


        public static int GetValue(string key, int valueIfNotFound)
        {
            if (Application.Current.Properties.ContainsKey(key))
            {
                object v = Application.Current.Properties[key];
                return (int)v;
            }
            return valueIfNotFound;
        }


        public double GetValueAsDouble(string key, double valueIfNotFound)
        {
            if (Application.Current.Properties.ContainsKey(key))
            {
                double v = Convert.ToDouble(Application.Current.Properties[key]);
                return double.IsNaN(v) ? valueIfNotFound : v;
            }
            return valueIfNotFound;
        }


        public static string GetValueAsString(string key, string valueIfNotFound)
        {
            if (Application.Current.Properties.ContainsKey(key))
            {
                string v = Application.Current.Properties[key] as string;
                if (!string.IsNullOrEmpty(v))
                {
                    return v;
                }
            }
            return valueIfNotFound;
        }

        public string GetValue(string key)
        {
            if (Application.Current.Properties.ContainsKey(key))
            {
                object v = Application.Current.Properties[key];
                return v as string;
            }
            return "";
        }


        public int GetValueOrZero(string key)
        {
            return GetValue(key, 0);
        }


        public double GetValueOrZeroAsDouble(string key)
        {
            return GetValueAsDouble(key, 0.0);
        }


        //
        //
        //  Hard Definition of Settings values
        //
        //
        public static string SourceDatabase
        {
            get
            {
                string defaultFirstTime = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return GetValueAsString("sourceDatabase", defaultFirstTime);
            }

            set
            {
                SetValue("sourceDatabase", value);
            }
        }

        public enum SortOrder
        {
            SortByPercentage,
            SortByAmount,
            SortByMarketCap,
            SortByPE,
            SortByVolume,
            SortByAlphabet
        };

        public SortOrder SortBy
        {
            get
            {
                return (SortOrder)GetValue("sortBy", (int)SortOrder.SortByPercentage);
            }

            set
            {
                SetValue("sortBy", (int)value);
                OnPropertyChanged("sortBy");
            }
        }


        public int RefreshRate
        {
            get
            {
                int v = GetValue("refreshRate", 4);

                if (v < 1)
                {
                    v = 1;
                }

                // Debug.WriteLine("GET REFRESH @ " + v.ToString());
                return v;
            }

            set
            {
                SetValue("refreshRate", value);
                OnPropertyChanged("refreshRate");

                // Debug.WriteLine("SET REFRESH IS NOW " + RefreshRate.ToString());
            }
        }

        public bool ShowClodedAccounts
        {
            get
            {
                int v = GetValue("ShowClodedAccounts", 1);  // Yes by default

                return v != 0;
            }

            set
            {
                SetValue("ShowClodedAccounts", value ? 1 : 0);
                OnPropertyChanged("ShowClodedAccounts");
            }
        }

        public bool ShowLoanProjection
        {
            get
            {
                int v = GetValue("ShowLoanProjection", 1);  // Yes by default

                return v != 0;
            }

            set
            {
                SetValue("ShowLoanProjection", value ? 1 : 0);
                OnPropertyChanged("ShowLoanProjection");
            }
        }

        public bool ManageRentalProperties
        {
            get
            {
                int v = GetValue("ManageRentalProperties", 0); // Off by default

                return v != 0;
            }

            set
            {
                SetValue("ManageRentalProperties", value ? 1 : 0);
                OnPropertyChanged("ManageRentalProperties");
            }
        }
    }

}
