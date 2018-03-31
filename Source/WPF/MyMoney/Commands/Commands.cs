using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Walkabout.Commands
{
    public static class AppCommands
    {

        // FILE 
        public readonly static RoutedUICommand CommandFileImport;
        public readonly static RoutedUICommand CommandFileExportGraph;
        public readonly static RoutedUICommand CommandFileExtensionAssociation;
        public readonly static RoutedUICommand CommandFileAddUser;
        public readonly static RoutedUICommand CommandFileBackup;
        public readonly static RoutedUICommand CommandFileRestore;
        public readonly static RoutedUICommand CommandRevertChanges;
        public readonly static RoutedUICommand CommandOpenContainingFolder;

        // VIEW
        public readonly static RoutedUICommand CommandViewSecurities;
        public readonly static RoutedUICommand CommandViewCurrencies;
        public readonly static RoutedUICommand CommandViewAliases;
        public readonly static RoutedUICommand CommandViewOptions;

        public readonly static RoutedUICommand CommandViewThemeVS2010;
        public readonly static RoutedUICommand CommandViewThemeFlat;
        public readonly static RoutedUICommand CommandViewThemeOSX;

        // History
        public readonly static RoutedUICommand CommandBack;
        public readonly static RoutedUICommand CommandForward;

        // REPORTS
        public readonly static RoutedUICommand CommandReportBudget;
        public readonly static RoutedUICommand CommandReportNetWorth;
        public readonly static RoutedUICommand CommandReportInvestment;
        public readonly static RoutedUICommand CommandTaxReport;
        public readonly static RoutedUICommand CommandW2Report;
        public readonly static RoutedUICommand CommandReportCashFlow;
        public readonly static RoutedUICommand CommandReportUnaccepted;

        // QUERY
        public readonly static RoutedUICommand CommandQueryShowForm;
        public readonly static RoutedUICommand CommandQueryRun;
        public readonly static RoutedUICommand CommandQueryClear;
        public readonly static RoutedUICommand CommandQueryAdhoc;
        public readonly static RoutedUICommand CommandQueryShowLastUpdate;

        // ONLINE
        public readonly static RoutedUICommand CommandOnlineSyncAccount;
        public readonly static RoutedUICommand CommandOnlineUpdateSecurities;
        public readonly static RoutedUICommand CommandDownloadAccounts;
        public readonly static RoutedUICommand CommandStockQuoteAccountCreds;

        // Help
        public readonly static RoutedUICommand CommandHelpAbout;
        public readonly static RoutedUICommand CommandViewHelp;
        public readonly static RoutedUICommand CommandAddSampleData;
        public readonly static RoutedUICommand CommandTroubleshootCheckTransfer;
        public readonly static RoutedUICommand CommandViewChanges;

        static AppCommands()
        {

            // FILE
            CommandFileImport = new RoutedUICommand("File Import", "FileImport", typeof(AppCommands));
            CommandFileExportGraph = new RoutedUICommand("Export Account Dependencies (Graph)", "FileExportGraph", typeof(AppCommands));

            CommandFileExtensionAssociation = new RoutedUICommand("File Extension association", "FileExtensionAssociation", typeof(AppCommands));
            CommandFileAddUser = new RoutedUICommand("Add User", "AddUser", typeof(AppCommands));
            CommandFileBackup = new RoutedUICommand("Backup", "Backup", typeof(AppCommands));
            CommandFileRestore = new RoutedUICommand("Restore", "Restore", typeof(AppCommands));
            CommandRevertChanges = new RoutedUICommand("Revert", "Revert", typeof(AppCommands));
            CommandOpenContainingFolder = new RoutedUICommand("OpenContainingFolder", "OpenContainingFolder", typeof(AppCommands));

            // VIEW
            CommandViewSecurities = new RoutedUICommand("View Securities", "ViewSecurities", typeof(AppCommands));
            CommandViewCurrencies = new RoutedUICommand("View Currencies", "ViewCurrencies", typeof(AppCommands));
            CommandViewAliases = new RoutedUICommand("View Aliases", "ViewAliases", typeof(AppCommands));
            CommandViewOptions = new RoutedUICommand("View Options", "ViewOptions", typeof(AppCommands));
            CommandViewThemeVS2010 = new RoutedUICommand("View Theme VS2010", "ViewThemeVS2010", typeof(AppCommands));
            CommandViewThemeFlat = new RoutedUICommand("View Theme Flat", "ViewThemeFlat", typeof(AppCommands));
            CommandViewThemeOSX = new RoutedUICommand("View Theme OSX", "ViewThemeOSX", typeof(AppCommands));

            // History
            CommandBack = new RoutedUICommand("Back", "Back", typeof(AppCommands));
            CommandForward = new RoutedUICommand("Forward", "Forward", typeof(AppCommands));

            // REPORTS            
            CommandReportBudget = new RoutedUICommand("Report Budget", "ReportBudger", typeof(AppCommands));
            CommandReportNetWorth = new RoutedUICommand("Report Net Worth", "ReportNetWorth", typeof(AppCommands));
            CommandReportInvestment = new RoutedUICommand("Report Investment", "ReportInvestment", typeof(AppCommands));
            CommandTaxReport = new RoutedUICommand("Tax Report", "TaxReport", typeof(AppCommands));
            CommandW2Report = new RoutedUICommand("W2 Report", "W2Report", typeof(AppCommands));
            CommandReportCashFlow = new RoutedUICommand("Report Cash Flow", "ReportCashFlow", typeof(AppCommands));
            CommandReportUnaccepted = new RoutedUICommand("Report Unaccepted", "ReportUnaccepted", typeof(AppCommands));

            // QUERY
            CommandQueryShowForm = new RoutedUICommand("Query Show Query Form", "QueryShowQueryForm", typeof(AppCommands));
            CommandQueryRun = new RoutedUICommand("Query Run", "QueryRun", typeof(AppCommands));
            CommandQueryClear = new RoutedUICommand("Query Clear", "QueryClear", typeof(AppCommands));
            CommandQueryAdhoc = new RoutedUICommand("Query Adhoc", "QueryAdhoc", typeof(AppCommands));
            CommandQueryShowLastUpdate = new RoutedUICommand("Query Last Update", "QueryLastUpdate", typeof(AppCommands));

            CommandOnlineSyncAccount = new RoutedUICommand("Sync Account", "SyncAccount", typeof(AppCommands));
            CommandOnlineUpdateSecurities = new RoutedUICommand("UpdateSecurities", "Update Securities", typeof(AppCommands));
            CommandDownloadAccounts = new RoutedUICommand("DownloadAccounts", "DownloadAccounts", typeof(AppCommands));
            CommandStockQuoteAccountCreds = new RoutedUICommand("StockQuoteAccountCreds", "StockQuoteAccountCreds", typeof(AppCommands));

            // HELP
            CommandHelpAbout= new RoutedUICommand("HelpAbout", "About", typeof(AppCommands));
            CommandViewHelp = new RoutedUICommand("ViewHelp", "View Help", typeof(AppCommands));
            CommandAddSampleData = new RoutedUICommand("AddSampleData", "Add Sample Data", typeof(AppCommands));
            CommandTroubleshootCheckTransfer = new RoutedUICommand("TroubleshootCheckTransfer", "Check Transfer", typeof(AppCommands));
            CommandViewChanges = new RoutedUICommand("CommandViewChanges", "View Changes", typeof(AppCommands));

        }
    }
}
