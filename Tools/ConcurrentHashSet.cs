using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tools
{
    public class ConcurrentHashSet<T> : ISet<T>
    {
        ConcurrentDictionary<T, bool> dict;

        public ConcurrentHashSet(int concurrencyLevel, int capacity)
        {
            dict = new ConcurrentDictionary<T, bool>(concurrencyLevel, capacity);
        }

        public ConcurrentHashSet(IEnumerable<T> items)
        {
            dict = new ConcurrentDictionary<T, bool>(items.Select(i => new KeyValuePair<T, bool>(i, true)));
        }

        public ConcurrentHashSet(IEnumerable<T> items, IEqualityComparer<T> comparer)
        {
            dict = new ConcurrentDictionary<T, bool>(items.Select(i => new KeyValuePair<T, bool>(i, true)), comparer);
        }

        public ConcurrentHashSet()
        {
            dict = new ConcurrentDictionary<T, bool>();
        }

        public int Count => dict.Count;

        public bool IsReadOnly => false;

        public bool Add(T item) => dict.TryAdd(item, true);

        public void Clear() => dict.Clear();

        public bool Contains(T item) => dict.ContainsKey(item);

        public void CopyTo(T[] array, int arrayIndex) => dict.Keys.CopyTo(array, arrayIndex);

        public void ExceptWith(IEnumerable<T> other)
        {
            bool remove;
            foreach (T item in other) dict.TryRemove(item, out remove);
        }

        public IEnumerator<T> GetEnumerator() => dict.Keys.GetEnumerator();

        public void IntersectWith(IEnumerable<T> other) => ExceptWith(dict.Keys.Where(i => !other.Contains(i)).ToList());

        public bool IsProperSubsetOf(IEnumerable<T> other) => (other.Count() > Count) && IsSubsetOf(other);

        public bool IsProperSupersetOf(IEnumerable<T> other) => (Count > other.Count()) && IsSupersetOf(other);

        public bool IsSubsetOf(IEnumerable<T> other) => !dict.Keys.Any(i => !other.Contains(i));

        public bool IsSupersetOf(IEnumerable<T> other) => !other.Any(i => !dict.ContainsKey(i));
        public bool Overlaps(IEnumerable<T> other) => dict.Keys.Any(other.Contains);

        public bool Remove(T item)
        {
            bool remove;
            return dict.TryRemove(item, out remove);
        }

        public bool SetEquals(IEnumerable<T> other) => (other.Count() == Count) && IsSubsetOf(other);

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            var toRemove = new HashSet<T>(dict.Keys.Where(i => other.Contains(i)));
            ExceptWith(toRemove);
            UnionWith(other.Where(i => !toRemove.Contains(i)));
        }

        public void UnionWith(IEnumerable<T> other)
        {
            foreach (var item in other) Add(item);
        }

        void ICollection<T>.Add(T item) => Add(item);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
