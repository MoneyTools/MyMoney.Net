using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using Walkabout.Utilities;

namespace Walkabout.Charts
{

    public class ChartData
    {
        private readonly IList<ChartDataSeries> allSeries = new List<ChartDataSeries>();

        public ChartData()
        {
        }

        public string Title { get; set; }

        public IList<ChartDataSeries> Series
        {
            get { return this.allSeries; }
        }

        public ChartDataSeries AddSeries(ChartDataSeries s)
        {
            return this.InsertSeries(this.allSeries.Count, s);
        }

        public ChartDataSeries InsertSeries(int i, ChartDataSeries s)
        {
            if (i >= this.allSeries.Count)
            {
                this.allSeries.Add(s);
            }
            else
            {
                this.allSeries.Insert(i, s);
            }
            return s;
        }

        internal void Export()
        {
            if (this.Series != null && this.Series.Count > 0)
            {
                string name = System.IO.Path.GetTempFileName() + ".csv";
                TempFilesManager.AddTempFile(name);
                using (StreamWriter writer = new StreamWriter(name))
                {
                    writer.Write("Label");
                    foreach (var series in this.Series)
                    {
                        writer.Write(", {0}", series.Name);
                    }
                    writer.WriteLine();

                    int columns = this.Series[0].Values.Count;
                    for (int i = 0; i < columns; i++)
                    {
                        for (int j = 0, n = this.Series.Count; j < n; j++)
                        {
                            var s = this.Series[j];
                            var item = s.Values[i];
                            if (j == 0)
                            {
                                writer.Write("{0}, ", item.Label);
                            }
                            else
                            {
                                writer.Write(", ");
                            }
                            writer.Write("{0}", item.Value);
                        }
                        writer.WriteLine();
                    }
                }

                NativeMethods.ShellExecute(IntPtr.Zero, "Open", name, "", "", NativeMethods.SW_SHOWNORMAL);
            }
        }
    }

    public class ChartDataSeries
    {
        public ChartDataSeries()
        {
            this.Values = new List<ChartDataValue>();
        }

        public ChartDataSeries(string name)
        {
            this.Name = name;
            this.Values = new List<ChartDataValue>();
        }

        public string Name { get; set; }

        public IList<ChartDataValue> Values { get; set; }

        public ChartCategory Category { get; set; }

        public object UserData { get; set; }

        public bool Flipped { get; set; }

    }

    public class ChartDataValue
    {
        private double value;
        private string label;
        private object userdata;
        private Color? color;
        private bool hidden; // for filtering out values.

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

        public bool Hidden
        {
            get { return this.hidden; }
            set { this.hidden = value; }
        }

        public object UserData
        {
            get { return this.userdata; }
            set { this.userdata = value; }
        }

    }

    public class ChartCategory
    {
        private int color;
        private object wpfcolor;
        private string name;

        public ChartCategory()
        {
        }

        public ChartCategory(string name, int color)
        {
            this.name = name;
            this.color = color;
        }

        public string Name { get { return this.name; } set { this.name = value; } }

        public int Color { get { return this.color; } set { this.color = value; } }


        public object WpfColor { get { return this.wpfcolor; } set { this.wpfcolor = value; } }
    }

}

