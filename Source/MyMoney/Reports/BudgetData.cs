using System;
using System.Collections.Generic;
using System.ComponentModel;
using Walkabout.Data;

namespace Walkabout.Reports
{
    /// <summary>
    /// The view model for the budget area charts
    /// </summary>
    public class BudgetData : INotifyPropertyChanged
    {
        DateTime date;
        string name;
        double budget;
        double actual;
        double budgetCumulative;
        double actualCumulative;
        double average;
        bool projected;
        List<Transaction> budgeted = new List<Transaction>();

        public BudgetData(string name)
        {
            this.name = name;
        }

        public void Add(Transaction t)
        {
            budgeted.Add(t);
        }

        public List<Transaction> Budgeted
        {
            get { return budgeted; }
        }

        public DateTime BudgetDate
        {
            get { return date; }
            set
            {
                date = value;
                OnPropertyChanged("BudgetDate");
            }
        }

        public string Name
        {
            get { return this.name; }
        }

        public double Actual
        {
            get { return actual; }
            set
            {
                actual = value;
                OnPropertyChanged("Actual");
                OnPropertyChanged("Average");
            }
        }

        public double Budget
        {
            get { return budget; }
            set
            {
                budget = value;
                OnPropertyChanged("Budget");
            }
        }

        public double ActualCumulative
        {
            get { return actualCumulative; }
            set
            {
                actualCumulative = value;
                OnPropertyChanged("ActualCumulative");
                OnPropertyChanged("AverageCumulative");
            }
        }

        public double BudgetCumulative
        {
            get { return budgetCumulative; }
            set
            {
                budgetCumulative = value;
                OnPropertyChanged("BudgetCumulative");
            }
        }

        public double Average
        {
            get { return average; }
            set
            {
                average = value;
                OnPropertyChanged("Average");
            }
        }

        public bool IsProjected 
        {
            get { return projected; }
            set
            {
                projected = value;
                OnPropertyChanged("IsProjected");
            }
        }

        // INotifyPropertyChanged event
        public event PropertyChangedEventHandler PropertyChanged;

        void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        internal void AddActual(Transaction t)
        {
            double amount = -(double)t.amount;// make it positive.
            this.Actual += amount;
            this.Add(t);                
        }

        internal void AddBudget(Transaction t)
        {
            this.Budget += (double)t.amount;
            this.Add(t);
        }


        internal void Merge(BudgetData b)
        {
            this.budget += b.budget;
            this.actual += b.actual;
            this.average += b.average;
            budgeted.AddRange(b.budgeted);
        }
    }
}
