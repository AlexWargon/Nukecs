using System.Runtime.CompilerServices;
using Unity.Burst;
using UnityEngine;

namespace Wargon.Nukecs
{
    public static class dbug
    {
        private static string _hexColor;
        private const string COLOR_FORMAT = "<color=#{0}>{1}</color>";

        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(object massage)
        {
            Debug.Log(massage);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string message, Color color)
        {
            _hexColor = ColorUtility.ToHtmlStringRGB(color);
            Debug.Log(string.Format(COLOR_FORMAT, _hexColor, message));
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string massage)
        {
            //CustomConsoleWindow.AddMessage(massage);
            Debug.Log(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error(string massage)
        {
            Debug.LogError(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error_no_componnet<T>(Entity entity)
        {
            Debug.LogError($"entity: {entity.id}, has no componnet {typeof(T).Name}" );
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void warn(string massage)
        {
            Debug.LogWarning(massage);
        }
    }
}