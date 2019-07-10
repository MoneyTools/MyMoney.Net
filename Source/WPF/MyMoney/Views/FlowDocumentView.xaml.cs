using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using Walkabout.Data;
using Walkabout.Interfaces;
using Walkabout.Utilities;
using Walkabout.Help;
using Walkabout.Interfaces.Reports;
using Walkabout.Reports;
using Walkabout.Interfaces.Views;
using System.Windows.Threading;
using System.Windows.Media;
using Walkabout.Controls;

namespace Walkabout.Views
{
    /// <summary>
    /// Interaction logic for FlowDocumentView.xaml
    /// </summary>
    public partial class FlowDocumentView : UserControl, IView
    {
        IReportWriter writer;

        public FlowDocumentView()
        {
            InitializeComponent();                   
        }

        public void FocusQuickFilter()
        {
            QuickFilterUX.FocusTextBox();
        }

        public bool ShowSearchStrip
        {
            get
            {
                return SearchArea.Visibility == System.Windows.Visibility.Visible;
            }
            set
            {
                SearchArea.Visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }
        }

        public void AddControl(Control control)
        {           
            ButtonStrip.Children.Add(control);
        }

        public void Generate(IReport report)
        {
            this.Viewer.Document.Blocks.Clear();

            Paragraph p = new Paragraph();
            p.Inlines.Add(new Run() { Text = "Loading..." });
            p.FontSize = 18;
            this.Viewer.Document.Blocks.Add(p);

            this.Viewer.Dispatcher.BeginInvoke(new Action(() =>
            {
                FlowDocumentReportWriter writer = new FlowDocumentReportWriter(this.Viewer.Document);
                report.Generate(writer);
                this.writer = writer;
                ResetExpandAllToggleButton();
            }), DispatcherPriority.ContextIdle);
        }

        public FlowDocumentScrollViewer DocumentViewer
        {
            get { return this.Viewer; }
        }

        public void AddWidget(UIElement e)
        {
            Grid.Children.Add(e);
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

        IServiceProvider serviceProvider;

        public IServiceProvider ServiceProvider
        {
            get { return serviceProvider; }
            set { serviceProvider = value; }
        }

        public void Commit()
        {           
        }

        public string Caption
        {
            get { return string.Empty; }
        }

        object selectedRow;

        public object SelectedRow
        {
            get { return selectedRow; }
            set { selectedRow = value; }
        }

        public ViewState ViewState
        {
            get { return new ViewState(); }
            set 
            { 
                // todo:
            }
        }


        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            return new ViewState();
        }

        FindManager findManager;
        string quickFilter;

        public string QuickFilter
        {
            get { return quickFilter; }
            set 
            {
                FlowDocumentScrollViewer viewer = this.Viewer;
                FlowDocument doc = viewer.Document;

                if (quickFilter != value || findManager == null)
                {
                    findManager = new FindManager(doc);
                }

                quickFilter = value;

                TextRange textRange = findManager.FindNext(value);

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

                SetFindString(quickFilter);
            }
        }

        private void SetFindString(string text)
        {
            DependencyObject findToolBarHost = Viewer.Template.FindName("PART_FindToolBarHost", Viewer) as DependencyObject;
            if (findToolBarHost != null)
            {
                TextBox box = findToolBarHost.FindFirstDescendantOfType<TextBox>();
                if (box != null)
                {
                    box.Text = quickFilter;
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
            QuickFilter = filter;
        }

        bool resetting;

        private void OnToggleExpandAll_Checked(object sender, RoutedEventArgs e)
        {
            if (resetting) return;
            ToggleExpandAll.ToolTip = "Hide Details";
            writer.ExpandAll();
            ToggleExpandAllImage.SetResourceReference(Image.SourceProperty, "CollapseAllIcon");
        }

        private void OnToggleExpandAll_Unchecked(object sender, RoutedEventArgs e)
        {
            if (resetting) return;
            ToggleExpandAll.ToolTip = "Show Details";
            writer.CollapseAll();
            ToggleExpandAllImage.SetResourceReference(Image.SourceProperty, "ExpandAllIcon");
        }
        void ResetExpandAllToggleButton()
        {
            resetting = true;
            this.ToggleExpandAll.IsEnabled = writer.CanExpandCollapse;
            this.ToggleExpandAll.ToolTip = "Show Details";
            this.ToggleExpandAll.IsChecked = false;
            ToggleExpandAllImage.SetResourceReference(Image.SourceProperty, "ExpandAllIcon");
            resetting = false;
        }


    }
}
