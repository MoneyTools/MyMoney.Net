using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Walkabout.Utilities
{

    /// <summary>
    /// This class takes a bunch of numbers associated with given object and category.
    /// Then when GetBestMatch is called it figures out which category the given value
    /// has the highest probability of belonging to.
    /// </summary>
    public class CategoryProbabilies<T>
    {
        class CategorySpread
        {
            internal decimal Total;
            internal int Count;
            internal decimal Miniumum = decimal.MaxValue;
            internal decimal Maximum = decimal.MinValue;
            internal List<double> Values = new List<double>();
            internal List<T> Data = new List<T>();

            internal decimal Mean { get { return Total / (decimal)Count; } }

            private decimal deviation = -1;

            internal decimal StandardDeviation
            {
                get
                {
                    if (deviation == -1)
                    {                        
                        deviation = (decimal)MathHelpers.StandardDeviation(Values);
                    }
                    return deviation;
                }
            }
        }

        int total = 0;
        Dictionary<string, CategorySpread> categories = new Dictionary<string, CategorySpread>();

        /// <summary>
        /// Add a data object with associated category and value.
        /// </summary>
        /// <param name="data">The data object which will be returned from GetBestMatch</param>
        /// <param name="category">The category associated with this value</param>
        /// <param name="value">The value</param>
        public void Add(T data, string category, decimal value)
        {
            CategorySpread spread = null;
            if (!categories.TryGetValue(category, out spread))
            {
                spread = new CategorySpread();
                categories[category] = spread;
            }

            spread.Miniumum = Math.Min(spread.Miniumum, value);
            spread.Maximum = Math.Max(spread.Maximum, value);
            spread.Count++;
            spread.Total += value;
            spread.Values.Add((double)value);
            spread.Data.Add(data);

            total++;
        }

        public int Count
        {
            get { return this.total; }
        }

        /// <summary>
        /// Get the data object that has the most likely category for the given value.
        /// </summary>
        /// <param name="value">The value we want to match</param>
        /// <returns>The data object</returns>
        public T GetBestMatch(decimal value)
        {
            if (categories.Count == 0)
            {
                return default(T);
            }

            CategorySpread result = null;

            if (categories.Count > 1)
            {
                int max = 0;

                foreach (CategorySpread spread in categories.Values)
                {
                    max = Math.Max(max, spread.Values.Count);
                }

                // trim out any set that is less than 1/5th the size of the maximum set.
                // those are just too rare to be useful as a best match.
                int min = max / 5;

                decimal smallestDeviation = decimal.MaxValue;
                foreach (CategorySpread spread in categories.Values.Where(s => s.Count > min))
                {

                    decimal mean = spread.Mean;
                    decimal deviation = spread.StandardDeviation;
                    if (deviation == 0)
                    {
                        // can't use it, not enough samples.
                        continue;
                    }

                    // how many standard deviations is the given value from the mean?
                    decimal deviations = Math.Abs(Math.Abs(value) - Math.Abs(mean)) / deviation;

                    // Pick the category where the given value is the closest to one standard deviation
                    // from the mean.  Dividing by the standard deviation means if we have hundreds of samples 
                    // in one category with a broad standard deviation, like category "Food:Groceries", and the
                    // value falls close to the mean then this category is favored over something that has only 
                    // a few samples and a narrow standard deviation.
                    if (result == null || deviations < smallestDeviation)
                    {
                        smallestDeviation = deviations;
                        result = spread;
                    }
                }
            }

            if (result == null)
            {
                // return something if we can (might have just one category).
                return categories.Values.LastOrDefault().Data.LastOrDefault();
            }

            if (result != null)
            {
                return result.Data.LastOrDefault();
            }

            return default(T);
        }
    }

}
