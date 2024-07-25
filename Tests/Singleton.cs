using System.Runtime.CompilerServices;
using Unity.Burst;

namespace Wargon.Nukecs.Tests {
    public struct Singleton<T> where T: unmanaged
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
                    instance.Data.IsCreated = true;
                }
                
                return ref instance.Data.Value;
            }
        }

        private struct Reference
        {
            internal T Value;
            internal bool IsCreated;
        }
    }
}