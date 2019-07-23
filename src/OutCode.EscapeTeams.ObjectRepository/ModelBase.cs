using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OutCode.EscapeTeams.ObjectRepository 
{
    public abstract class ModelBase
    {
        /// <summary>
        /// Primary key of this object.
        /// </summary>
        public Guid Id => Entity.Id;

        /// <summary>
        /// Underlying database item associated with the model.
        /// </summary>
        protected internal abstract BaseEntity Entity { get; }

        protected ObjectRepositoryBase ObjectRepository { get; private set; }

        internal void SetOwner(ObjectRepositoryBase owner)
        {
            if (ObjectRepository != null)
                throw new InvalidOperationException("ObjectRepository already set!");
            ObjectRepository = owner;
        }

        /// <summary>
        /// Returns a list of related entities by mathing the current object's ID to specified property.
        /// </summary>
        protected IEnumerable<T> Multiple<T>(Func<Expression<Func<T, Guid?>>> propertyGetter, [CallerMemberName] string callingProperty = "") where T : ModelBase
        {
            var multiple = ObjectRepository.Set(GetType()).GetMultiple(propertyGetter);

            if (!multiple.ContainsKey(Id))
            {
                multiple.TryAdd(Id, new ConcurrentList<T>());
            }
            return multiple[Id];
        }

        /// <summary>
        /// Returns a single entity by matching the specified value to it's ID.
        /// </summary>
        protected T Single<T>(Guid? id, [CallerMemberName] string callingProperty = "", [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) where T : ModelBase
        {
            if (id == null)
                return null;

            var set = ObjectRepository.Set<T>();

            var value = set.Find(id.Value);

            if (value == null)
            {
                throw new ObjectRepositoryException<T>(id, callingProperty, $"(file {file} at line {line})");
            }

            return value;
        }

        public event Action<ModelChangedEventArgs> PropertyChanging;
        
        protected void UpdateProperty<TValue,TEntity>(TEntity entity, Func<Expression<Func<TEntity, TValue>>> expressionGetter, TValue newValue)
        {
            var c = PropertyUpdater<TEntity, TValue>.GetPropertyUpdater(expressionGetter);
            
            var oldValue = c.UpdateValue(entity, newValue);

            PropertyChanging?.Invoke(ModelChangedEventArgs.PropertyChange(this, c.Name, oldValue, newValue));
        }

        internal class PropertyUpdater<TEntity, TValue>
        {
            internal static readonly ConcurrentDictionary<Func<Expression<Func<TEntity, TValue>>>, PropertyUpdater<TEntity, TValue>> Cache = new ConcurrentDictionary<Func<Expression<Func<TEntity, TValue>>>, PropertyUpdater<TEntity, TValue>>();
            private readonly PropertyInfo _propertyInfo;

            public static PropertyUpdater<TEntity, TValue> GetPropertyUpdater(Func<Expression<Func<TEntity, TValue>>> expressionGetter) => Cache.GetOrAdd(expressionGetter, x => new PropertyUpdater<TEntity, TValue>(x));

            private PropertyUpdater(Func<Expression<Func<TEntity, TValue>>> expressionGetter)
            {
                var propertyExpr = (MemberExpression) expressionGetter().Body;

                _propertyInfo = (PropertyInfo) propertyExpr.Member;
            }

            public string Name => _propertyInfo.Name;


            public TValue UpdateValue(TEntity entity, TValue newValue)
            {
                var oldValue = (TValue)_propertyInfo.GetOrCreateGetter().DynamicInvoke(entity);
                _propertyInfo.GetOrCreateSetter().DynamicInvoke(entity, newValue);
                return oldValue;
            }
        }
    }
}