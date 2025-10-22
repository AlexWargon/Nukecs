using System.Runtime.CompilerServices;

namespace Wargon.Nukecs
{
    public static unsafe class EntityAspectExtensions{
        

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T GetAspect<T>(this ref Entity entity) where T : unmanaged, IAspect<T>, IAspect
        {
            var aspect = entity.worldPointer->GetAspect<T>();
            aspect->Update(ref entity);
            return ref *aspect;
        }

        // public static EntityData GetData(this ref Entity entity)
        // {
        //     return entity.ArchetypeRef.GetEntityData(entity);
        // }

        public static void SetData(this ref Entity entity, EntityData data)
        {
            entity.ArchetypeRef.SetEntityData(data);
        }
    }
}