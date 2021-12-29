using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Walkabout.Views;

namespace Walkabout.Utilities
{
    class FlowDocumentViewClipboardClient : IClipboardClient
    {
        FlowDocumentView view;
        public FlowDocumentViewClipboardClient(FlowDocumentView f)
        {
            this.view = f;
        }

        public bool CanCut => false;

        public bool CanCopy => true;

        public bool CanPaste => false;

        public bool CanDelete => false;

        public void Copy()
        {
            this.view.Copy();
        }

        public void Cut()
        {
        }

        public void Delete()
        {
        }

        public void Paste()
        {
        }
    }
    class TextBoxClipboardClient : IClipboardClient
    {
        TextBox box;
        public TextBoxClipboardClient(TextBox b)
        {
            this.box = b;
        }

        public bool CanCut => true;

        public bool CanCopy => true;

        public bool CanPaste => true;

        public bool CanDelete => true;

        public void Copy()
        {
            this.box.Copy();
        }

        public void Cut()
        {
            this.box.Cut();
        }

        public void Delete()
        {
            this.box.SelectedText = string.Empty;
        }

        public void Paste()
        {
            this.box.Paste();
        }
    }

    class RichTextBoxClipboardClient : IClipboardClient
    {
        RichTextBox box;
        public RichTextBoxClipboardClient(RichTextBox b)
        {
            this.box = b;
        }

        public bool CanCut => true;

        public bool CanCopy => true;

        public bool CanPaste => true;

        public bool CanDelete => true;

        public void Copy()
        {
            this.box.Copy();
        }

        public void Cut()
        {
            this.box.Cut();
        }

        public void Delete()
        {
            this.box.Selection.Text = string.Empty;
        }

        public void Paste()
        {
            this.box.Paste();
        }
    }

}
