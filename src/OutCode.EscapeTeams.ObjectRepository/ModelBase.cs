using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
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

            var value = set.Indexes[nameof(Id)].GetOrDefault(id.Value);

            if (value == null)
            {
                throw new ObjectRepositoryException<T>(id, callingProperty, $"(file {file} at line {line})");
            }

            return value;
        }

        public event Action<ModelChangedEventArgs> PropertyChanging;

        private static readonly ConcurrentDictionary<FieldInfo, Func<object, object>> GetterCache = new ConcurrentDictionary<FieldInfo, Func<object, object>>();
        private static readonly ConcurrentDictionary<MethodInfo, Delegate> SetterCache = new ConcurrentDictionary<MethodInfo, Delegate>();

        protected void UpdateProperty<T>(Expression<Func<T>> expressionGetter, T newValue)
        {
            var propertyExpr = (MemberExpression) expressionGetter.Body;

            var source = (MemberExpression) propertyExpr.Expression;
            var entityInfo = (FieldInfo) source.Member;
            var owner = (ConstantExpression) source.Expression;
            var entityOwner = owner.Value;

            var getDelegate = GetOrCreateGetDelegate(entityInfo);

            var entity = getDelegate(entityOwner);

            var propertyInfo = (PropertyInfo) propertyExpr.Member;

            var propertyGetter = GetOrCreateSetDelegate(propertyInfo.GetMethod, typeof(Func<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType));
            var oldValue = propertyGetter.DynamicInvoke(entity);
            var setDelegate = GetOrCreateSetDelegate(propertyInfo.SetMethod, typeof(Action<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType));
            setDelegate.DynamicInvoke(entity, newValue);

            PropertyChanging?.Invoke(ModelChangedEventArgs.PropertyChange(this, propertyInfo.Name, oldValue, newValue));
        }

        private Func<object, object> GetOrCreateGetDelegate(FieldInfo entityInfo)
        {
            if (!GetterCache.TryGetValue(entityInfo, out var func))
            {
                lock (GetterCache)
                {
                    if (!GetterCache.TryGetValue(entityInfo, out func))
                    {
                        var field = entityInfo;

                        string methodName = entityInfo.ReflectedType.FullName + ".get_" + field.Name;
                        var getterMethod = new DynamicMethod(methodName, typeof(object), new[] {typeof(object)}, true);
                        var gen = getterMethod.GetILGenerator();
                        gen.Emit(OpCodes.Ldarg_0);
                        gen.Emit(OpCodes.Castclass, entityInfo.DeclaringType);
                        gen.Emit(OpCodes.Ldfld, field);
                        gen.Emit(OpCodes.Castclass, typeof(object));
                        gen.Emit(OpCodes.Ret);

                        func = (Func<object, object>) getterMethod.CreateDelegate(typeof(Func<object, object>));
                        GetterCache[entityInfo] = func;
                    }
                }
            }
            return func;
        }
        private Delegate GetOrCreateSetDelegate(MethodInfo entityInfo, Type delegateType)
        {
            if (!SetterCache.TryGetValue(entityInfo, out var func))
            {
                lock (SetterCache)
                {
                    if (!SetterCache.TryGetValue(entityInfo, out func))
                    {
                        func = entityInfo.CreateDelegate(delegateType);
                        SetterCache[entityInfo] = func;
                    }
                }
            }
            return func;
        }
    }
}