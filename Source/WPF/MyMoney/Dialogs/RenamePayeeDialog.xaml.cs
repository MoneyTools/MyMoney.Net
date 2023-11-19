using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Walkabout.Data;
using Walkabout.Utilities;
using Walkabout.Views;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for RenamePayeeDialog.xaml
    /// </summary>
    public partial class RenamePayeeDialog : BaseDialog
    {
        #region PROPERTIES

        private readonly EventHandler<ChangeEventArgs> handler;
        private readonly DelayedActions delayedActions = new DelayedActions();

        private MyMoney money;

        public MyMoney MyMoney
        {
            get { return this.money; }
            set
            {
                if (this.money != null)
                {
                    this.money.Payees.Changed -= this.handler;
                }
                this.money = value;
                if (this.money != null)
                {
                    this.money.Payees.Changed -= this.handler;
                    this.money.Payees.Changed += this.handler;
                }
                this.LoadPayees();
            }
        }

        public IServiceProvider ServiceProvider { get; set; }

        public string Pattern
        {
            get { return this.textBox1.Text.Trim(); }
            set { this.textBox1.Text = value; }
        }


        private Payee selectedPayee;

        public Payee Payee
        {
            get { return this.selectedPayee; }
            set
            {
                this.selectedPayee = value;
                this.textBox1.Text = value != null ? value.Name : string.Empty;
                this.comboBox1.SelectedItem = value;
            }
        }

        public Payee RenameTo
        {
            set
            {
                this.comboBox1.SelectedItem = value;
            }
        }


        public string Value
        {
            get { return this.comboBox1.Text; }
            set { this.comboBox1.Text = value; }
        }

        public bool Alias
        {
            get { return this.checkBoxAuto.IsChecked == true; }
            set { this.checkBoxAuto.IsChecked = value; }
        }

        #endregion



        /// <summary>
        /// Easy wrapper for launching the Rename Payee Dialog
        /// </summary>
        /// <param name="myMoney"></param>
        /// <param name="payeeToRename"></param>
        /// <returns></returns>
        public static RenamePayeeDialog ShowDialogRenamePayee(IServiceProvider sp, MyMoney myMoney, Payee payeeToRename)
        {
            return ShowDialogRenamePayee(sp, myMoney, payeeToRename, payeeToRename);
        }

        /// <summary>
        /// Easy wrapper for launching the Rename Payee Dialog
        /// This version allows you to set the "Rename To" field, used in the Drag and Drop scenario
        /// </summary>
        /// <param name="myMoney"></param>
        /// <param name="fromPayee"></param>
        /// <param name="renameToThisPayee"></param>
        /// <returns></returns>
        public static RenamePayeeDialog ShowDialogRenamePayee(IServiceProvider sp, MyMoney myMoney, Payee fromPayee, Payee renameToThisPayee)
        {
            RenamePayeeDialog dialog = new RenamePayeeDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.MyMoney = myMoney;
            dialog.ServiceProvider = sp;
            dialog.Payee = fromPayee;
            dialog.RenameTo = renameToThisPayee;
            return dialog;
        }


        /// <summary>
        /// Constructor
        /// </summary>
        public RenamePayeeDialog()
        {
            this.handler = new EventHandler<ChangeEventArgs>(this.OnPayees_Changed);

            this.InitializeComponent();

            this.okButton.Click += new RoutedEventHandler(this.OnOkButton_Click);

            this.comboBox1.TextChanged += new RoutedEventHandler(this.OnComboBox1_TextChanged);
            this.textBox1.TextChanged += this.OnPatternTextChanged;
            this.checkBoxUseRegex.Checked += this.OnRegexChanged;
            this.checkBoxUseRegex.Unchecked += this.OnRegexChanged;
            this.checkBoxAuto.Checked += this.OnAutoChanged;
            this.checkBoxAuto.Unchecked += this.OnAutoChanged;
        }

        private void EnableButtons()
        {
            this.okButton.IsEnabled = !string.IsNullOrWhiteSpace(this.textBox1.Text) && !string.IsNullOrWhiteSpace(this.comboBox1.Text);
        }

        private void OnAutoChanged(object sender, RoutedEventArgs e)
        {
            this.CheckState();
        }

        private void OnRegexChanged(object sender, RoutedEventArgs e)
        {
            this.CheckState();
        }

        private void OnPatternTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            this.CheckState();
        }

        private void CheckState()
        {
            this.EnableButtons();
            if (!string.IsNullOrWhiteSpace(this.textBox1.Text))
            {
                this.delayedActions.StartDelayedAction("CheckConflicts", this.CheckConflicts, TimeSpan.FromMilliseconds(50));
            }
        }

        private void OnComboBox1_TextChanged(object sender, RoutedEventArgs e)
        {
            this.EnableButtons();
        }

        private void CheckConflicts()
        {
            bool foundConflicts = false;
            if (this.Alias)
            {
                AliasType atype = this.checkBoxUseRegex.IsChecked == true ? AliasType.Regex : AliasType.None;
                Alias a = new Alias() { Pattern = this.Pattern, AliasType = atype };
                IEnumerable<Alias> conflicts = this.money.FindSubsumedAliases(a);
                if (conflicts.Count() > 0)
                {
                    foundConflicts = true;
                    this.ClashPromp.Visibility = Visibility.Visible;
                    this.ClashingAliases.ItemsSource = (from i in conflicts select i.Pattern).ToList();
                    this.ClashingAliases.Visibility = Visibility.Visible;
                }
            }

            if (!foundConflicts)
            {
                this.ClashPromp.Visibility = Visibility.Collapsed;
                this.ClashingAliases.ItemsSource = null;
                this.ClashingAliases.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            if (this.money != null)
            {
                this.money.Payees.Changed -= this.handler;
                this.money = null;
            }

            base.OnClosed(e);
        }

        private void OnOkButton_Click(object sender, RoutedEventArgs e)
        {
            Payee q = this.money.Payees.FindPayee(this.Value, true);
            string pattern = this.Pattern;
            bool cancelled = true;
            bool added = false;
            cancelled = false;
            if (pattern != q.Name)
            {
                AliasType atype = this.checkBoxUseRegex.IsChecked == true ? AliasType.Regex : AliasType.None;
                Alias a = null;
                if (this.Alias)
                {
                    a = this.money.Aliases.FindAlias(pattern);
                    if (a == null)
                    {
                        // map this alias to the payee just chosen by the user.
                        a = new Alias();
                        a.Pattern = pattern;
                        a.Payee = q;
                        added = true;
                    }
                    else
                    {
                        a.Payee = q;
                    }
                    a.AliasType = atype;
                }
                else
                {
                    // create temporary alias object for GetTransactionsByAlias.
                    a = new Alias();
                    a.Pattern = pattern;
                    a.AliasType = atype;
                    a.Payee = q;
                }

                Debug.Assert(a.Payee != null);

                if (a.Matches(q.Name))
                {
                    // return the new payee 
                    this.selectedPayee = a.Payee;
                }

                IEnumerable<Transaction> transactions = this.money.Transactions.GetAllTransactions();
                if (this.ServiceProvider != null)
                {
                    // make sure we search visible transactions being edited as well.
                    TransactionCollection viewModel = this.ServiceProvider.GetService(typeof(TransactionCollection)) as TransactionCollection;
                    if (viewModel != null)
                    {
                        transactions = transactions.Concat(viewModel);
                    }
                }

                // Now see if we have any matches
                IEnumerable<PersistentObject> result = this.money.FindAliasMatches(a, transactions);
                if (!result.Any())
                {
                    if (MessageBoxResult.Cancel == MessageBoxEx.Show("No matching transactions found (that don't already have the target Payee), do you want to save it anyway?", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning))
                    {
                        this.textBox1.Focus();
                        this.textBox1.SelectAll();
                        cancelled = true;
                    }
                }
                if (!cancelled)
                {
                    List<Alias> subsumed = new List<Alias>(this.money.FindSubsumedAliases(a));

                    HashSet<string> conflicts = new HashSet<string>();
                    foreach (var b in subsumed)
                    {
                        if (b.Payee != a.Payee)
                        {
                            conflicts.Add(b.Payee.Name);
                        }
                    }
                    if (conflicts.Count > 0)
                    {
                        List<string> sorted = new List<string>(conflicts);
                        bool truncated = false;
                        if (sorted.Count > 10)
                        {
                            sorted.RemoveRange(10, sorted.Count - 10);
                            truncated = true;
                        }
                        sorted.Sort();
                        if (truncated)
                        {
                            sorted.Add("...");
                        }

                        if (MessageBoxResult.Cancel == MessageBoxEx.Show("There are subsumed Aliases that map to " + conflicts.Count + " different Payees: [" +
                            string.Join(", ", sorted) + "].  Are you sure you want to continue with this rename?",
                            "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning))
                        {
                            this.textBox1.Focus();
                            this.textBox1.SelectAll();
                            cancelled = true;
                        }
                    }

                    if (!cancelled)
                    {
                        foreach (var b in subsumed)
                        {
                            this.money.Aliases.RemoveAlias(b);
                        }

                        if (added)
                        {
                            this.money.Aliases.AddAlias(a);
                        }
                        // Now the user really wants to switch all transactions
                        // referencing p over to q, then remove p.
                        int count = this.money.ApplyAlias(a, transactions);
                    }
                }
            }
            else
            {
                // warn the user that they changed nothing?
            }

            if (!cancelled)
            {
                this.DialogResult = true;
                this.Close();
            }

        }

        private void OnPayees_Changed(object sender, ChangeEventArgs args)
        {
            this.LoadPayees();
        }

        private void LoadPayees()
        {
            if (this.money != null)
            {
                List<Payee> a = (List<Payee>)this.money.Payees.GetPayees();
                a.Sort(new PayeeComparer());
                this.comboBox1.ItemsSource = a;
            }
        }

        private void CamelCaseButton_Click(object sender, RoutedEventArgs e)
        {
            string text = this.textBox1.Text;
            this.comboBox1.Text = text.CamelCase();
        }
    }
}
