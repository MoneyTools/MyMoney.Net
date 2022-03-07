using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;
using Walkabout.Tests.Wrappers;

namespace Walkabout.Tests
{
    static class AutomationExtensions
    {
        internal static AutomationElement FindFirstWithRetries(this AutomationElement parent, TreeScope scope, Condition condition, int retries = 5, int millisecondDelay = 200)
        {
            AutomationElement result = null;
            while (retries > 0 && result == null)
            {
                result = parent.FindFirst(scope, condition);
                if (result == null)
                {
                    Thread.Sleep(millisecondDelay);
                }
                retries--;
            }
            return result;
        }

        public static bool IsButtonEnabled(this AutomationElement parent, string name)
        {
            AutomationElement button = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (button == null)
            {
                throw new Exception("Button '" + name + "' not found");
            }

            return button.Current.IsEnabled;
        }

        public static AutomationElement ClickButton(this AutomationElement parent, string name)
        {
            AutomationElement button = null;
            for (int retries = 5; retries > 0; retries--)
            {
                button = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
                if (button != null && button.Current.IsEnabled)
                {
                    break;
                }
                Thread.Sleep(250);
            }
            if (button == null)
            {
                throw new Exception("Button '" + name + "' not found");
            }
            if (!button.Current.IsEnabled)
            {
                throw new Exception("Cannot invoke disabled button: " + name);
            }

            InvokePattern invoke = (InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern);
            invoke.Invoke();
            return button;
        }

        public static AutomationElement SelectTab(this AutomationElement parent, string name)
        {
            AutomationElement tab = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (tab == null)
            {
                throw new Exception("Tab '" + name + "' not found");
            }
            SelectionItemPattern selectionItem = (SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern);
            selectionItem.Select();
            return tab;
        }

        public static string GetTextBox(this AutomationElement parent, string name)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("TextBox '" + name + "' not found");
            }
            if (!box.Current.IsEnabled)
            {
                throw new Exception("TextBox '" + name + "' is not enabled");
            }

            ValuePattern p = (ValuePattern)box.GetCurrentPattern(ValuePattern.Pattern);
            return p.Current.Value;
        }

        public static AutomationElement SetTextBox(this AutomationElement parent, string name, string value)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("TextBox '" + name + "' not found");
            }
            if (!box.Current.IsEnabled)
            {
                throw new Exception("TextBox '" + name + "' is not enabled");
            }
            ValuePattern p = (ValuePattern)box.GetCurrentPattern(ValuePattern.Pattern);
            p.SetValue(value);
            return box;
        }

        public static bool IsChecked(this AutomationElement parent, string name)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("CheckBox '" + name + "' not found");
            }

            TogglePattern p = (TogglePattern)box.GetCurrentPattern(TogglePattern.Pattern);
            return p.Current.ToggleState == ToggleState.On;
        }

        public static void SetChecked(this AutomationElement parent, string name, bool value)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("CheckBox '" + name + "' not found");
            }
            if (!box.Current.IsEnabled)
            {
                throw new Exception("CheckBox '" + name + "' is not enabled");
            }

            TogglePattern p = (TogglePattern)box.GetCurrentPattern(TogglePattern.Pattern);
            if (value)
            {
                if (p.Current.ToggleState != ToggleState.On)
                {
                    p.Toggle();
                }
            }
            else
            {
                if (p.Current.ToggleState == ToggleState.On)
                {
                    p.Toggle();
                }
            }
        }

        public static void SetRadioButton(this AutomationElement parent, string automationId, bool value)
        {
            if (!value)
            {
                throw new ArgumentException("Cannot clear a radio button", "value");
            }

            AutomationElement radio = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            if (radio == null)
            {
                throw new Exception("RadioButton '" + automationId + "' not found");
            }
            if (!radio.Current.IsEnabled)
            {
                throw new Exception("RadioButton '" + automationId + "' is not enabled");
            }

            SelectionItemPattern p = (SelectionItemPattern)radio.GetCurrentPattern(SelectionItemPattern.Pattern);
            p.Select();
        }

        public static bool GetRadioButton(this AutomationElement parent, string automationId)
        {
            AutomationElement radio = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
            if (radio == null)
            {
                throw new Exception("RadioButton '" + automationId + "' not found");
            }
            if (!radio.Current.IsEnabled)
            {
                throw new Exception("RadioButton '" + automationId + "' is not enabled");
            }

            SelectionItemPattern p = (SelectionItemPattern)radio.GetCurrentPattern(SelectionItemPattern.Pattern);
            return p.Current.IsSelected;
        }

        public static AutomationElement Expand(this AutomationElement parent, string name)
        {
            AutomationElement expando = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (expando == null)
            {
                throw new Exception("Expander '" + name + "' not found");
            }

            for (int retries = 10; retries > 0; retries--)
            {
                ExpandCollapsePattern p = (ExpandCollapsePattern)expando.GetCurrentPattern(ExpandCollapsePattern.Pattern);
                p.Expand();

                // either WPF or Automation is broken here because sometimes it does not expand.
                Thread.Sleep(250);
                if (p.Current.ExpandCollapseState == ExpandCollapseState.Expanded)
                {
                    break;
                }
                else
                {
                    Console.WriteLine("Expander '" + name + "' failed, trying again");
                }
            }


            return expando;
        }

        public static string GetComboBoxSelection(this AutomationElement parent, string name)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("ComboBox '" + name + "' not found");
            }
            if (!box.Current.IsEnabled)
            {
                throw new Exception("ComboBox '" + name + "' is not enabled");
            }

            SelectionPattern si = (SelectionPattern)box.GetCurrentPattern(SelectionPattern.Pattern);
            AutomationElement[] array = si.Current.GetSelection();
            if (array == null || array.Length == 0)
            {
                return "";
            }

            AutomationElement item = array[0];
            ValuePattern sip = (ValuePattern)item.GetCurrentPattern(ValuePattern.Pattern);
            return sip.Current.Value;
        }

        public static string GetComboBoxText(this AutomationElement parent, string name)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("ComboBox '" + name + "' not found");
            }
            if (!box.Current.IsEnabled)
            {
                throw new Exception("ComboBox '" + name + "' is not enabled");
            }

            // editable combo boxes expose a ValuePattern.
            ValuePattern sip = (ValuePattern)box.GetCurrentPattern(ValuePattern.Pattern);
            return sip.Current.Value;
        }

        public static void SetComboBoxText(this AutomationElement parent, string name, string value)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("ComboBox '" + name + "' not found");
            }
            if (!box.Current.IsEnabled)
            {
                throw new Exception("ComboBox '" + name + "' is not enabled");
            }

            // editable combo boxes expose a ValuePattern.
            ValuePattern sip = (ValuePattern)box.GetCurrentPattern(ValuePattern.Pattern);
            sip.SetValue(value);
        }

        public static AutomationElement SetComboBox(this AutomationElement parent, string name, string value)
        {
            AutomationElement box = parent.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("ComboBox '" + name + "' not found");
            }
            if (!box.Current.IsEnabled)
            {
                throw new Exception("ComboBox '" + name + "' is not enabled");
            }

            ExpandCollapsePattern p = (ExpandCollapsePattern)box.GetCurrentPattern(ExpandCollapsePattern.Pattern);
            p.Expand();
            Thread.Sleep(250);

            AutomationElement item = box.FindFirstWithRetries(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.NameProperty, value),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)));
            if (item == null)
            {
                throw new Exception("ComboBoxItem '" + value + "' not found in combo box '" + name + "'");
            }

            SelectionItemPattern si = (SelectionItemPattern)item.GetCurrentPattern(SelectionItemPattern.Pattern);
            si.Select();

            p.Collapse();
            return box;
        }

        public static AutomationElement FindChildWindow(this AutomationElement parent, string name, int retries)
        {
            for (int i = 0; i < retries; i++)
            {
                AutomationElement childWindow = parent.FindFirst(TreeScope.Descendants,
                    new AndCondition(new PropertyCondition(AutomationElement.ClassNameProperty, "Window"),
                                     new PropertyCondition(AutomationElement.NameProperty, name)));

                if (childWindow != null)
                {
                    return childWindow;
                }

                Thread.Sleep(250);

                // this is needed to pump events so we actually get the new window we're looking for
                System.Windows.Forms.Application.DoEvents();
            }

            return null;
        }

        public static AutomationElement FindChildMenuPopup(this AutomationElement parent, int retries)
        {
            for (int i = 0; i < retries; i++)
            {
                foreach (AutomationElement popup in parent.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Popup")))
                {
                    if (!popup.Current.IsOffscreen)
                    {
                        return popup;
                    }
                }

                Thread.Sleep(250);

                // this is needed to pump events so we actually get the new window we're looking for
                System.Windows.Forms.Application.DoEvents();
            }

            return null;
        }


        public static AutomationElement FindChildContextMenu(this AutomationElement parent, int retries)
        {
            for (int i = 0; i < retries; i++)
            {
                foreach (AutomationElement popup in parent.FindAll(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.ClassNameProperty, "ContextMenu")))
                {
                    if (!popup.Current.IsOffscreen)
                    {
                        return popup;
                    }
                }

                Thread.Sleep(250);

                // this is needed to pump events so we actually get the new window we're looking for
                System.Windows.Forms.Application.DoEvents();
            }

            return null;
        }

        public static AutomationElement FindImage(this AutomationElement parent, int retries = 5)
        {

            for (; retries > 0; retries--)
            {
                AutomationElement e = parent.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "Image"));
                if (e != null)
                {
                    return e;
                }
                if (retries > 0)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            return null;
        }

        public static AutomationElement FindRichText(this AutomationElement parent, int retries = 5)
        {
            for (; retries > 0; retries--)
            {
                AutomationElement e = parent.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "RichTextBox"));
                if (e != null)
                {
                    return e;
                }
                if (retries > 0)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            return null;
        }

        public static QuickFilterWrapper FindQuickFilter(this AutomationElement parent, int retries = 5)
        {
            for (; retries > 0; retries--)
            {
                foreach (AutomationElement e in parent.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "QuickFilterUX")))
                {
                    if (!e.Current.IsOffscreen)
                    {
                        return new QuickFilterWrapper(e);
                    }
                }

                Thread.Sleep(250);
            }
            return null;
        }

    }
}
