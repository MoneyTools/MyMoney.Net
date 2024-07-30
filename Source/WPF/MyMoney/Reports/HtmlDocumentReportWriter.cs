using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;
using Walkabout.Interfaces.Reports;

namespace Walkabout.Reports
{
    internal class HtmlDocumentReportWriter : IReportWriter
    {
        private XmlWriter writer;
        private int depth;

        public bool CanExpandCollapse => false;

        public HtmlDocumentReportWriter(XmlWriter writer)
        {
            this.writer = writer;
            writer.WriteStartElement("html");
            writer.WriteStartElement("head");
            writer.WriteStartElement("link");
            writer.WriteAttributeString("rel", "stylesheet");
            writer.WriteAttributeString("href", "https://maxcdn.bootstrapcdn.com/bootstrap/3.4.1/css/bootstrap.min.css");
            writer.WriteEndElement(); // link
            writer.WriteEndElement(); // head
            writer.WriteStartElement("body");
            writer.WriteStartElement("div");
            writer.WriteAttributeString("class", "container");
        }

        private void IncrementDepth()
        {
            depth++;
        }

        private void DecrementDepth()
        {
            depth--;
            if (depth < 0)
            {
                throw new Exception("You closed too many tags");
            }
        }

        public void Close()
        {
            this.writer.WriteEndElement(); // div
            this.writer.WriteEndElement(); // body
            this.writer.WriteEndElement(); // html
        }

        public void EndCell()
        {
            this.DecrementDepth();
            this.writer.WriteEndElement();
        }

        public void EndColumnDefinitions()
        {
        }

        public void EndExpandableRowGroup()
        {
        }

        public void EndRow()
        {
            this.DecrementDepth();
            this.writer.WriteEndElement();
        }

        public void EndTable()
        {
            this.DecrementDepth();
            this.writer.WriteEndElement();
        }

        public void StartCell()
        {
            this.IncrementDepth();
            this.writer.WriteStartElement(thead > 0 ? "th" : "td");
        }

        public void StartCell(int rowSpan, int colSpan)
        {
            this.IncrementDepth();
            this.writer.WriteStartElement(thead > 0 ? "th" : "td");
            this.writer.WriteAttributeString("rowspan", rowSpan.ToString());
            this.writer.WriteAttributeString("colspan", colSpan.ToString());
        }

        public void StartColumnDefinitions()
        {
        }

        public void StartExpandableRowGroup()
        {
        }

        public void StartFooterRow()
        {
            this.IncrementDepth();
            this.writer.WriteStartElement("tfoot");
            this.writer.WriteStartElement("tr");
        }

        public void EndFooterRow()
        {
            this.DecrementDepth();
            this.writer.WriteEndElement();
            this.writer.WriteEndElement();
        }

        int thead = 0;

        public void StartHeaderRow()
        {
            this.IncrementDepth();
            thead++;
            this.writer.WriteStartElement("thead");
            this.writer.WriteStartElement("tr");
        }

        public void EndHeaderRow()
        {
            this.DecrementDepth();
            thead--;
            this.writer.WriteEndElement();
            this.writer.WriteEndElement();
        }

        public void StartRow()
        {
            this.IncrementDepth();
            this.writer.WriteStartElement("tr");
        }

        public void StartTable()
        {
            this.IncrementDepth();
            this.writer.WriteStartElement("table");
            writer.WriteAttributeString("class", "table table-striped");
        }

        public void WriteColumnDefinition(string width, double minWidth, double maxWidth)
        {
        }

        public void WriteElement(UIElement e)
        {
        }

        public void WriteHeading(string heading)
        {
            this.writer.WriteStartElement("h1");
            this.writer.WriteString(heading);
            this.writer.WriteEndElement();
        }

        public void WriteSubHeading(string subHeading)
        {
            this.writer.WriteStartElement("h2");
            this.writer.WriteString(subHeading);
            this.writer.WriteEndElement();
        }

        public void WriteHyperlink(string text, FontStyle style, FontWeight weight, MouseButtonEventHandler clickHandler)
        {
            // not supported since there is no where to link to.
            this.writer.WriteStartElement("span");
            this.writer.WriteAttributeString("style", this.GetCssStyles(style, weight, Brushes.Black));
            this.writer.WriteString(text);
            this.writer.WriteEndElement();
        }

        public void WriteNumber(string number)
        {
            this.writer.WriteString(number);
        }

        public void WriteNumber(string number, FontStyle style, FontWeight weight, Brush foreground)
        {
            this.writer.WriteStartElement("span");
            this.writer.WriteAttributeString("style", this.GetCssStyles(style, weight, foreground));
            this.writer.WriteString(number);
            this.writer.WriteEndElement();
        }

        public void WriteParagraph(string text)
        {
            this.writer.WriteStartElement("p");
            this.writer.WriteString(text);
            this.writer.WriteEndElement();
        }

        public void WriteParagraph(string text, FontStyle style, FontWeight weight, Brush foreground)
        {
            this.writer.WriteStartElement("p");
            this.writer.WriteAttributeString("style", this.GetCssStyles(style, weight, foreground));
            this.writer.WriteString(text);
            this.writer.WriteEndElement();
        }

        private string GetCssStyles(FontStyle style, FontWeight weight, Brush foreground)
        {
            string css = "";
            var cssColor = this.GetCssColor(foreground);
            if (!string.IsNullOrEmpty(cssColor))
            {
                css += $"color: {cssColor};";
            }
            var cssStyle = this.GetCssFontStyle(style);
            if (!string.IsNullOrEmpty(cssStyle))
            {
                css += $"font-style: {cssStyle};";
            }
            var cssWeight = this.GetCssFontWeight(weight);
            if (!string.IsNullOrEmpty(cssWeight))
            {
                css += $"font-weight: {cssWeight};";
            }
            return css;
        }

        private string GetCssColor(Brush brush)
        {
            if (brush is SolidColorBrush s)
            {
                var c = s.Color;
                return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
            }
            return null;
        }

        private string GetCssFontStyle(FontStyle style)
        {
            if (style == FontStyles.Oblique || style == FontStyles.Italic)
            {
                return "italic";
            }
            return null;
        }

        private string GetCssFontWeight(FontWeight weight)
        {
            if (weight == FontWeights.Light || weight == FontWeights.Thin || weight == FontWeights.UltraLight || weight == FontWeights.ExtraLight)
            {
                return "lighter";
            }
            else if (weight == FontWeights.SemiBold || weight == FontWeights.Bold || weight == FontWeights.Heavy)
            {
                return "bold";
            }
            else if (weight == FontWeights.ExtraBold || weight == FontWeights.UltraBold)
            {
                return "bolder";
            }
            return null;
        }

        public void CollapseAll()
        {
        }

        public void ExpandAll()
        {
        }
    }
}
