# Payees

As you enter Payees they are automatically shown in the Payees panel on the left.  If a Payee is no longer used by any transactions it is automatically removed next time you save the database.  When you select a payee from the Payee panel, all transactions that specify that Payee appear and the charts then show a summary of those transactions.

![](../Images/Payees.png)

You can also get to this view from any transaction using Right Click "View By Payee".  Once you are in this view you can also select a transaction and get back to the account that transaction belongs in using "View By Account" or by using the back button.

You can also view a breakdown of the types of categories this Payee is involved in:

![](../Images/Payees1.png)

You can click a slice of the pie to see a "View by Category" for just the transactions involving this Payee and so on.  Slicing and dicing is very simple and fast.

**Renaming Payees**

When you download transactions from your Financial Institution you probably will get some pretty weird Payee information.  For example, you might get something like this:

    SAFEWAY      STORE00005330       WOODINVILLE      WA

When you'd rather have something like this:

    Safeway

You can right click such a transaction and select "**Rename Payee** "

![](../Images/Payees2.png)

Enter the new name and press OK.  This will do a one time rename across all your transactions.  Don't worry it is quick.

But even better, you can select the "**Auto-Rename** " checkbox and next time MyMoney downloads a transaction from your bank matching this "From" field, it will automatically replace it with the string your prefer.  This is called an [Alias](Aliases.md).

Aliases are important for "[Auto Categorization](AutoCategorization.md)" to work nicely.

If you use the [Regular
Expression](https://docs.microsoft.com/en-us/dotnet/standard/base-types/regular-expression-language-quick-reference) feature combined with auto-rename you can
make your auto-categorization more general.  For example, suppose you enter the following expression to match all Payee names starting with `WAL-MART` like this:

![](../Images/Payees3.png)

Then what happens here is it shows you a bunch of previous Alises you created
that will be subsumed by your new more general regular expression.  When you
click OK, all those previous Aliases will be removed and replaced by the one
more general `Regex` alias `WAL-MART.*`.

You can also do this consolidation in the [Alias View](Aliases.md).




