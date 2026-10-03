using System;

namespace Wargon.Nukecs
{
    [Flags]
    public enum SystemAccessMode : byte
    {
        None = 0,
        Read = 1,
        Write = 2,
        ReadWrite = Read | Write
    }

    public struct ComponentAccess
    {
        public int ComponentTypeIndex;
        public SystemAccessMode Mode;

        public ComponentAccess(int componentTypeIndex, SystemAccessMode mode)
        {
            ComponentTypeIndex = componentTypeIndex;
            Mode = mode;
        }
    }

    public struct SystemDependencyInfo
    {
        public string SystemName;
        public ComponentAccess[] Components;
        public int[] ReadResources;
        public int[] WriteResources;
        public int[] ReadEvents;
        public int[] WriteEvents;
        public bool UsesECB;

        public static SystemDependencyInfo Empty => new SystemDependencyInfo
        {
            SystemName = "",
            Components = Array.Empty<ComponentAccess>(),
            ReadResources = Array.Empty<int>(),
            WriteResources = Array.Empty<int>(),
            ReadEvents = Array.Empty<int>(),
            WriteEvents = Array.Empty<int>(),
            UsesECB = false
        };

        public bool HasConflict(in SystemDependencyInfo other)
        {
            if (Components != null && other.Components != null)
            {
                for (int i = 0; i < Components.Length; i++)
                {
                    for (int j = 0; j < other.Components.Length; j++)
                    {
                        if (Components[i].ComponentTypeIndex == other.Components[j].ComponentTypeIndex)
                        {
                            var a = Components[i].Mode;
                            var b = other.Components[j].Mode;
                            if ((a & SystemAccessMode.Write) != 0 || (b & SystemAccessMode.Write) != 0)
                                return true;
                        }
                    }
                }
            }

            if (UsesECB && other.UsesECB)
            {
                var aHasComps = Components != null && Components.Length > 0;
                var bHasComps = other.Components != null && other.Components.Length > 0;
                if (!aHasComps || !bHasComps)
                    return true;
            }

            if (ReadResources != null && other.WriteResources != null)
            {
                for (int i = 0; i < ReadResources.Length; i++)
                {
                    for (int j = 0; j < other.WriteResources.Length; j++)
                    {
                        if (ReadResources[i] == other.WriteResources[j])
                            return true;
                    }
                }
            }

            if (WriteResources != null && other.ReadResources != null)
            {
                for (int i = 0; i < WriteResources.Length; i++)
                {
                    for (int j = 0; j < other.ReadResources.Length; j++)
                    {
                        if (WriteResources[i] == other.ReadResources[j])
                            return true;
                    }
                }
            }

            if (WriteResources != null && other.WriteResources != null)
            {
                for (int i = 0; i < WriteResources.Length; i++)
                {
                    for (int j = 0; j < other.WriteResources.Length; j++)
                    {
                        if (WriteResources[i] == other.WriteResources[j])
                            return true;
                    }
                }
            }

            if (ReadEvents != null && other.WriteEvents != null)
            {
                for (int i = 0; i < ReadEvents.Length; i++)
                {
                    for (int j = 0; j < other.WriteEvents.Length; j++)
                    {
                        if (ReadEvents[i] == other.WriteEvents[j])
                            return true;
                    }
                }
            }

            if (WriteEvents != null && other.ReadEvents != null)
            {
                for (int i = 0; i < WriteEvents.Length; i++)
                {
                    for (int j = 0; j < other.ReadEvents.Length; j++)
                    {
                        if (WriteEvents[i] == other.ReadEvents[j])
                            return true;
                    }
                }
            }

            if (WriteEvents != null && other.WriteEvents != null)
            {
                for (int i = 0; i < WriteEvents.Length; i++)
                {
                    for (int j = 0; j < other.WriteEvents.Length; j++)
                    {
                        if (WriteEvents[i] == other.WriteEvents[j])
                            return true;
                    }
                }
            }

            return false;
        }
    }
}
