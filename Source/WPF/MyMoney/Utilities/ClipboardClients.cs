using System.Windows;
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

    class ComboBoxClipboardClient : IClipboardClient
    {
        ComboBox box;
        public ComboBoxClipboardClient(ComboBox b)
        {
            this.box = b;
        }

        public bool CanCut => this.box.IsEditable;

        public bool CanCopy => true;

        public bool CanPaste => this.box.IsEditable;

        public bool CanDelete => this.box.IsEditable;

        public void Copy()
        {
            var text = this.GetTextBox();
            if (text != null)
            {
                text.Copy();
            }
            else
            {
                Clipboard.SetText(this.box.Text);
            }
        }

        private TextBox GetTextBox()
        {
            return this.box.Template.FindName("PART_EditableTextBox", this.box) as TextBox;
        }

        public void Cut()
        {
            this.GetTextBox()?.Cut();
        }

        public void Delete()
        {
            var text = this.GetTextBox();
            if (text != null)
            {
                text.SelectedText = string.Empty;
            }
        }

        public void Paste()
        {
            this.GetTextBox()?.Paste();
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
