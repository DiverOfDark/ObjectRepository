using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace OutCode.EscapeTeams.ObjectRepository
{
    internal static class ReflectionExtensions
    {
        private static readonly ConcurrentDictionary<FieldInfo, Func<object, object>> GetterCache = new ConcurrentDictionary<FieldInfo, Func<object, object>>();
        private static readonly ConcurrentDictionary<MethodInfo, Delegate> SetterCache = new ConcurrentDictionary<MethodInfo, Delegate>();

        public static Func<object, object> GetOrCreateGetDelegate(this FieldInfo entityInfo)
        {
            return GetterCache.GetOrAdd(entityInfo, x =>
            {
                string methodName = entityInfo.ReflectedType.FullName + ".get_" + x.Name;
                var getterMethod = new DynamicMethod(methodName, typeof(object), new[] {typeof(object)}, true);
                var gen = getterMethod.GetILGenerator();
                gen.Emit(OpCodes.Ldarg_0);
                gen.Emit(OpCodes.Castclass, entityInfo.DeclaringType);
                gen.Emit(OpCodes.Ldfld, x);
                gen.Emit(OpCodes.Castclass, typeof(object));
                gen.Emit(OpCodes.Ret);

                return (Func<object, object>) getterMethod.CreateDelegate(typeof(Func<object, object>));
            });
        }

        public static Delegate GetOrCreateGetter(this PropertyInfo propertyInfo) => propertyInfo.GetMethod.GetOrCreateMethodDelegate(typeof(Func<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType));
        public static Delegate GetOrCreateSetter(this PropertyInfo propertyInfo) => propertyInfo.SetMethod.GetOrCreateMethodDelegate(typeof(Action<,>).MakeGenericType(propertyInfo.DeclaringType, propertyInfo.PropertyType));

        private static Delegate GetOrCreateMethodDelegate(this MethodInfo entityInfo, Type delegateType) => SetterCache.GetOrAdd(entityInfo, x => entityInfo.CreateDelegate(delegateType));
    }
}