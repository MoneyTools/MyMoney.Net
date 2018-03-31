using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    public class TreeViewWrapper
    {
        AutomationElement element;

        public TreeViewWrapper(AutomationElement e)
        {
            element = e.FindFirstWithRetries(TreeScope.Subtree, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tree));
            if (element == null) 
            {
                throw new Exception("No tree control found in this scope");
            }
        }

        public int Count
        {
            get
            {
                return Items.Count;
            }
        }

        public IEnumerable<AutomationElement> Selection
        {
            get
            {
                try
                {
                    SelectionPattern selection = (SelectionPattern)element.GetCurrentPattern(SelectionPattern.Pattern);
                    AutomationElement[] selected = selection.Current.GetSelection();
                    return (selected == null) ? new AutomationElement[0] : selected;
                }
                catch
                {
                    return new AutomationElement[0];
                }
            }
        }

        public List<AutomationElement> Items
        {
            get
            {
                AutomationElementCollection result = element.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem));                 
                List<AutomationElement> list = new List<AutomationElement>();
                if (result != null)
                {
                    foreach (AutomationElement e in result)
                    {
                        list.Add(e);
                    }
                }
                return list;
            }
        }

        public void Select(int i)
        {
            AutomationElement element = Items[i];
            Select(element);
        }

        public void Select(AutomationElement element)
        {
            SelectionItemPattern selection = (SelectionItemPattern)element.GetCurrentPattern(SelectionItemPattern.Pattern);
            selection.Select();
        }
    }
}
