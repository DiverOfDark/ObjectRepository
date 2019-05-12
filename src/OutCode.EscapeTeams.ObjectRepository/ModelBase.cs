using System;
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
        protected IEnumerable<T> Multiple<T>(Expression<Func<T, Guid?>> propertyGetter, [CallerMemberName] string callingProperty = "") where T : ModelBase
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

        protected void UpdateProperty<T>(Expression<Func<T>> expressionGetter, T newValue)
        {
            var propertyExpr = (MemberExpression) expressionGetter.Body;

            var source = (MemberExpression) propertyExpr.Expression;
            var entityInfo = (FieldInfo) source.Member;
            var owner = (ConstantExpression) source.Expression;
            var entityOwner = owner.Value;

            var getDelegate = entityInfo.GetOrCreateGetDelegate();

            var entity = getDelegate(entityOwner);

            var propertyInfo = (PropertyInfo) propertyExpr.Member;

            var oldValue = propertyInfo.GetOrCreateGetter().DynamicInvoke(entity);
            propertyInfo.GetOrCreateSetter().DynamicInvoke(entity, newValue);

            PropertyChanging?.Invoke(ModelChangedEventArgs.PropertyChange(this, propertyInfo.Name, oldValue, newValue));
        }

    }
}