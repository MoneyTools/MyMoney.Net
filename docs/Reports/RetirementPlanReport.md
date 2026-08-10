# Retirement Plan Report

If you have a brokerage account containing taxable assets, perhaps also some tax deferred accounts like 401k or
traditional IRA, and some tax free accounts like a Roth IRA, this report can plot out what your retirement income would
look like, how your retirement funds could be used, and lets you explore whether you should do a Roth conversion on any
tax deferred accounts.

To setup the report you need to enter some values in the RETIREMENT details as follows:

![retirement settings](../Images/RetirementSettings.png)

Then you will see the following if you started with:

- $3.7 million in a taxable brokerage account (with some cost basis)
- $1.4 million in a 401k tax deferred account
- $2 million in a Roth IRA tax free account.

The first part of the report shows a summary of assets remaining at the end date (in this case age 90):

- Assets remaining at age 90 = $6,777,737
- Taxable assets = 0
- Tax deferred assets = 0
- Tax free assets = $6,777,737
- Total taxes paid during retirement = $2,016,593

And then it shows some charts, the first one is a chart showing how each class of asset grows and/or is drawn down over
the years as follows:

![retirement networth](../Images/RetirementNetworth.png)

You can see here the retirement planner prefers not to use tax free assets, as this asset gives you more control and makes estate planning easier on beneficiaries.

The next chart shows where your retirement income is coming from, with different color bars for different types of
income. The darker brown bar is the additional income you need to take in order to pay the necessary taxes.

![retirement income](../Images/RetirementIncome.png)

This shows that you start off tapping into taxable assets until age 75 when required minimum distribution kicks in on
your tax deferred accounts.
Then by 88 the taxable account is empty and you have to take more from your tax deferred assets which causes a jump in taxes because those are taxed as income.

The next chart shows how much tax you had to pay each year, which is paid from your retirement funds also:

![retirement taxes](../Images/RetirementTaxes.png)

Here you normally see a jump in taxes when any Tax Deferred account required minimum distributions (RMD's) kick in or
when you enter a new tax bracket. Retirement planning is often about managing those tax brackets.

The final chart runs a Roth conversion simulation passing in diffferent years to implement the roth conversion from 0 (no conversion which we have above) out to 15 years, plotting the networth at age 90 and the total tax paid over each simulation. as follows:

![retirement roth](../Images/RetirementRoth.png)

This can help you figure out the optimal roth conversion strategy to maximize assets
and minimize taxes.  In this case the plot shows that minimal taxes happen with a
11 year conversion, but the maximum net worth happens with a 15 year conversion which shows that just minimizing taxes is not always the best
strategy.

You can then plug these numbers into the settings panel here:

![roth settings](../Images/RetirementRothSettings.png)

And see the details as the report updates.

Roth strategy is complicated and you will not always see these benefits. It depends on your situation.  In this case the
Roth conversion probably helps by reducing any additional taxes incurred by RMD's. The Roth conversion is also great for
estate planning as tax free accounts do not pass on a tricky tax burden to your beneficiaries.

Note that all of this is just an `estimate` based on the inflation rate and rate of return predictions you provided and it
does not compute your real cost basis for capital gains taxes, it uses an estimated capital gains based on your current
holdings.  This simulation also assumes today's (2026) tax brackets, and does not modify them over the years which we know
never happens. Chances are taxes will always go up, so this should be another consideration when it comes to minimizing
future taxes which the Roth conversation also helps with.
