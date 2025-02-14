using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Wargon.Nukecs
{
    public static class dbug
    {
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(object massage)
        {
            UnityEngine.Debug.Log(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void log(string massage)
        {
            //CustomConsoleWindow.AddMessage(massage);
            UnityEngine.Debug.Log(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error(string massage)
        {
            UnityEngine.Debug.LogError(massage);
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void error_no_componnet<T>(Entity entity)
        {
            UnityEngine.Debug.LogError($"entity: {entity.id}, has no componnet {typeof(T).Name}" );
        }
        [BurstDiscard]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void warn(string massage)
        {
            UnityEngine.Debug.LogWarning(massage);
        }
    }
}