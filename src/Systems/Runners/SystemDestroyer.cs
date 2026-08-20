using System.Runtime.InteropServices;

namespace Wargon.Nukecs
{
    internal unsafe class SystemDestroyer<T> : ISystemDestroyer where T : unmanaged, IOnDestroy
    {
        private T* system;
        private GCHandle gcHandle;
        public SystemDestroyer(ref T system)
        {
            fixed (T* ptr = &system)
            {
                this.system = ptr;
                gcHandle = GCHandle.Alloc(system);
            }
        }
        public void Destroy(ref World world)
        {
            system->OnDestroy(ref world);
            gcHandle.Free();
        }
    }
}