using System.Collections.Generic;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class CategoriesWrapper : TreeViewWrapper
    {
        public CategoriesWrapper(AutomationElement e)
            : base(e)
        {
        }

        public bool HasCategories
        {
            get
            {
                List<AutomationElement> list = new List<AutomationElement>();
                foreach (AutomationElement e in this.CategoryGroups)
                {
                    AutomationElementCollection result = e.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));
                    foreach (AutomationElement c in result)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool IsCategorySelected
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

        public List<AutomationElement> CategoryGroups
        {
            get
            {
                return this.Items;
            }
        }

        public List<AutomationElement> Categories
        {
            get
            {
                List<AutomationElement> list = new List<AutomationElement>();
                foreach (AutomationElement e in this.CategoryGroups)
                {
                    AutomationElementCollection result = e.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));
                    foreach (AutomationElement c in result)
                    {
                        list.Add(c);
                    }
                }
                return list;
            }
        }
    }
}
