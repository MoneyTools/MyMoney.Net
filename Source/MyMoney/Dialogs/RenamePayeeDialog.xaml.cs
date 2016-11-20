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

namespace Walkabout.Dialogs
{
    /// <summary>
    /// Interaction logic for RenamePayeeDialog.xaml
    /// </summary>
    public partial class RenamePayeeDialog : Window
    {
        #region PROPERTIES

        EventHandler<ChangeEventArgs> handler;

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

        public string Pattern
        {
            get { return this.textBox1.Text; }
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
        public static RenamePayeeDialog ShowDialogRenamePayee(MyMoney myMoney, Payee payeeToRename)
        {
            return ShowDialogRenamePayee(myMoney, payeeToRename, payeeToRename);
        }

        /// <summary>
        /// Easy wrapper for launching the Rename Payee Dialog
        /// This version allows you to set the "Rename To" field, used in the Drag and Drop scenario
        /// </summary>
        /// <param name="myMoney"></param>
        /// <param name="fromPayee"></param>
        /// <param name="renameToThisPayee"></param>
        /// <returns></returns>
        public static RenamePayeeDialog ShowDialogRenamePayee(MyMoney myMoney, Payee fromPayee, Payee renameToThisPayee)
        {
            RenamePayeeDialog dialog = new RenamePayeeDialog();
            dialog.Owner = Application.Current.MainWindow;
            dialog.MyMoney = myMoney;
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
        }

        void OnComboBox1_TextChanged(object sender, RoutedEventArgs e)
        {
            this.okButton.IsEnabled = string.IsNullOrWhiteSpace(this.comboBox1.Text) == false;
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
            bool close = true;
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
                        this.money.Aliases.AddAlias(a);
                        a.Pattern = pattern;
                        a.Payee = q;
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

                // Now see if we have any matches
                IEnumerable<PersistentObject> result = this.money.FindAliasMatches(a);                
                if (!result.Any())
                {
                    if (MessageBoxResult.Cancel == MessageBoxEx.Show("No matching transactions found (that don't already have the target Payee), do you want to save it anyway?", "Warning", MessageBoxButton.OKCancel, MessageBoxImage.Warning))
                    {
                        this.textBox1.Focus();
                        this.textBox1.SelectAll();
                        close = false;
                    }
                }

                // Now the user really wants to switch all transactions
                // referencing p over to q, then remove p.
                int count = this.money.ApplyAlias(a);

            }
            else
            {
                // warn the user that they changed nothing?
            }

            if (close)
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
