#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wargon.Nukecs.Editor
{
    public static class FastReflectionAccessor
    {
        private static readonly Dictionary<string, Func<object, object>> getters = new();
        private static readonly Dictionary<string, Action<object, object>> setters = new();
        private static readonly Dictionary<(string, string), Delegate> methods = new();
        private static string GetKey(Type type, string fieldName) => $"{type.FullName}.{fieldName}";
        
        public static object GetProperty(Type type, string propertyName, object instance)
        {
            var getter = GetPropertyGetter(type, propertyName);
            return getter(instance);
        }
        
        public static object GetValue(Type type, string fieldName, object instance)
        {
            var getter = GetGetter(type, fieldName);
            return getter(instance);
        }
        public static Func<object, object> GetPropertyGetter(Type type, string propertyName)
        {
            var key = GetKey(type, propertyName);
            if (getters.TryGetValue(key, out var getter)) return getter;

            var param = Expression.Parameter(typeof(object), "instance");
            var castInstance = Expression.Convert(param, type);
            var property = Expression.Property(castInstance, propertyName);
            var castResult = Expression.Convert(property, typeof(object));
            var lambda = Expression.Lambda<Func<object, object>>(castResult, param);
            getter = lambda.Compile();

            getters[key] = getter;
            return getter;
        }
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
        
        public static Delegate GetMethod(Type targetType, string methodName, Type[] parameterTypes, Type returnType)
        {
            var key = (targetType.FullName, methodName);
            if (methods.TryGetValue(key, out var del)) return del;
            var method = targetType.GetMethod(methodName, parameterTypes);
            if (method == null)
                throw new MissingMethodException(targetType.Name, methodName);

            var instanceParameter = Expression.Parameter(typeof(object), "instance");
            var parametersParameter = Expression.Parameter(typeof(object[]), "parameters");

            var callParameters = new Expression[parameterTypes.Length];

            for (int i = 0; i < parameterTypes.Length; i++)
            {
                var valueObj = Expression.ArrayIndex(parametersParameter, Expression.Constant(i));
                callParameters[i] = Expression.Convert(valueObj, parameterTypes[i]);
            }

            var instanceCast = Expression.Convert(instanceParameter, targetType);
            var call = Expression.Call(instanceCast, method, callParameters);

            Expression body = returnType == typeof(void)
                ? (Expression)call
                : Expression.Convert(call, typeof(object));

            var lambda = Expression.Lambda<Func<object, object[], object>>(
                body, instanceParameter, parametersParameter
            );
            del = lambda.Compile();
            methods[key] = del;
            return del;
        }
        public static Delegate GetMethodSingleParam<TParam>(Type targetType, string methodName, Type returnType)
        {
            var key = (targetType.FullName, methodName);
            if (methods.TryGetValue(key, out var del)) return del;
            var paramType = typeof(TParam);
            var method = targetType.GetMethod(methodName, new []{paramType});
            if (method == null)
                throw new MissingMethodException(targetType.Name, methodName);
            var paramName = method.GetParameters()[0].Name;
            var instanceParameter = Expression.Parameter(typeof(object), "instance");
            var param = Expression.Parameter(paramType, paramName);

            var instanceCast = Expression.Convert(instanceParameter, targetType);
            var call = Expression.Call(instanceCast, method, param);

            var body = returnType == typeof(void)
                ? (Expression)call
                : Expression.Convert(call, typeof(object));

            var lambda = Expression.Lambda<Func<object, TParam, object>>(
                body, instanceParameter, param
            );
            del = lambda.Compile();
            methods[key] = del;
            return del;
        }
    }
    public static class ObjectReflectionExtensions
    {
        public static object GetFieldValue(this object obj, Type objType, string fieldName)
        {
            return FastReflectionAccessor.GetValue(objType, fieldName, obj);
        }
        public static object GetFieldValue(this object obj, string fieldName)
        {
            var type = obj.GetType();
            return FastReflectionAccessor.GetValue(type, fieldName, obj);
        }
        public static object GetPropertyValue(this object obj, Type objType, string propertyName)
        {
            return FastReflectionAccessor.GetProperty(objType, propertyName, obj);
        }
        public static object GetPropertyValue(this object obj, string propertyName)
        {
            var type = obj.GetType();
            return FastReflectionAccessor.GetProperty(type, propertyName, obj);
        }
        public static void SetFieldValue(this object obj, string fieldName, object value)
        {
            var type = obj.GetType();
            var setter = FastReflectionAccessor.GetSetter(type, fieldName);
            setter(obj, value);
        }
        public static void SetPropertyValue(this object obj, string propertyName, object value)
        {
            var type = obj.GetType();
            var setter = FastReflectionAccessor.GetSetter(type, propertyName);
            setter(obj, value);
        }
        public static object InvokeMethod(this object obj, string methodName, Type[] parameterTypes, Type returnType, params object[] parameters)
        {
            var type = obj.GetType();
            var method = FastReflectionAccessor.GetMethod(type, methodName, parameterTypes, returnType);
            return method.DynamicInvoke(obj, parameters);
        }
        
        public static Delegate GetMethodDelegate(this object obj, Type objType, string methodName, Type[] parameterTypes, Type returnType)
        {
            return FastReflectionAccessor.GetMethod(objType, methodName, parameterTypes, returnType);
        }
        public static Delegate GetMethodDelegate<TParam>(this object obj, Type objType, string methodName, Type returnType)
        {
            return FastReflectionAccessor.GetMethodSingleParam<TParam>(objType, methodName, returnType);
        }
    }

    public static class type_db
    {
        internal static Dictionary<Type, type_data> map = new();

        public static type_data get_type_data(Type type)
        {
            if (!map.ContainsKey(type))
            {
                RuntimeHelpers.RunClassConstructor(typeof(type<>)
                    .MakeGenericType(type)
                    .TypeHandle);
                
            }
            return map[type];
        }
    }

    // ReSharper disable once InconsistentNaming
    public static class type<T>
    {
        public static readonly int index = type_data.count++;
        public static readonly Type val = typeof(T);
        public static readonly bool is_pointer = typeof(T).IsPointer;
        public static readonly bool is_enum = typeof(T).IsEnum;
        public static readonly bool is_generic = typeof(T).IsGenericType;
        public static readonly Type generic_type_definition = is_generic ? val.GetGenericTypeDefinition() : null;
        public static readonly type_data generic_argument00 = is_generic ? type_db.get_type_data(typeof(T).GetGenericArguments()[0]) : null;
        public static readonly type_data generic_argument01 = is_generic && typeof(T).GetGenericArguments().Length > 1 ? type_db.get_type_data(val.GetGenericArguments()[1]) : null;

        public static bool is_assignable_from(type_data other)
        {
            return val.IsAssignableFrom(other.val);
        }
        static type()
        {
            type_db.map[typeof(T)] = AsTypeData();
        }
        
        private static Func<object, object> MakeGetter(FieldInfo field)
        {
            var objParam = Expression.Parameter(typeof(object), "obj");
            var fieldAccess = Expression.Field(Expression.Convert(objParam, field.DeclaringType), field);
            var convert = Expression.Convert(fieldAccess, typeof(object));
            return Expression.Lambda<Func<object, object>>(convert, objParam).Compile();
        }

        private static Action<object, object> MakeSetter(FieldInfo field)
        {
            var objParam = Expression.Parameter(typeof(object), "obj");
            var valueParam = Expression.Parameter(typeof(object), "value");
            var fieldAccess = Expression.Field(Expression.Convert(objParam, field.DeclaringType), field);
            var assign = Expression.Assign(fieldAccess, Expression.Convert(valueParam, field.FieldType));
            return Expression.Lambda<Action<object, object>>(assign, objParam, valueParam).Compile();
        }
        public static type_data AsTypeData()
        {
            return new type_data(
                index,
                val, 
                is_enum,
                is_pointer,
                is_generic, 
                generic_type_definition, 
                generic_argument00, 
                generic_argument01);
        }
    }
    
    // ReSharper disable once InconsistentNaming
    public class type_data
    {
        public readonly int index;
        public readonly Type val;
        public readonly bool is_enum;
        public readonly bool is_pointer;
        public readonly bool is_generic;
        public readonly Type generic_type_definition;
        public readonly type_data generic_argument00;
        public readonly type_data generic_argument01;
        public string name => val.Name;
        public string full_name => val.FullName;
        public bool is_value => val.IsValueType;
        public bool is_class => val.IsClass;
        public bool is_primitive => val.IsPrimitive;
        public static int count;
        public type_data(int idx, Type v, bool isEnum, bool is_ptr, bool is_gen, Type gen_def, type_data gen_arg0, type_data gen_arg1)
        {
            index = idx;
            val = v;
            is_enum = isEnum;
            is_pointer = is_ptr;
            is_generic = is_gen;
            generic_type_definition = gen_def;
            generic_argument00 = gen_arg0;
            generic_argument01 = gen_arg1;
        }
    }
}
#endif