using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using Unity.Collections;

namespace Wargon.Nukecs {
    [Serializable]
    public unsafe struct ComponentData {
        public byte[] componentData;
        public string componentName;
        public bool IsValid => !string.IsNullOrEmpty(componentName); 
        public void SetData<T>(T value) where T : unmanaged {
            componentData = StructToBytes(value);
        }

        public T GetData<T>() where T : unmanaged {
            return BytesToStruct<T>(componentData);
        }

        public static ComponentData Create<T>() where T : unmanaged {
            ComponentData componentData;
            componentData.componentData = StructToBytes(default(T));
            componentData.componentName = typeof(T).Name;
            return componentData;
        }
        public static ComponentData Create<T>(T value) where T : unmanaged {
            ComponentData componentData;
            componentData.componentData = StructToBytes(value);
            componentData.componentName = typeof(T).Name;
            return componentData;
        }
        public static byte[] StructToBytes<T>(T str) where T : unmanaged {
            int size = sizeof(T);
            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try {
                Marshal.StructureToPtr(str, ptr, true);
                Marshal.Copy(ptr, arr, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }

            return arr;
        }
        public static T BytesToStruct<T>(byte[] arr) where T : unmanaged
        {
            T component;
            int size = sizeof(T);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.Copy(arr, 0, ptr, size);
                component = Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            return component;
        }
        public static byte[] SerializeComponent(object component)
        {
            if (component == null)
            {
                return Array.Empty<byte>();
            }

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(ms, component);
                return ms.ToArray();
            }
        }

        public static object DeserializeComponent(byte[] data, Type type)
        {
            if (data == null || data.Length == 0)
            {
                return Activator.CreateInstance(type);
            }

            using (MemoryStream ms = new MemoryStream(data))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                return formatter.Deserialize(ms);
            }
        }
    }
    
}