# Statements

It is handy to be able to link your [Balanced Accounts](BalancingAccounts.md) with
the actual statement you got from your bank so that you can jump to that statement
from any balanced transaction by selecting the `Goto Statement` context menu item.

You can provide a statement (usually a pdf file) while you are balancing an account
by clicking browse next to the Statement file name field.  MyMoney will copy the statement
to a special Statements folder next to your MyMoney database file and it will build
and index of these statements so that later when you select `Goto Statement` on
a reconciled transaction it will be able to find and open that statement.

Sometimes your bank might consolidate multiple accounts in one statement.  For
example, it is common for banks to combine the statement for your Savings account
and your Checking account in one statement.  In this situation, you should provide
the same statement file in each of your accounts during balancing.  MyMoney is smart enough to
store only one copy of the file and it will be available from the reconciled
transactions from either account.