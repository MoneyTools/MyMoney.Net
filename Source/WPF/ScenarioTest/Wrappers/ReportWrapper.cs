using System.Diagnostics;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class ReportWrapper
    {
        private readonly AutomationElement e;

        public ReportWrapper(AutomationElement e)
        {
            this.e = e;
        }

        public DateTime GetDate()
        {
            var e = GetDateField();
            ValuePattern vp = (ValuePattern)e.GetCurrentPattern(ValuePattern.Pattern);
            string s = vp.Current.Value;
            if (DateTime.TryParse(s, out DateTime d))
            {
                return d;
            }
            return DateTime.Now;
        }

        public void SetDate(DateTime date)
        {
            var e = GetDateField();
            ValuePattern vp = (ValuePattern)e.GetCurrentPattern(ValuePattern.Pattern);
            vp.SetValue(date.ToShortDateString());
        }

        public bool IsVisible
        {
            get
            {
                return !this.e.Current.IsOffscreen;
            }
        }

        protected AutomationElement GetDocument()
        {

            AutomationElement doc = this.e.FindFirstWithRetries(TreeScope.Descendants,
               new PropertyCondition(AutomationElement.LocalizedControlTypeProperty, "document"));
            if (doc == null)
            {
                throw new Exception("Document object not found");
            }

            return doc;
        }

        public string FindText(string text)
        {
            for (int retries = 5; retries-- > 0;)
            {
                var doc = GetDocument();
                StringBuilder sb = new StringBuilder();
                TextPattern tp = (TextPattern)doc.GetCurrentPattern(TextPattern.Pattern);
                var range = tp.DocumentRange.FindText(text, false, true);
                if (range != null)
                {
                    range.ExpandToEnclosingUnit(System.Windows.Automation.Text.TextUnit.Paragraph);
                    return range.GetText(1000).Trim();
                }
            }
            throw new Exception("Text not found");
        }

        public string GetText(int maxLength = 10000)
        {
            var doc = GetDocument();
            StringBuilder sb = new StringBuilder();
            TextPattern tp = (TextPattern)doc.GetCurrentPattern(TextPattern.Pattern);
            return tp.DocumentRange.GetText(maxLength);
        }

        AutomationElement GetDateField()
        {
            AutomationElement date = this.e.FindFirstWithRetries(TreeScope.Descendants,
               new PropertyCondition(AutomationElement.NameProperty, "ReportDate"));
            if (date == null)
            {
                throw new Exception("ReportDate field not found");
            }
            return date;
        }

        public void CloseReport()
        {
            AutomationElement closeButton = this.e.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "CloseReport"));
            if (closeButton != null && !closeButton.Current.IsOffscreen)
            {
                try
                {
                    InvokePattern invoke = (InvokePattern)closeButton.GetCurrentPattern(InvokePattern.Pattern);
                    invoke.Invoke();
                } 
                catch
                {
                    // ignore it
                }
            }
        }
    }
}
