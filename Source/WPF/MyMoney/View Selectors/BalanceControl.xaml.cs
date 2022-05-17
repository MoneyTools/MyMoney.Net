using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Walkabout.Attachments;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Views.Controls
{
    /// <summary>
    /// Interaction logic for BalanceControl.xaml
    /// </summary>
    public partial class BalanceControl : UserControl
    {
        MyMoney myMoney;
        Account account;
        private StatementManager statements;
        Category interestCategory;
        Transaction interestTransaction;
        bool weAddedInterest;
        bool played;
        bool initializing;
        decimal lastBalance;
        List<string> previousReconciliations;
        bool eventWired;
        private StatementItem statement;

        public event EventHandler<BalanceEventArgs> Balanced;

        public BalanceControl()
        {
            InitializeComponent();

            this.TextBoxStatementBalance.LostFocus += new RoutedEventHandler(this.StatementBalance_LostFocus);                        
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            RegisterFocusEvents(VisualTreeHelper.GetParent(this) != null);
        }

        private void RegisterFocusEvents(bool register)
        {
            if (register && !eventWired)
            {
                Keyboard.AddPreviewGotKeyboardFocusHandler(this, new KeyboardFocusChangedEventHandler(TooltipTracker));
                eventWired = true;
            }
            else if (!register && eventWired)
            {
                Keyboard.RemoveGotKeyboardFocusHandler(this, new KeyboardFocusChangedEventHandler(TooltipTracker));
                eventWired = false;
            }
        }

        private void TooltipTracker(object sender, KeyboardFocusChangedEventArgs e)
        {
            string text = null;
            DependencyObject d = e.NewFocus as FrameworkElement;
            // make sure this new focus is in our element tree.
            if (d != null && WpfHelper.FindAncestor<BalanceControl>(d) != null)
            {
                // Search up the visual hierarchy for a tooltip.
                object tooltip = WpfHelper.FindInheritedProperty <BalanceControl>(d, FrameworkElement.ToolTipProperty);
                if (tooltip != null)
                {
                    text = tooltip.ToString();
                }
            }
            TextBlockMessage.Text = text;
        }
        

        public void Reconcile(MyMoney money, Account a, StatementManager statementManager)
        {
            this.initializing = true;
            this.myMoney = money;
            this.account = a;
            this.statements = statementManager;

            DateTime oldestUnreconciledDate = DateTime.MaxValue;

            HashSet<DateTime> previous = new HashSet<DateTime>();
            foreach (Transaction t in this.myMoney.Transactions.GetAllTransactions())
            {
                if (t.Account == a )
                {
                    if (t.ReconciledDate.HasValue)
                    {
                        previous.Add(t.ReconciledDate.Value);
                    }
                    else
                    {
                        if (t.Date < oldestUnreconciledDate)
                        {
                            oldestUnreconciledDate = t.Date;
                        }
                    }
                }
            }

            if (previous.Count == 0)
            {
                // Make sure that we have at least one date in the dropdown
                // In order to include the oldest un-reconciled transaction we need to start at least 1 day before the
                // oldest un-reconciled date.
                oldestUnreconciledDate = oldestUnreconciledDate.AddDays(-1);
                previous.Add(oldestUnreconciledDate); 
            }

            foreach (var stmt in statementManager.GetStatements(a))
            {
                previous.Add(stmt.Date);
            }

            previousReconciliations = new List<string>(from d in previous orderby d ascending select d.ToShortDateString());
            previousReconciliations.Add(""); // add one more so user has to move selection to select a previous statement.
            this.ComboBoxPreviousReconcileDates.ItemsSource = previousReconciliations;

            this.AccountInfo.Text = string.Format("{0} ({1})", a.Name, a.AccountId);


            DateTime estdate = a.LastBalance;
            if (estdate == DateTime.MinValue)
            {
                this.SelectedPreviousStatement = oldestUnreconciledDate;
            }
            else
            {
                string lastDate = estdate.ToShortDateString();
                if (!previousReconciliations.Contains(lastDate))
                {
                    previousReconciliations.Add(lastDate);
                }

                this.SelectedPreviousStatement = estdate;
            }
            this.ComboBoxPreviousReconcileDates.SelectedIndex = previousReconciliations.Count - 1;
            estdate = this.SelectedPreviousStatement.AddMonths(1);
            this.ComboBoxPreviousReconcileDates.SelectionChanged += new SelectionChangedEventHandler(ComboBoxPreviousReconcileDates_SelectionChanged);
            
            this.StatementDate = estdate;

            this.myMoney.Transactions.Changed += new EventHandler<ChangeEventArgs>(Transactions_Changed);

            this.interestCategory = (this.account.Type == AccountType.Brokerage) ?
                this.myMoney.Categories.InvestmentInterest : this.myMoney.Categories.InterestEarned;

            FindInterestTransaction(estdate);

            this.StatementDatePicker.KeyDown += new KeyEventHandler(ChildKeyDown);
            this.StatementDatePicker.SelectedDateChanged += new EventHandler<SelectionChangedEventArgs>(OnStatementDateChanged);
            this.TextBoxStatementBalance.KeyDown += new KeyEventHandler(ChildKeyDown);
            this.TextBoxInterestEarned.LostFocus += new RoutedEventHandler(TextBoxInterestEarned_LostFocus);
            this.initializing = false;

            // setup initial values.
            UpdateBalances(estdate);

            this.Loaded += new RoutedEventHandler(OnLoad);
        }


        void FindInterestTransaction(DateTime date)
        {
            this.interestTransaction = null;
            DateTime prevMonth = date.AddMonths(-1);
            IList<Transaction> list = this.myMoney.Transactions.GetTransactionsByCategory(interestCategory, null);
            for (int i = list.Count - 1; i >= 0; i--)
            { // most likely to be at the end of the list.
                Transaction t = (Transaction)list[i];
                if (t.Account == this.account && t.Category == interestCategory && t.Date <= date && t.Date > prevMonth &&
                    t.Status != TransactionStatus.Reconciled && t.Status != TransactionStatus.Void)
                {
                    this.interestTransaction = t;
                    this.InterestEarned = t.Amount;
                    return;
                }
            }
            this.InterestEarned = 0;
        }

        protected void OnLoad(object sender, RoutedEventArgs e)
        {
            this.StatementDatePicker.Focus();
        }

        public void Transactions_Changed(object sender, ChangeEventArgs e)
        {
            try
            {
                decimal d = this.myMoney.ReconciledBalance(this.account, this.StatementDate);
                this.LastBalance = d;
                this.FindInterestTransaction(this.StatementDate);
                this.YourNewBalance = this.myMoney.ReconciledBalance(this.account, this.StatementDate.AddMonths(1));
            }
            catch (Exception)
            {
                // we can get System.InvalidOperationException: Collection was modified; enumeration operation may not execute
                // if a download thread is busily modifying the Money data at the moment...
                // todo: we really need a proper locking mechanism on the Money data to make this stuff safe...
                // or we need a proper journaling system that provides stable versioned snapshots of the money data to 
                // accompany the ChangeEvents so that handlers can operating on each new version.
            }
        }

        public event EventHandler StatementDateChanged;

        void OnStatementDateChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBalances(this.StatementDate);
        }


        /// <summary>
        /// Call once a the beginning to initialized the default values
        /// and once if the user changes the Statement date
        /// </summary>
        /// <param name="statementDate"></param>
        private void UpdateBalances(DateTime statementDate)
        {
            decimal d = this.myMoney.ReconciledBalance(this.account, statementDate);
            this.LastBalance = d;

            this.YourNewBalance = this.myMoney.ReconciledBalance(this.account, statementDate.AddMonths(1));

            // in case we are re-editing a previously reconciled statement.
            this.statement = this.statements.GetStatement(this.account, statementDate);
            var stmt = this.statements.GetStatementFullPath(this.account, statementDate);
            this.StatementFileName.Text = stmt;

            decimal savedBalance = this.statements.GetStatementBalance(this.account, statementDate);
            if (savedBalance != 0)
            {
                this.NewBalance = savedBalance;
            }
            else
            {
                // try and compute the expected balance.
                this.NewBalance = this.myMoney.EstimatedBalance(this.account, statementDate);
            }

            if (StatementDateChanged != null)
            {
                StatementDateChanged(this, EventArgs.Empty);
            }

            CheckDone(false);
        }

        public bool IsLatestStatement
        {
            get
            {
                return (string)this.ComboBoxPreviousReconcileDates.SelectedItem == previousReconciliations.Last();
            }
        }

        public DateTime SelectedPreviousStatement
        {
            get;
            set;
        }

        public DateTime StatementDate
        {
            get { return this.StatementDatePicker.SelectedDate.Value; }
            set
            {
                this.StatementDatePicker.SelectedDate = value;
            }
        }

        public decimal LastBalance
        {
            get
            {
                return this.lastBalance;
            }
            set
            {
                this.lastBalance = value;
                this.TextBlockPreviousBalance.Text = value.ToString("C");
                SetColor(this.TextBlockPreviousBalance, value);
            }
        }

        public decimal NewBalance
        {
            get
            {
                decimal result = 0;
                if (!decimal.TryParse(this.TextBoxStatementBalance.Text, NumberStyles.Currency | NumberStyles.AllowParentheses | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture.NumberFormat, out result))
                {
                    result = 0;
                }
                return result;
            }
            set
            {
                this.TextBoxStatementBalance.Text = value.ToString("C");
                SetColor(this.TextBoxStatementBalance, value);
            }
        }

        public decimal YourNewBalance
        {
            get
            {
                try
                {
                    return decimal.Parse(this.TextBlockCurrentBalance.Text, NumberStyles.Currency | NumberStyles.AllowParentheses | NumberStyles.AllowCurrencySymbol);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            set
            {

                this.TextBlockCurrentBalance.Text = value.ToString("C");
                SetColor(this.TextBlockCurrentBalance, value);
                CheckDone(true);
            }
        }


        public decimal Delta
        {
            get
            {
                try
                {
                    return decimal.Parse(this.TextBlockCurrentDelta.Text, NumberStyles.Currency | NumberStyles.AllowParentheses | NumberStyles.AllowCurrencySymbol);
                }
                catch (Exception)
                {
                    return 0;
                }
            }

            set
            {
                this.TextBlockCurrentDelta.Text = value.ToString("C");
                SetColor(this.TextBlockCurrentDelta, value);
            }
        }

        public decimal InterestEarned
        {
            get
            {
                try
                {
                    return decimal.Parse(this.TextBoxInterestEarned.Text, NumberStyles.Currency | NumberStyles.AllowParentheses | NumberStyles.AllowCurrencySymbol);
                }
                catch (Exception)
                {
                    return 0;
                }
            }
            set
            {
                if (this.interestTransaction != null)
                {
                    this.interestTransaction.Amount = value;
                }
                this.TextBoxInterestEarned.Text = value.ToString("C");
                SetColor(this.TextBoxInterestEarned, value);
            }
        }


        void CheckDone(bool celebrate)
        {
            this.CongratsButton.Visibility = System.Windows.Visibility.Collapsed;

            if (initializing) return;

            if (this.YourNewBalance == -this.NewBalance && this.NewBalance != 0)
            {
                this.ValueSign.Visibility = System.Windows.Visibility.Visible;
                this.ValueSign.ToolTip = string.Format("Click here to change the sign on your new statement balance to '{0}' in order to reconcile this account", -this.NewBalance);
            }
            else
            {
                this.ValueSign.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (this.YourNewBalance == this.NewBalance)
            {
                this.Done.IsEnabled = true;
                if (celebrate)
                {
                    this.TextBlockMessage.Text = "Your account is now balanced, click 'Done' to commit this set of reconciled transactions.";
                }
                else
                {
                    this.TextBlockMessage.Text = "Your account is already balanced.";
                }

                this.CongratsButton.Visibility = System.Windows.Visibility.Visible;
                if (!played && celebrate)
                {
                    played = true;
                }
            }
            else
            {
                if (!this.IsKeyboardFocusWithin)
                {
                    this.TextBlockMessage.Text = "";
                }
                this.Done.IsEnabled = false;
            }

            this.Delta = NewBalance - YourNewBalance;
        }

        void SetColor(FrameworkElement e, decimal d)
        {
            Brush brush = d >= 0 ? AppTheme.Instance.GetThemedBrush("PositiveCurrencyForegroundBrush") :
                AppTheme.Instance.GetThemedBrush("NegativeCurrencyForegroundBrush");

            Control c = e as Control;
            if (c != null)
            {
                c.Foreground = brush;
            }
            else
            {
                TextBlock t = e as TextBlock;
                if (t != null)
                {
                    t.Foreground = brush;
                }
            }

        }

        bool CheckValidDecimal(string name, TextBox textBox)
        {
            string msg = null;
            string s = textBox.Text.Trim();
            decimal d = 0;
            if (s == string.Empty)
            {
                msg = "Please enter a value for " + name;
            }
            else
            {
                try
                {
                    d = decimal.Parse(s, NumberStyles.Currency | NumberStyles.AllowParentheses | NumberStyles.AllowCurrencySymbol);
                    SetColor(textBox, d);
                }
                catch (Exception e)
                {
                    msg = e.Message;
                }
            }
            if (msg != null)
            {
                MessageBoxEx.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }));
                return false;
            }
            return true;
        }

        private void StatementBalance_LostFocus(object sender, RoutedEventArgs e)
        {
            if (this.Visibility != System.Windows.Visibility.Visible || this.TextBoxStatementBalance.IsFocused)
            {
                return;
            }
            if (CheckValidDecimal("new balance", this.TextBoxStatementBalance))
            {
                CheckDone(true);
            }
        }

        void OnDone(bool cancelled)
        {
            bool hasStatement = false;
            try
            {
                this.myMoney.Transactions.Changed -= new EventHandler<ChangeEventArgs>(Transactions_Changed);

                if (!cancelled)
                {
                    var fileName = StatementFileName.Text.Trim('"');
                    if (!string.IsNullOrEmpty(fileName) && !System.IO.File.Exists(fileName))
                    {
                        throw new Exception("File not found: " + fileName);
                    }

                    if (this.statement != null)
                    {
                        hasStatement = this.statements.UpdateStatement(this.account, this.statement, this.StatementDate, fileName, this.YourNewBalance, true);
                    }
                    else
                    {
                        hasStatement = this.statements.AddStatement(this.account, this.StatementDate, fileName, this.YourNewBalance, true);
                    }
                }
            } 
            catch (Exception ex)
            {
                MessageBoxEx.Show(ex.Message, "Error with Statement file", MessageBoxButton.OK, MessageBoxImage.Error);
                StatementFileName.Focus();
                StatementFileName.SelectAll();
                return;
            }
            this.interestTransaction = null;
            if (Balanced != null)
            {
                Balanced(this, new BalanceEventArgs(!cancelled, hasStatement));
            }
        }

        protected void ChildKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                OnDone(true);
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            OnDone(true);
        }

        private void TextBoxInterestEarned_LostFocus(object sender, RoutedEventArgs e)
        {
            decimal value = this.InterestEarned;
            if (this.interestTransaction == null && value != 0)
            {
                Transaction t = this.myMoney.Transactions.NewTransaction(this.account);
                this.interestTransaction = t;
                t.Category = this.interestCategory;
                t.Amount = value;
                t.Payee = this.myMoney.Payees.FindPayee(this.account.Name, true);
                t.Date = this.StatementDate;
                this.myMoney.Transactions.AddTransaction(t);
                this.weAddedInterest = true;
                this.myMoney.Rebalance(this.account);
            }
            if (this.interestTransaction != null)
            {
                this.interestTransaction.Amount = value;
                if (value == 0 && weAddedInterest)
                {
                    this.myMoney.Transactions.RemoveTransaction(this.interestTransaction);
                    this.interestTransaction = null;
                    this.myMoney.Rebalance(this.account);
                }
            }
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (this.StatementDate > this.account.LastBalance)
            {
                this.account.LastBalance = this.StatementDate;
            }
            OnDone(false);
        }

        private void PreviousBalanceHelp_Click(object sender, RoutedEventArgs e)
        {
            TextBlockMessage.Text = (string)TextBlockPreviousBalance.ToolTip;
        }

        private void CurrentBalanceHelp_Click(object sender, RoutedEventArgs e)
        {
            TextBlockMessage.Text = (string)TextBlockCurrentBalance.ToolTip;
        }

        private void CurrentDeltaHelp_Click(object sender, RoutedEventArgs e)
        {
            TextBlockMessage.Text = (string)TextBlockCurrentDelta.ToolTip;
        }


        private void ComboBoxPreviousReconcileDates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // show the transactions from this selected reconcile date so user can debug why the last balance doesn't match.
            foreach (string item in e.AddedItems)
            {
                if (string.IsNullOrEmpty(item))
                {
                    // this is the empty item we added.
                }
                else
                {
                    DateTime date = DateTime.Parse(item);

                    this.SelectedPreviousStatement = date.AddMonths(-1);

                    //
                    // Setting the StatementDate will fire the event to all listeners
                    //
                    this.StatementDate = date;
                    break;
                }
            }
        }

        private void TextBoxStatementBalance_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetColor(this.TextBoxStatementBalance, this.NewBalance);
        }

        /// <summary>
        /// Change the sign of the balance value entered by the user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnBalanceValueSign_Clicked(object sender, RoutedEventArgs e)
        {
            this.NewBalance = -this.NewBalance;
            CheckDone(true);
        }

        private void TextBoxStatementBalance_KeyDown(object sender, KeyEventArgs e)
        {
        }

        private void OnTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private void OnTextBoxPreviewLeftMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            TextBox box = (TextBox)sender;
            if (!box.IsFocused)
            {
                box.Focus();
                // stop mouse up undoing the SelectAll in this case.
                captured = this.CaptureMouse();
                e.Handled = true;
            }
        }

        bool captured;

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (captured)
            {
                this.ReleaseMouseCapture();
                e.Handled = true;
            }
            base.OnMouseLeftButtonUp(e);
        }

        private void OnBrowseStatement(object sender, RoutedEventArgs e)
        {
            OpenFileDialog od = new OpenFileDialog();
            od.Filter = "All Files (*.*)|*.*";
            od.CheckFileExists = true;
            if (od.ShowDialog() == true)
            {
                StatementFileName.Text = od.FileName;
            }
        }
    }

    public class BalanceEventArgs : EventArgs
    {
        bool balanced;
        bool hasStatement;
        public BalanceEventArgs(bool balanced, bool hasStatement)
        {
            this.balanced = balanced;
            this.hasStatement = hasStatement;
        }
        public bool Balanced
        {
            get { return this.balanced; }
        }
        public bool HasStatement
        {
            get { return this.hasStatement; }
        }
    }

}
