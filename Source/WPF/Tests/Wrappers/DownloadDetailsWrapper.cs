using System;
using System.Collections.Generic;
using System.Windows.Automation;

namespace Walkabout.Tests.Wrappers
{
    class DownloadDetailsWrapper
    {
        AutomationElement panel;
        AutomationElement tree;

        public DownloadDetailsWrapper(AutomationElement panel)
        {
            this.panel = panel;
            this.tree = panel.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "OfxEventTree"));
        }

        public List<DownloadedOnlineAccountWrapper> GetOnlineAccounts()
        {
            AutomationElement tree = this.panel.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Tree));
            if (tree == null)
            {
                throw new Exception("Tree not found");
            }
            List<DownloadedOnlineAccountWrapper> result = new List<DownloadedOnlineAccountWrapper>();
            foreach (AutomationElement e in tree.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)))
            {
                result.Add(new DownloadedOnlineAccountWrapper(e));
            }
            return result;
        }

        public void Close()
        {
            AutomationElement button = this.panel.FindFirstWithRetries(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, "ButtonCloseDownloads"));
            if (button == null)
            {
                throw new Exception("Cannot find ButtonCloseDownloads");
            }
            InvokePattern invoke = (InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern);
            invoke.Invoke();
        }
    }

    class DownloadedOnlineAccountWrapper
    {
        AutomationElement treeitem;

        public DownloadedOnlineAccountWrapper(AutomationElement treeitem)
        {
            this.treeitem = treeitem;
        }

        public HyperlinkWrapper Hyperlink
        {
            get
            {
                AutomationElement link = this.treeitem.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink));
                if (link == null)
                {
                    throw new Exception("HyperLink not found");
                }
                return new HyperlinkWrapper(link);
            }
        }

        public List<DownloadedAccountWrapper> GetAccounts()
        {
            List<DownloadedAccountWrapper> result = new List<DownloadedAccountWrapper>();
            foreach (AutomationElement e in this.treeitem.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)))
            {
                result.Add(new DownloadedAccountWrapper(e));
            }
            return result;
        }
    }

    class DownloadedAccountWrapper
    {
        AutomationElement treeitem;

        public DownloadedAccountWrapper(AutomationElement treeitem)
        {
            this.treeitem = treeitem;
        }

        public HyperlinkWrapper Hyperlink
        {
            get
            {
                AutomationElement link = this.treeitem.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink));
                if (link == null)
                {
                    throw new Exception("HyperLink not found");
                }
                return new HyperlinkWrapper(link);
            }
        }

        public void Select()
        {
            SelectionItemPattern sip = (SelectionItemPattern)this.treeitem.GetCurrentPattern(SelectionItemPattern.Pattern);
            sip.Select();
            return;
        }

    }
}
