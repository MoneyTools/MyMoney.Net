using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using Walkabout.Data;
using Walkabout.Interfaces.Views;
using Walkabout.Importers;
using Walkabout.Utilities;

namespace Walkabout.Views
{

    /// <summary>
    /// Interaction logic for RentInputControl1.xaml
    /// </summary>
    public partial class LoansView : UserControl, IView
    {
        #region MENUS COMMNANDS

        public static readonly RoutedUICommand CommandGotoRelatedTransaction = new RoutedUICommand("GotoRelatedTransaction", "CommandGotoRelatedTransaction", typeof(LoansView));

        #endregion

        public void FocusQuickFilter()
        {
        }

        private Account accountSelected;
        private Loan loanAccount;


        public Account AccountSelected
        {
            get
            {
                return this.accountSelected;
            }
            set
            {
                if (value != null)
                {
                    this.FireBeforeViewStateChanged();

                    this.accountSelected = value;
                    this.loanAccount = this.Money.GetOrCreateLoanAccount(this.accountSelected);
                    this.loanAccount.Rebalance();
                    this.TheDataGrid.ItemsSource = this.loanAccount.Payments;

                    this.FireAfterViewStateChanged();
                }
            }
        }

        public ObservableCollection<LoanPaymentAggregation> LoanPayments
        {
            get { return this.loanAccount.Payments; }
            set { this.loanAccount.Payments = value; }
        }

        private LoanPaymentAggregation CurrentSelectedItem
        {
            get { return this.TheDataGrid.SelectedValue as LoanPaymentAggregation; }
        }

        public LoansView()
        {
            this.InitializeComponent();
        }

        #region IView

        public MyMoney Money { get; set; }

        public void ActivateView()
        {
            this.Focus();
        }

        public event EventHandler BeforeViewStateChanged;

        private void OnBeforeViewStateChanged()
        {
            if (BeforeViewStateChanged != null)
            {
                BeforeViewStateChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        private void OnAfterViewStateChanged()
        {
            if (AfterViewStateChanged != null)
            {
                AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(0));
            }
        }

        private IServiceProvider sp;

        public IServiceProvider ServiceProvider
        {
            get { return this.sp; }
            set { this.sp = value; }
        }

        private IStatusService status;
        public IStatusService ServiceStatus
        {
            get
            {
                if (this.status == null)
                {
                    if (this.ServiceProvider != null)
                    {
                        this.status = (IStatusService)this.ServiceProvider.GetService(typeof(IStatusService));
                    }
                }
                return this.status;
            }
        }

        public void Commit()
        {
            this.TheDataGrid.CommitEdit();
        }

        public string Caption
        {
            get
            {

                return "Loan -" + (this.AccountSelected == null ? "no account" : this.AccountSelected.Name);
            }
        }

        public object SelectedRow
        {
            get { return this.TheDataGrid.SelectedItem; }
            set { this.TheDataGrid.SelectedItem = value; }
        }


        public ViewState ViewState
        {
            get
            {
                ViewStateForLoan vs = null;

                if (this.AccountSelected != null)
                {
                    vs = new ViewStateForLoan();
                    vs.LoanAccountId = this.AccountSelected.Id;
                }
                return vs;
            }


            set
            {
                ViewStateForLoan vs = value as ViewStateForLoan;
                if (vs != null)
                {
                    this.viewStateLock++;
                    this.AccountSelected = this.Money.Accounts.FindAccountAt(vs.LoanAccountId);
                    this.viewStateLock--;
                }
            }
        }

        private int viewStateLock;

        private void FireBeforeViewStateChanged()
        {
            if (this.viewStateLock == 0 && BeforeViewStateChanged != null)
            {
                BeforeViewStateChanged(this, new EventArgs());
            }

        }

        private void FireAfterViewStateChanged()
        {
            if (AfterViewStateChanged != null)
            {
                AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(0));
            }
            this.ServiceStatus.ShowMessage(this.CreateStatusText());
        }

        private string CreateStatusText()
        {
            return this.LoanPayments.Count.ToString() + " Payments";
        }


        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            // to-do: implement our own custom view state
            return new ViewState();
        }

        private string quickFilter;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                if (this.quickFilter != value)
                {
                    this.quickFilter = value;
                    // to-do
                }
            }
        }

        public bool IsQueryPanelDisplayed { get; set; }

        #endregion

        private void TheDataGrid_InitializingNewItem(object sender, InitializingNewItemEventArgs e)
        {
            LoanPaymentAggregation current = this.TheDataGrid.SelectedItem as LoanPaymentAggregation;
            LoanPaymentAggregation lvp = e.NewItem as LoanPaymentAggregation;

            // Add new manual entry
            lvp.LoanPayementManualEntry = new LoanPayment() { AccountId = this.AccountSelected.Id };
            this.Money.LoanPayments.Add(lvp.LoanPayementManualEntry);

            if (current == null)
            {
                lvp.Date = DateTime.Now;
            }
            else
            {
                lvp.Date = current.Date.AddMonths(1);
            }
            lvp.Account = this.AccountSelected;
        }

        private void TheDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (((Walkabout.Data.LoanPaymentAggregation)e.Row.Item).IsReadOnly == true)
            {
                e.Cancel = false;
            }
        }



        //
        // Used for know what field on the row was edited
        //
        private decimal editingPrincipalBefore;
        private decimal editingInterestBefore;
        private decimal editingPercentageBefore;
        private LoanPaymentAggregation currentEditRow;

        private void TheDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                this.currentEditRow = e.Row.Item as LoanPaymentAggregation;
                if (this.currentEditRow != null)
                {
                    //
                    // Cache the value of the 4 possible field being edited
                    // DataGrid is pretty stupid here, instead of giving us an event with the value before and after editing
                    // you have to wait for the data grid to actually make the field updated before you can look at the value 
                    // that the user just edited
                    // so to work around this we cache the last value, send our self and event and we compare the before and after value
                    //
                    this.editingPrincipalBefore = this.currentEditRow.Principal;
                    this.editingInterestBefore = this.currentEditRow.Interest;
                    this.editingPercentageBefore = this.currentEditRow.Percentage;

                    // Note: this must be DispatcherPriority.Background otherwise Updated value are happens too soon and
                    // doesn't see the new value!
                    this.Dispatcher.BeginInvoke(new Action(this.Rebalance), DispatcherPriority.ApplicationIdle);
                }
            }
        }

        private void Rebalance()
        {
            // Priority 1) to the PERCENTAGE FIELD
            if (this.editingPercentageBefore != this.currentEditRow.Percentage)
            {
                this.currentEditRow.Interest = 0;
                this.currentEditRow.Principal = 0;

                // have to fix forwards from edited row subsequent
                foreach (LoanPaymentAggregation l in this.LoanPayments)
                {
                    if (l.Date >= this.currentEditRow.Date)
                    {
                        if (l.SplitForInterest != null || l.SplitForPrincipal != null)
                        {
                            l.Percentage = this.currentEditRow.Percentage;
                        }

                        if (l.SplitForInterest != null)
                        {
                            l.Interest = 0;
                        }

                        if (l.SplitForPrincipal != null)
                        {
                            l.Principal = 0;
                        }
                    }
                }
            }
            else
            {
                // make sure the 3 values are consistent, depending on what was edited.
                if (this.currentEditRow.Payment != 0)
                {
                    // Priority 2) to the PRINCIPAL field
                    if (this.editingPrincipalBefore != this.currentEditRow.Principal)
                    {
                        this.currentEditRow.Interest = this.currentEditRow.Payment - this.currentEditRow.Principal;
                    }
                    else
                    {
                        // Priority 3) to the INTEREST field
                        if (this.editingInterestBefore != this.currentEditRow.Interest)
                        {
                            this.currentEditRow.Principal = this.currentEditRow.Payment - this.currentEditRow.Interest;
                        }
                    }
                }
            }

            this.loanAccount.Rebalance();
        }


        private void TheDataGrid_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Key.Delete == e.Key)
            {
                if (this.CurrentSelectedItem != null && this.CurrentSelectedItem.IsReadOnly == true)
                {
                    // not allowed to delete these ReadOnly entries
                    e.Handled = true;
                }
            }
        }

        private void TheDataGrid_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command == DataGrid.DeleteCommand)
            {
                if (this.CurrentSelectedItem != null && this.CurrentSelectedItem.IsReadOnly)
                {
                    // Cancel delete operation
                    e.Handled = true;
                }
                else
                {
                    if (MessageBoxEx.Show("Are you sure you want to delete?", "Please confirm.", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        this.Money.LoanPayments.Remove(this.CurrentSelectedItem.LoanPayementManualEntry);
                    }
                    else
                    {
                        // Cancel Delete.
                        e.Handled = true;
                    }
                }
            }
        }


        /// <summary>
        /// Jump to the account of the associated transaction is from
        /// This will also select the related transaction inside the transaction view
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCommandGotoRelatedTransaction(object sender, RoutedEventArgs e)
        {
            LoanPaymentAggregation l = this.CurrentSelectedItem;

            if (l != null && l.Transaction != null)
            {
                if (this.ServiceProvider != null)
                {
                    // Get the main service
                    IViewNavigator view = this.ServiceProvider.GetService(typeof(IViewNavigator)) as IViewNavigator;
                    if (view != null)
                    {
                        // Request to change the current view
                        view.NavigateToTransaction(l.Transaction);
                    }
                }
            }
        }

        private void OnCommandViewExport(object sender, ExecutedRoutedEventArgs e)
        {
            Exporters exporter = new Exporters();
            exporter.SupportXml = false;
            exporter.ExportPrompt(this.LoanPayments.ToArray());
        }

    }

    public class ViewStateForLoan : ViewState
    {
        public int LoanAccountId { get; set; }


        public override void ReadXml(XmlReader r)
        {
            if (r.IsEmptyElement)
            {
                return;
            }

            while (r.Read() && !r.EOF && r.NodeType != XmlNodeType.EndElement)
            {
                if (r.NodeType == XmlNodeType.Element)
                {
                    switch (r.Name)
                    {
                        case "LoanAccount":
                            this.LoanAccountId = Convert.ToInt32(r.ReadString());
                            break;
                    }
                }
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            if (writer != null)
            {
                writer.WriteElementString("LoanAccount", this.LoanAccountId.ToString());
            }
        }
    }



    public class ZeroToOpacityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            decimal? zero = value as decimal?;

            if (zero != null && zero == 0)
            {
                return 0.4; // very low opacity
            }
            return 1; // 100% opaque 
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Not a valid method to call
            return 0;
        }
    }





}

