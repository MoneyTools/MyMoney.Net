using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using Walkabout.Data;
using Walkabout.Dialogs;
using Walkabout.Importers;
using Walkabout.Ofx;
using Walkabout.Utilities;

namespace Walkabout.Views.Controls
{
    public class DownloadControlSelectionChangedEventArgs : EventArgs
    {
        public DownloadData Data { get; set; }
    }

    /// <summary>
    /// Interaction logic for OfxDownloadControl.xaml
    /// </summary>
    public partial class DownloadControl : UserControl
    {
        private DelayedActions delayedActions = new DelayedActions();

        public DownloadControl()
        {
            this.InitializeComponent();

            this.DownloadEventTree.SelectedItemChanged += new RoutedPropertyChangedEventHandler<object>(this.OnSelectedItemChanged);
            this.DownloadEventTree.MouseLeftButtonUp += this.DownloadEventTree_MouseLeftButtonUp;
        }

        public event EventHandler<DownloadControlSelectionChangedEventArgs> SelectionChanged;
        public event EventHandler<DownloadData> DetailsClicked;

        private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            DownloadData selection = e.NewValue as DownloadData;
            if (selection != null && selection.Added.Count > 0)
            {
                delayedActions.StartDelayedAction("RaiseSelectionChanged", this.RaiseSelectionChanged, TimeSpan.FromMilliseconds(50));
            }
        }

        private void DownloadEventTree_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (this.DownloadEventTree.SelectedItem is DownloadData selection)
            {                
                // another click means user wants to see the rows again, even if item is already selected
                if (selection != null && selection.Added.Count > 0)
                {
                    var target = this.DownloadEventTree.InputHitTest(e.GetPosition(this.DownloadEventTree));
                    if (target is FrameworkElement fe && fe.DataContext == selection)
                    {
                        delayedActions.StartDelayedAction("RaiseSelectionChanged", this.RaiseSelectionChanged, TimeSpan.FromMilliseconds(50));
                    }
                }
            }
        }

        private void RaiseSelectionChanged()
        {
            if (this.DownloadEventTree.SelectedItem is DownloadData selection)
            {
                if (SelectionChanged != null)
                {
                    Debug.WriteLine("Raising selection changed");
                    SelectionChanged(this, new DownloadControlSelectionChangedEventArgs() { Data = selection });
                }
            }
        }

        private void ButtonRemoveOnlineAccount_Clicked(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            if (b != null)
            {
                DownloadData ofxData = b.DataContext as DownloadData;

                if (ofxData != null)
                {
                    OnlineAccount oa = ofxData.OnlineAccount;
                    MyMoney money = oa.Parent?.Parent as MyMoney;
                    if (oa != null && money != null)
                    {
                        MessageBoxResult result = MessageBoxEx.Show("Permanently delete the online account \"" + ofxData.Caption + "\"", null, MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            oa.OnDelete();
                            foreach (Account a in money.Accounts.GetAccounts())
                            {
                                if (a.OnlineAccount == oa)
                                {
                                    a.OnlineAccount = null;
                                }
                            }
                            ThreadSafeObservableCollection<DownloadData> entries = this.DownloadEventTree.ItemsSource as ThreadSafeObservableCollection<DownloadData>;
                            entries.Remove(ofxData);
                        }
                    }

                }
            }
        }

        private void OnDetailsClick(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)sender;
            DownloadData ofxData = link.DataContext as DownloadData;
            if (DetailsClicked != null)
            {
                DetailsClicked(this, ofxData);
            }
        }

        internal void SelectEntry(DownloadData last)
        {
            foreach (var e in this.DownloadEventTree.ItemsSource)
            {
                if (e is DownloadData && e == last)
                {
                    var item = this.DownloadEventTree.ItemContainerGenerator.ContainerFromItem(e);
                    if (item is TreeViewItem treeitem)
                    {
                        treeitem.IsSelected = true;
                        return;
                    }
                }
            }
            this.delayedActions.StartDelayedAction("SelectItem", () => this.SelectEntry(last), TimeSpan.FromMilliseconds(100));
        }
    }
}
