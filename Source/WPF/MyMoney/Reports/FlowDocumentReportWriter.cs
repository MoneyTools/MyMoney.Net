using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Utilities;

namespace Walkabout.Reports
{
    public class FlowDocumentReportWriter : IReportWriter
    {
        private readonly FlowDocument doc;
        private Section section;
        private NestedTableState current = new NestedTableState();
        private readonly Stack<NestedTableState> nested = new Stack<NestedTableState>();
        private readonly List<ToggleButton> expandableRowGroups = new List<ToggleButton>();
        private List<TableRow> groupedRows;
        private readonly double pixelsPerDip;

        private class NestedTableState
        {
            internal Table table;
            internal TableRow row;
            internal TableRowGroup group;
            internal TableCell cell;
            internal Paragraph paragraph;
        }


        public FlowDocumentReportWriter(FlowDocument document, double pixelsPerDip)
        {
            this.pixelsPerDip = pixelsPerDip;
            document.ClearValue(FlowDocument.MinPageWidthProperty);
            this.doc = document;
            this.doc.Blocks.Clear();

            this.section = new Section();
            this.doc.Blocks.Add(this.section);
        }

        public FlowDocument Document { get { return this.doc; } }

        public TableRow CurrentRow { get { return this.current.row; } }

        public Paragraph CurrentParagraph { get { return this.current.paragraph; } }

        public void WriteHeading(string title)
        {
            this.EndTable();
            this.EndRow();
            this.EndCell();

            this.section = new Section();
            this.doc.Blocks.Add(this.section);
            this.WriteParagraph(title);
            this.current.paragraph.Style = this.doc.Resources["ReportHeadingStyle"] as Style;
        }

        public void WriteSubHeading(string subHeading)
        {
            this.EndTable();
            this.EndRow();
            this.EndCell();

            this.section = new Section();
            this.doc.Blocks.Add(this.section);
            this.WriteParagraph(subHeading);
            this.current.paragraph.Style = this.doc.Resources["ReportSubHeadingStyle"] as Style;
        }

        public void WriteElement(UIElement e)
        {
            this.current.paragraph = new Paragraph();
            this.current.paragraph.Inlines.Add(new InlineUIContainer(e));
            if (this.current.cell != null)
            {
                this.current.cell.Blocks.Add(this.current.paragraph);
            }
            else
            {
                this.section.Blocks.Add(this.current.paragraph);
            }
        }

        public void WriteParagraph(string text)
        {
            this.WriteParagraph(text, FontStyles.Normal, FontWeights.Normal, null);
        }

        public void WriteParagraph(string text, FontStyle style, FontWeight weight, Brush foreground)
        {
            this.WriteParagraph(text, style, weight, foreground, null);
        }

        public Paragraph WriteParagraph(string text, FontStyle style, FontWeight weight, Brush foreground, double? fontSize)
        {
            this.current.paragraph = new Paragraph();
            Run run = new Run();
            run.Text = text;
            if (style != FontStyles.Normal)
            {
                run.FontStyle = style;
            }
            if (weight != FontWeights.Normal)
            {
                run.FontWeight = weight;
            }
            if (fontSize.HasValue)
            {
                run.FontSize = fontSize.Value;
            }
            if (foreground != null)
            {
                run.Foreground = foreground;
            }
            this.current.paragraph.Inlines.Add(run);
            if (this.current.cell != null)
            {
                this.current.cell.Blocks.Add(this.current.paragraph);
            }
            else
            {
                this.section.Blocks.Add(this.current.paragraph);
            }
            return this.current.paragraph;
        }

        public void WriteHyperlink(string text, FontStyle style, FontWeight weight, MouseButtonEventHandler clickHandler)
        {
            Paragraph p = this.WriteParagraph(text, style, weight, AppTheme.Instance.GetThemedBrush("HyperlinkForeground"), null);
            p.Tag = text;
            p.PreviewMouseLeftButtonDown += clickHandler;
            p.Cursor = Cursors.Arrow;
            //p.TextDecorations.Add(TextDecorations.Underline);
        }

        public void WriteNumber(string number)
        {
            this.WriteNumber(number, FontStyles.Normal, FontWeights.Normal, null);
        }

        public void WriteNumber(string number, FontStyle style, FontWeight weight, Brush foreground)
        {
            this.current.paragraph = new Paragraph();
            Run run = new Run();
            run.Text = number;
            if (style != FontStyles.Normal)
            {
                run.FontStyle = style;
            }
            if (weight != FontWeights.Normal)
            {
                run.FontWeight = weight;
            }
            if (foreground != null)
            {
                run.Foreground = foreground;
            }
            this.current.paragraph.Inlines.Add(run);
            this.current.paragraph.Style = this.doc.Resources["NumericStyle"] as Style;
            if (this.current.cell != null)
            {
                this.current.cell.Blocks.Add(this.current.paragraph);
            }
            else
            {
                this.section.Blocks.Add(this.current.paragraph);
            }
        }

        public void WriteCurrencyHeading(Currency currency)
        {
            if (currency != null)
            {
                this.WriteHeading("Currency " + currency.Symbol);
            }
        }

        public void AddInline(Paragraph p, UIElement childUIElement)
        {
            var inline = new InlineUIContainer(childUIElement);
            inline.BaselineAlignment = BaselineAlignment.Bottom;
            p.Inlines.Add(inline);
        }

        public void StartTable()
        {
            Table table = new Table();
            if (this.current.table != null)
            {
                // nested table
                if (this.current.row == null)
                {
                    this.StartRow();
                }
                if (this.current.cell == null)
                {
                    this.StartCell();
                }
                this.current.cell.Blocks.Add(table);
                this.nested.Push(this.current);
                this.current = new NestedTableState();
            }
            else
            {
                // root level
                this.section = new Section();
                this.section.Blocks.Add(table);
                this.doc.Blocks.Add(this.section);
            }

            this.current.table = table;
        }

        private double maxWidth;
        private double tableWidth;

        public double MaxWidth { get { return this.maxWidth; } }

        public void StartColumnDefinitions()
        {
            this.tableWidth = 0;
        }

        private class ColumnWidthExtensions
        {
            public double MinWidth { get; set; }
            public double MaxWidth { get; set; }
        };

        public void WriteColumnDefinition(string width, double minWidth, double maxWidth)
        {
            GridLength gridLength = GridLength.Auto;
            width = width.Trim();
            if (string.Compare(width, "auto", StringComparison.OrdinalIgnoreCase) == 0)
            {
                this.tableWidth += 100; // give this column a minimum size at least
                gridLength = new GridLength(0, GridUnitType.Auto);
            }
            else if (width.EndsWith("*"))
            {
                double w = 1;
                double.TryParse(width.TrimEnd('*'), out w);
                if (w < 1)
                {
                    w = 1;
                }
                this.tableWidth += 100; // give this column a minimum size at least
                gridLength = new GridLength(w, GridUnitType.Star);
            }
            else
            {
                double w;
                if (double.TryParse(width, out w))
                {
                    this.tableWidth += w;
                    gridLength = new GridLength(w);
                }
                else
                {
                    throw new ArgumentException("width should be 'auto', '*', or a valid number", "width");
                }
            }
            var ext = new ColumnWidthExtensions() { MinWidth = minWidth, MaxWidth = maxWidth };
            this.current.table.Columns.Add(new TableColumn() { Width = gridLength, Tag = ext });
        }

        public void EndColumnDefinitions()
        {
            this.maxWidth = Math.Max(this.maxWidth, this.tableWidth);

            // The layout gets really slow and messy if you squeeze tables smaller than these column definitions, 
            // so we set the MinPageWidth to this value to stop that from happening, and the FlowDocumentScrollVIewer
            // will display a horizontal scrollbar instead.
            if (this.maxWidth > 0)
            {
                this.doc.MinPageWidth = this.maxWidth + 100;
            }
        }

        public void StartHeaderRow()
        {
            this.StartRow();
            this.current.row.Style = this.doc.Resources["RowHeaderStyle"] as Style;
        }

        public void StartFooterRow()
        {
            this.StartRow();
            this.current.row.Style = this.doc.Resources["RowFooterStyle"] as Style;
        }

        private ToggleButton expander; // current one
        private bool firstExpandableRow;

        // this will add an expander to the beginning of the next row that can be used to 
        // expand/collapse all rows inside this group.
        public void StartExpandableRowGroup()
        {
            this.expander = new ToggleButton();
            this.expander.Cursor = System.Windows.Input.Cursors.Arrow;
            this.expander.SetResourceReference(FrameworkElement.StyleProperty, "NuclearTreeViewItemToggleButton");
            this.expandableRowGroups.Add(this.expander);
            this.groupedRows = new List<TableRow>();
            this.firstExpandableRow = true; // add this toggle button to the first cell of the next row.
        }

        public void EndExpandableRowGroup()
        {
            this.expander.Tag = this.groupedRows;
            this.expander.Checked += this.OnExpanderChecked;
            this.expander.Unchecked += this.OnExpanderUnchecked;
            this.OnExpanderUnchecked(this.expander, null); // start out collapsed.
            this.groupedRows = null;
            this.firstExpandableRow = false;
        }

        private void OnExpanderUnchecked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            List<TableRow> rows = button.Tag as List<TableRow>;
            if (rows != null)
            {
                foreach (TableRow row in rows)
                {
                    TableRowGroup parent = row.Parent as TableRowGroup;
                    parent.Rows.Remove(row);
                    row.Tag = parent;
                }
            }
        }

        private void OnExpanderChecked(object sender, RoutedEventArgs e)
        {
            ToggleButton button = (ToggleButton)sender;
            TableRow previousRow = Walkabout.Utilities.WpfHelper.FindAncestor<TableRow>(button);
            List<TableRow> rows = button.Tag as List<TableRow>;
            if (rows != null)
            {
                foreach (TableRow row in rows)
                {
                    TableRowGroup parent = row.Tag as TableRowGroup;
                    parent.Rows.Insert(parent.Rows.IndexOf(previousRow) + 1, row);
                    previousRow = row;
                }
            }
        }

        public bool CanExpandCollapse { get { return this.expandableRowGroups.Count > 0; } }

        public void ExpandAll()
        {
            foreach (ToggleButton button in this.expandableRowGroups)
            {
                button.IsChecked = true;
            }
        }

        public void CollapseAll()
        {
            foreach (ToggleButton button in this.expandableRowGroups)
            {
                button.IsChecked = false;
            }
        }

        public void StartRow()
        {
            if (this.current.table == null)
            {
                this.StartTable();
            }
            if (this.current.group == null)
            {
                this.current.group = new TableRowGroup();
                this.current.table.RowGroups.Add(this.current.group);
            }
            this.current.row = new TableRow();
            this.current.group.Rows.Add(this.current.row);

            if (this.groupedRows != null)
            {
                if (this.firstExpandableRow)
                {
                    this.firstExpandableRow = false;
                    if (this.expander != null)
                    {
                        this.StartCell();
                        this.WriteElement(this.expander);
                        this.EndCell();
                    }
                }
                else
                {
                    // no expander on these guys.
                    this.groupedRows.Add(this.current.row);

                    this.StartCell();
                    this.EndCell();
                }
            }

        }


        public void StartCell()
        {
            this.StartCell(1, 1);

        }

        public void StartCell(int rowSpan, int colSpan)
        {
            if (this.current.row == null)
            {
                this.StartRow();
            }

            this.current.cell = new TableCell();
            if (rowSpan != 1)
            {
                this.current.cell.RowSpan = rowSpan;
            }
            if (colSpan != 1)
            {
                this.current.cell.ColumnSpan = colSpan;
            }
            this.current.row.Cells.Add(this.current.cell);


        }

        public void EndCell()
        {
            this.current.cell = null;
        }

        public void EndRow()
        {
            this.current.cell = null;
            this.current.row = null;
        }

        public void EndTable()
        {
            if (this.current.table != null)
            {
                this.FixAutoColumns(this.current.table);
            }
            this.current.table = null;
            this.current.group = null;
            this.current.row = null;
            this.current.cell = null;

            if (this.nested.Count == 0)
            {
                this.current = new NestedTableState();
            }
            else
            {
                this.current = this.nested.Pop();
            }
        }

        private void FixAutoColumns(Table table)
        {
            // 'Auto' sized columns in FlowDocument tables suck.
            // This code fixes that.
            Brush brush = Brushes.Black;

            List<TableColumn> columns = new List<TableColumn>(this.current.table.Columns);
            if (columns.Count == 0)
            {
                return;
            }
            List<double> maxWidths = new List<double>();
            for (int i = 0; i < columns.Count; i++)
            {
                maxWidths.Add(0);
            }

            foreach (var group in this.current.table.RowGroups)
            {
                foreach (var row in group.Rows)
                {
                    int i = 0;
                    foreach (var cell in row.Cells)
                    {
                        if (columns[i].Width.IsAuto)
                        {
                            foreach (var block in cell.Blocks)
                            {
                                if (block is Table)
                                {
                                    // can't measure these ones.
                                    return;
                                }
                                TextRange range = new TextRange(block.ContentStart, block.ContentEnd);
                                string text = range.Text;
                                if (!string.IsNullOrEmpty(text))
                                {
                                    Typeface tface = new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch);
                                    FormattedText ft = new FormattedText(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, tface, block.FontSize, brush, this.pixelsPerDip);
                                    if (i >= maxWidths.Count)
                                    {
                                        Debug.WriteLine($"Column index {i} is out of range!");
                                    }
                                    else
                                    {
                                        maxWidths[i] = Math.Max(maxWidths[i], ft.Width + cell.Padding.Left + cell.Padding.Right);
                                    }
                                }
                            }
                        }
                        i += cell.ColumnSpan;
                    }
                }
            }

            // fixup 'auto' columns so they have the right width
            int j = 0;
            foreach (var col in this.current.table.Columns)
            {
                ColumnWidthExtensions ext = col.Tag as ColumnWidthExtensions;
                if (col.Width.IsAuto && ext != null && ext.MinWidth > 0)
                {
                    double w = Math.Max(ext.MinWidth, maxWidths[j] + 10);
                    w = Math.Min(w, ext.MaxWidth);
                    col.Width = new GridLength(w);
                }
                j++;
            }

        }

    }
}
