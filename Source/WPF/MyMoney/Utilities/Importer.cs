using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Runtime.Serialization;
using Walkabout.Data;

namespace Walkabout.Migrate
{
    /// <summary>
    /// This class matches the Exporter in terms of being able to import the XML or CSV data that was exported by the Exporter
    /// This class does not handle QIF or OFX, for those we have dedicated importers as they are more complicated.
    /// </summary>
    public class Importer
    {
        Dictionary<long, long> remappedIds = new Dictionary<long, long>();

        public Importer(MyMoney money)
        {
            this.Money = money;
        }

        public MyMoney Money { get; set; }

        internal Account Import(string file, out int count)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".xml")
            {
                return ImportXml(file, out count);
            }
            else
            {
                throw new NotSupportedException("File extension " + ext + " is not yet supported");
            }

        }

        /// <summary>
        /// Import the Transactions & accounts in the given file and return the first account.
        /// </summary>
        /// <param name="file"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        internal Account ImportXml(string file, out int count)
        {
            int c = 0;
            Account acct = null;
            using (XmlReader reader = XmlReader.Create(file))
            {
                foreach (object o in ImportObjects(reader, null, null))
                {
                    Account a = o as Account;
                    if (a != null)
                    {
                        acct = a;
                    }
                    Transaction t = o as Transaction;
                    if (t != null)
                    {
                        c++;
                    }
                }
            }
            count = c;
            return acct;
        }

        static DataContractSerializer AccountSerializer = new DataContractSerializer(typeof(Account));
        static DataContractSerializer TransactionSerializer = new DataContractSerializer(typeof(Transaction));
        static DataContractSerializer InvestmentSerializer = new DataContractSerializer(typeof(Investment));
        static DataContractSerializer SplitSerializer = new DataContractSerializer(typeof(Split));

        /// <summary>
        /// Import the given XML content which can contain Account, Transaction, Investment or Split objects.
        /// It deserializes each type of object and merges the accounts with existing accounts, and returns the
        /// Transactions, Investment and/or Split objects unmerged, leaving it up to the caller to figure out
        /// how to merge them.  It also adds the transactions and investments to the given selected account.
        /// Splits are added to the given selectedTransaction if any.
        /// </summary>
        public IEnumerable<object> ImportObjects(XmlReader reader, Account selected, Transaction selectedTransaction)
        {
            List<object> result = new List<object>();
            Money.BeginUpdate(this);
            try
            {
                    reader.MoveToElement();
                    while (!reader.EOF)
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            switch (reader.LocalName)
                            {
                                case "Account":
                                    Account a = (Account)AccountSerializer.ReadObject(reader, false);
                                    a = MergeAccount(a);
                                    result.Add(a);                                    
                                    break;
                                case "Transaction":
                                    Transaction t = (Transaction)TransactionSerializer.ReadObject(reader, false);
                                    Account ta = selected;
                                    if (ta == null)
                                    {
                                        // this code path is used when importing entire account from a file.
                                        ta = Money.Accounts.FindAccount(t.AccountName);
                                    }
                                    AddTransaction(ta, t);
                                    result.Add(t);
                                    break;
                                case "Split":
                                    Split s = (Split)SplitSerializer.ReadObject(reader, false);      
                                    if (selectedTransaction != null)
                                    {
                                        s.PostDeserializeFixup(this.Money, selectedTransaction, true);
                                        s.Id = -1;
                                        selectedTransaction.NonNullSplits.AddSplit(s);
                                    }
                                    result.Add(s);
                                    break;
                                case "Investment":
                                    Investment i = (Investment)InvestmentSerializer.ReadObject(reader, false);
                                    if (i.SecurityName != null)
                                    {
                                        result.Add(i);
                                        AddInvestment(selected, i);
                                    }
                                    break;
                                default:
                                    reader.Read();
                                    break;
                            }
                        }
                        else
                        {
                            reader.Read();
                        }
                    }
            }
            finally
            {
                Money.EndUpdate();
            }
            return result;
        }

        public Account ImportAccount(string xml)
        {
            DataContractSerializer accountSerializer = new DataContractSerializer(typeof(Account));
            try
            {
                using (var sr = new StringReader(xml))
                {
                    using (XmlReader reader = XmlReader.Create(sr))
                    {
                        Account a = (Account)accountSerializer.ReadObject(reader, false);
                        MergeAccount(a);
                        return a;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private Account MergeAccount(Account a)
        {
            Account found = Money.Accounts.FindAccount(a.Name);
            if (found == null)
            {
                a.PostDeserializeFixup(Money);
                a.Id = -1;
                Money.Accounts.AddAccount(a);
                found = a;
            }
            return found;
        }


        private void AddTransaction(Account a, Transaction t)
        {
            if (a == null)
            {
                throw new Exception("Cannot add transactions before we find account information in the imported xml file");
            }

            t.PostDeserializeFixup(Money, Money.Transactions, a, true);

            if (t.Unaccepted)
            {
                t.Account.Unaccepted++;
            }
            if (t.Status == TransactionStatus.Reconciled)
            {
                // do not copy & paste reconciled status.
                t.Status = TransactionStatus.Cleared;
            }

            long originalId = t.Id;
            t.Id = -1;
            t.Account = a;
            Money.Transactions.Add(t);

            remappedIds[originalId] = t.Id;

        }

        private void AddInvestment(Account a, Investment i)
        {
            if (a == null)
            {
                throw new Exception("Cannot add investments before we find account information in the imported xml file");
            }

            long id;
            if (!remappedIds.TryGetValue(i.Id, out id))
            {
                throw new Exception("Cannot find original transaction mentioned by this investment");
            }

            string name = i.SecurityName;
            i.Security = Money.Securities.FindSecurity(name, true);

            Transaction u = Money.Transactions.FindTransactionById(id);
            if (u != null)
            {
                Investment j = u.GetOrCreateInvestment();
                j.Merge(i);
            }
            else
            {
                throw new Exception("Cannot add investment on a transaction that doesn't exist yet");
            }
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
            foreach (Transaction u in Money.Transactions)
            {
                if (u.IsDeleted)
                    continue;
                if (u.Date == t.Date && u.Account == from && u.Payee == t.Payee)
                {
                    if (u.IsSplit)
                    {
                        Transfer stran = FindSplitTransfer(u.Splits, t, from);
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
            foreach (Transaction u in Money.Transactions)
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
