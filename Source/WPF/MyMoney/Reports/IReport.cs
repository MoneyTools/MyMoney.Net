using System.Windows;
using System.Windows.Media;

namespace Walkabout.Interfaces.Reports
{
    public interface IReport
    {
        void Generate(IReportWriter writer);

        void Export(string filename);
    }

    public enum ReportInterval
    {
        Days, Months, Years
    }

    public interface IReportWriter
    {
        void WriteHeading(string heading);
        void WriteSubHeading(string subHeading);
        void StartTable();
        void StartColumnDefinitions();
        void WriteColumnDefinition(string width, double minWidth, double maxWidth);
        void EndColumnDefinitions();
        void StartHeaderRow();
        void StartFooterRow();
        void StartRow();
        void EndRow();
        void StartCell();
        void StartCell(int rowSpan, int colSpan);
        void WriteParagraph(string text);
        void WriteParagraph(string text, FontStyle style, FontWeight weight, Brush foreground);
        void WriteNumber(string number);
        void WriteNumber(string number, FontStyle style, FontWeight weight, Brush foreground);
        void EndCell();
        void EndTable();

        // this will add an expander to the beginning of the next row that can be used to 
        // expand/collapse all rows inside this group.
        void StartExpandableRowGroup();
        void EndExpandableRowGroup();

        /// <summary>
        ///  Plug in general UI element, only works for WPF based report writers
        /// </summary>
        void WriteElement(UIElement e);

        bool CanExpandCollapse { get; }
        void ExpandAll();
        void CollapseAll();
    }
}
