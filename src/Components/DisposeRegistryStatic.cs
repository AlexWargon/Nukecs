namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    
    public static unsafe class DisposeRegistryStatic<T> where T : unmanaged, IDisposable
    {
        // Keeps the delegate alive: its function pointer is stored and called later.
        private static DisposeDelegate _dispose;

        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(DisposeDelegate))]
        public static void Dispose(byte* buffer, int index) {
            ((T*)buffer)[index].Dispose();
        }
        public static void Register()
        {
            var componentType = ComponentTypeMap.GetComponentType(typeof(T));
            _dispose ??= Dispose;
            componentType.disposeFn = Marshal.GetFunctionPointerForDelegate(_dispose);
            ComponentTypeMap.SetComponentType<T>(componentType);
        }
    }

    public unsafe delegate void DisposeDelegate(byte* buffer, int index);
    
    public static unsafe class CopyRegistryStatic<T> where T : unmanaged, ICopyable<T>
    {
        // Keeps the delegate alive: its function pointer is stored and called later.
        private static CopyDelegate _copy;

        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(CopyDelegate))]
        public static void Copy(byte* fromBuffer, byte* toBuffer, int fromEntity, int toEntity, int fromIndexInChunk, int toIndexInChunk) {
            var castedBuffer = (T*)fromBuffer;
            ref var refFrom = ref castedBuffer[fromIndexInChunk];
            ((T*)toBuffer)[toIndexInChunk] = refFrom.Copy(toEntity);
        }

        public static void Register()
        {
            var componentType = ComponentTypeMap.GetComponentType(typeof(T));
            _copy ??= Copy;
            componentType.copyFn = Marshal.GetFunctionPointerForDelegate(_copy);
            ComponentTypeMap.SetComponentType<T>(componentType);
        }
    }
    public unsafe delegate void CopyDelegate(byte* fromBuffer, byte* toBuffer, int fromEntity, int toEntity, int fromIndexInChunk, int toIndexInChunk);
    public unsafe delegate void PoolResizeDelegate(byte* buffer, int index);
    public static unsafe class OnPoolResizeRegistryStatic<T> where T : unmanaged, IOnPoolResize
    {
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(PoolResizeDelegate))]
        public static void OnResize(byte* buffer, int index) {
            ((T*)buffer)[index].OnPoolResize(buffer);
        }
        public static void Register()
        {
            // var componentType = ComponentTypeMap.GetComponentType(typeof(T));
            // componentType.copyFn = Marshal.GetFunctionPointerForDelegate(new CopyDelegate(Copy));
            // ComponentTypeMap.SetComponentType<T>(componentType);
        }
    }
}