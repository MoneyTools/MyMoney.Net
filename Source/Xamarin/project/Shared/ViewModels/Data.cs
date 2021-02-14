using SQLite;
using System;
using System.Diagnostics;
using VTeamWidgets;

namespace XMoney.ViewModels
{
    public class Data
    {
        public SQLiteConnection dbConnection;

        public Data()
        {
        }

        public static Data Get { get; } = new Data();

        public bool LoadDatabase()
        {
            try
            {
                this.dbConnection = this.OpenDatabase();

                if (dbConnection != null)
                {
                    var watch = Stopwatch.StartNew();
                    Accounts.Cache(this.dbConnection);
                    ShowElapsed(watch, "Accounts");

                    Payees.Cache(this.dbConnection);
                    ShowElapsed(watch, "Payees");

                    Categories.Cache(this.dbConnection);
                    ShowElapsed(watch, "Categories");

                    Transactions.Cache(this.dbConnection);
                    ShowElapsed(watch, "Transactions");

                    Splits.Cache(this.dbConnection);
                    ShowElapsed(watch, "Splits");

                    LoanPayments.Cache(this.dbConnection);
                    ShowElapsed(watch, "LoanPayments");

                    RentBuildings.Cache(this.dbConnection);
                    ShowElapsed(watch, "RentBuildings");


                    // Now that All the data is loaded give each data type a chance to evaluate their complete states
                    {
                        Accounts.OnAllDataLoaded();
                        Payees.OnAllDataLoaded();
                        Categories.OnAllDataLoaded();
                        Transactions.OnAllDataLoaded();
                        Splits.OnAllDataLoaded();
                        LoanPayments.OnAllDataLoaded();
                        RentBuildings.OnAllDataLoaded();
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                App.AlertConfirm("Error opening data", ex.Message);
            }
            return false;
        }

        private static void ShowElapsed(Stopwatch watch, string text)
        {
            Debug.WriteLine("************* " + text + "  " + watch.ElapsedMilliseconds.ToString());
        }

        protected SQLiteConnection OpenDatabase()
        {
            try
            {
                if (MyXPlatform.Current.FileExist(Settings.SourceDatabase))
                {
                    return new SQLiteConnection(Settings.SourceDatabase);
                }
                else
                {
                    App.AlertConfirm("File not found", Settings.SourceDatabase, "Ok");
                }
            }
            catch (Exception ex)
            {
                App.AlertConfirm("Not a valid database", Settings.SourceDatabase + " " + ex.Message + " " + ex.InnerException, "Ok");
            }

            return null;
        }
    }
}
