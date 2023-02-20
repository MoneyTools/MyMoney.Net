#define ALL
#define XAML

using Microsoft.Win32;
using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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

        public Report(Currency defaultCurrency)
        {
            this.DefaultCurrency = defaultCurrency;
        }

        public abstract Task Generate(IReportWriter writer);

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

        public string GetFormattedAmount(decimal amount, int decimalPlace = 2)
        {
            return string.Format(this.currencyCulture, "{0:C" + decimalPlace.ToString() + "}", amount);
        }

        public string GetFormattedNormalizedAmount(decimal amount, int decimalPlace = 2)
        {
            amount /= this.currency.Ratio;
            return this.GetFormattedAmount(amount, decimalPlace);
        }

        protected void WriteTrailer(IReportWriter writer, DateTime reportDate)
        {
            if (this.DefaultCurrency.Ratio != 1)
            {
                var amount = string.Format(this.CurrencyCulture, "{0:C}", 1 / this.DefaultCurrency.Ratio);
                var ri = new RegionInfo(this.CurrencyCulture.Name);
                writer.WriteParagraph("Conversion $1 USD is " + amount + " in " + ri.CurrencyEnglishName,
                    FontStyles.Italic, FontWeights.Normal, Brushes.Gray);
            }
            writer.WriteParagraph("Generated for " + reportDate.ToLongDateString(),
                FontStyles.Italic, FontWeights.Normal, Brushes.Gray);
        }

    }

}
