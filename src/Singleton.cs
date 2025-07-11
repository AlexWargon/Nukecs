﻿using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Wargon.Nukecs.Tests {
    public struct Singleton<T> where T : unmanaged, IInit 
    {
        private static readonly SharedStatic<Reference> instance = SharedStatic<Reference>.GetOrCreate<Singleton<T>>();
        public static ref T Instance
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (instance.Data.IsCreated == false)
                {
                    instance.Data.Value = new T();
                    instance.Data.Value.Init();
                    instance.Data.IsCreated = true;
                }

                return ref instance.Data.Value;
            }
        }

        public static void Set(ref T reference) {
            instance.Data.Value = reference;
            instance.Data.IsCreated = true;
        }
        private struct Reference
        {
            internal T Value;
            internal bool IsCreated;
        }
    }

    public interface IInit {
        void Init();
    }
}