using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Threading;

namespace Walkabout.Tests.Wrappers
{
    public class DialogWrapper
    {
        /*
        SystemMenuBar
	        Restore: 61728
	        Move: 61456
	        Size: 61440
	        Minimize: 61472
	        Maximize: 61488
	        Close: 61536
        */
        protected AutomationElement window;

        public DialogWrapper(AutomationElement window)
        {
            this.window = window;
        }

        public AutomationElement Element { get { return window; } }

        public void Close()
        {
            ClickButton("CloseButton");
        }

        public void Minimize()
        {
            ClickButton("MinimizeButton");
        }

        public void Maximize()
        {
            ClickButton("MaximizeRestoreButton");
        }

        public bool IsButtonEnabled(string name)
        {
            AutomationElement button = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (button == null)
            {
                throw new Exception("Button '" + name + "' not found");
            }

            return button.Current.IsEnabled;
        }

        protected AutomationElement ClickButton(string name)
        {
            AutomationElement button = null;
            for (int retries = 5; retries > 0; retries--)
            {
                button = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
                if (button != null && button.Current.IsEnabled)
                {
                    break;
                }
                window.SetFocus();
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

        protected AutomationElement SelectTab(string name)
        {
            AutomationElement tab = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (tab == null)
            {
                throw new Exception("Tab '" + name + "' not found");
            } 
            SelectionItemPattern selectionItem = (SelectionItemPattern)tab.GetCurrentPattern(SelectionItemPattern.Pattern);
            selectionItem.Select();
            return tab;
        }

        protected string GetTextBox(string name)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        protected AutomationElement SetTextBox(string name, string value)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        protected bool IsChecked(string name)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
            if (box == null)
            {
                throw new Exception("CheckBox '" + name + "' not found");
            }

            TogglePattern p = (TogglePattern)box.GetCurrentPattern(TogglePattern.Pattern);
            return p.Current.ToggleState == ToggleState.On;
        }

        protected void SetChecked(string name, bool value)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        protected void SetRadioButton(string automationId, bool value)
        {
            if (!value)
            {
                throw new ArgumentException("Cannot clear a radio button", "value");
            }

            AutomationElement radio = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
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

        protected bool GetRadioButton(string automationId)
        {
            AutomationElement radio = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
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

        protected AutomationElement Expand(string name)
        {
            AutomationElement expando = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        protected string GetComboBoxSelection(string name)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        protected string GetComboBoxText(string name)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        protected void SetComboBoxText(string name, string value)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        protected AutomationElement SetComboBox(string name, string value)
        {
            AutomationElement box = window.FindFirstWithRetries(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, name));
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

        public AutomationElement FindChildWindow(string name, int retries)
        {
            for (int i = 0; i < retries; i++)
            {
                AutomationElement childWindow = window.FindFirst(TreeScope.Descendants,
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

        public AutomationElement FindChildMenuPopup(int retries)
        {
            for (int i = 0; i < retries; i++)
            {
                foreach (AutomationElement popup in window.FindAll(TreeScope.Descendants,
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


        public AutomationElement FindChildContextMenu( int retries)
        {
            for (int i = 0; i < retries; i++)
            {
                foreach (AutomationElement popup in window.FindAll(TreeScope.Descendants,
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

        public bool HasModalChildWindow
        {
            get
            {
                AutomationElement childWindow = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "Window"));
                if (childWindow != null && !childWindow.Current.IsOffscreen)
                {
                    WindowPattern wp = (WindowPattern)childWindow.GetCurrentPattern(WindowPattern.Pattern);
                    return wp.Current.IsModal;
                }
                return false;
            }
        }

        public bool IsBlocked
        {
            get
            {
                return State == WindowInteractionState.BlockedByModalWindow || HasModalChildWindow;
            }
        }

        public bool IsInteractive
        {
            get
            {

                return State == WindowInteractionState.ReadyForUserInteraction || State == WindowInteractionState.Running && !HasModalChildWindow;
            }
        }

        public bool IsNotResponding
        {
            get
            {

                return State == WindowInteractionState.NotResponding;
            }
        }

        private WindowInteractionState State
        {
            get
            {
                WindowPattern wp = (WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern);
                return wp.Current.WindowInteractionState;
            }
        }

        public void WaitForInputIdle(int milliseconds)
        {
            WindowPattern wp = (WindowPattern)window.GetCurrentPattern(WindowPattern.Pattern);
            wp.WaitForInputIdle(milliseconds);
        }
    }
}
