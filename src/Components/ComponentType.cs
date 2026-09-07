using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    public struct ComponentType<T> where T : unmanaged {
        private static readonly SharedStatic<ComponentTypeData> ID = SharedStatic<ComponentTypeData>.GetOrCreate<ComponentType<T>>();
        private struct KindContext { }
        // 0 = not initialized, 1 = IComponent, 2 = other query parameter.
        // Kept separately to preserve the serialized ComponentTypeData layout.
        private static readonly SharedStatic<byte> Kind = SharedStatic<byte>.GetOrCreate<KindContext>();

        public static bool IsComponent {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get {
                EnsureRegistered();
                return Kind.Data == 1;
            }
        }

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
            if (Kind.Data == 0)
                Kind.Data = typeof(IComponent).IsAssignableFrom(typeof(T)) ? (byte)1 : (byte)2;
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
