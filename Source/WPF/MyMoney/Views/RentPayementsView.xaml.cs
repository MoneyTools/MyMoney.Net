using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Walkabout.Data;
using Walkabout.Interfaces.Views;

namespace Walkabout.Views
{

    /// <summary>
    /// Interaction logic for RentInputControl1.xaml
    /// </summary>
    public partial class RentInputControl : UserControl, IView
    {

        RentalBuildingSingleYear yearMonth;


        public RentInputControl()
        {
            this.InitializeComponent();
        }
        public void FocusQuickFilter()
        {
        }

        public RentInputControl(RentalBuildingSingleYear month)
        {
            this.InitializeComponent();

            this.yearMonth = month;
            IsVisibleChanged += new DependencyPropertyChangedEventHandler(this.RentInputControl_IsVisibleChanged);
        }


        void RentInputControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                this.OnBeforeViewStateChanged();
                this.OnAfterViewStateChanged();
            }
        }


        DispatcherTimer dispatcherTimer = null;

        private void TheDataGrid_RowEditEnding(object sender, DataGridRowEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (this.dispatcherTimer == null)
                {
                    this.dispatcherTimer = new DispatcherTimer();
                    this.dispatcherTimer.Tick += new EventHandler(this.dispatcherTimer_Tick);
                    this.dispatcherTimer.Interval = new TimeSpan(0, 0, 5);
                }
                this.dispatcherTimer.Stop();
                this.dispatcherTimer.Start();
            }
        }

        void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            this.dispatcherTimer.Stop();

            try
            {
                this.TheDataGrid.Items.Refresh();
            }
            catch
            {
            }
            finally
            {
            }

        }



        #region IView

        public MyMoney Money { get; set; }

        public void ActivateView()
        {
            this.Focus();
        }

        public event EventHandler BeforeViewStateChanged;

        void OnBeforeViewStateChanged()
        {
            if (BeforeViewStateChanged != null)
            {
                BeforeViewStateChanged(this, EventArgs.Empty);
            }
        }

        public event EventHandler<AfterViewStateChangedEventArgs> AfterViewStateChanged;

        void OnAfterViewStateChanged()
        {
            if (AfterViewStateChanged != null)
            {
                AfterViewStateChanged(this, new AfterViewStateChangedEventArgs(0));
            }
        }

        IServiceProvider sp;

        public IServiceProvider ServiceProvider
        {
            get { return this.sp; }
            set { this.sp = value; }
        }

        public void Commit()
        {
            //tdo
        }

        public string Caption
        {
            get { return "Rent Input"; }
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
                // todo;
                return null;
            }
            set
            {
                // todo
            }
        }

        public ViewState DeserializeViewState(System.Xml.XmlReader reader)
        {
            // todo;
            return null;
        }

        string quickFilter;

        public string QuickFilter
        {
            get { return this.quickFilter; }
            set
            {
                if (this.quickFilter != value)
                {
                    this.quickFilter = value;
                    // todo
                }
            }
        }

        public bool IsQueryPanelDisplayed { get; set; }

        #endregion 

    }
}

