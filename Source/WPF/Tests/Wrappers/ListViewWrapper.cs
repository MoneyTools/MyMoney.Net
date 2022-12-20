using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class ListViewWrapper
    {
        AutomationElement element;

        public ListViewWrapper(AutomationElement e)
        {
            this.element = e.FindFirstWithRetries(TreeScope.Subtree, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List));
            if (this.element == null)
            {
                throw new Exception("No list control found in this scope after 5 retries");
            }
        }

        public AutomationElement Element { get { return this.element; } }

        public int Count
        {
            get
            {
                return this.Items.Count;
            }
        }

        public AutomationElement Select(int index)
        {
            List<AutomationElement> items = this.Items;
            if (index >= items.Count)
            {
                throw new Exception("Item index " + index + " is out of range, list only has " + items.Count + " items");
            }
            AutomationElement item = items[index];

            for (int retries = 5; retries > 0; retries--)
            {
                if (item.Current.IsEnabled)
                {
                    break;
                }
                Thread.Sleep(200);
            }

            SelectionItemPattern select = (SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern);
            select.Select();
            return item;
        }

        public IEnumerable<AutomationElement> Selection
        {
            get
            {
                SelectionPattern selection = (SelectionPattern)this.element.GetCurrentPattern(SelectionPattern.Pattern);
                AutomationElement[] selected = selection.Current.GetSelection();
                return (selected == null) ? new AutomationElement[0] : selected;
            }
        }

        public List<AutomationElement> Items
        {
            get
            {
                AutomationElementCollection result = this.element.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
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

        public string GetUniqueCaption(string baseName)
        {
            int index = 1;
            while (true)
            {
                string caption = baseName;
                if (index > 1)
                {
                    caption += " " + index;
                }
                bool found = false;
                foreach (AutomationElement e in this.Items)
                {
                    if (e.Current.Name == caption)
                    {
                        found = true;
                    }
                }
                if (!found)
                {
                    return caption;
                }
                index++;
            }
        }
    }
}
