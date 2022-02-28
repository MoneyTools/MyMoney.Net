using System;
using System.Linq;
using System.Collections;
using System.Diagnostics;
using System.Windows;
using Walkabout.Data;
using Walkabout.Controls;
using Walkabout.Utilities;
using System.Text;
using System.Collections.Generic;
using Walkabout.Views;

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for RenamePayeeDialog.xaml
    /// </summary>
    public partial class RenamePayeeDialog : BaseDialog
    {
        #region PROPERTIES

        EventHandler<ChangeEventArgs> handler;
        DelayedActions delayedActions = new DelayedActions();

        private MyMoney money;

        public MyMoney MyMoney
        {
            get { return this.money; }
            set
            {
                if (this.money == null && value != null)
                {
                    value.Payees.Changed += handler;
                }
                this.money = value;
                LoadPayees();
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
            get { return this.checkBoxAuto.IsChecked==true; }
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
            handler = new EventHandler<ChangeEventArgs>(OnPayees_Changed);

            InitializeComponent();

            okButton.Click += new RoutedEventHandler(OnOkButton_Click);

            comboBox1.TextChanged += new RoutedEventHandler(OnComboBox1_TextChanged);
            textBox1.TextChanged += OnPatternTextChanged;
            checkBoxUseRegex.Checked += OnRegexChanged;
            checkBoxUseRegex.Unchecked += OnRegexChanged;
            checkBoxAuto.Checked += OnAutoChanged;
            checkBoxAuto.Unchecked += OnAutoChanged;
        }

        private void EnableButtons()
        {
            this.okButton.IsEnabled = !string.IsNullOrWhiteSpace(this.textBox1.Text) && !string.IsNullOrWhiteSpace(this.comboBox1.Text);
        }

        private void OnAutoChanged(object sender, RoutedEventArgs e)
        {
            CheckState();
        }

        private void OnRegexChanged(object sender, RoutedEventArgs e)
        {
            CheckState();
        }

        private void OnPatternTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            CheckState();
        }

        void CheckState() 
        { 
            EnableButtons();
            if (!string.IsNullOrWhiteSpace(this.textBox1.Text))
            {
                delayedActions.StartDelayedAction("CheckConflicts", CheckConflicts, TimeSpan.FromMilliseconds(50));
            }
        }

        void OnComboBox1_TextChanged(object sender, RoutedEventArgs e)
        {
            EnableButtons();
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
                    ClashPromp.Visibility = Visibility.Visible;
                    ClashingAliases.ItemsSource = (from i in conflicts select i.Pattern).ToList();
                    ClashingAliases.Visibility = Visibility.Visible;
                }
            }

            if (!foundConflicts)
            {
                ClashPromp.Visibility = Visibility.Collapsed;
                ClashingAliases.ItemsSource = null;
                ClashingAliases.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            this.money.Payees.Changed -= handler;
            this.money = null;

            base.OnClosed(e);
        }

        void OnOkButton_Click(object sender, RoutedEventArgs e)
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
                if (ServiceProvider != null)
                {
                    // make sure we search visible transactions being edited as well.
                    TransactionCollection viewModel = ServiceProvider.GetService(typeof(TransactionCollection)) as TransactionCollection;
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
                    foreach(var subsumed in this.money.FindSubsumedAliases(a))
                    {
                        this.money.Aliases.RemoveAlias(subsumed);
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

        void OnPayees_Changed(object sender, ChangeEventArgs args)
        {
            this.LoadPayees();
        }

        void LoadPayees()
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
