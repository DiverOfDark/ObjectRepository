using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace OutCode.EscapeTeams.ObjectRepository
{
    public abstract class TableDictionary : IEnumerable
    {
        private readonly ObjectRepositoryBase _owner;

        // Foreign types which are using this table. Type -> PropertyName -> Getter + ConcurrentDictionary<Guid, ConcurrentList<T(ModelBase)>>
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, Tuple<Delegate, object>>> _foreignIndexes;

        protected TableDictionary(ObjectRepositoryBase owner)
        {
            _owner = owner;
            _foreignIndexes = new ConcurrentDictionary<Type, ConcurrentDictionary<string, Tuple<Delegate, object>>>();
        }

        public ConcurrentDictionary<Guid, ConcurrentList<TForeign>> GetMultiple<TForeign>(Expression<Func<TForeign, Guid?>> foreignKey)
            where TForeign : ModelBase
        {
            var type = typeof(TForeign);
            var guidMember = ((MemberExpression)((UnaryExpression)foreignKey.Body).Operand).Member.Name;

            ConcurrentDictionary<string, Tuple<Delegate, object>> t;

            if (!_foreignIndexes.TryGetValue(type, out t))
            {
                lock (this)
                {
                    if (!_foreignIndexes.TryGetValue(type, out t))
                    {
                        t = new ConcurrentDictionary<string, Tuple<Delegate, object>>();
                        _foreignIndexes.TryAdd(type, t);
                    }
                }
            }

            if (!t.TryGetValue(guidMember, out var value))
            {
                lock (this)
                {
                    if (!t.TryGetValue(guidMember, out value))
                    {
                        var func = foreignKey.Compile();
                        var other =
                            new ConcurrentDictionary<Guid, ConcurrentList<TForeign>>(
                                _owner.Set<TForeign>()
                                    .GroupBy(func)
                                    .Where(v => v.Key.HasValue)
                                    .Select(v => new KeyValuePair<Guid, ConcurrentList<TForeign>>(v.Key.Value, new ConcurrentList<TForeign>(v))));

                        _owner.ModelChanged += (change) =>
                        {
                            if (type != change.Source.GetType())
                                return;

                            Guid? removeKey = null;
                            Guid? addKey = null;
                            
                            
                            switch (change.ChangeType)
                            {
                                case ChangeType.Add:
                                    addKey = func((TForeign)change.Source);
                                    break;
                                case ChangeType.Remove:
                                    removeKey = func((TForeign)change.Source);
                                    break;
                                case ChangeType.Update when change.PropertyName == guidMember:
                                    removeKey = (Guid?) change.OldValue;
                                    addKey = (Guid?) change.NewValue;
                                    break;
                            }
                            
                            if (removeKey != null)
                            {
                                other[removeKey.Value].Remove((TForeign) change.Source);
                            }

                            if (addKey != null)
                            {
                                if (!other.TryGetValue(addKey.Value, out var list))
                                {
                                    lock (this)
                                    {
                                        if (!other.TryGetValue(addKey.Value, out list))
                                        {
                                            list = new ConcurrentList<TForeign>();
                                            other.TryAdd(addKey.Value, list);
                                        }
                                    }
                                }

                                other[addKey.Value].Add((TForeign) change.Source);

                            }
                        };
                        
                        value = Tuple.Create((Delegate) func, (object) other);
                        t.TryAdd(guidMember, value);
                    }
                }
            }

            return (ConcurrentDictionary<Guid, ConcurrentList<TForeign>>) value.Item2;
        }

        public abstract IEnumerator GetEnumerator();
    }
}