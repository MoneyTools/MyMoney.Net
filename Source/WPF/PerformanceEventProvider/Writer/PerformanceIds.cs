using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.VisualStudio.Diagnostics.PerformanceProvider
{

    public enum ComponentId
    {
        None = 0,

        WPF = 1,        

        Money = 2
    }

    public enum CategoryId
    {
        None = 0,
        
        Model = 200,

        View = 300,
        
        Controller = 400,
    }

    public enum MeasurementId
    {
        None = 0,

        // Model
        Load = 200,
        ScanAttachments = 201,

        // View
        AppInitialize = 300,
        MainWindowInitialize = 301,
        TransactionViewInitialize = 302,
        AccountsControlInitialize = 303,
        CategoriesControlInitialize = 304,
        PayeesControlInitialize = 305,
        SecuritiesControlInitialize = 306,
        AreaChartInitialize = 307,
        CategoryChartInitialize = 308,

        Loaded = 310,

        PrepareContainerForItemOverride = 320,

        ViewTransactions = 330,
        UpdateCharts = 331,
        UpdateStockQuoteHistory = 332,
        LoadStockDownloadLog = 333,
    }
}
