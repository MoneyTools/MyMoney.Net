using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace Walkabout.Charts
{

    public class ChartData
    {
        IList<ChartDataSeries> allSeries = new List<ChartDataSeries>();

        public ChartData()
        {
        }

        public string Title { get; set; }

        public IList<ChartDataSeries> Series
        {
            get { return allSeries; }
        }

        public ChartDataSeries AddSeries(ChartDataSeries s)
        {
            return InsertSeries(allSeries.Count, s);
        }

        public ChartDataSeries InsertSeries(int i, ChartDataSeries s)
        {
            if (i >= allSeries.Count)
            {
                allSeries.Add(s);
            }
            else
            {
                allSeries.Insert(i, s);
            }
            return s;
        }
    }

    public class ChartDataSeries
    {
        public ChartDataSeries()
        {
            Values = new List<ChartDataValue>();
        }

        public ChartDataSeries(string name)
        {
            this.Name = name;
            Values = new List<ChartDataValue>();
        }

        public string Name { get; set; }

        public IList<ChartDataValue> Values { get; set; }

        public ChartCategory Category { get; set; }

        public object UserData { get; set; }

        public bool Flipped { get; set; }

    }

    public class ChartDataValue
    {
        double value;
        string label;
        object userdata;
        Color? color;

        public ChartDataValue()
        {
        }

        public ChartDataValue(string label, double value, object userdata)
        {
            this.label = label;
            this.value = value;
            this.userdata = userdata;
        }

        public double Value
        {
            get { return this.value; }
            set { this.value = value; }
        }

        public string Label
        {
            get { return this.label; }
            set { this.label = value; }
        }

        public Color? Color
        {
            get { return this.color; }
            set { this.color = value; }
        }

        public object UserData
        {
            get { return this.userdata; }
            set { this.userdata = value; }
        }

    }

    public class ChartCategory
    {
        int color;
        object wpfcolor;
        string name;

        public ChartCategory()
        {
        }

        public ChartCategory(string name, int color)
        {
            this.name = name;
            this.color = color;
        }

        public string Name { get { return name; } set { this.name = value; } }

        public int Color { get { return color; } set { this.color = value; } }


        public object WpfColor { get { return wpfcolor; } set { this.wpfcolor = value; } }
    }

}

