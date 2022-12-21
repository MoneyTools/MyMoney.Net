using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class SecuritiesWrapper : ListViewWrapper
    {
        public SecuritiesWrapper(AutomationElement e)
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

        public bool IsSecuritySelected
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
