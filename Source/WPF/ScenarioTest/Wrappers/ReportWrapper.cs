using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Xml.Linq;
using Walkabout.Tests.Interop;

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

        AutomationElement GetDateField()
        {
            AutomationElement date = this.e.FindFirstWithRetries(TreeScope.Descendants,
               new PropertyCondition(AutomationElement.NameProperty, "ReportDate"));
            if (date == null)
            {
                throw new Exception("ReportDate' field not found");
            }
            return date;
        }
    }
}
