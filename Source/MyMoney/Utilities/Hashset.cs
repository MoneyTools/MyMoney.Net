using System;
using System.Collections.Generic;

namespace Walkabout.Utilities
{
    /// <summary>
    /// This class provides a generic Hashset, which is a Generic Dictionary with non-generic Hashset semantics.
    /// </summary>
    /// <typeparam name="TKey">The type of key</typeparam>
    /// <typeparam name="TValue">The type of value</typeparam>
    public class Hashtable<TKey, TValue> : IDictionary<TKey, TValue>, ICollection<KeyValuePair<TKey, TValue>>, IEnumerable<KeyValuePair<TKey, TValue>>
    {
        Dictionary<TKey, TValue> implementation = new Dictionary<TKey, TValue>();

        public TValue this[TKey key]
        {
            get
            {
                TValue result = default(TValue);
                if (key != null)
                {
                    implementation.TryGetValue(key, out result);
                }
                return result;
            }
            set
            {
                implementation[key] = value;
            }
        }

        internal bool Contains(TKey key)
        {
            return implementation.ContainsKey(key);
        }

        #region IDictionary<TKey, TValue>
        public void Add(TKey key, TValue value)
        {
            implementation.Add(key, value);
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null)
            {
                return false;
            }
            return implementation.ContainsKey(key);
        }

        public ICollection<TKey> Keys
        {
            get { return implementation.Keys; }
        }

        public bool Remove(TKey key)
        {
            if (key == null)
            {
                return false;
            }
            return implementation.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return implementation.TryGetValue(key, out value);
        }

        public ICollection<TValue> Values
        {
            get { return implementation.Values; }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((IDictionary<TKey, TValue>)implementation).Add(item);
        }

        public void Clear()
        {
            implementation.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)implementation).Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)implementation).CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return implementation.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)implementation).Remove(item);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return implementation.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((System.Collections.IEnumerable)implementation).GetEnumerator();
        }
        #endregion
    }

}
