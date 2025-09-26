#if UNITY_EDITOR && NUKECS_DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Wargon.Nukecs.Editor
{
    public static class ComponentDrawerGenerator
    {
        private static readonly Dictionary<Type, Func<object, object>> cache = new();

        public static Func<object, object> GetDrawer(Type componentType)
        {
            if (!cache.TryGetValue(componentType, out var fun))
            {
                fun = Generate(componentType);
                cache[componentType] = fun;
            }
            return fun;
        }

        private static Func<object, object> Generate(Type type)
        {
            var objParam = Expression.Parameter(typeof(object), "obj");
            var typedVar = Expression.Variable(type, "c");

            // c = (T)obj;
            var assignTyped = Expression.Assign(typedVar, Expression.Convert(objParam, type));

            var bodyExpressions = new List<Expression> { assignTyped };
            BuildFields(type, typedVar, bodyExpressions);

            // return (object)c;  //
            var ret = Expression.Convert(typedVar, typeof(object));
            var body = Expression.Block(new[] { typedVar }, bodyExpressions.Concat(new[] { ret }));

            var lambda = Expression.Lambda<Func<object, object>>(body, objParam);
            Debug.Log(lambda.Body.ToString());
            return lambda.Compile();
        }

        // Recursively adds render and assignment expressions for all fields of the type
        private static void BuildFields(Type ownerType, Expression ownerExpr, List<Expression> dest)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = ownerType.GetFields(flags);

            foreach (var f in fields)
            {
                // Skipping private non-serializable fields
                if (f.IsPrivate && f.GetCustomAttribute<SerializeField>() == null)
                    continue;

                var fieldExpr = Expression.Field(ownerExpr, f);
                dest.Add(BuildDrawAndAssign(f, fieldExpr));
            }
        }

        private static Expression BuildDrawAndAssign(FieldInfo field, Expression fieldExpr)
        {
            var ft = field.FieldType;
            var label = Expression.Constant(field.Name);
            var emptyOpts = Expression.NewArrayInit(typeof(GUILayoutOption), Array.Empty<Expression>());

            // -------- enums (inevitable boxing due to API EnumPopup) --------
            if (ft.IsEnum)
            {
                var mi = typeof(EditorGUILayout).GetMethod(
                    "EnumPopup",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Enum), typeof(GUILayoutOption[]) },
                    null);

                var asEnum = Expression.Convert(fieldExpr, typeof(Enum)); // boxing enum
                var call = Expression.Call(mi!, label, asEnum, emptyOpts);
                var castBack = Expression.Convert(call, ft);
                return Expression.Assign(fieldExpr, castBack);
            }
            // ObjectRef<T>
            if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(ObjectRef<>))
            {
                var valueProp = ft.GetProperty("Value");
                if (valueProp == null || !valueProp.CanRead || !valueProp.CanWrite)
                    return null;

                var valueExpr = Expression.Property(fieldExpr, valueProp);
                var genericArg = ft.GetGenericArguments()[0];
                var labelExpr = Expression.Constant(field.Name);

                // EditorGUILayout.ObjectField(label, obj, type, allowSceneObjects, options)
                var method = typeof(EditorGUILayout).GetMethod(
                    nameof(EditorGUILayout.ObjectField),
                    new[] { typeof(string), typeof(UnityEngine.Object), typeof(Type), typeof(bool), typeof(GUILayoutOption[]) }
                );

                if (typeof(UnityEngine.Object).IsAssignableFrom(genericArg))
                {
                    // EditorGUILayout.ObjectField(label, value, typeof(T), true, null)
                    var call = Expression.Call(
                        method!,
                        labelExpr,
                        Expression.Convert(valueExpr, typeof(UnityEngine.Object)),
                        Expression.Constant(genericArg, typeof(Type)),
                        Expression.Constant(true, typeof(bool)),
                        Expression.Constant(null, typeof(GUILayoutOption[]))
                    );

                    return Expression.Assign(valueExpr, Expression.Convert(call, genericArg));
                }
                else
                {
                    var toStringMethod = typeof(object).GetMethod(nameof(ToString));
                    var valueToString = Expression.Call(valueExpr, toStringMethod!);

                    var labelM = typeof(EditorGUILayout).GetMethod(
                        nameof(EditorGUILayout.LabelField),
                        new[] { typeof(string), typeof(string), typeof(GUILayoutOption[]) }
                    );

                    var call = Expression.Call(
                        labelM!,
                        labelExpr,
                        valueToString,
                        Expression.Constant(null, typeof(GUILayoutOption[]))
                    );
                    return call;
                }
            }

            // -------- UnityEngine.Object refs --------
            if (typeof(UnityEngine.Object).IsAssignableFrom(ft))
            {
                var mi = typeof(EditorGUILayout).GetMethod(
                    "ObjectField",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(UnityEngine.Object), typeof(Type), typeof(bool), typeof(GUILayoutOption[]) },
                    null);

                var call = Expression.Call(
                    mi!,
                    label,
                    Expression.Convert(fieldExpr, typeof(UnityEngine.Object)),
                    Expression.Constant(ft, typeof(Type)),
                    Expression.Constant(true),    // allowSceneObjects
                    emptyOpts);

                var castBack = Expression.Convert(call, ft);
                return Expression.Assign(fieldExpr, castBack);
            }

            // -------- Primitives and common types without boxing --------
            Expression CallLabeled(string name, Type paramT)
            {
                var mi = typeof(EditorGUILayout).GetMethod(
                    name,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), paramT, typeof(GUILayoutOption[]) },
                    null);
                return Expression.Call(mi!, label, fieldExpr, emptyOpts);
            }

            if (ft == typeof(int))        return Expression.Assign(fieldExpr, CallLabeled(nameof(EditorGUILayout.IntField), typeof(int)));
            if (ft == typeof(float))      return Expression.Assign(fieldExpr, CallLabeled(nameof(EditorGUILayout.FloatField), typeof(float)));
            if (ft == typeof(bool))       return Expression.Assign(fieldExpr, CallLabeled(nameof(EditorGUILayout.Toggle), typeof(bool)));
            if (ft == typeof(string))     return Expression.Assign(fieldExpr, CallLabeled(nameof(EditorGUILayout.TextField), typeof(string)));

            if (ft == typeof(Vector2))
            {
                var mi = typeof(EditorGUILayout).GetMethod(
                    nameof(EditorGUILayout.Vector2Field),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Vector2), typeof(GUILayoutOption[]) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr, emptyOpts));
            }
            if (ft == typeof(Vector3))
            {
                var mi = typeof(EditorGUILayout).GetMethod(
                    nameof(EditorGUILayout.Vector3Field),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Vector3), typeof(GUILayoutOption[]) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr, emptyOpts));
            }
            if (ft == typeof(Vector4))
            {
                var mi = typeof(EditorGUILayout).GetMethod(nameof(EditorGUILayout.Vector4Field),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Vector4), typeof(GUILayoutOption[]) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr, emptyOpts));
            }
            if (ft == typeof(float2))
            {
                var mi = typeof(CustomDrawers).GetMethod(
                    nameof(CustomDrawers.DrawFloat2),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(float2) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr));
            }
            if (ft == typeof(float3))
            {
                var mi = typeof(CustomDrawers).GetMethod(
                    nameof(CustomDrawers.DrawFloat3),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(float3) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr));
            }
            if (ft == typeof(float4))
            {
                var mi = typeof(CustomDrawers).GetMethod(nameof(CustomDrawers.DrawFloat4),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(float4) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr));
            }
            if (ft == typeof(Entity))
            {
                var mi = typeof(CustomDrawers).GetMethod(
                    nameof(CustomDrawers.DrawEntity),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Entity) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr));
            }
            if (ft == typeof(Color))
            {
                var mi = typeof(EditorGUILayout).GetMethod(
                    nameof(EditorGUILayout.ColorField),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(Color), typeof(GUILayoutOption[]) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr, emptyOpts));
            }
            if (ft == typeof(AnimationCurve))
            {
                var mi = typeof(EditorGUILayout).GetMethod(
                    nameof(EditorGUILayout.CurveField),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(AnimationCurve), typeof(GUILayoutOption[]) }, null);
                return Expression.Assign(fieldExpr, Expression.Call(mi!, label, fieldExpr, emptyOpts));
            }

            if (ft.IsValueType && !ft.IsPrimitive && !ft.IsEnum)
            {
                // sub = field; ...draw sub's fields...; field = sub;
                var subVar = Expression.Variable(ft, field.Name + "_tmp");
                var assignSub = Expression.Assign(subVar, fieldExpr);

                var nested = new List<Expression> { assignSub };
                BuildFields(ft, subVar, nested);
                var writeBack = Expression.Assign(fieldExpr, subVar);

                return Expression.Block(new[] { subVar }, nested.Concat(new[] { writeBack }));
            }

            // -------- Fallback: Just a Label to see that the field is not supported --------
            var labelMethod = typeof(EditorGUILayout).GetMethod(nameof(EditorGUILayout.LabelField), new[] { typeof(string), typeof(GUILayoutOption[]) });
            return Expression.Call(
                null,
                labelMethod!,
                Expression.Constant($"{field.Name} (unsupported)"),
                Expression.Constant(null, typeof(GUILayoutOption[]))
            );
        }
    }

    public static class CustomDrawers
    {
        private static GUIStyle _goStyle;

        private static GUIStyle ObjectFieldStyle =>
            _goStyle ??= new GUIStyle(GUI.skin.GetStyle("ObjectField"))
            {
                fixedHeight = EditorGUIUtility.singleLineHeight
            };

        public static float2 DrawFloat2(string label, float2 value)
        {
            return EditorGUILayout.Vector2Field(label, value);
        }

        public static float3 DrawFloat3(string label, float3 value)
        {
            return EditorGUILayout.Vector3Field(label, value);
        }

        public static float4 DrawFloat4(string label, float4 value)
        {
            return EditorGUILayout.Vector4Field(label, value);
        }

        public static Entity DrawEntity(string label, Entity value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(EditorGUIUtility.labelWidth - 4));

            if (!World.Get(0).IsAlive || !EditorApplication.isPlaying) return value;
            var eName = value.Has<Name>() ? $"(e:{value.id}) {value.Get<Name>().value.Value}" : $"(e:{value.id})";
            var content = EditorGUIUtility.ObjectContent(null, typeof(GameObject));
            content.text = eName;
            content.image = EditorGUIUtility.IconContent("greenLight").image;
            if (GUILayout.Button(content, ObjectFieldStyle))
            {
                ECSDebugWindowUI.CanWriteToWorld = false;
                ECSDebugWindowUI.Instance.SelectEntityFromField(value);
            }

            EditorGUILayout.EndHorizontal();
            return value;
        }



        
    }

    public class Expressions
    {
        private static readonly MethodInfo objectFieldMethod = typeof(EditorGUILayout).GetMethod(
            nameof(EditorGUILayout.ObjectField),
            new[] { typeof(string), typeof(UnityEngine.Object), typeof(Type), typeof(bool)}
        );
        public static Expression GetObjectCall(FieldInfo field, Expression fieldExpr)
        {
            EditorGUILayout.ObjectField("", null, typeof(Transform), true);
            var fieldType = field.FieldType;
            var valueField = fieldType.GetProperty("Value");
            var valueExpr = Expression.Property(fieldExpr, valueField!);
            var objType = fieldType.GetGenericArguments()[0];
            var objTypeConst = Expression.Constant(objType, typeof(Type));
            var labelExpr = Expression.Constant(field.Name);
            var allowSceneObj = Expression.Constant(true, typeof(bool));

            // EditorGUILayout.ObjectField(label, value, objType, true)
            var call = Expression.Call(
                objectFieldMethod,
                labelExpr,
                Expression.Convert(valueExpr, typeof(UnityEngine.Object)),
                objTypeConst,
                allowSceneObj
            );

            var assignValue = Expression.Assign(valueExpr, Expression.Convert(call, objType));
            return assignValue;
        }
    }
}
#endif