using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Interfaces.Reports;
using Walkabout.Utilities;

namespace Walkabout.Reports
{
    public class FlowDocumentReportWriter : IReportWriter
    {
        FlowDocument doc;
        Section section;
        NestedTableState current = new NestedTableState();
        Stack<NestedTableState> nested = new Stack<NestedTableState>();
        List<ToggleButton> expandableRowGroups = new List<ToggleButton>();
        List<TableRow> groupedRows;
        double pixelsPerDip;

        class NestedTableState
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
            doc.Blocks.Add(section); 
        }

        public FlowDocument Document { get { return doc; } }

        public TableRow CurrentRow { get { return current.row; } }

        public Paragraph CurrentParagraph { get { return current.paragraph; } }

        public void WriteHeading(string title)
        {
            EndTable();
            EndRow();
            EndCell();

            this.section = new Section();
            doc.Blocks.Add(section); 
            WriteParagraph(title);
            current.paragraph.Style = doc.Resources["ReportHeadingStyle"] as Style;
        }

        public void WriteSubHeading(string subHeading)
        {
            EndTable();
            EndRow();
            EndCell();

            this.section = new Section();
            doc.Blocks.Add(section);
            WriteParagraph(subHeading);
            current.paragraph.Style = doc.Resources["ReportSubHeadingStyle"] as Style;
        }

        public void WriteElement(UIElement e)
        {
            current.paragraph = new Paragraph();
            current.paragraph.Inlines.Add(new InlineUIContainer(e));
            if (current.cell != null)
            {
                current.cell.Blocks.Add(current.paragraph);
            }
            else
            {
                section.Blocks.Add(current.paragraph);
            }
        }

        public void WriteParagraph(string text)
        {
            WriteParagraph(text, FontStyles.Normal, FontWeights.Normal, null);
        }

        public void WriteParagraph(string text, FontStyle style, FontWeight weight, Brush foreground)
        {
            WriteParagraph(text, style, weight, foreground, null);
        }

        public Paragraph WriteParagraph(string text, FontStyle style, FontWeight weight, Brush foreground, double? fontSize)
        {
            current.paragraph = new Paragraph();
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
            current.paragraph.Inlines.Add(run);
            if (current.cell != null)
            {
                current.cell.Blocks.Add(current.paragraph);
            }
            else
            {
                section.Blocks.Add(current.paragraph);
            }
            return current.paragraph;
        }

        public void WriteHyperlink(string text, FontStyle style, FontWeight weight, MouseButtonEventHandler clickHandler)
        {
            Paragraph p = WriteParagraph(text, style, weight, AppTheme.Instance.GetThemedBrush("HyperlinkForeground"), null);
            p.Tag = text;
            p.PreviewMouseLeftButtonDown += clickHandler;
            p.Cursor = Cursors.Arrow;
            //p.TextDecorations.Add(TextDecorations.Underline);
        }

        public void WriteNumber(string number)
        {
            WriteNumber(number, FontStyles.Normal, FontWeights.Normal, null);
        }

        public void WriteNumber(string number, FontStyle style, FontWeight weight, Brush foreground)
        {
            current.paragraph = new Paragraph();
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
            current.paragraph.Inlines.Add(run);
            current.paragraph.Style = doc.Resources["NumericStyle"] as Style;
            if (current.cell != null)
            {
                current.cell.Blocks.Add(current.paragraph);
            }
            else
            {
                section.Blocks.Add(current.paragraph);
            }            
        }

        public void StartTable()
        {            
            Table table = new Table();
            if (current.table != null)
            {
                // nested table
                if (current.row == null)
                {
                    StartRow();
                }
                if (current.cell == null)
                {
                    StartCell();
                }
                current.cell.Blocks.Add(table);
                nested.Push(current);
                current = new NestedTableState();
            }
            else
            {
                // root level
                this.section = new Section();
                this.section.Blocks.Add(table);
                doc.Blocks.Add(section); 
            }

            current.table = table;
        }

        double maxWidth;
        double tableWidth;

        public double MaxWidth { get { return maxWidth; } }

        public void StartColumnDefinitions()
        {
            tableWidth = 0;
        }

        class ColumnWidthExtensions
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
                tableWidth += 100; // give this column a minimum size at least
                gridLength = new GridLength(0, GridUnitType.Auto);
            }
            else if (width.EndsWith("*"))
            {
                double w = 1;
                double.TryParse(width.TrimEnd('*'), out w);
                if (w < 1) {
                    w = 1;
                }
                tableWidth += 100; // give this column a minimum size at least
                gridLength = new GridLength(w, GridUnitType.Star);
            }
            else
            {
                double w;
                if (double.TryParse(width, out w))
                {
                    tableWidth += w;
                    gridLength = new GridLength(w);
                }
                else
                {
                    throw new ArgumentException("width should be 'auto', '*', or a valid number", "width");
                }
            }
            var ext =  new ColumnWidthExtensions() { MinWidth = minWidth, MaxWidth = maxWidth };
            current.table.Columns.Add(new TableColumn() { Width = gridLength, Tag = ext } );
        }

        public void EndColumnDefinitions()
        {
            maxWidth = Math.Max(maxWidth, tableWidth);

            // The layout gets really slow and messy if you squeeze tables smaller than these column definitions, 
            // so we set the MinPageWidth to this value to stop that from happening, and the FlowDocumentScrollVIewer
            // will display a horizontal scrollbar instead.
            if (maxWidth > 0)
            {
                this.doc.MinPageWidth = maxWidth + 100;
            }
        }

        public void StartHeaderRow()
        {
            StartRow();
            current.row.Style = doc.Resources["RowHeaderStyle"] as Style;
        }

        public void StartFooterRow()
        {
            StartRow();
            current.row.Style = doc.Resources["RowFooterStyle"] as Style;
        }

        ToggleButton expander; // current one
        bool firstExpandableRow;

        // this will add an expander to the beginning of the next row that can be used to 
        // expand/collapse all rows inside this group.
        public void StartExpandableRowGroup()
        {
            expander = new ToggleButton();
            expander.Cursor = System.Windows.Input.Cursors.Arrow;
            expander.SetResourceReference(FrameworkElement.StyleProperty, "NuclearTreeViewItemToggleButton");
            expandableRowGroups.Add(expander);
            groupedRows = new List<TableRow>();
            firstExpandableRow = true; // add this toggle button to the first cell of the next row.
        }

        public void EndExpandableRowGroup()
        {
            expander.Tag = groupedRows;
            expander.Checked += OnExpanderChecked;
            expander.Unchecked += OnExpanderUnchecked;
            OnExpanderUnchecked(expander, null); // start out collapsed.
            groupedRows = null;
            firstExpandableRow = false;
        }

        void OnExpanderUnchecked(object sender, RoutedEventArgs e)
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

        void OnExpanderChecked(object sender, RoutedEventArgs e)
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
            if (current.table == null)
            {
                StartTable();
            }
            if (current.group == null) 
            {
                current.group = new TableRowGroup();
                current.table.RowGroups.Add(current.group);
            }
            current.row = new TableRow();
            current.group.Rows.Add(current.row);

            if (groupedRows != null)
            {
                if (firstExpandableRow)
                {
                    firstExpandableRow = false;
                    if (expander != null)
                    {
                        StartCell();
                        WriteElement(expander);
                        EndCell();
                    }
                }
                else 
                {
                    // no expander on these guys.
                    groupedRows.Add(current.row);

                    StartCell();
                    EndCell();
                }
            }

        }


        public void StartCell()
        {
            StartCell(1, 1);

        }

        public void StartCell(int rowSpan, int colSpan)
        {
            if (current.row == null) StartRow();
            current.cell = new TableCell();
            if (rowSpan != 1)
            {
                current.cell.RowSpan = rowSpan;
            }
            if (colSpan != 1)
            {
                current.cell.ColumnSpan = colSpan;
            }
            current.row.Cells.Add(current.cell);


        }

        public void EndCell()
        {
            current.cell = null;
        }

        public void EndRow()
        {
            current.cell = null;
            current.row = null;
        }

        public void EndTable()
        {
            if (current.table != null)
            {
                FixAutoColumns(current.table);
            }
            current.table = null;
            current.group = null;
            current.row = null;
            current.cell = null;

            if (nested.Count == 0)
            {
                current = new NestedTableState();
            }
            else
            {
                current = nested.Pop();
            }
        }

        private void FixAutoColumns(Table table)
        {
            // 'Auto' sized columns in FlowDocument tables suck.
            // This code fixes that.
            Brush brush = Brushes.Black;

            List<TableColumn> columns = new List<TableColumn>(current.table.Columns);
            if (columns.Count == 0)
            {
                return;
            }
            List<double> maxWidths = new List<double>();
            for (int i = 0; i < columns.Count; i++)
            {
                maxWidths.Add(0);
            }

            foreach (var group in current.table.RowGroups)
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
            foreach (var col in current.table.Columns)
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
