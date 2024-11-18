using System.Collections.Generic;
using System;
using UnityEngine;

namespace Wargon.Nukecs.Generated
{
    public static partial class GeneratedComponentList
    {
        private static List<Type> _allComponents;

        //[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void InitializeComponentList()
        {
            _allComponents = new List<Type>();

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType("Wargon.Nukecs.Generated.GeneratedComponentList");
                if (type != null)
                {
                    var field = type.GetField("AssemblyComponents", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (field != null && field.GetValue(null) is IEnumerable<Type> components)
                    {
                        _allComponents.AddRange(components);
                    }
                }
            }
            
        }
    
        public static IReadOnlyList<Type> GetAllComponents()
        {
            return _allComponents;
        }
    }
    public static partial class GeneratedDisposeRegistryStatic
    {
        public static readonly IntPtr[] fn;
        static GeneratedDisposeRegistryStatic()
        {
            fn = new IntPtr[ComponentAmount.Value.Data + 1];
        }

        public static void RegisterTypes()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var type = assembly.GetType("Wargon.Nukecs.Generated.GeneratedDisposeRegistryStatic");
                if (type != null)
                {
                    type.GetMethod("Register").Invoke(null, null);
                }
            }
        }
    }
}