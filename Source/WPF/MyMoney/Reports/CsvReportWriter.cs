using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Interfaces.Reports;

namespace Walkabout.Reports
{
    internal class CsvReportWriter : IReportWriter
    {
        private StreamWriter writer;
        int columnSpan = 0;
        bool tableOpen;
        bool newCell;
        bool cellText = false;

        public CsvReportWriter(StreamWriter writer)
        {
            this.writer = writer;
        }

        public bool CanExpandCollapse => false;

        public void CollapseAll()
        {
        }

        public void EndCell()
        {
            if (this.cellText)
            {
                writer.Write("\"");
            }
            writer.Write(",");
            while (this.columnSpan > 1)
            {
                writer.Write(",");
                this.columnSpan--;
            }
            this.cellText = false;
        }

        public void EndColumnDefinitions()
        {
        }

        public void EndExpandableRowGroup()
        {
            this.EndRow();
        }

        public void EndFooterRow()
        {
            this.EndRow();
        }

        public void EndHeaderRow()
        {
            this.EndRow();
        }

        public void EndRow()
        {
            writer.WriteLine();
            this.columnSpan = 0;
        }

        public void EndTable()
        {
            this.newCell = false;
            writer.WriteLine();
            this.columnSpan = 0;
            this.tableOpen = false;
            this.cellText = false;
        }

        public void ExpandAll()
        {
        }

        public void StartCell()
        {
            this.newCell = true;
            this.cellText = false;
        }

        public void StartCell(int rowSpan, int colSpan)
        {
            this.StartCell();
            this.columnSpan = colSpan;
        }

        public void StartColumnDefinitions()
        {
        }

        public void StartExpandableRowGroup()
        {
        }

        public void StartFooterRow()
        {
        }

        public void StartHeaderRow()
        {
        }

        public void StartRow()
        {
        }

        public void StartTable()
        {
            this.newCell = false;
            this.cellText = false;
            this.tableOpen = true;
        }

        public void WriteColumnDefinition(string width, double minWidth, double maxWidth)
        {
        }

        public void WriteElement(UIElement e)
        {
        }

        private string CsvSafeString(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            // everything is going inside double quotes, so we have to protect any existing double quotes
            // which is done by replacing them with 2 quotes like this "".
            s = s.Replace("\"", "\"\"");
            return s;
        }

        public void WriteHeading(string heading)
        {
            if (this.newCell)
            {
                writer.Write("\"");
                this.cellText = true;
            }
            writer.Write(this.CsvSafeString(heading));
            if (!this.tableOpen) writer.WriteLine(",");
        }

        public void WriteHyperlink(string text, FontStyle style, FontWeight weight, MouseButtonEventHandler clickHandler)
        {
            if (this.newCell)
            {
                writer.Write("\"");
                this.cellText = true;
            }
            writer.Write(this.CsvSafeString(text));
            if (!this.tableOpen) writer.WriteLine(",");
        }

        public void WriteNumber(string number)
        {
            if (this.newCell)
            {
                writer.Write("\"");
                this.cellText = true;
            }
            writer.Write(this.CsvSafeString(number));
            if (!this.tableOpen) writer.WriteLine(",");
        }

        public void WriteNumber(string number, FontStyle style, FontWeight weight, Brush foreground)
        {
            if (this.newCell)
            {
                writer.Write("\"");
                this.cellText = true;
            }
            writer.Write(this.CsvSafeString(number));
            if (!this.tableOpen) writer.WriteLine(",");
        }

        public void WriteParagraph(string text)
        {
            if (this.newCell)
            {
                writer.Write("\"");
                this.cellText = true;
            }
            writer.Write(this.CsvSafeString(text));
            if (!this.tableOpen) writer.WriteLine(",");
        }

        public void WriteParagraph(string text, FontStyle style, FontWeight weight, Brush foreground)
        {
            if (this.newCell)
            {
                writer.Write("\"");
                this.cellText = true;
            }
            writer.Write(this.CsvSafeString(text));
            if (!this.tableOpen) writer.WriteLine(",");
        }

        public void WriteSubHeading(string subHeading)
        {
            if (this.newCell)
            {
                writer.Write("\"");
                this.cellText = true;
            }
            writer.Write(this.CsvSafeString(subHeading));
            if (!this.tableOpen) writer.WriteLine(",");
        }
    }
}
