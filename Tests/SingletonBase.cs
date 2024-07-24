using System.Runtime.CompilerServices;

namespace Wargon.Nukecs.Tests {
    public abstract class SingletonBase<T> where T : class, new() {
        public static T Singleton {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _instance ??= new T();
        }
        private static T _instance;

    }
}