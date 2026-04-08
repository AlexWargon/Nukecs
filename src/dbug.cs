using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEngine;

namespace Wargon.Nukecs
{
    public static class dbug
    {
        [BurstDiscard]
        [Conditional("NUKECS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(object message)
        {
            UnityEngine.Debug.Log(message);
        }
        [BurstDiscard]
        [Conditional("NUKECS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string message, Color color)
        {
            UnityEngine.Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{message}</color>");
        }
        [BurstDiscard]
        [Conditional("NUKECS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string message)
        {
            UnityEngine.Debug.Log(message);
        }
        [BurstDiscard]
        [Conditional("NUKECS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
        [BurstDiscard]
        [Conditional("NUKECS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error_no_component<T>(Entity entity)
        {
            UnityEngine.Debug.LogError($"entity: {entity.id}, has no component {typeof(T).Name}" );
        }
        [BurstDiscard]
        [Conditional("NUKECS_DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void warn(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }
    }
}
