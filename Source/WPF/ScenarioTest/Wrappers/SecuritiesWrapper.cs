using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    internal class SecuritiesWrapper : ListViewWrapper
    {
        private AutomationElement selector;
        public SecuritiesWrapper(AutomationElement e)
            : base(e)
        {
            selector = e;
        }

        public void Expand()
        {
            ExpandCollapsePattern p = (ExpandCollapsePattern)this.selector.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            p.Expand();
        }

        public bool IsExpanded
        {
            get
            {
                ExpandCollapsePattern p = (ExpandCollapsePattern)this.selector.GetCurrentPattern(ExpandCollapsePattern.Pattern);
                return p.Current.ExpandCollapseState == ExpandCollapseState.Expanded;
            }
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
