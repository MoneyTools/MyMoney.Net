# Tax Report

You can associate your categories with "Tax Categories" using the field shown in the Category Properties dialog below:

![](../Images/Tax%20Report.png)

The tax categories are defined here: [http://turbotax.intuit.com/txf/TXF041.jsp](http://turbotax.intuit.com/txf/TXF041.jsp)

The "Reports" menu contains an "Income Tax Report" and a "W3 and Other Tax Forms" report.

### Income Tax Report
The income tax report provides the following options to select the year, and specify how you want the details to be reported and whether to show capital gains only.

![](../Images/Tax%20Report1.png)

The **Export**  button that will export a *.txf file which can be imported into Turbo Tax.

If you have set your Tax Categories correctly the report shows a summary for each Tax Category with a total for that each category. 

It also shows long term and short term capital gains (See [Cost Basis](../Accounts/CostBasis.md)) for any security sales you have in your investment accounts for the selected year.

To import into Turbo Tax, use the Import menu to import “**From Accounting Software** ” then pick “Other Financial Software (TXF file), then click “**Browse to Find File** �� and click Import and wha-la.  The following shows long term capital gains information imported from a txf file (which saves a lot of typing):

![](../Images/Tax%20Report2.jpeg)


**Known bugs:**
* 1099-DIV  “total      dividend income” doesn’t import into TurboTax properly yet – that requires      more work to fill out the total income versus qualified and non-qualified      and so on which needs more information.  The work around is to just      copy that information to turbo tax manually which is pretty easy to      do.  It might work if you split each transaction to provide all the      1099-DIV categories, but that is probably more work than is worth the      effort.
* Window resizing is really      slow when the Tax Report is visible for some reason, some sort of WPF flow      document bug I suspect. 






