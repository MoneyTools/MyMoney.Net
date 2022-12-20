using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using Walkabout.Tests.Interop;
using System.Windows.Input;
using System.Windows;
using System.Threading;
using System.Diagnostics;

namespace Walkabout.Tests.Wrappers
{
    public class ContextMenu
    {
        AutomationElement control;
        bool isOpened;
        bool isPopupMenu;
        MainWindowWrapper root;

        public ContextMenu(AutomationElement control, bool isPopupMenu)
        {
            this.isPopupMenu = isPopupMenu;
            this.root = MainWindowWrapper.FindMainWindow(control.Current.ProcessId);
            this.control = control;
        }

        /// <summary>
        /// Opens the context menu and returns the AutomationElement for the Menu.
        /// </summary>
        /// <param name="e">Automation element to right-click</param>
        public AutomationElement Open(bool throwIfNotOpened)
        {
            if (!this.isPopupMenu)
            {
                return null;
            }
            this.isOpened = false;
            AutomationElement openMenu = null;

            // see if this is a SubMenuItem already
            object pattern;
            if (this.control.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out pattern))
            {
                ExpandCollapsePattern expandCollapse = (ExpandCollapsePattern)pattern;
                expandCollapse.Expand();
                Thread.Sleep(250);

                for (int retries = 5; retries > 0; retries--)
                {
                    AutomationElement firstChild = this.control.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem));
                    if (firstChild != null)
                    {
                        openMenu = this.control;
                    }
                    else
                    {
                        Thread.Sleep(250);
                    }
                }

            }
            else
            {
                for (int outerRetries = 5; outerRetries > 0; outerRetries--)
                {
                    if (this.control == null)
                    {
                        // use the context menu key
                        Input.TapKey(Key.Apps);
                    }
                    else
                    {
                        Rect bounds = (Rect)this.control.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty);
                        Point clickLocation = new Point(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));

                        if (this.control.Current.ControlType == ControlType.Menu)
                        {
                            Input.MoveToAndLeftClick(clickLocation);
                        }
                        else
                        {
                            Input.MoveToAndRightClick(clickLocation);
                        }
                    }

                    if (!this.isPopupMenu)
                    {
                        openMenu = this.root.Element.FindChildMenuPopup(5);
                    }
                    else
                    {
                        openMenu = this.root.Element.FindChildContextMenu(5);
                    }

                }
            }

            if (openMenu == null)
            {
                if (throwIfNotOpened)
                {
                    string message = "Error: context menu is not appearing!";
                    throw new ApplicationException(message);
                }
            }

            return openMenu;
        }

        public void InvokeMenuItem(string menuItemId)
        {
            AutomationElement subMenu = null;
            if (!this.isPopupMenu)
            {
                subMenu = this.control;
            }
            else if (!this.isOpened)
            {
                subMenu = this.Open(true);
            }

            AutomationElement menuItem = this.FindSubMenuItem(subMenu, menuItemId);
            this.InvokeMenuItem(menuItem);
            this.isOpened = false;
        }

        public ContextMenu OpenSubMenu(string menuItemId)
        {
            AutomationElement subMenu = null;
            if (!this.isPopupMenu)
            {
                subMenu = this.control;
            }
            else if (!this.isOpened)
            {
                subMenu = this.Open(true);
            }

            AutomationElement subMenuItem = this.FindSubMenuItem(subMenu, menuItemId);
            return this.ExpandSubMenuItem(subMenuItem);
        }

        private AutomationElement FindSubMenuItem(AutomationElement contextMenuItem, string menuItemId)
        {
            AutomationElement subMenuItem = contextMenuItem.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.AutomationIdProperty, menuItemId));

            if (subMenuItem == null)
            {
                int count = 0;
                foreach (AutomationElement child in contextMenuItem.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)))
                {
                    count++;
                    string name = child.Current.Name;
                    string id = child.Current.AutomationId;
                    bool enabled = child.Current.IsEnabled;
                    bool offscreen = child.Current.IsOffscreen;
                    Debug.WriteLine("Found menu item: " + name + " with id " + id);
                }
                if (count == 0)
                {
                    throw new Exception("Very strange, the menu has no child elements");
                }
                throw new Exception("Menu item with id = '" + menuItemId + "' not found");
            }
            return subMenuItem;
        }

        private void InvokeMenuItem(AutomationElement item)
        {
            object pattern;
            if (!item.Current.IsEnabled)
            {
                throw new Exception("SubMenuItem is not enabled");
            }

            if (item.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
            {
                InvokePattern invoke = (InvokePattern)pattern;
                invoke.Invoke();
                return;
            }
            throw new Exception("SubMenuItem does not contain InvokePattern");
        }

        private ContextMenu ExpandSubMenuItem(AutomationElement subMenuItem)
        {
            if (!subMenuItem.Current.IsEnabled)
            {
                throw new Exception("SubMenuItem is not enabled");
            }
            object pattern;
            if (subMenuItem.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out pattern))
            {
                ContextMenu menu = new ContextMenu(subMenuItem, true);
                menu.Open(true);
                return menu;
            }
            throw new Exception("SubMenuItem does not contain ExpandCollapsePattern");
        }

    }
}
