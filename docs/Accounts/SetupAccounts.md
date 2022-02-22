# Creating Accounts

There are two ways to setup accounts.  You can do it manually using the following context menu item available in the accounts panel:

![](../Images/Setup%20Accounts.png)

Or you can download your accounts using the [Online Banking](OnlineBanking.md) features.

If you are importing data from QFX files then the QFX file contains the account names, and import will automatically create the accounts for you.  **So QFX format is preferred over QIF.**   Some banks also provide OFX file download, which is just as good as QFX.  If you have to import QIF files then you will need to create the accounts first.

Either way, the New Account dialog contains the following information.  You can also get back to this dialog by right clicking on the account and selecting **Properties** :

![](../Images/Setup%20Accounts1.png)

Most of these fields are optional, except Name, and Account Number.  Name is the name of the account you would like to use, doesn't have to match your bank account name.

**Account number**  is the number that uniquely identifies this account.

**Online Account Id** :  is used to match downloaded transactions, so needs to be correct.  Sometimes these can be a bit tricky - for example Fidelity removes the "-" connector.  So if you want to use the [Online Banking](OnlineBanking.md) feature you should "Add" the account that way to be sure the account number is correct.  See also [Account Aliases](AccountAliases.md)**.**

The **Description**  is entirely optional.

The **Account Type** combo shows

- The following choices:

  ![image](../Images/Setup%20Accounts2.png)

- **Savings**  and **Checking**  are designed for [Bank Accounts](BankAccounts.md).   MoneyMarket is just another kind of savings account.

- **Cash**  is like a bank account, and can be used to track petty cash,

- **Credit**  is for credit cards but is very similar to bank accounts.  See [Credit Card Accounts](CreditCardAccounts.md).

- **Brokerage** is for brokerage accounts.  See [Investment Accounts](InvestmentAccounts.md).

- **Retirement**  is for retirement accounts like 401k, 403b, and Roth IRA. See [Investment Accounts](InvestmentAccounts.md).

- **Asset**  is an account that you can deposit assets into just so they get added to your net worth statement.  See [Assets](Assets.md)**.**



The **Tax Status** combo allows you to specify if the account is Taxable, Tax Deferred or Tax Free.  This impacts how
taxable gains are computed in the [Networth](../Reports/NetworthReport.md) and [Investment Portfolio](../Reports/InvestmentPortfolio.md). 
For example, create a Tax Deferred 401k account or a Tax Free Roth IRA account.

The **Opening Balance**  is the amount that you need to enter in order to start balancing the account, so whatever month you want start from, grab the ending balance from your previous bank statement.

The **Online Account**  field is how you connect your account to [Online Banking](OnlineBanking.md). 

The **Currency**  is optional and is only needed if you plan to have multiple accounts with different currencies.

The **Web Site**  is a URL link to the website for your bank or credit card and the ">>" button will bring this up in your web browser.

The **Aliases**  are the strings you want to match this account with when downloading OFX or QFX transactions from your bank, see [Account Aliases](AccountAliases.md)**.**

The **Reconcile Warning**  field helps you remember to balance your accounts before your bank removes the online statements.  See [Balancing Accounts](BalancingAccounts.md).

The **Closed**  checkbox simply removes the account from the default view to cleanup the list of accounts.  Keeping these around can help with transfers from accounts that are still open.  Closing an account does not remove any data.  You can hide and show closed accounts using this "Display Closed Accounts" command on the Accounts Panel context menu:

You probably never need to delete any data.  15 years of transactions for a single family is about 15 megabytes.  So with the size of hard drives these days, you can easily store all your transactions for life, including [Attachments](../Basics/Attachments.md).  Of course, I would then recommend the SQL Lite database which will be able to handle bigger databases than the XML file formats.

The **Last Download**  date can be used to reset the Online Account synchronization date. 


