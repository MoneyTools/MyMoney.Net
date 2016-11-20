using System;
using System.Collections.Generic;

namespace Walkabout.Charts
{

    public class ChartData
    {
        IList<ChartSeries> allSeries = new List<ChartSeries>();
        IDictionary<object, ChartSeries> index = new Dictionary<object, ChartSeries>();
        IList<ChartCategory> categories = new List<ChartCategory>();
        IDictionary<string, ChartCategory> catindex = new Dictionary<string, ChartCategory>();
        string title;

        public ChartData()
        {
        }

        public string Title { get { return title; } set { title = value; } }

        public ChartCategory AddCategory(string name, int color)
        {
            return AddCategory(new ChartCategory(name, color));
        }

        public ChartCategory AddCategory(ChartCategory cat)
        {
            categories.Add(cat);
            catindex[cat.Name] = cat;
            return cat;
        }

        public ChartCategory FindCategory(string name)
        {
            ChartCategory result = null;
            catindex.TryGetValue(name, out result);
            return result;
        }

        public IList<ChartSeries> AllSeries
        {
            get { return allSeries; }
        }

        public IList<ChartCategory> Categories
        {
            get { return categories; }
        }

        public ChartSeries AddSeries(string title, object key)
        {
            return AddSeries(new ChartSeries(title, key));
        }

        public ChartSeries AddSeries(ChartSeries s)
        {
            return InsertSeries(allSeries.Count, s);
        }

        public ChartSeries InsertSeries(ChartSeries s)
        {
            return InsertSeries(0, s);
        }

        public ChartSeries InsertSeries(int i, ChartSeries s)
        {
            if (i >= allSeries.Count)
            {
                allSeries.Add(s);
            }
            else
            {
                allSeries.Insert(i, s);
            }
            object key = s.Key;
            if (key == null) key = s.Title;
            index[key] = s;
            return s;
        }

        public ChartSeries GetSeries(object key)
        {
            if (!index.ContainsKey(key)) return null;
            return index[key];
        }
    }

    public class ChartSeries
    {
        IList<ChartValue> values = new List<ChartValue>();
        string title;
        double min;
        double max;
        bool dirty;
        double total;
        object key;
        ChartCategory cat;
        Dictionary<object, ChartValue> index = new Dictionary<object, ChartValue>();

        public event EventHandler Changed;

        public ChartSeries()
        {
        }

        public ChartSeries(string title, object key)
        {
            this.title = title;
            this.key = key;
        }

        public ChartCategory Category
        {
            get { return this.cat; }
            set { this.cat = value; }
        }

        public string Title { get { return title; } set { this.title = value; } }
        public object Key { get { return key; } }

        public double Total { get { return total; } }

        public IList<ChartValue> Values
        {
            get { return values; }
        }

        public object Tag { get; set; }

        public void AddColumn(object key, string label)
        {
            if (!index.ContainsKey(key))
            {
                AddColumn(key, label, 0);
            }
        }

        public void AddColumn(object key, string label, double value)
        {
            ChartValue cv = null;
            if (!index.ContainsKey(key))
            {
                cv = new ChartValue(label, value, key);
                values.Add(cv);
                index[key] = cv;
            }
            else
            {
                cv = index[key];
                cv.Value += value;
            }
            total += value;
            this.dirty = true;
        }

        public bool HasColumn(object key)
        {
            return index.ContainsKey(key);
        }

        public void IncrementValue(object key, double v)
        {
            total += v;
            ChartValue cv = index[key];
            cv.Value += v;
            this.dirty = true;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
        public void BeginUpdate()
        {
        }

        public void EndUpdate()
        {
            this.dirty = true;
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public double Minimum
        {
            get
            {
                if (dirty) Recalc();
                return min;
            }
        }
        public double Maximum
        {
            get
            {
                if (dirty) Recalc();
                return max;
            }
        }
        public double Range
        {
            get
            {
                if (dirty) Recalc();
                return Math.Max(Math.Abs(max), Math.Abs(min));
            }
        }

        public bool Flipped { get; set; }

        void Recalc()
        {
            min = max = total = 0;
            bool first = true;
            foreach (ChartValue cv in values)
            {
                double v = cv.Value;
                total += v;
                if (first)
                {
                    min = max = v;
                }
                else
                {
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                first = false;
            }
            dirty = false;
        }

    }

    public class ChartValue
    {
        double value;
        string label;
        object userdata;

        public ChartValue()
        {
        }

        public ChartValue(string label, double value, object userdata)
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

