using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Threading;

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
            window.ClickButton("ScanButton");
        }

        public void ClickZoomIn()
        {
            window.ClickButton("ZoomInButton");
        }

        public void ClickZoomOut()
        {
            window.ClickButton("ZoomOutButton");
        }

        public void ClickSave()
        {
            window.ClickButton("SaveButton");
        }

        public void ClickRotateLeft()
        {
            window.ClickButton("RotateLeftButton");
        }
        
        public void ClickRotateRight()
        {
            window.ClickButton("RotateRightButton");
        }
        
        public void ClickCropImage()
        {
            window.ClickButton("CropImageButton");
        }
        
        public void ClickCut()
        {
            window.ClickButton("CutButton");
        }
        
        public void ClickCopy()
        {
            window.ClickButton("CopyButton");
        }
        
        public void ClickPaste()
        {
            window.ClickButton("PasteButton");
        }
        
        public void ClickDelete()
        {
            window.ClickButton("DeleteButton");
        }
        
        public void ClickPrint()
        {
            window.ClickButton("PrintButton");
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
