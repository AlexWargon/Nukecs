using System;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
//namespace Wargon.Nukecs.Editor
//{
// public static class StaticAllocatorChecker
// {
// #if UNITY_EDITOR
//     [InitializeOnLoadMethod]
//     static void OnEditorLoad()
//     {
//         EditorApplication.playModeStateChanged += state =>
//         {
//             if (state == PlayModeStateChange.EnteredPlayMode)
//             {
//                 CheckAllStaticAllocators();
//             }
//         };
//     }
// #endif
//
//     public static void CheckAllStaticAllocators()
//     {
//         int leakCount = 0;
//         var assembly = Assembly.GetExecutingAssembly();
//
//         foreach (var type in assembly.GetTypes())
//         {
//             leakCount += CheckStaticFields(type, type.Name);
//         }
//
//         if (leakCount == 0)
//             Debug.Log("[AllocatorChecker] Живых аллокаторов не найдено");
//         else
//             Debug.LogWarning($"[AllocatorChecker] Найдено {leakCount} живых аллокатора(ов)");
//     }
//
//     private static int CheckStaticFields(Type type, string path)
//     {
//         int count = 0;
//
//         foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
//         {
//             var value = field.GetValue(null);
//             if (value == null) continue;
//
//             string fullPath = $"{path}.{field.Name}";
//
//             // INativeDisposable: NativeArray, NativeList, UnsafeList
//             if (value is INativeDisposable disposable)
//             {
//                 if (disposable.IsCreated)
//                 {
//                     Debug.LogWarning($"[AllocatorChecker] Живой аллокатор: {fullPath} ({value.GetType().Name})");
//                     count++;
//                 }
//             }
//             // AllocatorHandle
//             else if (value is AllocatorManager.AllocatorHandle handle)
//             {
//                 if (handle.va)
//                 {
//                     Debug.LogWarning($"[AllocatorChecker] Живой AllocatorHandle: {fullPath}");
//                     count++;
//                 }
//             }
//             // Рекурсивно проверяем вложенные классы/структуры
//             else if (!value.GetType().IsPrimitive && !value.GetType().IsEnum && !value.GetType().IsPointer)
//             {
//                 count += CheckObjectFields(value, fullPath);
//             }
//         }
//
//         return count;
//     }
//
//     private static int CheckObjectFields(object obj, string path)
//     {
//         int count = 0;
//         var type = obj.GetType();
//
//         foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
//         {
//             var value = field.GetValue(obj);
//             if (value == null) continue;
//
//             string fullPath = $"{path}.{field.Name}";
//
//             if (value is INativeDisposable disposable)
//             {
//                 if (disposable.IsCreated)
//                 {
//                     Debug.LogWarning($"[AllocatorChecker] Живой аллокатор: {fullPath} ({value.GetType().Name})");
//                     count++;
//                 }
//             }
//             else if (value is AllocatorManager.AllocatorHandle handle)
//             {
//                 if (handle.IsValid)
//                 {
//                     Debug.LogWarning($"[AllocatorChecker] Живой AllocatorHandle: {fullPath}");
//                     count++;
//                 }
//             }
//             else if (!value.GetType().IsPrimitive && !value.GetType().IsEnum && !value.GetType().IsPointer)
//             {
//                 count += CheckObjectFields(value, fullPath);
//             }
//         }
//
//         return count;
//     }
// }
#endif