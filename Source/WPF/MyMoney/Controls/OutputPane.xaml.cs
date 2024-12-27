using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Walkabout.Controls
{
    /// <summary>
    /// Interaction logic for OutputWindow.xaml
    /// </summary>
    public partial class OutputPane : UserControl
    {
        public static RoutedEvent ShowOutputEvent = EventManager.RegisterRoutedEvent("ShowOutput", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(OutputPane));
        public static RoutedEvent HideOutputEvent = EventManager.RegisterRoutedEvent("HideOutput", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(OutputPane));


        public OutputPane()
        {
            this.InitializeComponent();
        }

        public void Show()
        {
            this.RaiseEvent(new RoutedEventArgs(ShowOutputEvent));
        }

        public void Hide()
        {
            this.RaiseEvent(new RoutedEventArgs(HideOutputEvent));
        }

        public void Clear()
        {
            this.OutputTextView.Document.Blocks.Clear();
        }

        public bool IsClear
        {
            get { return this.OutputTextView.Document.Blocks.Count == 0; }
        }

        public void AppendParagraph(Paragraph p)
        {
            this.OutputTextView.Document.Blocks.Add(p);
        }

        public void AppendText(string text)
        {
            if (this.OutputTextView.Document.Blocks.Count == 0)
            {
                this.OutputTextView.Document.Blocks.Add(new Paragraph());
                TextPointer pos = this.OutputTextView.Document.ContentEnd;
                this.OutputTextView.Selection.Select(pos, pos);
            }

            TextPointer ptr = this.OutputTextView.Document.ContentEnd;
            if (this.OutputTextView.Document.Blocks.LastBlock is Paragraph p)
            {
                if (!(p.Inlines.LastInline.PreviousInline is LineBreak))
                {
                    ptr.InsertLineBreak();
                }
            }

            int delta = this.OutputTextView.Selection.End.GetOffsetToPosition(this.OutputTextView.Document.ContentEnd);
            ptr.InsertTextInRun(text);
            ptr = this.OutputTextView.Document.ContentEnd;
            ptr.InsertLineBreak();
            ptr = this.OutputTextView.Document.ContentEnd;

            if (delta < 10)
            {
                this.OutputTextView.Selection.Select(ptr, ptr);
                this.OutputTextView.ScrollToEnd();
            }
        }

        internal void AppendHeading(string heading)
        {
            this.OutputTextView.Document.Blocks.Add(new Paragraph(new Run(heading))
            {
                FontWeight = FontWeights.Bold,
                FontSize = 12
            });

        }
    }
}
