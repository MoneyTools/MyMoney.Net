# Portfolio Report

If you select the "**Investment Portfolio** " option under the Reports menu you will get a an all up report, and if you click the [Portfolio Tab](../Accounts/InvestmentAccounts.md) on a single [Investment Accounts](Investment%20Accounts) you will get a portfolio
report for a single account.

 The all up report ignores transfers between your accounts.  This report will not include cash balances from bank accounts, or liabilities from any outstanding Loans or any other [Assets](../Accounts/Assets.md).  See [Networth Report](NetworthReport.md) for that.

![](../Images/Investment%20Portfolio.png)

The Gain/Loss is computed from when the stocks were purchased.   If you marked an account as tax deferred in the Account properties, then the securities in that account will be separated out in this pie chart prefixed with "**Tax Deferred** ".  Similarly Retirement accounts are also grouped separately.

The above summary is followed by a list of all positions still held (after accounting for all Sell/Remove transactions) grouped by the security type.  For example, you will see a summary like this:

![](../Images/Investment%20Portfolio1.png)

Each row here is expandable if you click on the little triangle on the left. For example, if you expand "**Microsoft corp total** " here you will see a list of details like this:

![](../Images/Investment%20Portfolio2.png)

This list contains a row for each unique buy date and unit price when you bought this stock.  So each row then has unique "[*cost basis](../Accounts/CostBasis.md)" which is why it is separated this way, and lists only those purchases that still have unsold shares.  Stock sales are matched in First-in First-out (FIFO) order since you usually minimize capital gains taxes that way.  If you sold your stock a different way, then this report will not be correct. 

The **Quantity**  column then is the current number of shares that you own today (taking stock splits into account) rounded to the nearest integer.  If this number is not what you expected, then check the Stock Split history in the Securities View. 

The **Market Value**  then is this Quantity times the current market price.  The Gain/Loss represents your gain or loss since you purchased that security.  The Unit Cost is what you paid for the security and the Cost Basis is what you paid, but adjusted by any stock splits. 

See [Tax Report](TaxReport.md) for information on how to export capital gains tax information into Turbo Tax.




