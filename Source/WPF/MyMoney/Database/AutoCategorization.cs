﻿using System;
using System.Collections.Generic;
using System.Linq;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    internal class AutoCategorization
    {
        private KNearestNeighbor<Category> singleNeighbors = new KNearestNeighbor<Category>();
        private KNearestNeighbor<Category> splitNeighbors = new KNearestNeighbor<Category>();
        private int splitCount;
        private int normalCount;

        /// <summary>
        /// Find the best category match for given payee and amount from past history of transactions and splits.
        /// </summary>
        /// <param name="t">The current transaction</param>
        /// <param name="payeeOrTransferCaption">The payee we are matching</param>
        /// <returns></returns>
        public static object AutoCategoryMatch(Transaction t, string payeeOrTransferCaption)
        {
            MyMoney money = t.MyMoney;
            object found = null;
            Account a = t.Account;
            if (a != null)
            {
                AutoCategorization ac = new AutoCategorization();
                found = ac.FindPreviousTransactionByPayee(a, t, payeeOrTransferCaption);
                if (found == null)
                {
                    // try other accounts;
                    foreach (Account other in money.payeeAccountIndex.FindAccountsRelatedToPayee(payeeOrTransferCaption))
                    {
                        found = ac.FindPreviousTransactionByPayee(other, t, payeeOrTransferCaption);
                        if (found != null)
                        {
                            break;
                        }
                    }
                }
            }
            return found;
        }

        private object FindPreviousTransactionByPayee(Account a, Transaction t, string payeeOrTransferCaption)
        {
            MyMoney money = t.MyMoney;
            IList<Transaction> list = money.Transactions.GetTransactionsFrom(a);
            int len = list.Count;

            if (len == 0)
            {
                // Nothing to do here
                return null;
            }

            // Tally of how close the current transaction is to the amounts for a given category or split.
            this.singleNeighbors = new KNearestNeighbor<Category>();
            this.splitNeighbors = new KNearestNeighbor<Category>();
            this.splitCount = this.normalCount = 0;

            Transaction closestByDate = null;
            long ticks = 0;

            decimal amount = t.Amount;
            for (int i = 0; i < len; i++)
            {
                Transaction u = list[i];
                if (amount == 0)
                {
                    // we can't use the probabilities when the amount is zero, so we just return
                    // the closest transaction by date because in the case of something like a paycheck
                    // the most recent paycheck usually has the closest numbers on the splits.
                    if (u.Category != null && u.Transfer == null && u.Payee != null && string.Compare(u.PayeeOrTransferCaption, payeeOrTransferCaption, true) == 0)
                    {
                        long newTicks = Math.Abs((u.Date - t.Date).Ticks);
                        if (closestByDate == null || newTicks < ticks)
                        {
                            closestByDate = u;
                            ticks = newTicks;
                        }
                    }
                }
                else if (u.Amount != 0)
                {
                    this.AddPossibility(t, u, payeeOrTransferCaption);
                }
            }

            if (closestByDate != null)
            {
                return closestByDate;
            }

            IEnumerable<Tuple<object, Category>> result = null;
            if (this.splitCount > this.normalCount)
            {
                result = this.splitNeighbors.GetNearestNeighbors(1, t.Amount);
            }
            else
            {
                result = this.singleNeighbors.GetNearestNeighbors(1, t.Amount);
            }

            if (result != null && result.Any())
            {
                var first = result.First();
                var it = first.Item1 as Transaction;
                if (it != null && it.IsSplit)
                {
                    closestByDate = null;
                    ticks = 0;

                    // if this is a "Split" transaction, then we should grab the closest date
                    // so that the copied splits have the best chance of matching.
                    // (e.g. in a split paycheck scenario)
                    foreach (var u in list)
                    {
                        if (u.IsSplit && u.PayeeOrTransferCaption == t.PayeeOrTransferCaption)
                        {
                            long newTicks = Math.Abs((u.Date - t.Date).Ticks);
                            if (closestByDate == null || newTicks < ticks)
                            {
                                closestByDate = u;
                                ticks = newTicks;
                            }
                        }
                    }
                    return closestByDate;
                }

                return first.Item1;
            }

            return null;
        }

        private void AddPossibility(Transaction t, Transaction u, string payeeOrTransferCaption)
        {
            if (u != t && u.Category != null && u.Payee != null && string.Compare(u.PayeeOrTransferCaption, payeeOrTransferCaption, true) == 0)
            {
                if (u.IsSplit)
                {
                    foreach (var s in u.Splits)
                    {
                        if (s.Payee == null && s.Category != null && s.Amount != 0)
                        {
                            this.singleNeighbors.Add(s, s.Category, s.Amount);
                        }
                    }
                    this.splitNeighbors.Add(u, u.Category, u.Amount);
                    this.splitCount++;
                }
                else
                {
                    this.normalCount++;
                    // absolute value because for this purpose of categorization we don't care if it was 
                    // a purchase or refund on that category.
                    this.singleNeighbors.Add(u, u.Category, u.Amount);
                }
            }
        }
    }
}
