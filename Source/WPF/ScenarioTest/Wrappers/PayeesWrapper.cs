﻿using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class PayeesWrapper : ListViewWrapper
    {
        public PayeesWrapper(AutomationElement e)
            : base(e)
        {
        }

        public bool HasPayees
        {
            get
            {
                return this.Count > 0;
            }
        }

        public bool IsPayeeSelected
        {
            get
            {
                foreach (AutomationElement e in this.Selection)
                {
                    return true;
                }
                return false;
            }
        }
    }
}
