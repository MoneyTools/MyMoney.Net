using System;
using System.Collections.Generic;
using System.Xml;
using Walkabout.Data;

namespace Walkabout.Interfaces.Views
{
    public class AfterViewStateChangedEventArgs : EventArgs
    {
        public AfterViewStateChangedEventArgs(long selectedRowId)
        {
            this.SelectedRowId = selectedRowId;
        }

        public long SelectedRowId { get; set; }
    }

    public interface IView
    {
        MyMoney Money { get; set; }
        void ActivateView();

        event EventHandler BeforeViewStateChanged;
        event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        IServiceProvider ServiceProvider { get; set; }
        void Commit();
        string Caption { get; }
        object SelectedRow { get; set; }

        ViewState ViewState { get; set; }
        ViewState DeserializeViewState(XmlReader reader);

        string QuickFilter { get; set; }
        bool IsQueryPanelDisplayed { get; set; }

        void FocusQuickFilter();
    }


    public interface ITransactionView : IView
    {
        Account ActiveAccount { get; }
        Category ActiveCategory { get; }
        Security ActiveSecurity { get; }
        Payee ActivePayee { get; }
        RentBuilding ActiveRental { get; }
    }
    

    /// <summary>
    /// Interface abstraction for decoupling the views from the main controlling windows
    /// This allows one view to make a request to the main control for jumping to another view
    /// </summary>
    public interface IViewNavigator
    {
        void NavigateToTransaction(Transaction transaction);
        void NavigateToSecurity(Security security);
        void ViewTransactions(IEnumerable<Transaction> list);
        void ViewTransactionsBySecurity(Security security);
    }
}
