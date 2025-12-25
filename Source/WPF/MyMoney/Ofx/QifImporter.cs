using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Walkabout.Data;
using Walkabout.Utilities;

namespace Walkabout.Migrate
{
    /// <summary>
    /// Implements parts of the QIF specification for importing.
    /// See http://money.mvps.org/articles/qifspecification.aspx 
    /// </summary>
    public class QifImporter : Importer
    {
        public static string SpecialImportFileName = "~IMPORT~.QIF";

        public QifImporter(MyMoney myMoney)
            : base(myMoney)
        {
        }

        public Account Import(Account currentlySelectedAccount, string filename, out int count)
        {
            count = 0;
            string name = Path.GetFileNameWithoutExtension(filename);
            Account a = this.Money.Accounts.FindAccount(name);
            if (a == null)
            {
                if (name != Path.GetFileNameWithoutExtension(SpecialImportFileName))
                {
                    string message = string.Format(
                        "The following account does not currently exit" +
                        Environment.NewLine + Environment.NewLine +
                        "\"{0}\"" +
                        Environment.NewLine + Environment.NewLine +
                        "Would you like to create it?", name);

                    if (MessageBoxEx.Show(message, "New Account", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        a = this.Money.Accounts.AddAccount(name);
                    }
                }

                if (a == null)
                {
                    if (currentlySelectedAccount == null)
                    {
                        // TODO - PROMPT THE USER TO SELECT AN ACCOUNT
                        MessageBoxEx.Show("You must first select an account to import to");
                        return null;
                    }
                    else
                    {
                        string msg = string.Format("Would you like to merge this QIF data with the selected account?{0}\"{1}\"",
                            Environment.NewLine + Environment.NewLine,
                            currentlySelectedAccount.Name
                            );

                        if (MessageBoxEx.Show(msg, "Merge QIF", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                        {
                            a = currentlySelectedAccount;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }

            count = this.ImportQif(a, filename);

            return a;
        }

        private string AppendMemo(string memo, string line)
        {
            if (string.IsNullOrEmpty(memo))
            {
                return line;
            }
            return memo + ", " + line;
        }

        public int ImportQif(Account a, string filename)
        {
            bool merge = false;
            MyMoney myMoney = this.Money;

            if (myMoney.Transactions.GetTransactionsFrom(a).Count > 0)
            {
                merge = true;
            }

            using (StreamReader r = new StreamReader(filename, true))
            {
                string line = r.ReadLine();

                if (line.StartsWith("!Type:"))
                {
                    string atype = line.Substring(6);
                    AccountType at = AccountType.Checking;
                    bool accountTypeMismatch = at != a.Type;
                    switch (atype)
                    {
                        case "Bank":
                            at = filename.IndexOf("Checking") >= 0 ? AccountType.Checking : AccountType.Savings;
                            break;
                        case "Cash":
                            at = AccountType.Cash;
                            break;
                        case "CCard":
                            at = AccountType.Credit;
                            break;
                        case "Invst":
                            at = AccountType.Brokerage;
                            accountTypeMismatch = a.Type != AccountType.Brokerage && a.Type != AccountType.Retirement;
                            break;

                        case "Oth A":
                            // Microsoft Money supports an account type call "Other" and when you export 
                            // QIF from MSMoney, the Type will show up as "!Type:Oth A"
                            // The best thing to do is match it to a Checking account type
                            at = AccountType.Checking;
                            break;

                        default:
                            throw new Exception(string.Format("Account type {0} not supported", a.Type));

                    }
                    if (merge)
                    {
                        if (accountTypeMismatch)
                        {
                            throw new Exception(string.Format("Account type {0} in QIF doesn't match selected account type {1}", at, a.Type));
                        }
                    }
                    else
                    {
                        a.Type = at;
                    }
                }

                // see http://www.respmech.com/mym2qifw/qif_new.htm 
                Transaction t = myMoney.Transactions.NewTransaction(a);
                if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                {
                    t.GetOrCreateInvestment();
                }
                Payee openingBalance = myMoney.Payees.FindPayee("Opening Balance", true);

                Dictionary<long, Transaction> newTransactions = new Dictionary<long, Transaction>();
                int lineNumber = 1;
                int transactionsAdded = 0;
                while (line != null)
                {
                    if (line.Length > 0)
                    {
                        char ltype = line[0];
                        line = line.Substring(1);
                        switch (ltype)
                        {
                            case 'A': // address
                                t.Memo = this.AppendMemo(t.Memo, line);
                                break;
                            case '!': // comment, Type:?
                                break;
                            case 'D': // date
                                // In some cases the QIF will
                                // have the date in the following format 01/30'2000
                                // so before processing the date we replace the "'" with "/"
                                line = line.Replace('\'', '/');
                                t.Date = DateTime.Parse(line);
                                break;
                            case 'C': // status
                                if (line.Length == 1)
                                {
                                    switch (line[0])
                                    {
                                        case '*':
                                        case 'X':
                                            t.Status = TransactionStatus.Cleared;
                                            break;
                                        case 'R':
                                            // we do not set state to reconciled because we don't want to import bugs
                                            // from Microsoft Money.  Our reconciliation system works a lot better if
                                            // user starts from scratch.
                                            break;
                                        default:
                                            break;
                                    }
                                }
                                else
                                {
                                    throw new Exception("Unknown format: C" + line);
                                }
                                break;
                            case 'M': // memo
                                t.Memo = this.AppendMemo(t.Memo, line);
                                break;
                            case 'T': // amount
                            case 'U': // amount

                                //
                                // While Importing there maybe cases where transaction go "reconciled" see case 'C'
                                // while setting the t.Amount there's code in there to Throw if the Transaction was already Reconciled
                                // to work around this we Temporarily disable this by simulating that we are in Balancing mode
                                //
                                t.IsReconciling = true;

                                t.Amount = decimal.Parse(line);

                                // Turn back normal mode
                                t.IsReconciling = false;
                                break;

                            case 'N': // number, (or investment action)
                                try
                                {
                                    t.Number = line;
                                    if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                                    {
                                        if (t.Investment == null)
                                        {
                                            t.Investment = t.GetOrCreateInvestment();
                                        }
                                        switch (line)
                                        {
                                            case "ShrsIn":
                                                t.Investment.Type = InvestmentType.Add;
                                                break;
                                            case "ShrsOut":
                                                t.Investment.Type = InvestmentType.Remove;
                                                break;
                                            case "Sell":
                                                t.Investment.Type = InvestmentType.Sell;
                                                break;
                                            case "Buy":
                                                t.Investment.Type = InvestmentType.Buy;
                                                break;
                                        }
                                    }
                                }
                                catch { }
                                break;
                            case 'Y': // Security
                                MyMoney.EnsureInvestmentAccount(a, line, lineNumber);
                                t.Investment.Security = myMoney.Securities.FindSecurity(line.Trim(), true);
                                break;
                            case 'I': // Price
                                MyMoney.EnsureInvestmentAccount(a, line, lineNumber);
                                t.Investment.UnitPrice = ParseDecimal(line);
                                break;
                            case 'Q': // Quantity
                                MyMoney.EnsureInvestmentAccount(a, line, lineNumber);
                                t.Investment.Units = ParseDecimal(line);
                                break;
                            case 'O': // Commission
                                MyMoney.EnsureInvestmentAccount(a, line, lineNumber);
                                t.Investment.Commission = ParseDecimal(line);
                                break;
                            case 'P': // payee
                                if (line.Length > 4 && line.Substring(0, 4) == "VOID")
                                {
                                    t.Payee = myMoney.Payees.FindPayee(line.Substring(4).Trim(), true);
                                    t.Status = TransactionStatus.Void;
                                }
                                else
                                {
                                    t.Payee = myMoney.Payees.FindPayee(line, true);
                                }
                                break;
                            case 'L': // category or transfer
                                if (line.Length > 0 && line[0] == '[')
                                {
                                    string n = line.Substring(1, line.Length - 2);
                                    Account to = myMoney.Accounts.AddAccount(n);
                                    if (to == a)
                                    {
                                        if (t.Payee == openingBalance)
                                        {
                                            a.OpeningBalance = t.Amount;
                                            t = null; // No need to add a transaction for setting the "opening balance"
                                        }
                                        else
                                        {
                                            throw new Exception("Illegal transfer");
                                        }
                                    }
                                    else
                                    {
                                        t.Transfer = this.FindMatchingTransfer(t, to);
                                        if (t.Transfer != null)
                                        {
                                            Category c = t.Transfer.Transaction.Category;
                                            if (c == myMoney.Categories.TransferToDeletedAccount ||
                                                c == myMoney.Categories.TransferFromDeletedAccount)
                                            {
                                                // Remove any "Xfer to delete"                                                 
                                                t.Category = t.Transfer.Transaction.Category = null;
                                            }

                                            // if we have a Payee lets keep this information as part of the memo
                                            if (t.Payee != null && !string.IsNullOrEmpty(t.Payee.Name))
                                            {
                                                if (string.IsNullOrEmpty(t.Memo))
                                                {
                                                    t.Memo = t.Payee.Name;
                                                }
                                                else
                                                {
                                                    t.Memo = t.Payee.Name + ": " + t.Memo;
                                                }
                                            }
                                        }
                                        t.to = to;
                                    }
                                }
                                else
                                {
                                    t.Category = myMoney.Categories.GetOrCreateCategory(line, CategoryType.Expense);
                                }
                                break;
                            case 'S': // split
                                if (t.to != null)
                                {
                                    // Cannot have the main transaction be a transfer
                                    // and have splits.
                                    t.to = null;
                                    t.Transfer = null;
                                }
                                Split s = t.NonNullSplits.AddSplit();
                                // the master of the split is put in the special category "Split".
                                t.Category = myMoney.Categories.Split;
                                Account splitTo = null;
                                if (line.Length > 0 && line[0] == '[')
                                {
                                    s.Payee = myMoney.Payees.Transfer;
                                    string n = line.Substring(1, line.Length - 2);
                                    s.to = splitTo = myMoney.Accounts.AddAccount(n);
                                }
                                else
                                {
                                    s.Category = myMoney.Categories.GetOrCreateCategory(line, CategoryType.Expense);
                                }
                                line = r.ReadLine();
                                if (line != null && line.Length > 0 && line[0] == 'E')
                                {
                                    line = line.Substring(1);
                                    s.Memo = line;
                                    line = r.ReadLine();
                                }
                                if (line != null && line.Length > 0 && line[0] == '$')
                                {
                                    s.Amount = decimal.Parse(line.Substring(1));
                                }
                                else
                                {
                                    throw new Exception("Expecting Split $");
                                }
                                if (s.to != null)
                                {
                                    // now we can find the matching transfer.
                                    this.FindMatchingSplitTransfer(s, splitTo);
                                }
                                break;
                            case '^': // commit
                                if (t != null)
                                {
                                    if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                                    {
                                        switch (t.Investment.Type)
                                        {
                                            case InvestmentType.Buy:
                                                t.Amount = -t.Amount;
                                                break;
                                            case InvestmentType.Sell:
                                                break;
                                            case InvestmentType.Add:
                                                break;
                                            case InvestmentType.Remove:
                                                t.Amount = -t.Amount;
                                                break;
                                        }
                                        if (t.Payee == null && t.Investment.Security != null &&
                                            !string.IsNullOrEmpty(t.Investment.Security.Name))
                                        {
                                            t.Payee = myMoney.Payees.FindPayee(t.Investment.Security.Name, true);
                                        }
                                    }
                                    bool merged = false;
                                    if (merge)
                                    {
                                        t.Unaccepted = true;
                                        Transaction u = myMoney.Transactions.Merge(myMoney.Aliases, t, newTransactions);
                                        if (u != null)
                                        {
                                            merged = true;
                                            t = u;
                                        }
                                    }
                                    if (!merged)
                                    {
                                        transactionsAdded++;
                                        myMoney.Transactions.AddTransaction(t);
                                    }
                                    t.IsDownloaded = true;
                                    newTransactions[t.Id] = t;
                                }
                                t = myMoney.Transactions.NewTransaction(a);
                                if (a.Type == AccountType.Brokerage || a.Type == AccountType.Retirement)
                                {
                                    t.GetOrCreateInvestment();
                                }
                                break;
                            default:
                                throw new Exception(string.Format("Unknown format '{0}' on line {1} : {2}", ltype, lineNumber, line));
                        }
                    }
                    line = r.ReadLine();
                    lineNumber++;
                }
                r.Close();
                _ = myMoney.Rebalance(a);
                return transactionsAdded;
            }
        }

        private static decimal ParseDecimal(string input)
        {
            string d = input.Trim();
            if (input.Trim() == string.Empty)
            {
                return 0;
            }

            return decimal.Parse(d);
        }

    }
}
