using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {

    public interface IComponent {
        private static int count = -1;
        
        [RuntimeInitializeOnLoadMethod]
        public static void Initialization() {
            Count();
        }

        public static int Count() {
            if (count != -1) return count;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    if (typeof(IComponent).IsAssignableFrom(type)) {
                        count++;
                    }
                }
            }

            return count;
        }
    }
    public struct Component {
        public static int Count;
    }

    public struct ComponentMeta<T> where T : unmanaged {
        private static readonly SharedStatic<int> id;

        public static int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => id.Data;
        }
        static ComponentMeta() {
            id = SharedStatic<int>.GetOrCreate<ComponentMeta<T>>();
            Init();
        }
        [BurstDiscard]
        private static void Init() {
            id.Data = Interlocked.Increment(ref Component.Count);
            ComponentsMap.Add(typeof(T), UnsafeUtility.AlignOf<T>(), Index);
        }
    }
    /// <summary>
    ///     Component Type Shared
    /// </summary>
    /// <typeparam name="T"></typeparam>
    // public abstract class CTS<T> where T : struct {
    //     public static readonly SharedStatic<int> ID;
    //
    //     static CTS() {
    //         ID = SharedStatic<int>.GetOrCreate<CTS<T>>();
    //     }
    // }
    public static class ComponentsMap {
        private static readonly Dictionary<Type, int> Aligns = new();
        private static readonly Dictionary<int, Type> TypeByIndex = new();
        private static readonly Dictionary<Type, int> IndexByType = new();
        public static readonly List<int> TypesIndexes = new();

        public static void Add(Type type, int align, int index) {
            Aligns[type] = align;
            TypeByIndex[index] = type;
            IndexByType[type] = index;
            TypesIndexes.Add(index);
        }

        public static int AlignOf(Type type) => Aligns[type];
        public static Type GetType(int index) => TypeByIndex[index];
        public static int Index(Type type) => IndexByType[type];
    }
}