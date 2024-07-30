using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using Walkabout.Data;
using Walkabout.Interfaces.Reports;
using Walkabout.Interfaces.Views;
using Walkabout.Reports;
using Walkabout.Utilities;

namespace Walkabout.Views
{
    /// <summary>
    /// Interaction logic for FlowDocumentView.xaml
    /// </summary>
    public partial class FlowDocumentView : UserControl, IView
    {
        private IReport report;
        private IReportWriter writer;

        public FlowDocumentView()
        {
            this.InitializeComponent();
        }

        public void FocusQuickFilter()
        {
            this.QuickFilterUX.FocusTextBox();
        }

        public bool ShowSearchStrip
        {
            get
            {
                return this.SearchArea.Visibility == System.Windows.Visibility.Visible;
            }
            set
            {
                this.SearchArea.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public void AddControl(Control control)
        {
            this.ButtonStrip.Children.Add(control);
        }

        private bool generatingReport;

        public async Task Generate(IReport report)
        {
            if (!this.generatingReport)
            {
                this.generatingReport = true;
                try
                {
                    await this.InternalGenerate(report);
                }
                finally
                {
                    this.generatingReport = false;
                }
            }
        }

        private async Task InternalGenerate(IReport report)
        {
            this.report = report;
            this.Viewer.Document.Blocks.Clear();
            this.writer = null;
            this.ResetExpandAllToggleButton();

            Paragraph p = new Paragraph();
            p.Inlines.Add(new Run() { Text = "Loading..." });
            p.FontSize = 18;
            this.Viewer.Document.Blocks.Add(p);

            var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            FlowDocumentReportWriter writer = new FlowDocumentReportWriter(this.Viewer.Document, pixelsPerDip);
            await report.Generate(writer);
            this.writer = writer;
            this.ResetExpandAllToggleButton();
            this.OnAfterViewStateChanged();
            this.Viewer.Document.Focus(); // helps make the F1 help work properly.
        }

        public FlowDocumentScrollViewer DocumentViewer
        {
            get { return this.Viewer; }
        }

        public void AddWidget(UIElement e)
        {
            this.Grid.Children.Add(e);
        }

        #region IView 

        public MyMoney Money { get; set; }


        public void ActivateView()
        {
            this.Focus();
        }

        public event EventHandler BeforeViewStateChanged;

        private void OnBeforeViewStateChanged()
        {
            if (BeforeViewStateChanged != null)
            {
                BeforeViewStateChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        private void OnAfterViewStateChanged()
        {
            if (AfterViewStateChanged != null)
            {
                AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(0));
            }
        }

        private IServiceProvider serviceProvider;

        public IServiceProvider ServiceProvider
        {
            get { return this.serviceProvider; }
            set { this.serviceProvider = value; }
        }

        public void Commit()
        {
        }

        public string Caption
        {
            get { return string.Empty; }
        }

        private object selectedRow;

        public object SelectedRow
        {
            get { return this.selectedRow; }
            set { this.selectedRow = value; }
        }

        public ViewState ViewState
        {
            get { return new ReportViewState(this.report.GetState()); }
            set
            {
                if (value is ReportViewState s)
                {
                    var type = s.ReportState.GetReportType();
                    var report = (IReport)Activator.CreateInstance(type);
                    report.ServiceProvider = this.serviceProvider;
                    report.ApplyState(s.ReportState);
                    if (ReportCreated != null)
                    {
                        ReportCreated(this, report);
                    }
                    _ = this.Generate(report);
                }
            }
        }

        public event EventHandler<IReport> ReportCreated;

        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            return new ViewState();
        }

        private FindManager findManager;
        private string quickFilter;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                FlowDocumentScrollViewer viewer = this.Viewer;
                FlowDocument doc = viewer.Document;

                if (this.quickFilter != value || this.findManager == null)
                {
                    this.findManager = new FindManager(doc);
                }

                this.quickFilter = value;

                TextRange textRange = this.findManager.FindNext(value);

                if (textRange != null)
                {
                    viewer.Selection.Select(textRange.Start, textRange.End);
                    Paragraph p = textRange.Start.Paragraph;
                    if (p != null)
                    {
                        p.BringIntoView();
                        viewer.Focus();
                    }
                }

                this.SetFindString(this.quickFilter);
            }
        }

        private void SetFindString(string text)
        {
            DependencyObject findToolBarHost = this.Viewer.Template.FindName("PART_FindToolBarHost", this.Viewer) as DependencyObject;
            if (findToolBarHost != null)
            {
                TextBox box = findToolBarHost.FindFirstDescendantOfType<TextBox>();
                if (box != null)
                {
                    box.Text = this.quickFilter;
                }
            }
        }

        public bool IsQueryPanelDisplayed { get; set; }
        #endregion

        #region Clipboard
        internal void Copy()
        {
            FlowDocumentScrollViewer viewer = this.Viewer;
            TextSelection selection = viewer.Selection;
            if (!selection.IsEmpty)
            {
                DataObject data = new DataObject();
                data.SetText(selection.Text);

                MemoryStream rtf = new MemoryStream();
                selection.Save(rtf, DataFormats.Rtf);
                rtf.Seek(0, SeekOrigin.Begin);
                StreamReader reader = new StreamReader(rtf);
                string rtfText = reader.ReadToEnd();
                data.SetData(DataFormats.Rtf, rtfText);

                Clipboard.SetDataObject(data);
            }
        }
        #endregion 

        private void OnCloseReport(object sender, RoutedEventArgs e)
        {
            if (Closed != null)
            {
                Closed(this, EventArgs.Empty);
            }
        }

        public event EventHandler Closed;

        private void OnQuickFilterValueChanged(object sender, string filter)
        {
            this.QuickFilter = filter;
        }

        private bool resetting;

        private void OnToggleExpandAll_Checked(object sender, RoutedEventArgs e)
        {
            if (this.resetting)
            {
                return;
            }

            this.ToggleExpandAll.ToolTip = "Hide Details";
            if (this.writer != null)
            {
                this.writer.ExpandAll();
            }
        }

        private void OnToggleExpandAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (this.resetting)
            {
                return;
            }

            this.ToggleExpandAll.ToolTip = "Show Details";
            if (this.writer != null)
            {
                this.writer.CollapseAll();
            }
        }

        private void ResetExpandAllToggleButton()
        {
            this.resetting = true;
            this.ToggleExpandAll.IsEnabled = (this.writer != null) ? this.writer.CanExpandCollapse : false;
            this.ToggleExpandAll.ToolTip = "Show Details";
            this.ToggleExpandAll.IsChecked = false;
            this.resetting = false;
        }

        private void OnExportHtml(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            if (this.report != null)
            {
                Microsoft.Win32.SaveFileDialog sd = new Microsoft.Win32.SaveFileDialog();
                sd.Filter = "HTML files (*.htm)|*.htm";
                if (sd.ShowDialog() == true)
                {
                    var fileName = sd.FileName;
                    try
                    {
                        this.GenerateHtmlReport(fileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBoxEx.Show(ex.Message, "Export HTML Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void GenerateHtmlReport(string fileName)
        {
            XmlWriterSettings s = new XmlWriterSettings();
            s.Indent = true;
            using (var writer = XmlWriter.Create(fileName, s))
            {
                HtmlDocumentReportWriter htmlDocumentReportWriter = new HtmlDocumentReportWriter(writer);
                this.report.Generate(htmlDocumentReportWriter);
                htmlDocumentReportWriter.Close();
            }
        }

        private void CanExecute_ExportHtml(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = this.report != null;
        }
    }

    /// <summary>
    /// Todo: serialize this if we can, so restart can show the same report.
    /// </summary>
    internal class ReportViewState : ViewState
    {
        public IReportState ReportState { get; private set; }

        public ReportViewState(IReportState reportState)
        {
            this.ReportState = reportState;
        }
    }

}
