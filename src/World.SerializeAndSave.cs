namespace Wargon.Nukecs
{
    public unsafe partial struct World
    {
        public byte[] Serialize()
        {
            return UnsafeWorld->AllocatorHandle.AllocatorWrapper.MemoryAllocator.Serialize();
        }

        public void Deserialize(byte[] data)
        {
            UnsafeWorld->AllocatorHandle.AllocatorWrapper.MemoryAllocator.Deserialize(data);
        }

        public void SaveToFile(string path)
        {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandle.AllocatorWrapper.MemoryAllocator.SaveToFile(path);
        }

        public void LoadFromFile(string path)
        {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandle.AllocatorWrapper.MemoryAllocator.LoadFromFile(path);
        }
    }
}