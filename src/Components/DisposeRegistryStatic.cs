using Unity.Collections.LowLevel.Unsafe;

namespace Wargon.Nukecs
{
    using System;
    using System.Runtime.InteropServices;
    using Unity.Burst;
    
    public static unsafe class DisposeRegistryStatic<T> where T : unmanaged, IDisposable
    {
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(DisposeDelegate))]
        public static void Dispose(byte* buffer, int index) {
            ((T*)buffer)[index].Dispose();
        }
        public static void Register()
        {
            var componentType = ComponentTypeMap.GetComponentType(typeof(T));
            componentType.disposeFn = Marshal.GetFunctionPointerForDelegate(new DisposeDelegate(Dispose));
            ComponentTypeMap.SetComponentType<T>(componentType);
        }
    }

    public unsafe delegate void DisposeDelegate(byte* buffer, int index);
    
    public static unsafe class CopyRegistryStatic<T> where T : unmanaged, ICopyable<T>
    {
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(CopyDelegate))]
        public static void Copy(byte* fromBuffer, byte* toBuffer, int from, int to) {
            var castedBuffer = (T*)fromBuffer;
            ref var refFrom = ref castedBuffer[from];
            ((T*)toBuffer)[to] = refFrom.Copy(to);
        }

        public static void Register()
        {
            var componentType = ComponentTypeMap.GetComponentType(typeof(T));
            componentType.copyFn = Marshal.GetFunctionPointerForDelegate(new CopyDelegate(Copy));
            ComponentTypeMap.SetComponentType<T>(componentType);
        }
    }
    public unsafe delegate void CopyDelegate(byte* fromBuffer, byte* toBuffer, int from, int to);
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