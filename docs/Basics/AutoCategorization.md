# Auto Categorization

When transactions are downloaded they will not have any "Category" information.  But often times the category is 1:1 related to the "Payee".  For example, you usually buy "Auto:Fuel" from ARCO, or you normally by "Food:Groceries" from Safeway.

When you first enter an empty Category field, MyMoney will automatically find the most likely category and enter it for you.  The auto-categorization works best if you have already filled in the "Amount" of the transaction since it uses the following algorithm:

1. Find all other transactions      involving this **Payee**  and group them by their categories.
1. Find the mean for each group      and find how many standard deviations the new amount is from that mean.
1. Pick the group that is      closest.
1. Try and avoid picking a      matching transaction that contains a Split unless there's no other choice.


To illustrate how well this works, you can use this to figure out which car was just filled with gas based on the amount spent (assuming the cars are different - for example, it will easily tell the difference between an SUV and a Hybrid).  Obviously it works better if you fill the car at a consistently empty state.  But the statistical nature of the algorithm can handle some variation here also.

This works best if your Payee names are normalized using payee aliasing.

[Home](../index.md) | [Categories](Categories.md) | [Aliases](Aliases.md)
