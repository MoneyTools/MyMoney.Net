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
                    this.implementation.TryGetValue(key, out result);
                }
                return result;
            }
            set
            {
                this.implementation[key] = value;
            }
        }

        internal bool Contains(TKey key)
        {
            return this.implementation.ContainsKey(key);
        }

        #region IDictionary<TKey, TValue>
        public void Add(TKey key, TValue value)
        {
            this.implementation.Add(key, value);
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null)
            {
                return false;
            }
            return this.implementation.ContainsKey(key);
        }

        public ICollection<TKey> Keys
        {
            get { return this.implementation.Keys; }
        }

        public bool Remove(TKey key)
        {
            if (key == null)
            {
                return false;
            }
            return this.implementation.Remove(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return this.implementation.TryGetValue(key, out value);
        }

        public ICollection<TValue> Values
        {
            get { return this.implementation.Values; }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ((IDictionary<TKey, TValue>)this.implementation).Add(item);
        }

        public void Clear()
        {
            this.implementation.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)this.implementation).Contains(item);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((IDictionary<TKey, TValue>)this.implementation).CopyTo(array, arrayIndex);
        }

        public int Count
        {
            get { return this.implementation.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return ((IDictionary<TKey, TValue>)this.implementation).Remove(item);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return this.implementation.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((System.Collections.IEnumerable)this.implementation).GetEnumerator();
        }
        #endregion
    }

}
