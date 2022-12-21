using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class AttachmentDialogWrapper : DialogWrapper
    {
        internal AttachmentDialogWrapper(AutomationElement e)
            : base(e)
        {
        }

        public void ClickScan()
        {
            this.window.ClickButton("ScanButton");
        }

        public void ClickZoomIn()
        {
            this.window.ClickButton("ZoomInButton");
        }

        public void ClickZoomOut()
        {
            this.window.ClickButton("ZoomOutButton");
        }

        public void ClickSave()
        {
            this.window.ClickButton("SaveButton");
        }

        public void ClickRotateLeft()
        {
            this.window.ClickButton("RotateLeftButton");
        }

        public void ClickRotateRight()
        {
            this.window.ClickButton("RotateRightButton");
        }

        public void ClickCropImage()
        {
            this.window.ClickButton("CropImageButton");
        }

        public void ClickCut()
        {
            this.window.ClickButton("CutButton");
        }

        public void ClickCopy()
        {
            this.window.ClickButton("CopyButton");
        }

        public void ClickPaste()
        {
            this.window.ClickButton("PasteButton");
        }

        public void ClickDelete()
        {
            this.window.ClickButton("DeleteButton");
        }

        public void ClickPrint()
        {
            this.window.ClickButton("PrintButton");
        }

        public AutomationElement ScrollViewer
        {
            get
            {
                return this.Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
            }
        }
    }
}
