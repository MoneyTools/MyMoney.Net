using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Walkabout.Utilities
{
    /// <summary>
    /// This observable collection is handy for implementing Quick Search item filtering in 
    /// a consistent way across different types of collections.  For example, it parses the
    /// quick search string into tokens so you and do compound searches like "food & 50".
    /// The implementer subclasses from this collection and implements the IsMatch method 
    /// which will be passed the individual search tokens, like "food" and "50" in the
    /// above example.
    /// </summary>
    /// <typeparam name="T">The items in the collection</typeparam>
    public abstract class FilteredObservableCollection<T> : ObservableCollection<T> 
    {
        string filter;
        IEnumerable<T> original;

        protected FilteredObservableCollection(IEnumerable<T> collection) : base(collection)
        {
            this.original = collection;
        }

        protected FilteredObservableCollection(IEnumerable<T> collection, string filter)
        {
            this.original = collection;
            SetFilter(filter);
        }

        public string Filter
        {
            get { return this.filter; }
            set
            {
                if (this.filter != value)
                {
                    SetFilter(value);
                }
            }
        }


        private void SetFilter(string filter)
        {
            this.filter = filter;
            this.Clear();

            QuickFilterParser<T> parser = new QuickFilterParser<T>();
            Filter<T> expr = parser.Parse(filter);

            if (expr != null)
            {
                string dgml = expr.ToXml();
                Debug.WriteLine(dgml);
            }
            // Only include the items that match the filter expression.
            Stopwatch timer = new Stopwatch();
            timer.Start();
            int count = 0;
            int found = 0;
            foreach (T item in original)
            {
                count++;
                if (expr == null || expr.IsMatch(this, item))
                {
                    Add(item);
                }
            }
            timer.Stop();
            Debug.WriteLine("Filtered " + count + " items, found " + found + " matches in " + timer.ElapsedMilliseconds + " ms");
        }


        public abstract bool IsMatch(T item, FilterLiteral filterToken);

    }

}
