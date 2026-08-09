# Retirement Plan Report

If you have a brokerage account containing taxable assets, perhaps also some tax deferred accounts like 401k or
traditional IRA, and some tax free accounts like a Roth IRA, this report can plot out what your retirement income would
look like, how your retirement funds could be used, and lets you explore whether you should do a Roth conversion on any
tax deferred accounts.

To setup the report you need to enter some values in the RETIREMENT details as follows:

![retirement settings](../Images/RetirementSettings.png)

Then you will see the following if you started with:

- $3.7 million in a taxable brokerage account (with some cost basis)
- $1.4 million in a 401k account
- $2 million in a Roth IRA tax free account.

The first part of the report shows a summary of assets remaining at the end date (in this case age 90):

- Assets remaining at age : 90 = $8,071,236
- Taxable assets = $0
- Tax deferred assets = $402,249
- Tax free assets = $7,668,987
- Total taxes paid during retirement = $1,431,130

And then it shows some charts, the first one is a chart showing how each class of asset grows and/or is drawn down over
the years as follows:

![retirement networth](../Images/RetirementNetworth.png)

The next chart shows where your retirement income is coming from, with different color bars for different types of
income. The darker brown bar is the additional income you need to take in order to pay the necessary taxes (if any).

![retirement income](../Images/RetirementIncome.png)

This shows that you start off tapping into taxable assets until age 75 when required minimum distribution kicks in on
your tax deferred accounts. In this case we start a bit earlier at 73 because by then your taxable account is empty.
Then by 79 the tax deferred account is empty and you switch to tax free assets until age 90.  Fortunately the tax free
account had plenty of time to grow so this will see you though nicely.

The final chart shows how much tax you had to pay each year which is paid from your retirement funds (over and above the
desired income):

![retirement taxes](../Images/RetirementTaxes.png)

Here you normally see a jump in taxes when any Tax Deferred account required minimum distributions (RMD's) kick in or
when you enter a new tax bracket. Retirement planning is often about managing those tax brackets.

You can then try different strategies on your tax deferred account to see if you can minimize the total tax.  For
example if you enable the Roth Conversion strategy over 10 years you will see the following improvements:

| Metric | Without Roth conversion | With Roth conversion |
| --- | ---: | ---: |
| Net worth at age 90 | $8,071,236 | $8,427,076 |
| Total taxes | $1,431,130 | $919,341 |

This is a nice win/win situation.  This is not always the case, it depends on your situation.  In this case the Roth
conversion probably helps by reducing any additional taxes incurred by RMD's.
The Roth conversion is also great for estate planning as tax free accounts do not pass on a tricky tax burden to your beneficiaries.

Note that all of this is just an estimate based on the inflation rate and rate of return predictions you provided and it
does not compute your real cost basis for capital gains taxes, it uses an estimated capital gains based on your current
holdings.
