using System.ComponentModel;
using System.Windows.Media;
using Walkabout.Utilities;
using Walkabout.Data;
using System.Collections.Generic;

namespace Walkabout.Charts
{
    /// <summary>
    /// The view model for the pie charts
    /// </summary>
    public class CategoryData : INotifyPropertyChanged
    {
        string name;
        double total;
        Color? color;
        Brush background;
        List<Transaction> transactions = new List<Transaction>();

        public CategoryData(Category c)
        {
            this.Category = c;
            this.name = c.Name;
        }

        public Category Category { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1002:DoNotExposeGenericLists")]
        public List<Transaction> Transactions { get { return transactions; } }

        public string Name
        {
            get { return this.name; }
        }

        public string FormattedValue
        {
            get { return Total.ToString("C2"); }
        }

        public Brush Background
        {
            get
            {
                if (background == null)
                {
                    background = ColorAndBrushGenerator.CreateLinearBrushFromSolidColor(this.Color, 45);
                }
                return background;
            }
            set
            {
                background = value;
            }
        }

        static bool IsSpecialCategory(string name)
        {
            return (name == "Unknown" || name == "Xfer from Deleted Account" || name == "Xfer to Deleted Account");
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
                    this.color = GetColorFromCategoryName(name);
                }
                return color?? Colors.Transparent;
            }
            set
            {
                this.color = value;

                // Reset the color
                background = ColorAndBrushGenerator.CreateLinearBrushFromSolidColor(value, 45);
            }

        }


        public double Total
        {
            get { return total; }
            set
            {
                total = value;
                OnPropertyChanged("Total");
            }
        }

        // INotifyPropertyChanged event
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
