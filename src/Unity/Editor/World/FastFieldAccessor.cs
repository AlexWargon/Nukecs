#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Wargon.Nukecs.Editor
{
    public static class FastFieldAccessor
    {
        private static readonly Dictionary<string, Func<object, object>> getters = new();
        private static readonly Dictionary<string, Action<object, object>> setters = new();

        private static string GetKey(Type type, string fieldName) => $"{type.FullName}.{fieldName}";

        
        public static Func<object, object> GetGetter(Type type, string fieldName)
        {
            var key = GetKey(type, fieldName);
            if (getters.TryGetValue(key, out var getter)) return getter;

            var param = Expression.Parameter(typeof(object), "instance");
            var castInstance = Expression.Convert(param, type);
            var field = Expression.Field(castInstance, fieldName);
            var castResult = Expression.Convert(field, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(castResult, param);
            getter = lambda.Compile();

            getters[key] = getter;
            return getter;
        }

        public static Action<object, object> GetSetter(Type type, string fieldName)
        {
            var key = GetKey(type, fieldName);
            if (setters.TryGetValue(key, out var setter)) return setter;

            var instanceParam = Expression.Parameter(typeof(object), "instance");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var castInstance = Expression.Convert(instanceParam, type);
            var field = Expression.Field(castInstance, fieldName);
            var castValue = Expression.Convert(valueParam, field.Type);

            var assign = Expression.Assign(field, castValue);
            var lambda = Expression.Lambda<Action<object, object>>(assign, instanceParam, valueParam);

            setter = lambda.Compile();

            setters[key] = setter;
            return setter;
        }
    }
}
#endif