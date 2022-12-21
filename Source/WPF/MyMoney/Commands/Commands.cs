using System.Windows.Input;

namespace Walkabout.Commands
{
    public static class AppCommands
    {

        // FILE 
        public static readonly RoutedUICommand CommandFileImport;
        public static readonly RoutedUICommand CommandFileExportGraph;
        public static readonly RoutedUICommand CommandFileExtensionAssociation;
        public static readonly RoutedUICommand CommandFileAddUser;
        public static readonly RoutedUICommand CommandFileBackup;
        public static readonly RoutedUICommand CommandFileRestore;
        public static readonly RoutedUICommand CommandRevertChanges;
        public static readonly RoutedUICommand CommandOpenContainingFolder;

        // VIEW
        public static readonly RoutedUICommand CommandViewSecurities;
        public static readonly RoutedUICommand CommandViewCurrencies;
        public static readonly RoutedUICommand CommandViewAliases;
        public static readonly RoutedUICommand CommandViewOptions;
        public static readonly RoutedUICommand CommandToggleTheme;

        // History
        public static readonly RoutedUICommand CommandBack;
        public static readonly RoutedUICommand CommandForward;

        // REPORTS
        public static readonly RoutedUICommand CommandReportBudget;
        public static readonly RoutedUICommand CommandReportNetWorth;
        public static readonly RoutedUICommand CommandReportInvestment;
        public static readonly RoutedUICommand CommandTaxReport;
        public static readonly RoutedUICommand CommandW2Report;
        public static readonly RoutedUICommand CommandReportCashFlow;
        public static readonly RoutedUICommand CommandReportUnaccepted;

        // QUERY
        public static readonly RoutedUICommand CommandQueryShowForm;
        public static readonly RoutedUICommand CommandQueryRun;
        public static readonly RoutedUICommand CommandQueryClear;
        public static readonly RoutedUICommand CommandQueryAdhoc;
        public static readonly RoutedUICommand CommandQueryShowLastUpdate;

        // ONLINE
        public static readonly RoutedUICommand CommandOnlineSyncAccount;
        public static readonly RoutedUICommand CommandOnlineUpdateSecurities;
        public static readonly RoutedUICommand CommandDownloadAccounts;
        public static readonly RoutedUICommand CommandStockQuoteServiceOptions;

        // Help
        public static readonly RoutedUICommand CommandHelpAbout;
        public static readonly RoutedUICommand CommandViewHelp;
        public static readonly RoutedUICommand CommandAddSampleData;
        public static readonly RoutedUICommand CommandTroubleshootCheckTransfer;
        public static readonly RoutedUICommand CommandViewChanges;

        // ContextMenu on Graphs & Charts
        public static readonly RoutedUICommand CommandYearToDate;
        public static readonly RoutedUICommand CommandNext;
        public static readonly RoutedUICommand CommandPrevious;
        public static readonly RoutedUICommand CommandSetRange;
        public static readonly RoutedUICommand CommandShowAll;
        public static readonly RoutedUICommand CommandZoomIn;
        public static readonly RoutedUICommand CommandZoomOut;
        public static readonly RoutedUICommand CommandAddSeries;
        public static readonly RoutedUICommand CommandRemoveSeries;
        public static readonly RoutedUICommand CommandShowBudget;
        public static readonly RoutedUICommand CommandExportData;


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
            CommandToggleTheme = new RoutedUICommand("Toggle Theme", "ToggleTheme", typeof(AppCommands));

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
            CommandStockQuoteServiceOptions = new RoutedUICommand("StockQuoteServiceOptions", "StockQuoteServiceOptions", typeof(AppCommands));

            // HELP
            CommandHelpAbout = new RoutedUICommand("HelpAbout", "About", typeof(AppCommands));
            CommandViewHelp = new RoutedUICommand("ViewHelp", "View Help", typeof(AppCommands));
            CommandAddSampleData = new RoutedUICommand("AddSampleData", "Add Sample Data", typeof(AppCommands));
            CommandTroubleshootCheckTransfer = new RoutedUICommand("TroubleshootCheckTransfer", "Check Transfer", typeof(AppCommands));
            CommandViewChanges = new RoutedUICommand("CommandViewChanges", "View Changes", typeof(AppCommands));

            // Cusrtom Report range dialog
            CommandYearToDate = new RoutedUICommand("Year to date", "CommandYearToDate", typeof(AppCommands));
            CommandNext = new RoutedUICommand("Next", "CommandNext", typeof(AppCommands));
            CommandPrevious = new RoutedUICommand("Previous", "CommandPrevious", typeof(AppCommands));
            CommandSetRange = new RoutedUICommand("Set range", "CommandSetRange", typeof(AppCommands));
            CommandShowAll = new RoutedUICommand("Show all", "CommandShowAll", typeof(AppCommands));
            CommandZoomIn = new RoutedUICommand("Zoom in", "CommandZoomIn", typeof(AppCommands));
            CommandZoomOut = new RoutedUICommand("Zoom out", "CommandZoomOut", typeof(AppCommands));
            CommandAddSeries = new RoutedUICommand("Add series", "CommandAddSeries", typeof(AppCommands));
            CommandRemoveSeries = new RoutedUICommand("Remove series", "CommandRemoveSeries", typeof(AppCommands));
            CommandShowBudget = new RoutedUICommand("Show budget", "CommandShowBudget", typeof(AppCommands));
            CommandExportData = new RoutedUICommand("Export...", "CommandExportData", typeof(AppCommands));
        }
    }
}
