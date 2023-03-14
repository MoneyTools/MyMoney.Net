using System.ComponentModel;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Configuration;

namespace Walkabout.Data
{
    /// <summary>
    /// This class stores settings that are specific to a particular database instance,
    /// for example, what the default currency is, whether you are using the Rental module,
    /// and when does the fiscal year start.  More general "user" settings that apply to
    /// all database instances are stored in Settings.cs.
    /// </summary>
    public class DatabaseSettings : INotifyPropertyChanged
    {
        private string fileName;
        private int? fiscalYearStart;
        private string displayCurrency = "USD";
        private bool? rentalManagement;
        private bool showCurrency = true;

        // Needed by XmlSerializer
        public DatabaseSettings() { }

        private static string GetSettingsFileName(IDatabase database)
        {
            var path = database.DatabasePath;
            return Path.Combine(Path.GetDirectoryName(path), Path.GetFileName(path) + ".settings");
        }

        internal static DatabaseSettings LoadFrom(IDatabase database)
        {
            var result = new DatabaseSettings();
            var settingsPath = GetSettingsFileName(database);
            if (File.Exists(settingsPath))
            {
                using (var r = XmlReader.Create(settingsPath))
                {
                    var s = new XmlSerializer(typeof(DatabaseSettings));
                    result = (DatabaseSettings)s.Deserialize(r);
                }
            }
            result.fileName = settingsPath;
            return result;
        }

        public string SettingsFileName => this.fileName;

        public void Save()
        {
            if (!string.IsNullOrEmpty(this.fileName))
            {
                var writerSettings = new XmlWriterSettings() { Indent = true };
                using (var r = XmlWriter.Create(this.fileName, writerSettings))
                {
                    var s = new XmlSerializer(typeof(DatabaseSettings));
                    s.Serialize(r, this);
                }
            }
        }

        public void RaiseAllEvents()
        {
            this.OnPropertyChanged(nameof(this.FiscalYearStart));
            this.OnPropertyChanged(nameof(this.RentalManagement));
            this.OnPropertyChanged(nameof(this.DisplayCurrency));
            this.OnPropertyChanged(nameof(this.ShowCurrency));
        }

        public int FiscalYearStart
        {
            get => this.fiscalYearStart.HasValue ? this.fiscalYearStart.Value : 0;
            set
            {
                if (this.fiscalYearStart != value)
                {
                    this.fiscalYearStart = value;
                    this.OnPropertyChanged(nameof(this.FiscalYearStart));
                }
            }
        }

        public string DisplayCurrency
        {
            get => this.displayCurrency;
            set
            {
                if (this.displayCurrency != value)
                {
                    this.displayCurrency = value;
                    this.OnPropertyChanged(nameof(this.DisplayCurrency));
                }
            }
        }

        public bool ShowCurrency
        {
            get => this.showCurrency;
            set
            {
                if (this.showCurrency != value)
                {
                    this.showCurrency = value;
                    this.OnPropertyChanged(nameof(this.ShowCurrency));
                }
            }
        }
        public bool RentalManagement
        {
            get => this.rentalManagement.HasValue ? this.rentalManagement.Value : false;
            set
            {
                if (this.rentalManagement != value)
                {
                    this.rentalManagement = value;
                    this.OnPropertyChanged(nameof(this.RentalManagement));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal bool MigrateSettings(Settings settings)
        {
            bool changed = false;
            if (!this.fiscalYearStart.HasValue)
            {
                changed = true;
                this.fiscalYearStart = settings.MigrateFiscalYearStart();
            }
            if (!this.rentalManagement.HasValue)
            {
                changed = true;
                this.rentalManagement = settings.MigrateRentalManagement();
            }
            return changed;
        }
    }
}
