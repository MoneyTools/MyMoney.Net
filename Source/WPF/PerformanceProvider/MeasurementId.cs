namespace Walkabout.PerformanceProvider
{
    public enum MeasurementId
    {
        MainWindowInitialize = 50,
        Load = 51,
        Loaded = 52,
        AppInitialize = 53,

        TransactionViewInitialize = 100,
        ViewTransactions = 101,
        UpdateView = 102,

        DownloadStockQuoteHistory = 200,
        UpdateStockQuoteHistory = 201,

        GraphGenerate = 300,
        GraphPrepare = 301,
        UpdateCharts = 302,

        SecuritiesControlInitialize = 400,
        ScanAttachments = 401,
        PayeesControlInitialize = 402,
        Indexing = 403,
        CategoryChartInitialize = 404,
        CategoriesControlInitialize = 405,
        AreaChartInitialize = 406,
        AccountsControlInitialize = 407,

        PrepareContainerForItemOverride = 500, // DataGrid

    }
}
