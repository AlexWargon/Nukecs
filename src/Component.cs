using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Wargon.Nukecs {
    public struct Component {
        public static int Count;
    }
    
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

    public struct ComponentMeta<T> where T : unmanaged {
        public static readonly int Index;

        static ComponentMeta() {
            Index = Component.Count++;
            CTS<T>.ID.Data = Index;
            ComponentsMap.Add(typeof(T), UnsafeUtility.AlignOf<T>(), Index);
        }
    }

    public static class ComponentsMap {
        private static readonly Dictionary<Type, int> Aligns = new();
        private static readonly Dictionary<int, Type> TypeByIndex = new();
        private static readonly Dictionary<Type, int> IndexByType = new();

        public static void Add(Type type, int align, int index) {
            Aligns[type] = align;
            TypeByIndex[index] = type;
            IndexByType[type] = index;
        }

        public static int AlignOf(Type type) => Aligns[type];
        public static Type GetType(int index) => TypeByIndex[index];
        public static int Index(Type type) => IndexByType[type];
    }
}