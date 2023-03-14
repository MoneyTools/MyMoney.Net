using NUnit.Framework;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Windows.Automation;
using System.Windows.Input;
using System.Xml.Linq;
using Walkabout.Data;
using Walkabout.Tests.Interop;

namespace Walkabout.Tests.Wrappers
{
    public class TransactionViewWrapper : GridViewWrapper
    {
        private static readonly Dictionary<string, GridViewColumnWrappers> mapping;

        static TransactionViewWrapper()
        {
            mapping = new Dictionary<string, GridViewColumnWrappers>();
            mapping["TheGrid_BankTransactionDetails"] = new GridViewColumnWrappers(
                    new GridViewColumnWrapper("A", "Attachment", "Button"),
                    new GridViewColumnWrapper("Num", "Number", "TextBox"),
                    new GridViewColumnWrapper("Date", "Date", "DatePicker"),
                    new GridViewColumnWrapper("C", "Color", "Custom"),
                    new CompoundGridViewColumnWrapper("Payee/Category/Memo",
                        new GridViewColumnWrapper("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),
                    new GridViewColumnWrapper("S", "Status", "Button"),
                    new GridViewColumnWrapper("Sales Tax", "SalesTax", "TextBox"),
                    new CompoundGridViewColumnWrapper("Payment",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("Payment", "Payment", "TextBox")
                    ),
                    new CompoundGridViewColumnWrapper("Deposit",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("Deposit", "Deposit", "TextBox")
                    )
                );


            mapping["TheGrid_InvestmentActivity"] = new GridViewColumnWrappers(
                    new GridViewColumnWrapper("A", "Attachment", "Button"),
                    new GridViewColumnWrapper("Num", "Number", "TextBox"),
                    new GridViewColumnWrapper("Date", "Date", "DatePicker"),
                    new GridViewColumnWrapper("C", "Color", "Custom"),
                    new CompoundGridViewColumnWrapper("Payee/Category/Memo",
                        new GridViewColumnWrapper("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),

                    new GridViewColumnWrapper("Activity", "Activity", "ComboBox"),
                    new GridViewColumnWrapper("Security", "Security", "ComboBox"),
                    new GridViewColumnWrapper("Units", "Units", "TextBox"),
                    new GridViewColumnWrapper("UnitPrice", "UnitPrice", "TextBox"),

                    new GridViewColumnWrapper("S", "Status", "Button"),
                    new GridViewColumnWrapper("Sales Tax", "SalesTax", "TextBox"),
                    new CompoundGridViewColumnWrapper("Payment",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("", "Payment", "TextBox")
                    ),
                    new CompoundGridViewColumnWrapper("Deposit",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("", "Deposit", "TextBox")
                    )
                );

            mapping["TheGrid_TransactionFromDetails"] = new GridViewColumnWrappers(
                    new GridViewColumnWrapper("A", "Attachment", "Button"),
                    new GridViewColumnWrapper("Account", "Account", "TextBlock"),
                    new GridViewColumnWrapper("Num", "Number", "TextBox"),
                    new GridViewColumnWrapper("Date", "Date", "DatePicker"),
                    new GridViewColumnWrapper("C", "Color", "Custom"),
                    new CompoundGridViewColumnWrapper("Payee/Category/Memo",
                        new GridViewColumnWrapper("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),
                    new GridViewColumnWrapper("S", "Status", "Button"),
                    new CompoundGridViewColumnWrapper("Payment",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("", "Payment", "TextBox")
                    ),
                    new CompoundGridViewColumnWrapper("Deposit",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("", "Deposit", "TextBox")
                    )
            );

            mapping["TheGrid_BySecurity"] = new GridViewColumnWrappers(
                    new GridViewColumnWrapper("Account", "Account", "TextBlock"),
                    new GridViewColumnWrapper("Date", "Date", "DatePicker"),
                    new CompoundGridViewColumnWrapper("Payee/Category/Memo",
                        new GridViewColumnWrapper("", "Payee", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Category", "ComboBox"), // ExpandCollapse, Selection, Value
                        new GridViewColumnWrapper("", "Memo", "TextBox") // ExpandCollapse, Selection, Value
                    ),
                    new GridViewColumnWrapper("Security", "Security", "ComboBox"),
                    new GridViewColumnWrapper("Activity", "Activity", "ComboBox"),
                    new GridViewColumnWrapper("FIFO", "FIFO", "Custom"),
                    new GridViewColumnWrapper("Units", "Units", "TextBox"),
                    new GridViewColumnWrapper("Units A.S.", "UnitsAS", "TextBlock"),
                    new GridViewColumnWrapper("Holding", "Holding", "TextBlock"),
                    new GridViewColumnWrapper("Unit Price", "UnitPrice", "TextBlock"),
                    new GridViewColumnWrapper("Price A.S.", "UnitPriceAS", "TextBlock"),
                    new GridViewColumnWrapper("S", "Status", "Button"),
                    new CompoundGridViewColumnWrapper("Payment",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("", "Payment", "TextBox")
                    ),
                    new CompoundGridViewColumnWrapper("Deposit",
                        new GridViewColumnWrapper("", "Splits", "Button"),
                        new GridViewColumnWrapper("", "Deposit", "TextBox")
                    )
                );

        }

        public TransactionViewWrapper(MainWindowWrapper window, AutomationElement control) : base(window, control)
        {
        }

        public bool IsEditable
        {
            get
            {
                return this.IsBankAccount || this.IsInvestmentAccount;
            }
        }

        public bool IsBankAccount
        {
            get
            {
                string name = this.Control.Current.AutomationId;
                return name == "TheGrid_BankTransactionDetails";
            }
        }

        public bool IsInvestmentAccount
        {
            get
            {
                string name = this.Control.Current.AutomationId;
                return name == "TheGrid_InvestmentActivity";
            }
        }

        public bool HasTransactions
        {
            get
            {
                foreach (AutomationElement e in this.Control.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)))
                {
                    if (e.Current.Name == "Walkabout.Data.Transaction" || e.Current.Name == "{NewItemPlaceholder}")
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public override GridViewRowWrapper WrapRow(AutomationElement e, int index)
        {
            return new TransactionViewRow(this, e, index);
        }

        public override GridViewColumnWrappers Columns
        {
            get
            {
                string gridName = this.Control.Current.AutomationId;
                GridViewColumnWrappers cols;
                if (!mapping.TryGetValue(gridName, out cols))
                {
                    throw new Exception("DataGrid named '" + gridName + "' has no mapping");
                }
                this.Columns = cols;
                return cols;
            }
            set
            {
                base.Columns = value;
            }
        }

        internal void EnsureSortByDate()
        {
            this.SortBy(this.Columns.GetColumn("Date"));
        }

        /// <summary>
        /// Returns a disconnected single Transaction object deserialized from the data
        /// in the clipboard.
        /// </summary>
        internal Walkabout.Data.Transaction GetSelectedTransactionProxy()
        {
            var xml = this.GetSelectedTransactionXml();
            var doc = XDocument.Parse(xml);

            XNamespace ns = XNamespace.Get("http://schemas.vteam.com/Money/2010");
            XElement transactionElement = doc.Document.Root.Element(ns + "Transaction");
            Assert.IsNotNull(transactionElement, "XML is missing the Transaction info, is it a placeholder?");

            var s = new DataContractSerializer(typeof(Transaction), MyMoney.GetKnownTypes());
            Transaction t = (Transaction)s.ReadObject(transactionElement.CreateReader());

            return t;
        }

        internal string GetSelectedTransactionXml()
        {
            var selection = this.ScrollSelectionIntoView();
            for (int retries = 5; retries > 0; retries--)
            {
                try
                {
                    this.Focus();
                    selection.Focus();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("### Error setting focus on selection: " + ex.Message);
                    selection = selection.Refresh();
                }

                Thread.Sleep(50);
                Input.TapKey(Key.C, ModifierKeys.Control); // send Ctrl+C to copy row as XML
                Thread.Sleep(50);

                // key sending is completely async, so we have to give it time to arrive and be processed.
                for (int innerRetries = 5; innerRetries > 0; innerRetries--)
                {
                    try
                    {
                        if (Clipboard.ContainsText())
                        {
                            try
                            {
                                var xml = Clipboard.GetText();
                                if (xml.Contains("<Transaction"))
                                {
                                    return xml;
                                }
                            }
                            catch (Exception)
                            {
                                // ignore non xml data on the clipboard.
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                    Thread.Sleep(50);
                }
            }

            throw new Exception("Cannot seem to get XML from selected row");
        }

        internal void NavigateTransfer()
        {
            var selection = (TransactionViewRow)this.ScrollSelectionIntoView();
            if (selection == null)
            {
                return;
            }

            string tofrom;
            string sourceAccount = selection.ParseTransferPayee(out tofrom);
            decimal amount = selection.GetAmount();

            if (sourceAccount == null)
            {
                throw new Exception("Source account not found");
            }

            for (int retries = 5; retries > 0; retries--)
            {
                try
                {
                    this.Focus();
                    selection.Focus();
                }
                catch (Exception ex)
                {
                    Trace.WriteLine("### Error setting focus on selection: " + ex.Message);
                    selection = (TransactionViewRow)this.ScrollSelectionIntoView().Refresh();
                }

                Thread.Sleep(50);
                Input.TapKey(Key.F12);
                // key sending is completely async, so we have to give it time to arrive and be processed.
                Thread.Sleep(50);

                // update the new "transactions" grid in place.
                this.Control = this.Window.FindTransactionGrid().Control;

                // now verify we got a new selection
                var target = (TransactionViewRow)this.ScrollSelectionIntoView();
                if (target == null)
                {
                    throw new Exception("Navigation lost the selection");
                }

                string fromto;
                string targetAccount = target.ParseTransferPayee(out fromto);
                decimal targetAmount = target.GetAmount();

                if (sourceAccount == targetAccount && fromto == tofrom)
                {
                    // hasn't got the F12 yet.
                }
                else if (Math.Round(targetAmount, 2) != -Math.Round(amount, 2))
                {
                    // we jumped to the wrong place then!
                    throw new Exception($"F12 jumped to the wrong transaction, expecting account {targetAccount} with amount {-targetAmount}");
                }

                if (sourceAccount != null && targetAccount != null && ((tofrom == "to" && fromto == "from") || (tofrom == "from" && fromto == "to")))
                {
                    return;
                }

                // not there yet...so try again...
                Thread.Sleep(100);
            }

            throw new Exception("Timeout waiting for F12 to work");
        }

        internal void Export(string name)
        {
            this.Control = this.Window.FindTransactionGrid().Control;
            this.Control.SetFocus();
            ContextMenu menu = new ContextMenu(this.Control, true);
            menu.InvokeMenuItem("menuItemExport");
        }
    }

    public class TransactionViewRow : GridViewRowWrapper
    {
        private readonly TransactionViewWrapper view;

        public TransactionViewRow(TransactionViewWrapper view, AutomationElement item, int index) : base(view, item, index)
        {
            this.view = view;
        }

        internal AttachmentDialogWrapper ClickAttachmentsButton()
        {
            this.ScrollIntoView();

            // AutomationId:	"CommandScanAttachment"
            try
            {
                var cell = this.GetCell("Attachment");
                cell.Invoke();
            }
            catch (Exception ex)
            {
                throw new Exception("Cannot find the Attachment column: " + ex.Message);
            }

            AutomationElement dialog = this.view.Window.Element.FindChildWindow("Attachments", 5);
            if (dialog == null)
            {
                throw new Exception("Cannot find the AttachmentDialog");
            }
            return new AttachmentDialogWrapper(dialog);
        }

        internal string GetCheckNumber()
        {
            var cell = this.GetCell("Number");
            return cell.GetValue();
        }
        internal void SetCheckNumber(string num)
        {
            var cell = this.GetCell("Number");
            cell.SetValue(num);
        }

        internal string GetDate()
        {
            var cell = this.GetCell("Date");
            return cell.GetValue();
        }
        internal void SetDate(string dateTime)
        {
            var cell = this.GetCell("Date");
            cell.SetValue(dateTime);
        }

        internal string GetPayee()
        {
            var cell = this.GetCell("Payee");
            return cell.GetValue();
        }
        internal void SetPayee(string payee)
        {
            var cell = this.GetCell("Payee");
            cell.SetValue(payee);
        }

        internal string GetCategory()
        {
            var cell = this.GetCell("Category");
            return cell.GetValue();
        }
        internal void SetCategory(string category)
        {
            var cell = this.GetCell("Category");
            cell.SetValue(category);
        }
        internal string GetMemo()
        {
            var cell = this.GetCell("Memo");
            return cell.GetValue();
        }
        internal void SetMemo(string memo)
        {
            var cell = this.GetCell("Memo");
            cell.SetValue(memo);
        }
        internal decimal GetSalesTax()
        {
            return this.GetDecimalColumn("SalesTax");
        }

        internal void SetSalesTax(decimal tax)
        {
            var cell = this.GetCell("SalesTax");
            cell.SetValue(tax == 0 ? "" : tax.ToString());
        }
        internal decimal GetPayment()
        {
            return this.GetDecimalColumn("Payment");
        }
        internal decimal GetDeposit()
        {
            return this.GetDecimalColumn("Deposit");
        }

        internal decimal GetAmount()
        {
            int sign = 1;
            var cell = this.GetCell("Deposit");
            string s = cell.GetValue();
            if (string.IsNullOrEmpty(s))
            {
                sign = -1;
                cell = this.GetCell("Payment");
                s = cell.GetValue();
            }
            decimal p = 0;
            decimal.TryParse(s, out p);
            return p * sign;
        }

        internal void SetAmount(decimal amount)
        {
            if (amount < 0)
            {
                var cell = this.GetCell("Payment");
                cell.SetValue(amount == 0 ? "" : (-amount).ToString());
            }
            else
            {
                var cell = this.GetCell("Deposit");
                cell.SetValue(amount == 0 ? "" : amount.ToString());
            }
        }

        // investment transactions
        internal string GetActivity()
        {
            var cell = this.GetCell("Activity");
            return cell.GetValue();
        }

        internal void SetActivity(string activity)
        {
            var cell = this.GetCell("Activity");
            cell.SetValue(activity);
        }

        internal string GetSecurity()
        {
            var cell = this.GetCell("Security");
            return cell.GetValue();
        }
        internal void SetSecurity(string security)
        {
            var cell = this.GetCell("Security");
            cell.SetValue(security);
        }

        internal decimal GetUnits()
        {
            return this.GetDecimalColumn("Units");
        }

        internal void SetUnits(decimal units)
        {
            var cell = this.GetCell("Units");
            cell.SetValue(units == 0 ? "" : units.ToString());
        }

        internal decimal GetUnitPrice()
        {
            return this.GetDecimalColumn("UnitPrice");
        }
        internal void SetUnitPrice(decimal price)
        {
            var cell = this.GetCell("UnitPrice");
            cell.SetValue(price == 0 ? "" : price.ToString());
        }

        // todo: toggle status button
        // todo: edit splits.

        public bool IsTransfer
        {
            get
            {
                try
                {
                    string payee = this.GetPayee();
                    return payee.StartsWith("Transfer");
                }
                catch
                {
                    // Sometimes get "Invoke operation failed. Cannot edit another cell or row while the current one has validation errors"
                    return false;
                }
            }
        }

        /// <summary>
        /// Parse the transfer string 
        /// </summary>
        /// <param name="tofrom">The string 'to' or 'from' dependending on direction of transfer</param>
        /// <returns>The account that we are transfering to or from</returns>
        public string ParseTransferPayee(out string tofrom)
        {
            string payee = this.GetPayee();

            if (payee.StartsWith("Transfer"))
            {
                payee = payee[0..8].Trim();
                int i = payee.IndexOf(':');
                if (i > 0)
                {
                    tofrom = payee.Substring(0, i);
                    return payee.Substring(i + 1).Trim();
                }
            }

            throw new Exception("Expecting a Transfer transaction");
        }

        internal override void BeginEdit()
        {
            var payee = this.GetPayee();
            this.SetPayee(payee);
        }
    }
}
