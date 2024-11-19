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
            componentType.disposeFn = DisposeRegistryFunction.CreatePtr<T>();
            ComponentTypeMap.SetComponentType<T>(componentType);
            //Generated.GeneratedDisposeRegistryStatic.fn[ComponentType<T>.Index] = CreatePtr();
        }
    }
    
    internal static unsafe class DisposeRegistryFunction
    {
        internal static IntPtr CreatePtr<T>() where T : unmanaged, IDisposable
        {
            return Ptr(DisposeRegistryStatic<T>.Dispose);
        }

        internal static IntPtr Ptr(DisposeDelegate func)
        {
            return Marshal.GetFunctionPointerForDelegate(func);
        }
    }
    public unsafe delegate void DisposeDelegate(byte* comp, int index);
    
    public static unsafe class CopyRegistryStatic<T> where T : unmanaged, ICopyable<T>
    {
        [BurstCompile(CompileSynchronously = true)]
        [AOT.MonoPInvokeCallback(typeof(CopyDelegate))]
        public static void Copy(byte* buffer, int from, int to) {
            var castedBuffer = (T*)buffer;
            ref var refFrom = ref castedBuffer[from];
            castedBuffer[to] = refFrom.Copy(to);
        }

        public static void Register()
        {
            var componentType = ComponentTypeMap.GetComponentType(typeof(T));
            componentType.copyFn = CopyRegistryFunction.CreatePtr<T>();
            ComponentTypeMap.SetComponentType<T>(componentType);
        }
    }
    public unsafe delegate void CopyDelegate(byte* buffer, int from, int to);
    internal static unsafe class CopyRegistryFunction
    {
        internal static IntPtr CreatePtr<T>() where T : unmanaged, ICopyable<T>
        {
            return Ptr(CopyRegistryStatic<T>.Copy);
        }

        internal static IntPtr Ptr(CopyDelegate func)
        {
            return Marshal.GetFunctionPointerForDelegate(func);
        }
    }
}