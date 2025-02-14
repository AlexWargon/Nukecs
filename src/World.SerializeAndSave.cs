﻿namespace Wargon.Nukecs
{
    public unsafe partial  struct World
    {
        public byte[] Serialize()
        {
            return UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.Serialize();
        }

        public void Deserialize(byte[] data)
        {
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.Deserialize(data);
        }

        public void SaveToFile(string path)
        {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.SaveToFile(path);
        }

        public void LoadFromFile(string path)
        {
            UnsafeWorld->systemsUpdateJobDependencies.Complete();
            UnsafeWorld->AllocatorHandler.AllocatorWrapper.Allocator.LoadFromFile(path);
            for (var index = 0; index < UnsafeWorld->entities.Length; index++)
            {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
        }

        private void UpdateEtntitiesWorld()
        {
            
            for (var index = 0; index < UnsafeWorld->entities.Length; index++)
            {
                ref var entity = ref UnsafeWorld->entities.Ptr[index];
                entity.worldPointer = UnsafeWorld;
            }
            Update();
        }
    }
    public partial struct World
    {
        public async void LoadFromFileAsync(string path)
        {
            UnsafeWorldRef.systemsUpdateJobDependencies.Complete();
            await UnsafeWorldRef.AllocatorHandler.AllocatorWrapper.Allocator.LoadFromFileAsync(path);
            UpdateEtntitiesWorld();
        }

        public async void SaveToFileAsync(string path)
        {
            UnsafeWorldRef.systemsUpdateJobDependencies.Complete();
            await UnsafeWorldRef.AllocatorHandler.AllocatorWrapper.Allocator.SaveToFileAsync(path);
            
        }
    }
}