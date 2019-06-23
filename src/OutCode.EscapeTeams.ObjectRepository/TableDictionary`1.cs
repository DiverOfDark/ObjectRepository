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
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<object, T>> _indexes;
        private readonly ConcurrentDictionary<BaseEntity, T> _dictionary;

        public TableDictionary(ObjectRepositoryBase owner, IEnumerable<T> source):base(owner)
        {
            _owner = owner;
            _dictionary = new ConcurrentDictionary<BaseEntity,T>(source.Select(v => new KeyValuePair<BaseEntity, T>(v.Entity, v)));

            _columnsForIndex = new ConcurrentDictionary<string, Func<T, object>>();

            _indexes = new ConcurrentDictionary<string, ConcurrentDictionary<object, T>>();

            AddIndex(() => x => x.Id);
        }

        public void AddIndex(Func<Expression<Func<T, object>>> index)
        {
            var key = GetPropertyName(index);
            var func = index().Compile();

            _columnsForIndex.TryAdd(key, func);

            _owner.ModelChanged += change =>
            {
                if (change.Source is T model && change.PropertyName == key)
                {
                    _indexes[key].TryRemove(change.OldValue, out var _);
                    _indexes[key].TryAdd(change.NewValue, model);
                }
            };
            
            var dic = new ConcurrentDictionary<object, T>(_dictionary.Select(v => new KeyValuePair<object, T>(func(v.Value), v.Value)));
            _indexes.TryAdd(key, dic);
        }

        public T Find(Func<Expression<Func<T, object>>> index, object value)
        {
            if (_indexes[GetPropertyName(index)].TryGetValue(value, out T result))
            {
                return result;
            }

            return default;
        }

        public T Find(Guid id) => Find(() => x => x.Id, id);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => _dictionary.Values.GetEnumerator();

        public override IEnumerator GetEnumerator() => _dictionary.Values.GetEnumerator();

        public void Add(T instance)
        {
            instance.SetOwner(_owner);
            _dictionary.TryAdd(instance.Entity, instance);
            foreach (var index in _columnsForIndex)
            {
                _indexes[index.Key].TryAdd(index.Value(instance), instance);
            }
        }

        public void Remove(T itemEntity)
        {
            _dictionary.TryRemove(itemEntity.Entity, out _);
            foreach (var index in _columnsForIndex)
            {
                _indexes[index.Key].TryRemove(index.Value(itemEntity), out _);
            }
        }
    }
}