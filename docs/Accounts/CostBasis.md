# Cost Basis

The [Investment Portfolio](../Reports/InvestmentPortfolio.md) report computes the cost basis of shares that you still own based on when those shares were added to  your money accounts, taking into account any stock splits that have been recorded for those securities (see [Securities](../Basics/Securities.md)).  The [Tax Report](../Reports/TaxReport.md) also computes long term and short term capital gains based on those same Cost Basis calculations.

For example, suppose you bought 100 shares of Microsoft stock on  1/1/2001

![](../Images/Cost%20Basis.png)

Then if you enter the correct stock split information you would have a 2 for 1 split recorded on 2/18/2003:

![](../Images/Cost%20Basis1.png)

This means the portfolio report will show a calculation of your Cost Basis and total Gain/Loss showing your cost basis is $15 (half the $30 you paid before the stock split). The quantity will be doubled because in today's terms the stock split in 2003 means you now own 200 shares. 

Now it gets more interesting if you have several purchases on different dates each with different cost basis, then you do a single sale to sell all your holdings.  The sale has to record the correct cost basis in order compute the correct long or short term capital gains.

For example, suppose you have this:

![](../Images/Cost%20Basis2.png)

Then the portfolio report will double the units in the first 2 transactions but not the 3rd since 2013 comes after the last stock split.  So this leaves a total of 350 shares and the unit price is the weighted average, and the Cost Basis is the sum of what you paid for all the shares.  The Gain/Loss is computed using today's stock price.  Yeah, I wish I bought more :-)

![](../Images/Cost%20Basis3.png)

Now if you sell some shares:
![](../Images/Cost%20Basis4.png)

The capital gains tax report will report two separate events for the sale since they have different dates acquired, and therefore different cost basis.   Notice the stock split also means the first lot has half the acquisition price and therefore much higher gain.  It also determines if the gain is long term or short term capital gain.

![](../Images/Cost%20Basis5.png)

### Transfers

Now transfers can make this even more interesting.  Suppose you transferred those MSFT shares to a different account before selling them.  Then the first account will show this:

![](../Images/Cost%20Basis6.png)

And the Fidelity account will show this:

![](../Images/Cost%20Basis7.png)

But the original cost basis information has to flow over to the other account with the "transfer".  So in this case the Tax report will show the exact same thing.

### FIFO Rule

Now in computing the capital gains the assumption is made that when you sell some stocks you want to sell from your oldest holdings first in order to minimize capital gains taxes.  So the FIFO rule is used in matching sales with purchases.  This is why the above Capital Gains report shows the 250 shares were drawn from the first lot dated 2/8/2001 and the second lot dated 12/5/2001 and after this sale your portfolio report lists the remainder of 50 shares dated 12/5/2001 and the untouched third lot dated 1/7/2013 containing 50 shares.

![](../Images/Cost%20Basis8.png)







