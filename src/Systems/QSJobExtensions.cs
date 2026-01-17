using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs
{
    public static class QSJobExtensions
    {
        // public static Systems AddJob<TSystem>(this Systems systems) where TSystem : struct, IMoveSystemStaticSystemJob
        // {
        //     TSystem system = default;
        //
        //     var runner = new IMoveSystemStaticQuerySystemJobRunner<TSystem>
        //     {
        //         System = system,
        //         Mode = SystemMode.Parallel,
        //         EcbJob = default,
        //         Query = systems.World.UnsafeWorldRef.GetSystemParam2<Query<Transform, Input>.WithEntity>()
        //     };
        //     systems.runners.Add(runner);
        //     return systems;
        // }
    }
}