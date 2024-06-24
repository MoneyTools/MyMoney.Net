using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Views;

namespace Walkabout.Interfaces.Reports
{
    public interface IReport
    {
        IServiceProvider ServiceProvider { get; set; }

        Task Generate(IReportWriter writer);

        void Export(string filename);

        IReportState GetState();

        void ApplyState(IReportState state);

        void OnMouseLeftButtonClick(object sender, MouseButtonEventArgs e);
    }

    public interface IReportState
    {
        Type GetReportType();
    }

    public class SimpleReportState : IReportState
    {
        Type type;

        public SimpleReportState(Type reportType)
        {
            this.type = reportType;
        }

        public virtual Type GetReportType()
        {
            return this.type;
        }
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
        void WriteHyperlink(string text, FontStyle style, FontWeight weight, MouseButtonEventHandler clickHandler);
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
