using System.Collections.Generic;
using System.Linq;
using XMoney.ViewModels;

namespace XMoney.Views
{
    public class Filter
    {
        public Filter()
        {
            this.CategoryIds = new List<int>();
            this.Clear();
        }

        public int AccountId { get; set; }
        public List<int> CategoryIds { get; set; }
        public int PayeeId { get; set; }
        public decimal? Amount { get; set; }
        public int? Year { get; set; }
        public string DateText { get; set; }
        public string SearchText { get; set; }

        public void Clear()
        {
            this.AccountId = -1;
            this.CategoryIds.Clear();
            this.PayeeId = -1;
            this.Amount = null;
            this.DateText = "";
            this.Year = null;
            this.SearchText = "";
        }

        public bool HasFilters
        {
            get
            {
                if (this.AccountId == -1 && this.CategoryIds.Count == 0 && this.PayeeId == -1 && this.Amount == null && this.Year == null && this.DateText == "" && this.SearchText == "")
                {
                    // No filters
                    return false;
                }

                return true;
            }
        }

        public string GetDescription(bool allLevelsOfCategories = false)
        {
            string text = "";

            if (AccountId != -1)
            {
                text += Accounts.Get(AccountId).Name + " ";
            }

            if (CategoryIds.Count > 0)
            {
                string categories = "";

                foreach (int id in this.CategoryIds)
                {
                    if (categories != "")
                    {
                        categories += ", ";
                    }
                    categories += Categories.Get(id).Name;
                    if (allLevelsOfCategories == false)
                    {
                        // caller only wants the first Category
                        if (CategoryIds.Count > 1)
                        {
                            categories += "..."; // indicate that descending match is on
                        }
                        break;
                    }
                }

                text += categories + " ";
            }

            if (PayeeId != -1)
            {
                text += "Payee " + Payees.Get(PayeeId).Name + " ";
            }

            if (this.Amount != null)
            {
                text += ((decimal)this.Amount).ToString("C") + " ";
            }

            if (this.Year != null)
            {
                text += this.Year.ToString() + " ";
            }

            if (this.DateText != "")
            {
                text += this.DateText + " ";
            }

            return text.Trim();
        }

        public bool IsValid(Transactions transaction)
        {
            if (this.AccountId != -1)
            {
                if (this.AccountId != transaction.Account)
                {
                    return false;
                }
            }

            if (this.CategoryIds.Any())
            {
                if (transaction.Category == Categories.SplitCategoryId())
                {
                    List<Splits> splits = Splits.GetSplitsForTransaction(transaction.Id);
                    bool categoryFoundInSplits = false;
                    foreach (Splits split in splits)
                    {
                        if (this.CategoryIds.Contains(split.Category))
                        {
                            categoryFoundInSplits = true;
                            break;
                        }
                    }
                    if (!categoryFoundInSplits)
                    {
                        return false;
                    }
                }
                else
                {
                    if (!this.CategoryIds.Contains(transaction.Category))
                    {
                        return false;
                    }
                }
            }

            if (this.PayeeId != -1)
            {
                if (this.PayeeId != transaction.Payee)
                {
                    return false;
                }
            }

            if (this.Amount != null)
            {
                if (this.Amount != transaction.Amount)
                {
                    return false;
                }
            }

            if (this.Year != null)
            {
                if (this.Year != transaction.DateTime.Year)
                {
                    return false;
                }
            }

            if (this.DateText != string.Empty)
            {
                if (this.DateText != transaction.DateAsText)
                {
                    return false;
                }
            }

            if (this.SearchText != string.Empty && this.SearchText != null)
            {
                if (!transaction.IsTextMatch(this.SearchText))
                {
                    return false;
                }
            }

            return true;
        }

    }
}