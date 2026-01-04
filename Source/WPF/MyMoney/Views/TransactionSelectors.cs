using System;
using System.Collections.Generic;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using Walkabout.Data;

namespace Walkabout.Views
{
    /// <summary>
    /// This class contains the context needed for GetSelectedTransactions on TransactionSelector.
    /// </summary>
    public class TransactionSelectorContext
    {
        public bool IsReconciling;
        public DateTime? StatementReconcileDateBegin;
        public MyMoney Money;
    }

    /// <summary>
    /// The base class for all transaction selectors. These selectors can be chained together to
    /// produce filtered subsets of transactions.
    /// </summary>
    [XmlInclude(typeof(TransactionAccountSelector))]
    [XmlInclude(typeof(TransactionPayeeSelector))]
    [XmlInclude(typeof(TransactionCategorySelector))]
    [XmlInclude(typeof(TransactionSecuritySelector))]
    [XmlInclude(typeof(TransactionRangeSelector))]
    [XmlInclude(typeof(TransactionQuerySelector))]
    [XmlInclude(typeof(TransactionFilterSelector))]
    [XmlInclude(typeof(TransactionRentalSelector))]
    [XmlInclude(typeof(TransactionFixedSelector))]
    public abstract class TransactionSelector
    {
        public TransactionSelector Previous { get; set; }

        public abstract IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context);
    }

    /// <summary>
    /// This selector simply selects all transactions for a given account.
    /// Optionally also filtering a previous selector's results to return only 
    /// those transactions from the previous selector that also match the given account.
    /// </summary>
    public class TransactionAccountSelector : TransactionSelector
    {
        public string AccountName { get; set; }

        public TransactionAccountSelector() { }

        public TransactionAccountSelector(TransactionSelector previous, string accountName)
        {
            this.Previous = previous;
            this.AccountName = accountName;
        }

        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            var money = context.Money;
            var account = money.Accounts.FindAccount(this.AccountName);
            if (account != null)
            {
                var data = (this.Previous == null) ? money.Transactions.GetTransactionsFrom(account) : this.Previous.GetSelectedTransactions(context);                
                foreach (var t in data)
                {
                    if (t.Account == account)
                    {
                        yield return t;
                    }
                }
            }
        }
    }

    /// <summary>
    /// This selector simply selects all transactions for a given Payee.
    /// Optionally also filtering a previous selector's results to return only 
    /// those transactions from the previous selector that also match the given Payee.
    /// </summary>
    public class TransactionPayeeSelector : TransactionSelector
    {
        public string PayeeName { get; set; }

        public TransactionPayeeSelector() { }

        public TransactionPayeeSelector(TransactionSelector previous, string payeeName)
        {
            this.Previous = previous;
            this.PayeeName = payeeName;
        }

        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            var money = context.Money;
            var payee = money.Payees.FindPayee(this.PayeeName, false);
            if (payee != null)
            {
                var data = (this.Previous == null) ? money.Transactions.GetTransactionsByPayee(payee, null) : this.Previous.GetSelectedTransactions(context);                
                foreach (var t in data)
                {
                    if (t.Payee == payee)
                    {
                        yield return t;
                    }
                }
            }
        }
    }

    /// <summary>
    /// This selector simply selects all transactions for a given Category 
    /// (or any of it's subcategories).
    /// Optionally also filtering a previous selector's results to return only 
    /// those transactions from the previous selector that also match the given Category.
    /// (or any of it's subcategories).
    /// </summary>
    public class TransactionCategorySelector : TransactionSelector
    {
        public string CategoryName { get; set; }

        public TransactionCategorySelector() { }

        public TransactionCategorySelector(TransactionSelector previous, string categoryName)
        {
            this.Previous = previous;
            this.CategoryName = categoryName;
        }

        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            var money = context.Money;
            var category = money.Categories.FindCategory(this.CategoryName);
            if (category != null)
            {
                if (this.Previous == null)
                {
                    foreach (var t in money.Transactions.GetTransactionsByCategory(category, null))
                    {
                        yield return t;
                    }
                }
                else
                {
                    var matches = new Predicate<Category>((cat) => { return category.Contains(cat); });
                    foreach (var t in this.Previous.GetSelectedTransactions(context))
                    {
                        Category c = t.Category;

                        // logic must match what GetTransactionsByCategory does.
                        if (t.CategoryMatches(matches))
                        {
                            yield return t;
                        }
                        else if (t.SplitMatchesCategory(matches, out Split s))
                        {
                            // fake read only transaction to represent the split.
                            yield return new Transaction(t, s);
                        }
                        else if (money != null && (c == money.Categories.Unknown && t.Category == null && t.Transfer == null))
                        {
                            yield return t;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// This selector simply selects all transactions for a given Security.
    /// Optionally also filtering a previous selector's results to return only 
    /// those transactions from the previous selector that also match the given Payee.
    /// </summary>
    public class TransactionSecuritySelector : TransactionSelector
    {
        public string SecurityName { get; set; }

        public TransactionSecuritySelector() { }

        public TransactionSecuritySelector(TransactionSelector previous, string securityName)
        {
            this.Previous = previous;
            this.SecurityName = securityName;
        }

        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            var money = context.Money;
            var security = money.Securities.FindSecurity(this.SecurityName, false);
            if (security != null)
            {
                var data = (this.Previous == null) ? money.Transactions.GetTransactionsBySecurity(security, null) : this.Previous.GetSelectedTransactions(context);               
                foreach (var t in data)
                {
                    if (t.Investment != null && t.Investment.Security == security)
                    {
                        yield return t;
                    }
                }
            }
        }
    }

    /// <summary>
    /// This selector simply selects all transactions for a given Rental context.
    /// Optionally also filtering a previous selector's results to return only 
    /// those transactions from the previous selector that also match the given Rental context.
    /// </summary>
    public class TransactionRentalSelector : TransactionCategorySelector
    {
        public RentalBuildingSingleYearSingleDepartment Department { get; set; }

        public TransactionRentalSelector() { }

        public TransactionRentalSelector(TransactionSelector previous, RentalBuildingSingleYearSingleDepartment department)
            : base(previous, department.DepartmentCategory)
        {
            this.Previous = previous;
            this.Department = department;
        }
    }

    /// <summary>
    /// This selector simply selects all transactions that fall within the given date range.
    /// Optionally also filtering a previous selector's results to return only those
    /// transactions that fall within the given date range.
    /// </summary>
    public class TransactionRangeSelector : TransactionSelector
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public TransactionRangeSelector() { }

        public TransactionRangeSelector(TransactionSelector previous, DateTime startDate, DateTime endDate)
        {
            this.Previous = previous;
            this.StartDate = startDate;
            this.EndDate = endDate;
        }

        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            var money = context.Money;

            IEnumerable<Transaction> data = (this.Previous == null) ? money.Transactions.GetAllTransactionsByDate() : this.Previous.GetSelectedTransactions(context);
            foreach (var t in data)
            {
                if (t.Date >= this.StartDate && t.Date < this.EndDate)
                {
                    yield return t;
                }
            }
        }
    }

    /// <summary>
    /// This selector selects all transactions that match the given query.
    /// </summary>
    public class TransactionQuerySelector : TransactionSelector
    {
        public QueryRow[] Query { get; set; }

        public TransactionQuerySelector() { } // for XML serialization

        public TransactionQuerySelector(QueryRow[] query)
        {
            // Previous not supported, this must always be the root selector.
            this.Query = query;
        }

        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            return context.Money.Transactions.ExecuteQuery(this.Query);
        }
    }

    /// <summary>
    /// This selector selects all transactions that match the given TransactionFilter.
    /// Optionally also filtering a previous selector's results to return only those
    /// transactions that match the given TransactionFilter.
    /// </summary>
    public class TransactionFilterSelector : TransactionSelector
    {
        public TransactionFilter Filter { get; set; }

        public TransactionFilterSelector() { } // for xml serialization

        public TransactionFilterSelector(TransactionSelector previous, TransactionFilter filter)
        {
            this.Previous = previous;
            this.Filter = filter;
        }

        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            var predicate = this.GetTransactionIncludePredicate(context.IsReconciling, context.StatementReconcileDateBegin);
            var money = context.Money;
            IEnumerable<Transaction> data = (this.Previous == null) ? money.Transactions.GetAllTransactionsByDate() : this.Previous.GetSelectedTransactions(context);            
            foreach (var t in data)
            {
                if (predicate(t))
                {
                    yield return t;
                }
            }
        }

        public Predicate<Transaction> GetTransactionIncludePredicate(bool isReconciling, DateTime? statementDate)
        {
            Predicate<Transaction> predicate = null;
            switch (this.Filter)
            {
                case TransactionFilter.All:
                case TransactionFilter.Custom:
                    predicate = new Predicate<Transaction>((t) => { return true; });
                    break;
                case TransactionFilter.Accepted:
                    predicate = new Predicate<Transaction>((t) => { return !t.Unaccepted; });
                    break;
                case TransactionFilter.Unaccepted:
                    predicate = new Predicate<Transaction>((t) => { return t.Unaccepted; });
                    break;
                case TransactionFilter.Reconciled:
                    if (!isReconciling)
                    {
                        // We are not in BALANCING mode so use the normal un-reconcile filter (show all transactions that are not reconciled)
                        predicate = new Predicate<Transaction>((t) => { return t.Status == TransactionStatus.Reconciled; });
                    }
                    else
                    {
                        // While balancing we need to see the reconciled transactions for the current statement date as well as any 
                        // before or after that are not reconciled.
                        predicate = new Predicate<Transaction>((t) =>
                        {
                            return (t.Status == TransactionStatus.Reconciled) ||
                                    t.IsReconciling || this.IsIncludedInCurrentStatement(t, statementDate);
                        });
                    }
                    break;
                case TransactionFilter.Unreconciled:
                    if (!isReconciling)
                    {
                        // We are not in BALANCING mode so use the normal un-reconcile filter (show all transactions that are not reconciled)
                        predicate = new Predicate<Transaction>((t) => { return t.Status != TransactionStatus.Reconciled && t.Status != TransactionStatus.Void; });
                    }
                    else
                    {
                        // While balancing we need to see the reconciled transactions for the current statement date as well as any 
                        // before or after that are not reconciled.
                        predicate = new Predicate<Transaction>((t) =>
                        {
                            return (t.Status != TransactionStatus.Reconciled && t.Status != TransactionStatus.Void) ||
                                    t.IsReconciling || this.IsIncludedInCurrentStatement(t, statementDate);
                        });
                    }
                    break;
                case TransactionFilter.Categorized:
                    predicate = new Predicate<Transaction>((t) =>
                    {
                        if (t.Status == TransactionStatus.Void)
                        {
                            return false; // no point seeing these
                        }
                        if (t.IsFakeSplit)
                        {
                            return false;
                        }
                        if (t.IsSplit)
                        {
                            return t.Splits.Unassigned == 0; // then all splits are good!
                        }
                        return t.Category != null;
                    });
                    break;
                case TransactionFilter.Uncategorized:
                    predicate = new Predicate<Transaction>((t) =>
                    {
                        if (t.Status == TransactionStatus.Void)
                        {
                            return false; // no point seeing these
                        }
                        if (t.IsFakeSplit)
                        {
                            return false; // this represents a category by definition.
                        }
                        if (t.IsSplit)
                        {
                            return t.Splits.Unassigned > 0; // then there is more to categorize in the splits!
                        }
                        return t.Category == null;
                    });
                    break;
            }
            return predicate;
        }

        private bool IsIncludedInCurrentStatement(Transaction t, DateTime? statementDate)
        {
            if (statementDate.HasValue)
            {
                return t.Date >= statementDate.Value;
            }

            return true;
        }

    }

    /// <summary>
    /// Sometimes we just have a fixed list of transactions to return - this selector is not
    /// serializable and should be discouraged, but we still have some report drill downs that
    /// are hard to serialize without tons more context and these still need this type of selector.
    /// </summary>
    public class TransactionFixedSelector : TransactionSelector
    {
        private IList<Transaction> data;

        public TransactionFixedSelector() { }

        public TransactionFixedSelector(IList<Transaction> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            this.data = data;
        }
        public override IEnumerable<Transaction> GetSelectedTransactions(TransactionSelectorContext context)
        {
            return this.data;
        }
    }

}
