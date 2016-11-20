using System.Globalization;
using System.Text;
using System.Xml;
using System;

namespace Walkabout.Charts
{

    public class ChartSerializer
    {
        NumberFormatInfo info = NumberFormatInfo.InvariantInfo;
        XmlReader r;
        XmlWriter w;

        public static ChartData Read(string fileName)
        {
            ChartSerializer s = new ChartSerializer();
            ChartData data = new ChartData();
            if (System.IO.File.Exists(fileName))
            {
                using (XmlReader r = XmlReader.Create(fileName))
                {
                    s.r = r;
                    while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
                    {
                        switch (r.NodeType)
                        {
                            case XmlNodeType.Element:
                                if (r.LocalName == "ChartData")
                                {
                                    data = s.ReadChart();
                                }
                                break;
                        }
                    }
                }
            }
            return data;
        }

        ChartData ReadChart()
        {
            ChartData data = new ChartData();
            data.Title = r.GetAttribute("Title");
            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                switch (r.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (r.LocalName)
                        {
                            case "Category":
                                data.AddCategory(ReadCategory());
                                break;
                            case "Series":
                                data.AddSeries(ReadSeries());
                                break;
                        }
                        break;
                }
            }
            return data;
        }

        ChartCategory ReadCategory()
        {
            ChartCategory cat = new ChartCategory();
            cat.Name = r.GetAttribute("Name");
            cat.Color = Convert.ToInt32(r.GetAttribute("Color"));
            if (!r.IsEmptyElement)
            {
                while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
                {
                }
            }
            return cat;
        }


        ChartSeries ReadSeries()
        {
            ChartSeries series = new ChartSeries(r.GetAttribute("Title"), r.GetAttribute("Key"));

            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                switch (r.NodeType)
                {
                    case XmlNodeType.Element:
                        switch (r.LocalName)
                        {
                            case "Column":
                                ReadColumn(series);
                                break;
                        }
                        break;
                }
            }
            return series;
        }

        void ReadColumn(ChartSeries series)
        {
            string key = r.GetAttribute("Key");
            string label = r.GetAttribute("Label");
            double value = 0;
            if (r.MoveToAttribute("Value"))
            {
                value = r.ReadContentAsDouble();
                r.MoveToElement();
            }
            if (!r.IsEmptyElement)
            {
                while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
                {
                }
            }
            if (!string.IsNullOrEmpty(label))
            {
                if (key == null) key = label;
                series.AddColumn(key, label, value);
            }
        }

        //=========================== Save =============================================
        public static void Write(ChartData data, string fileName)
        {
            ChartSerializer s = new ChartSerializer();
            using (XmlTextWriter w = new XmlTextWriter(fileName, Encoding.UTF8))
            {
                s.w = w;
                w.Formatting = Formatting.Indented;
                w.WriteStartElement("ChartData");
                if (!string.IsNullOrEmpty(data.Title))
                    w.WriteAttributeString("Title", data.Title);
                foreach (ChartCategory c in data.Categories)
                {
                    s.WriteCategory(c);
                }
                foreach (ChartSeries series in data.AllSeries)
                {
                    s.WriteSeries(series);
                }
                w.WriteEndElement();
            }
        }

        void WriteCategory(ChartCategory c)
        {
            w.WriteStartElement("Category");
            w.WriteAttributeString("Name", c.Name);
            w.WriteAttributeString("Color", c.Color.ToString(info));
            w.WriteEndElement();
        }


        void WriteSeries(ChartSeries s)
        {
            w.WriteStartElement("Series");
            w.WriteAttributeString("Title", s.Title);
            if (s.Key != null) w.WriteAttributeString("Key", s.Key.ToString());
            foreach (ChartValue v in s.Values)
            {
                w.WriteStartElement("Column");
                if (v.UserData != null) w.WriteAttributeString("Key", v.UserData.ToString());
                w.WriteAttributeString("Label", v.Label);
                w.WriteStartAttribute("Value");
                w.WriteValue(v.Value);
                w.WriteEndAttribute();
                w.WriteEndElement();
            }
            w.WriteEndElement();
        }
    }

}
