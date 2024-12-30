#define ALL
#define XAML

using Microsoft.Win32;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Utilities;

namespace Walkabout.Reports
{
    public abstract class Report : IReport
    {
        private Currency currency;
        private CultureInfo currencyCulture;
        private IServiceProvider serviceProvider;
        private bool disposedValue;

        public IServiceProvider ServiceProvider
        {
            get => serviceProvider;
            set
            {
                serviceProvider = value;
                this.OnSiteChanged();
            }
        }

        public virtual void OnSiteChanged() { }

        public abstract IReportState GetState();

        public abstract void ApplyState(IReportState state);

        public abstract Task Generate(IReportWriter writer);

        public virtual void OnMouseLeftButtonClick(object sender, MouseButtonEventArgs e)
        {
        }

        public virtual void Export(string filename)
        {
            throw new NotImplementedException();
        }

        public void AddInline(Paragraph p, UIElement childUIElement)
        {
            var inline = new InlineUIContainer(childUIElement);
            inline.BaselineAlignment = BaselineAlignment.Bottom;
            p.Inlines.Add(inline);
        }

        protected void ExportReportAsCsv()
        {
            SaveFileDialog fd = new SaveFileDialog();
            fd.CheckPathExists = true;
            fd.AddExtension = true;
            fd.Filter = "CSV File (.csv)|*.csv";
            fd.FileName = this.GetType().Name;

            if (fd.ShowDialog(App.Current.MainWindow) == true)
            {
                try
                {
                    string filename = fd.FileName;
                    this.Export(filename);
                    if (System.IO.File.Exists(filename))
                    {
                        InternetExplorer.OpenUrl(IntPtr.Zero, filename);
                    }
                }
                catch (Exception ex)
                {
                    MessageBoxEx.Show(ex.Message, "Error Exporting .txf", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public Button CreateReportButton(string relativeIconPath, string caption, string toolTip)
        {
            Button button = new Button();
            button.ToolTip = toolTip;

            StackPanel panel = new StackPanel()
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(2)
            };

            Image img = new Image();
            BitmapFrame frame = BitmapFrame.Create(new Uri("pack://application:,,,/MyMoney;component/" + relativeIconPath));
            img.Source = frame;
            img.Width = frame.Width;
            img.Height = frame.Height;
            panel.Children.Add(img);

            panel.Children.Add(new TextBlock()
            {
                Text = caption,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });

            button.Content = panel;
            return button;
        }

        public Currency DefaultCurrency
        {
            get => this.currency;
            set
            {
                this.currency = value;
                if (value != null)
                {
                    this.currencyCulture = Currency.GetCultureForCurrency(value.Symbol);
                }
            }
        }

        public CultureInfo CurrencyCulture => this.currencyCulture;

        public decimal GetNormalizedAmount(decimal amount)
        {
            if (this.currency != null)
            {
                var ratio = this.currency.Ratio;
                if (ratio == 0)
                {
                    ratio = 1;
                }
                amount /= ratio;
            }
            return amount;
        }

        public string GetFormattedNormalizedAmount(decimal amount, int decimalPlace = 2)
        {
            amount = this.GetNormalizedAmount(amount);
            return StringHelpers.GetFormattedAmount(amount, this.currencyCulture, decimalPlace);
        }

        protected void WriteTrailer(IReportWriter writer, DateTime reportDate)
        {
            if (this.DefaultCurrency != null && this.DefaultCurrency.Ratio != 1)
            {
                var amount = string.Format(this.CurrencyCulture, "{0:C}", 1 / this.DefaultCurrency.Ratio);
                var ri = new RegionInfo(this.CurrencyCulture.Name);
                writer.WriteParagraph("Conversion $1 USD is " + amount + " in " + ri.CurrencyEnglishName,
                    FontStyles.Italic, FontWeights.Normal, Brushes.Gray);
            }
            writer.WriteParagraph("Generated for " + reportDate.ToLongDateString(),
                FontStyles.Italic, FontWeights.Normal, Brushes.Gray);
        }

        public void WriteCurrencyHeading(IReportWriter writer, Currency currency)
        {
            if (currency != null)
            {
                writer.WriteHeading("Currency " + currency.Symbol);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            this.Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
