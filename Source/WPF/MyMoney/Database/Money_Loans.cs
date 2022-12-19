using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;
using Walkabout.Utilities;

namespace Walkabout.Data
{
    public partial class MyMoney : PersistentObject
    {
        private LoanPayments loanPayments;
        private List<Loan> loans = new List<Loan>();

        [DataMember]
        public LoanPayments LoanPayments
        {
            get { return loanPayments; }
            set { loanPayments = value; loanPayments.Parent = this; }
        }

        public Loan GetOrCreateLoanAccount(Account a)
        {
            // This will have the side effect of updating the Account.Balance to the latest value
            Loan loan = (from l in loans where l.Account == a select l).FirstOrDefault();
            if (loan == null)
            {
                a.BatchMode = true;
                // BUGBUG: this is too expensive as GetLoanPaymentsAggregation walks all Transactions.GetAllTransactions
                // We need a more efficient way to associate loan payments with this loan account, doing it by category
                // alone seems wrong since what if you have multiple loans using the same categories?
                loan = new Loan(this, a);
                a.BatchMode = false;
                loans.Add(loan);
            }
            return loan;
        }
    }

    public class Loan
    {
        public Account Account { get; set; }

        internal void Rebalance()
        {
            // cannot carry a credit on a loan account, so if the balance is > 0 make it 0.
            this.Account.Balance = this.ComputeLoanAccountBalance(DateTime.Today);
        }

        public bool IsLiability { get; set; } // whether this loan is something you are paying off, or someone is paying you.

        public ObservableCollection<LoanPaymentAggregation> Payments { get; set; }

        public Loan(MyMoney money, Account a)
        {
            this.Account = a; // the loan account
            this.Payments = GetLoanPaymentsAggregation(money);
            var first = this.Payments.FirstOrDefault();
            if (first != null && first.Principal > 0)
            {
                IsLiability = true;
            }
            Rebalance();
        }

        /// <summary>
        /// Loans are built from 2 Categories - Principal + Interested
        /// </summary>
        /// <param name="toDate"></param>
        internal ObservableCollection<LoanPaymentAggregation> GetLoanPaymentsAggregation(MyMoney money)
        {
            List<LoanPaymentAggregation> view = new List<LoanPaymentAggregation>();

            int accountId = this.Account.Id;
            Category categoryPrincipal = this.Account.CategoryForPrincipal;
            Category categoryInterest = this.Account.CategoryForInterest;

            //-----------------------------------------------------------------
            // Get the loan transaction related to the 2 categories set 
            // to this account. Category for Principal & Category for Interest
            //
            foreach (Transaction t in money.Transactions.GetAllTransactions())
            {

                if (t.Splits != null && t.Splits.Count > 0)
                {
                    foreach (Split s in t.Splits)
                    {
                        if (s.Category != null)
                        {
                            AddPaymentIfMatchingCategoriesForPrincipalOrInterest(view, s.Category, categoryPrincipal, categoryInterest, t, s, t.Account, t.Date, s.Amount);
                        }
                    }
                }
                else
                {
                    if (t.Category != null)
                    {
                        AddPaymentIfMatchingCategoriesForPrincipalOrInterest(view, t.Category, categoryPrincipal, categoryInterest, t, null, t.Account, t.Date, t.Amount);
                    }
                }
            }

            //-----------------------------------------------------------------
            // Additional manual entry made for this Loan
            //
            foreach (LoanPayment l in money.LoanPayments)
            {
                if (!l.IsDeleted)
                {
                    if (l.AccountId == accountId)
                    {
                        LoanPaymentAggregation lp = new LoanPaymentAggregation();
                        lp.Account = this.Account;
                        lp.Date = l.Date;
                        lp.Principal = l.Principal;
                        lp.Interest = l.Interest;
                        lp.Payment = l.Principal + l.Interest;
                        lp.LoanPayementManualEntry = l;

                        view.Add(lp);
                    }
                }
            }

            //-----------------------------------------------------------------
            // Sort and recalculate the running balance for all the payment made
            // to this Loan
            //
            var sorted = from item in view
                         orderby item.Date ascending
                         select item;

            return new ObservableCollection<LoanPaymentAggregation>(sorted);
        }

        internal decimal ComputeLoanAccountBalance(DateTime date)
        {
            decimal runningBalance = 0;

            foreach (LoanPaymentAggregation l in this.Payments)
            {
                if (l.Date > date)
                {
                    break;
                }

                //-------------------------------------------------------------
                // Check to see if we need to re-calculate 
                // the Principal & Interest amounts using the Percentage
                //
                if (l.Principal == 0 && l.Interest == 0 && l.Percentage != 0)
                {
                    //
                    // Recalculate the Interest using the Percentage
                    //
                    l.Interest = runningBalance * (l.Percentage / 100) / 12;

                    // and the Principal if we know the total original Payment
                    l.Principal = l.Payment - l.Interest;
                }

                if (l.Interest != 0 && runningBalance != 0)
                {
                    // Reverse calculation of the interest rate
                    CalculatePercentageOfInterest(runningBalance, l);
                }

                // Reduce the debt by the amount of the Principal paid

                //
                // -1 will reverse the amount if this is a mortgage it will change this to -900 * -1 == 900
                //
                runningBalance += l.Principal * -1;

                // Snap shot the balance due at each payment
                l.Balance = runningBalance;
            }

            return runningBalance;
        }

        public static void CalculatePercentageOfInterest(decimal runningBalance, LoanPaymentAggregation l)
        {
            if (runningBalance == 0)
            {
                l.Percentage = 0;
            }
            else
            {
                l.Percentage = ((l.Interest * 12) / runningBalance) * 100;
            }
        }

        private static LoanPaymentAggregation AddPaymentIfMatchingCategoriesForPrincipalOrInterest(
            IList<LoanPaymentAggregation> view,
            Category category,
            Category categoryPrincipal,
            Category categoryInterest,
            Transaction t,
            Split s, // Could be null if this is a basic single transaction with no split, 
            Account transactionAccount,
            DateTime date,
            decimal amount
            )
        {
            if (categoryPrincipal == null || categoryInterest == null)
            {
                return null;
            }

            bool matchedThePrincipalCategory = false;
            bool matchedTheInterestCategory = false;

            decimal principal = 0;
            decimal interest = 0;

            if (categoryPrincipal.Contains(category))
            {
                principal = amount;
                matchedThePrincipalCategory = true;
            }


            if (categoryInterest.Contains(category))
            {
                interest = amount;
                matchedTheInterestCategory = true;
            }

            if (matchedThePrincipalCategory == false && matchedTheInterestCategory == false)
            {
                // No match to either 
                return null;
            }


            LoanPaymentAggregation lp = view.LastOrDefault();

            if (lp != null && lp.Transaction == t)
            {
                // This is from the same transaction split entry
                // we will attempt to merge the PRINCIPAL and the INTEREST onto the same View Loan Entry
            }
            else
            {
                // New view projection
                lp = new LoanPaymentAggregation();
                lp.Transaction = t;
                lp.Payment = TransactionAmountMinusAllOtherUnrelatedSplits(t, categoryPrincipal, categoryInterest);
                lp.Account = transactionAccount;
                lp.Date = date;
                lp.Category = category;

                view.Add(lp);
            }

            if (matchedThePrincipalCategory)
            {
                lp.Principal = principal;
            }

            if (matchedTheInterestCategory)
            {
                lp.Interest = interest;
            }


            if (s != null)
            {
                if (matchedThePrincipalCategory)
                {
                    lp.SplitForPrincipal = s;
                }

                if (matchedTheInterestCategory)
                {
                    lp.SplitForInterest = s;
                }

            }

            return lp;
        }

        private static decimal TransactionAmountMinusAllOtherUnrelatedSplits(Transaction t, Category categoryPrincipal, Category categoryInterest)
        {
            decimal netAmount = t.Amount;

            if (t.Splits != null)
            {
                foreach (Split s in t.Splits)
                {
                    if (s.Category == categoryPrincipal || s.Category == categoryInterest)
                    {
                        // These split are valid for this transaction
                    }
                    else
                    {
                        // This split is for other stuff not related to this transaction
                        netAmount -= s.Amount;
                    }
                }
            }

            return netAmount;
        }
    }


    //================================================================================
    [CollectionDataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    public class LoanPayments : PersistentContainer, ICollection<LoanPayment>
    {
        int nextItemToAdd = 0;
        Hashtable<int, LoanPayment> collection = new Hashtable<int, LoanPayment>();


        // for serialization only
        public LoanPayments()
            : base(null)
        {
        }

        public LoanPayments(PersistentObject parent)
            : base(parent)
        {
        }

        public void Clear()
        {
            if (this.collection.Count != 0)
            {
                nextItemToAdd = 0;

                lock (collection)
                {
                    this.collection.Clear();
                }
                this.FireChangeEvent(this, this, null, ChangeType.Reloaded);
            }
        }


        public int Count
        {
            get { return this.collection.Count; }
        }


        public void AddLoan(LoanPayment x)
        {
            lock (this.collection)
            {
                if (x.Id == 0)
                {
                    x.Id = this.nextItemToAdd++;

                }
                else if (this.nextItemToAdd <= x.Id)
                {
                    this.nextItemToAdd = x.Id + 1;
                }
                x.Parent = this;
                this.collection[x.Id] = x;
            }
        }


        // todo: there should be no references left at this point...
        public bool Remove(LoanPayment x)
        {
            return RemoveLoan(x);
        }

        internal bool RemoveLoan(LoanPayment x, bool forceRemoveAfterSave = false)
        {
            lock (collection)
            {
                if (x.IsInserted || forceRemoveAfterSave)
                {
                    //// nothing to sync then
                    if (this.collection.Contains(x.Id))
                    {
                        this.collection.Remove(x.Id);
                    }
                }
            }
            x.OnDelete();
            return true;
        }

        public List<LoanPayment> GetList()
        {
            List<LoanPayment> list = new List<LoanPayment>();
            lock (collection)
            {
                foreach (LoanPayment x in collection.Values)
                {
                    if (!x.IsDeleted)
                    {
                        list.Add(x);
                    }
                }
            }
            return list;
        }

        #region ICollection

        public void Add(LoanPayment item)
        {
            AddLoan(item);
        }

        public bool Contains(LoanPayment item)
        {
            return collection.Contains(item.Id);
        }

        public void CopyTo(LoanPayment[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public override void Add(object child)
        {
            Add((LoanPayment)child);
        }

        public override void RemoveChild(PersistentObject pe, bool forceRemoveAfterSave = false)
        {
            RemoveLoan((LoanPayment)pe, forceRemoveAfterSave);
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public new IEnumerator<LoanPayment> GetEnumerator()
        {
            foreach (LoanPayment e in this.collection.Values)
            {
                yield return e;
            }
        }

        protected override IEnumerator<PersistentObject> InternalGetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }


    //================================================================================
    [DataContract(Namespace = "http://schemas.vteam.com/Money/2010")]
    [TableMapping(TableName = "LoanPayments")]
    public class LoanPayment : PersistentObject
    {
        int id;
        int accountId;
        DateTime date;
        decimal principal;
        decimal interest;
        string memo;

        public decimal Payment
        {
            get { return Principal + Interest; }
        }

        public LoanPayment()
        { // for serialization only
        }

        public LoanPayment(LoanPayments container) : base(container) { }


        #region PERSITED PROPERTIES

        [XmlAttribute]
        [DataMember]
        [ColumnMapping(ColumnName = "Id", SqlType = typeof(SqlInt32), AllowNulls = false)]
        public int Id
        {
            get { return this.id; }
            set
            {
                if (this.id != value)
                {
                    this.id = value;
                    OnChanged("Id");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "AccountId", SqlType = typeof(SqlInt32), AllowNulls = false)]
        public int AccountId
        {
            get { return this.accountId; }
            set
            {
                if (this.accountId != value)
                {
                    this.accountId = value;
                    OnChanged("AccountId");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Date")]
        public DateTime Date
        {
            get { return this.date; }
            set
            {
                if (this.date != value)
                {
                    this.date = value;
                    OnChanged("Date");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Principal", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Principal
        {
            get { return this.principal; }
            set
            {
                if (this.principal != value)
                {
                    this.principal = value;
                    OnChanged("Principal");
                }
            }
        }

        [DataMember]
        [ColumnMapping(ColumnName = "Interest", SqlType = typeof(SqlMoney), AllowNulls = true)]
        public decimal Interest
        {
            get { return this.interest; }
            set
            {
                if (this.interest != value)
                {
                    this.interest = value;
                    OnChanged("Interest");
                }
            }
        }


        [DataMember]
        [ColumnMapping(ColumnName = "Memo", MaxLength = 255, AllowNulls = true)]
        public string Memo
        {
            get { return this.memo; }
            set { if (this.memo != value) { this.memo = Truncate(value, 255); OnChanged("Memo"); } }
        }

        #endregion

        public override string ToString()
        {
            return string.Format("{0}:{1}:{2}:{3}", this.Id, this.Date, this.Payment, this.Memo);
        }

        public static LoanPayment Deserialize(string xml)
        {
            LoanPayment loanEntry = null;
            try
            {
                //DataContractSerializer xs = new DataContractSerializer(typeof(RentExpense));
                //StringReader sr = new StringReader(xml);
                //XmlTextReader r = new XmlTextReader(sr);
                //loanEntry = (LoanPayment)xs.ReadObject(r);
                //r.Close();
            }
            catch
            {
            }
            return loanEntry;
        }
    }


    public class LoanPaymentAggregation : INotifyPropertyChanged
    {
        Account accountId;
        public Account Account
        {
            get { return accountId; }
            set
            {
                accountId = value;
                if (LoanPayementManualEntry != null)
                {
                    LoanPayementManualEntry.AccountId = value.Id;
                }

                NotifyPropertyChanged("AccountId");
            }
        }

        public string AccountName
        {
            get;
            set;
        }

        public Transaction Transaction { get; set; }

        DateTime date;
        public DateTime Date
        {
            get { return date; }
            set
            {
                date = value;
                if (LoanPayementManualEntry != null)
                {
                    LoanPayementManualEntry.Date = value;
                }
                NotifyPropertyChanged("Date");
            }
        }

        public int Year
        {
            get { return date.Year; }
        }

        public string YearMonth
        {
            get { return string.Format("{0}/{1}", date.Year, date.Month); }
        }


        public Category Category { get; set; }


        decimal principal;
        public decimal Principal
        {
            get { return principal; }
            set
            {
                if (principal != value)
                {
                    principal = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                    if (LoanPayementManualEntry != null)
                    {
                        LoanPayementManualEntry.Principal = principal;
                    }
                    if (SplitForPrincipal != null)
                    {
                        SplitForPrincipal.Amount = principal;
                    }

                    NotifyPropertyChanged("Principal");
                }
            }
        }

        decimal interest;
        public decimal Interest
        {
            get { return interest; }
            set
            {
                if (interest != value)
                {
                    interest = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                    if (LoanPayementManualEntry != null)
                    {
                        LoanPayementManualEntry.Interest = interest;
                    }
                    if (SplitForInterest != null)
                    {
                        SplitForInterest.Amount = interest;
                    }

                    NotifyPropertyChanged("Interest");
                }
            }
        }

        private decimal payment = 0;
        public Split SplitForPrincipal;
        public Split SplitForInterest;


        public decimal Payment
        {
            get
            {
                return payment;
            }
            set
            {
                payment = Math.Round(value, 2, MidpointRounding.AwayFromZero);

                NotifyPropertyChanged("Payment");
            }

        }

        decimal percentage;
        public decimal Percentage
        {
            get { return percentage; }
            set
            {
                if (percentage != value)
                {
                    percentage = Math.Round(value, 4, MidpointRounding.AwayFromZero);
                    NotifyPropertyChanged("Percentage");
                }
            }
        }

        public bool IsReadOnly
        {
            get
            {
                // Only allow editing of Payment field if this was a Manual Entry
                return LoanPayementManualEntry == null;
            }
        }

        decimal balance;
        public decimal Balance
        {
            get { return balance; }
            set
            {
                balance = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                NotifyPropertyChanged("Balance");
            }
        }

        public LoanPayment LoanPayementManualEntry { get; set; }


        public string Source
        {
            get
            {
                if (Transaction == null)
                {
                    return this.LoanPayementManualEntry.Memo;
                }

                return Account.Name + ">" + Transaction.CategoryFullName;
            }

            set
            {
                if (this.LoanPayementManualEntry != null)
                {
                    this.LoanPayementManualEntry.Memo = value;
                    NotifyPropertyChanged("Source");
                }
            }
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
    }
}
