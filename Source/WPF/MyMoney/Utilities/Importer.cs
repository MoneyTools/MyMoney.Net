using Walkabout.Data;

namespace Walkabout.Migrate
{
    /// <summary>
    /// This class matches the Exporter in terms of being able to import the XML or CSV data that was exported by the Exporter
    /// This class does not handle QIF or OFX, for those we have dedicated importers as they are more complicated.
    /// </summary>
    public abstract class Importer
    {
        public Importer(MyMoney money)
        {
            this.Money = money;
        }

        public MyMoney Money { get; set; }

        public virtual int Import(string file)
        {
            return 0;
        }

        /// <summary>
        ///  Try and find a transfer matching this transaction from another account that has not been hooked up as a real Transfer yet.
        ///  This is only used during QIF importing when we find QIF Category starting with '[' which means we are importing a transfer.
        /// </summary>
        /// <param name="t">The transaction to match</param>
        /// <param name="from">The account from </param>
        /// <returns></returns>
        public Transfer FindMatchingTransfer(Transaction t, Account from)
        {
            foreach (Transaction u in this.Money.Transactions)
            {
                if (u.IsDeleted)
                    continue;
                if (u.Date == t.Date && u.Account == from && u.Payee == t.Payee)
                {
                    if (u.IsSplit)
                    {
                        Transfer stran = this.FindSplitTransfer(u.Splits, t, from);
                        if (stran != null)
                        {
                            return stran;
                        }
                    }
                    else if (u.Amount == -t.Amount)
                    {
                        if (u.Transfer != null)
                        {
                            if (u.Transfer.Transaction == t && u.Transfer.Split == null)
                            {
                                return u.Transfer;
                            }

                            // another exact same transfer on the same day is already hooked up to a different transaction
                            // weird, but it happens sometimes when people transfer money to the wrong account then fix it. 
                            continue;
                        }

                        // hook it up!
                        return u.Transfer = new Transfer(0, u, t);
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// Find a split matching the given transaction.
        /// This is only used during QIF importing to link up transfers.
        /// </summary>
        /// <param name="t">The transaction we are importing</param>
        /// <param name="to"></param>
        /// <returns></returns>
        public Transfer FindSplitTransfer(Splits splits, Transaction t, Account to)
        {
            foreach (Split s in splits.GetSplits())
            {
                if (s.Amount == -t.Amount && s.Category == t.Category)
                {
                    if (s.Transfer != null)
                    {
                        if (s.Transfer.Transaction == t)
                        {
                            // already hooked up then!
                            return s.Transfer;
                        }

                        // another exact same transfer on the same day is already hooked up to a different transaction.
                        // weird, but it happens sometimes when people transfer money to the wrong account then fix it.
                        continue;
                    }

                    // Hook it up then, this Split looks to be the other side of the transfer transaction 't'.
                    return s.Transfer = new Transfer(0, s.Transaction, s, t);
                }
            }
            return null;
        }

        /// <summary>
        ///  Try and find a transfer matching this split from another account that has not been hooked up as a real Transfer yet.
        ///  This is only used during QIF importing when we find QIF Category starting with 'S[' which means we are importing a transfered split.
        /// </summary>
        /// <param name="s">The split to match</param>
        /// <param name="to">The account the split is transfered to</param>
        /// <returns></returns>
        public Transfer FindMatchingSplitTransfer(Split s, Account to)
        {
            Transaction t = s.Transaction;
            foreach (Transaction u in this.Money.Transactions)
            {
                if (u.IsDeleted)
                    continue;

                if (!u.IsSplit && u.Date == t.Date && u.Account == to && u.Payee == t.Payee &&
                    u.Amount == -s.Amount && u.Category == s.Category)
                {
                    if (u.Transfer != null)
                    {
                        if (u.Transfer.Transaction == t && u.Transfer.Split == s)
                        {
                            // already hooked up!
                            return u.Transfer;
                        }

                        // another exact same transfer on the same day is hooked up to a different transfer.
                        // weird, but it happens sometimes when people transfer
                        // money to the wrong account then fix it. 
                        continue;
                    }

                    // hook it up then!
                    return u.Transfer = new Transfer(0, u, t, s);
                }
            }
            return null;
        }

    }
}
