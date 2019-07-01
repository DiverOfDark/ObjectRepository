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
        
        protected void UpdateProperty<T>(Func<Expression<Func<T>>> expressionGetter, T newValue)
        {
            var c = PropertyUpdater<T>.GetPropertyUpdater(expressionGetter);
            
            var oldValue = c.UpdateValue(newValue);

            PropertyChanging?.Invoke(ModelChangedEventArgs.PropertyChange(this, c.Name, oldValue, newValue));
        }

        private class PropertyUpdater<T>
        {
            private static readonly ConcurrentDictionary<Func<Expression<Func<T>>>, PropertyUpdater<T>> Cache = new ConcurrentDictionary<Func<Expression<Func<T>>>, PropertyUpdater<T>>();
            private readonly object _entity;
            private readonly PropertyInfo _propertyInfo;

            public static PropertyUpdater<T> GetPropertyUpdater(Func<Expression<Func<T>>> expressionGetter) => Cache.GetOrAdd(expressionGetter, x => new PropertyUpdater<T>(x));

            private PropertyUpdater(Func<Expression<Func<T>>> expressionGetter)
            {
                var propertyExpr = (MemberExpression) expressionGetter().Body;

                var source = (MemberExpression) propertyExpr.Expression;
                var entityInfo = (FieldInfo) source.Member;
                var owner = (ConstantExpression) source.Expression;
                var entityOwner = owner.Value;

                var getDelegate = entityInfo.GetOrCreateGetDelegate();

                _entity = getDelegate(entityOwner);

                _propertyInfo = (PropertyInfo) propertyExpr.Member;
            }

            public string Name => _propertyInfo.Name;


            public T UpdateValue(T newValue)
            {
                var oldValue = (T)_propertyInfo.GetOrCreateGetter().DynamicInvoke(_entity);
                _propertyInfo.GetOrCreateSetter().DynamicInvoke(_entity, newValue);
                return oldValue;
            }
        }
    }
}