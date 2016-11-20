using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class PayeesWrapper : ListViewWrapper
    {
        public PayeesWrapper(AutomationElement e)
            : base(e)
        {
        }

        public bool HasPayees
        {
            get
            {
                return Count > 0;
            }
        }

        public bool IsPayeeSelected
        {
            get
            {
                foreach (AutomationElement e in Selection)
                {
                    return true;
                }
                return false;
            }
        }
    }
}
