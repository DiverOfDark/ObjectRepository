using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public class ConcurrentList<T> : IEnumerable<T>
    {
        private readonly ConcurrentDictionary<T, T> _dictionary;

        public ConcurrentList(IEnumerable<T> source) 
        {
            _dictionary = new ConcurrentDictionary<T, T>(source.Select(v => new KeyValuePair<T, T>(v, v)));
        }

        public ConcurrentList()
        {
            _dictionary = new ConcurrentDictionary<T, T>();
        }

        public void Add(T item) => _dictionary.TryAdd(item, item);

        public void Remove(T item)
        {
            T unused;
            _dictionary.TryRemove(item, out unused);
        }

        public bool TryTake(out T result)
        {
            result = _dictionary.Keys.FirstOrDefault();

            if (result != null)
            {
                Remove(result);
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator() => new EnumeratorWrapper(_dictionary.GetEnumerator());

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


        private class EnumeratorWrapper : IEnumerator<T>
        {
            private readonly IEnumerator<KeyValuePair<T, T>> _source;

            public EnumeratorWrapper(IEnumerator<KeyValuePair<T,T>> source)
            {
                _source = source;
            }

            public void Dispose() => _source.Dispose();

            public bool MoveNext() => _source.MoveNext();

            public void Reset() => _source.Reset();

            public T Current => _source.Current.Key;

            object IEnumerator.Current => Current;
        }
    }
}