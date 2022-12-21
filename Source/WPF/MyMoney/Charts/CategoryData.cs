using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Charts
{
    /// <summary>
    /// The view model for the pie charts
    /// </summary>
    public class CategoryData : INotifyPropertyChanged
    {
        private string name;
        private double total;
        private Color? color;
        private Brush background;
        private List<Transaction> transactions = new List<Transaction>();

        public CategoryData(Category c)
        {
            this.Category = c;
            this.name = c.Name;
        }

        public Category Category { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
        public List<Transaction> Transactions { get { return this.transactions; } }

        public string Name
        {
            get { return this.name; }
        }

        public string FormattedValue
        {
            get { return this.Total.ToString("C2"); }
        }

        public Brush Background
        {
            get
            {
                if (this.background == null)
                {
                    this.background = ColorAndBrushGenerator.CreateLinearBrushFromSolidColor(this.Color, 45);
                }
                return this.background;
            }
            set
            {
                this.background = value;
            }
        }

        private static bool IsSpecialCategory(string name)
        {
            return name == "Unknown" || name == "Xfer from Deleted Account" || name == "Xfer to Deleted Account";
        }

        public static Color GetColorFromCategoryName(string name)
        {
            Color color;

            if (name == "Transfer")
            {
                return Colors.Transparent;
            }
            else if (IsSpecialCategory(name))
            {
                // Special cases were we want these in Black
                color = Colors.Black;
            }
            else
            {
                color = ColorAndBrushGenerator.GenerateNamedColor(name);
            }
            return color;
        }

        public Color Color
        {
            get
            {
                if (this.color == null)
                {
                    this.color = GetColorFromCategoryName(this.name);
                }
                return this.color ?? Colors.Transparent;
            }
            set
            {
                this.color = value;

                // Reset the color
                this.background = ColorAndBrushGenerator.CreateLinearBrushFromSolidColor(value, 45);
            }

        }


        public double Total
        {
            get { return this.total; }
            set
            {
                this.total = value;
                this.OnPropertyChanged("Total");
            }
        }

        // INotifyPropertyChanged event
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
