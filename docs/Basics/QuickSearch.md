# Quick Search

At the top of many of the panels in MyMoney is a search box:

![](../Images/Quick%20Search.png)

This provides simple search capability.  You can enter any search string, hit ENTER and see a filtered list of transactions.

When a filtering search is in effect a close box appears:

![](../Images/Quick%20Search1.png)

If you click that close box, the filter is removed and all the hidden transactions are returned to view.

The matching is case-insensitive.

The search can be a boolean expression containing the following:
### not
Negates the set matched by expression on the right hand side.

## !
Same as "not"

### and
Finds items that have both strings

### &
Same as "and"

### or
Finds items that have either string

### |
Same as "or"

### ()
Parentheses can group sub expressions

### " "
Quotes can escape the above special symbols so they are matched as literals

### *
If your search starts with asterix (*), it will search all accounts rather than the currently selected account.


## For example:

![](../Images/Quick%20Search2.png)

find any transaction containing the string “costco” in any combination of upper or lower case.

![](../Images/Quick%20Search3.png)

find any transaction containing "costco" and "fuel", this is different from the above since the words could be anywhere in the string, not necessarily next to each other.

![](../Images/Quick%20Search4.png)

find any transaction containing either "costco" or "fuel" so now you might see fuel from other sources added to the matching transactions.

![](../Images/Quick%20Search5.png)

Find transactions containing both "costco" and "gas" or the word "fuel", so now you have just gas related transactions.

![](../Images/Quick%20Search6.png)

Find transactions containing the string "costco" but not the string "gas" so now you have all the other Costco transactions not involving buying gas.

![](../Images/Quick%20Search7.png)

Find transactions containing the literal string "costco &gas".  So the ampersand here is not treated as a logical operation because of the double quotes.

You can also do more [Advanced Queries](Queries.md) to find a specific set of transactions.


See also [Filtering](Filtering.md).



