using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            InitializeComponent();
        }

        public void Show()
        {
            RaiseEvent(new RoutedEventArgs(ShowOutputEvent));
        }

        public void Hide()
        {
            RaiseEvent(new RoutedEventArgs(HideOutputEvent));
        }

        public void Clear()
        {
            OutputTextView.Document.Blocks.Clear();
        }

        public void AppendParagraph(Paragraph p)
        {
            OutputTextView.Document.Blocks.Add(p);
        }

        public void AppendText(string text) 
        {
            if (OutputTextView.Document.Blocks.Count == 0)
            {
                OutputTextView.Document.Blocks.Add(new Paragraph());
                TextPointer pos = OutputTextView.Document.ContentEnd;
                OutputTextView.Selection.Select(pos, pos);
            }

            TextPointer ptr = OutputTextView.Document.ContentEnd;
            int delta = OutputTextView.Selection.End.GetOffsetToPosition(OutputTextView.Document.ContentEnd);
            ptr.InsertTextInRun(text);
            ptr = OutputTextView.Document.ContentEnd;
            ptr.InsertLineBreak();
            ptr = OutputTextView.Document.ContentEnd;

            if (delta < 10)
            {
                OutputTextView.Selection.Select(ptr, ptr);
                OutputTextView.ScrollToEnd();
            }
        }

        internal void AppendHeading(string heading)
        {
            OutputTextView.Document.Blocks.Add(new Paragraph(new Run(heading))
            {
                FontWeight = FontWeights.Bold,
                FontSize = 12
            });

        }
    }
}
