using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {

    public interface IComponent {
        public static int Count() {
            return ComponentAmount.Value.Data;
        }
    }

    public struct ComponentAmount {
        public static readonly SharedStatic<int> Value = SharedStatic<int>.GetOrCreate<ComponentAmount>();
    }
    public struct Component {
        /// <summary>
        /// Components count that are using right now
        /// </summary>
        public static readonly SharedStatic<int> Count;

        static Component() {
            Count = SharedStatic<int>.GetOrCreate<Component>();
            Count.Data = 0;
            //Initialization();
        }
        
        [BurstDiscard][RuntimeInitializeOnLoadMethod]
        public static void Initialization() {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                var types = assembly.GetTypes();
                foreach (var type in types) {
                    if (typeof(Wargon.Nukecs.IComponent).IsAssignableFrom(type) && type != typeof(Wargon.Nukecs.IComponent)) {
                        //Debug.Log($"Component {type.Name} with id {ComponentAmount.Value.Data}");
                        ComponentAmount.Value.Data++;
                    }
                }
            }
        }
    }
    
    public struct ComponentMeta<T> where T : unmanaged {
        private static readonly SharedStatic<int> id;

        public static int Index {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => id.Data;
        }
        static ComponentMeta() {
            id = SharedStatic<int>.GetOrCreate<ComponentMeta<T>>();
            id.Data = Component.Count.Data++;
            //Debug.Log($"{typeof(T).Name} with id {id.Data}");
            Init();
        }
        [BurstDiscard]
        private static void Init() {
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