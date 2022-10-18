using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Walkabout.Data;
using Walkabout.Migrate;

namespace Walkabout.Migrate
{
    public class XmlImporter : Importer
    {
        Dictionary<long, long> remappedIds = new Dictionary<long, long>();
        Account last;

        public XmlImporter(MyMoney money) : base(money)
        {
        }

        public Account LastAccount => last;

        public override int Import(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".xml")
            {
                return ImportXml(file);
            }
            else
            {
                throw new NotSupportedException("File extension " + ext + " is not yet supported");
            }
        }

        /// <summary>
        /// Import the Transactions & accounts in the given file and return the first account.
        /// </summary>
        /// <param name="file"></param
        /// <returns></returns>
        internal int ImportXml(string file)
        {
            int c = 0;
            using (XmlReader reader = XmlReader.Create(file))
            {
                foreach (object o in ImportObjects(reader, null, null))
                {
                    Account a = o as Account;
                    if (a != null)
                    {
                        this.last = a;
                    }
                    Transaction t = o as Transaction;
                    if (t != null)
                    {
                        c++;
                    }
                }
            }
            return c;
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

            // do not copy & paste any status.
            t.Status = TransactionStatus.None;

            // remove the nont-duplicate flag.
            t.Flags = t.Flags & ~(TransactionFlags.NotDuplicate);

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

    }
}
