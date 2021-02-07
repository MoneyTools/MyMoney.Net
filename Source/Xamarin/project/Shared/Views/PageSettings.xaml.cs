using System;
using VTeamWidgets;
using Xamarin.Forms;

namespace XMoney
{
    public partial class PageSettings : ContentPage
    {
        public PageSettings()
        {
            InitializeComponent();

            this.PathToDataBase.Text = Settings.SourceDatabase;

            this.AddSourceFolder.Clicked += Button_Clicked;

            checkBoxShowClosedAccounts.IsChecked = Settings.Get().ShowClodedAccounts;
            checkBoxShowLoanProjection.IsChecked = Settings.Get().ShowLoanProjection;
            checkBoxRental.IsChecked = Settings.Get().ManageRentalProperties;
        }

        private async void Button_Clicked(object sender, EventArgs e)
        {
            if (MyXPlatform.Current != null)
            {
                try
                {
                    //string[] fileTypes = new string[] { "*" };
                    var pickedFile = await MyXPlatform.Current.PickFile(null);  //fileTypes);

                    if (pickedFile != null)
                    {
                        Settings.SourceDatabase = pickedFile.FullPath;
                        this.PathToDataBase.Text = Settings.SourceDatabase;
                        await Navigation.PushAsync(new PageMain());
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        private void CheckBoxShowClosedAccounts_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            Settings.Get().ShowClodedAccounts = e.Value;
        }

        private void CheckBoxShowLoanProjection_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            Settings.Get().ShowLoanProjection = e.Value;
        }

        private void CheckBoxRental_CheckedChanged(object sender, CheckedChangedEventArgs e)
        {
            Settings.Get().ManageRentalProperties = e.Value;
        }
    }
}
