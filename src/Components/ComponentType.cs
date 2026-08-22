using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public struct ComponentType<T> where T : unmanaged {
        private static readonly SharedStatic<ComponentTypeData> ID = SharedStatic<ComponentTypeData>.GetOrCreate<ComponentType<T>>();

        public static unsafe int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                EnsureRegistered();
                return (*(ComponentTypeData*)ID.UnsafeDataPointer).index;
            }
        }

        public static unsafe ref ComponentTypeData Data {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                EnsureRegistered();
                return ref UnsafeUtility.AsRef<ComponentTypeData>(ID.UnsafeDataPointer);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [BurstDiscard]
        private static unsafe void EnsureRegistered() {
            if ((*(ComponentTypeData*)ID.UnsafeDataPointer).size != 0) return;
            var data = ComponentTypeMap.RegisterIfNeeded<T>();
            ID.Data = data;
        }
    }

    internal struct ComponentTypeInternal<T>
    {
        internal static readonly SharedStatic<int> Index = SharedStatic<int>.GetOrCreate<ComponentTypeInternal<T>>();
    }
}