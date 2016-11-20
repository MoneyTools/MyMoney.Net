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
            ClickButton("ScanButton");
        }

        public void ClickZoomIn()
        {
            ClickButton("ZoomInButton");
        }

        public void ClickZoomOut()
        {
            ClickButton("ZoomOutButton");
        }

        public void ClickSave()
        {
            ClickButton("SaveButton");
        }

        public void ClickRotateLeft()
        {
            ClickButton("RotateLeftButton");
        }
        
        public void ClickRotateRight()
        {
            ClickButton("RotateRightButton");
        }
        
        public void ClickCropImage()
        {
            ClickButton("CropImageButton");
        }
        
        public void ClickCut()
        {
            ClickButton("CutButton");
        }
        
        public void ClickCopy()
        {
            ClickButton("CopyButton");
        }
        
        public void ClickPaste()
        {
            ClickButton("PasteButton");
        }
        
        public void ClickDelete()
        {
            ClickButton("DeleteButton");
        }
        
        public void ClickPrint()
        {
            ClickButton("PrintButton");
        }

        public AutomationElement ScrollViewer
        {
            get
            {
                return this.Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "ScrollViewer"));
            }
        }

        public AutomationElement FindImage(int retries = 5)
        {
             
            for (; retries > 0; retries--)
            {
                AutomationElement e = ScrollViewer.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "Image"));
                if (e != null)
                {
                    return e;
                }
                if (retries > 0)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            return null;
        }

        public AutomationElement FindRichText(int retries = 5)
        {
            for (; retries > 0; retries--)
            {
                AutomationElement e = ScrollViewer.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "RichTextBox"));
                if (e != null)
                {
                    return e;
                }
                if (retries > 0)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            return null;
        }

    }
}
