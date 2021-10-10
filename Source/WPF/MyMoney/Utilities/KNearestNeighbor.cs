using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Walkabout.Utilities
{
    /// <summary>
    /// Simple nearest neighbor algorithm used to predict the label for a given new value.
    /// </summary>
    public class KNearestNeighbor<T>
    {
        class Feature<U>
        {
            internal U label;
            internal List<object> Data = new List<object>();
            internal List<decimal> Values = new List<decimal>();

            public Feature(U c)
            {
                label = c;
            }
        }

        int total = 0;
        Dictionary<T, Feature<T>> features = new Dictionary<T, Feature<T>>();

        /// <summary>
        /// Add a data object with associated category and value.
        /// </summary>
        /// <param name="data">The data object which will be returned from GetBestMatch (usually a Transaction or a Split)</param>
        /// <param name="label">The label associated with this value</param>
        /// <param name="value">The value</param>
        public void Add(object data, T label, decimal value)
        {
            if ((double)Math.Abs(value) < 0.1)
            {
                System.Diagnostics.Debug.WriteLine(value);
            }
            Feature<T> feature = null;
            if (!features.TryGetValue(label, out feature))
            {
                feature = new Feature<T>(label);
                features[label] = feature;
            }

            feature.Values.Add(value);
            feature.Data.Add(data);
            total++;
        }

        public int Count
        {
            get { return this.total; }
        }

        class DataScore<U> : IComparable<DataScore<T>>
        {
            public object Data;
            public decimal Score;
            public U Label;
            public int Count;

            public DataScore(object data, U label, decimal score)
            {
                this.Data = data;
                this.Score = score;
                this.Label = label;
                this.Count = 1;
            }

            public int CompareTo(DataScore<T> other)
            {
                if (Score > other.Score)
                {
                    return -1;
                }
                else if (Score < other.Score)
                {
                    return 1;
                }
                return 0;
            }

            public override string ToString()
            {
                return Label.ToString() + ": " + Score;
            }

            internal void Normalize()
            {
                this.Score /= this.Count;
            }
        }

        public IEnumerable<Tuple<object, T>> GetNearestNeighbors(int k, decimal value)
        {
            // This is computing distance and a score in one loop, weighted 
            // by the inverse of the distance from our test value, and that way
            // we favor training with lots of nearby values.  But a small number
            // of nearby values could be beaten by a large number of not super close
            // values.
            Dictionary<T, DataScore<T>> scores = new Dictionary<T, DataScore<T>>();
            foreach (var pair in features)
            {
                T label = pair.Key;
                for (int i = 0, n = pair.Value.Values.Count; i < n; i++)
                {
                    var item = pair.Value.Values[i];
                    var data = pair.Value.Data[i];
                    
                    // negate the score, so that SortedSet puts the smallest values
                    // (cloest distance) on the top of the list.
                    decimal score = -Math.Abs(item - value);                        
                    if (scores.TryGetValue(label, out DataScore<T> s))
                    {
                        s.Score += score;
                        s.Count++;
                    }
                    else
                    {
                        scores[label] = new DataScore<T>(data, label, score);
                    }
                }
            }

            // Normalize score based on # transactions so that a lot of transactions doesn't end up winning.
            // What we want is the label with the closest "average" score.
            foreach (var label in scores.Keys)
            {
                scores[label].Normalize();
            }

            SortedSet<DataScore<T>> sorted = new SortedSet<DataScore<T>>(scores.Values);
            return from i in sorted.Take(k) select new Tuple<object,T>(i.Data, i.Label);
        }

    }
}
