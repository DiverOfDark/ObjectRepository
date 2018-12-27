using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public class TableDictionary<T> : TableDictionary, ITableDictionary<T> where T : ModelBase
    {
        private readonly ObjectRepositoryBase _owner;
        private readonly ConcurrentDictionary<string, Func<T, object>> _columnsForIndex;
        private readonly ConcurrentDictionary<BaseEntity, T> _dictionary;

        public TableDictionary(ObjectRepositoryBase owner, IEnumerable<T> source):base(owner)
        {
            _owner = owner;
            _dictionary = new ConcurrentDictionary<BaseEntity,T>(source.Select(v => new KeyValuePair<BaseEntity, T>(v.Entity, v)));

            _columnsForIndex = new ConcurrentDictionary<string, Func<T, object>>();
            AddIndex(x => x.Id);

            Indexes = new ConcurrentDictionary<string, ConcurrentDictionary<object, T>>();

            foreach (var col in _columnsForIndex)
            {
                var dic = new ConcurrentDictionary<object, T>(source.Select(v => new KeyValuePair<object, T>(col.Value(v), v)));
                Indexes.TryAdd(col.Key, dic);
            }
        }

        public void AddIndex(Expression<Func<T, object>> index)
        {
            var key = ((MemberExpression) ((UnaryExpression) index.Body).Operand).Member.Name;
            var func = index.Compile();

            _columnsForIndex.TryAdd(key, func);
        }

        public ConcurrentDictionary<string, ConcurrentDictionary<object, T>> Indexes { get; }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _dictionary.Values.GetEnumerator();

        public override IEnumerator GetEnumerator() => _dictionary.Values.GetEnumerator();

        public void Add(T instance)
        {
            instance.SetOwner(_owner);
            _dictionary.TryAdd(instance.Entity, instance);
            foreach (var index in _columnsForIndex)
            {
                Indexes[index.Key].TryAdd(index.Value(instance), instance);
            }
        }

        public void Remove(T itemEntity)
        {
            _dictionary.TryRemove(itemEntity.Entity, out _);
            foreach (var index in _columnsForIndex)
            {
                Indexes[index.Key].TryRemove(index.Value(itemEntity), out _);
            }
        }
    }
}