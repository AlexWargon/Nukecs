using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    [TestFixture]
    public class AsyncLoadRelocationRegressionTests
    {
        [Test]
        public async Task LoadAsync_RelocatedArena_ReturnsUsableWorld()
        {
            var path = Path.Combine(Path.GetTempPath(), "nukecs-relocation-" + Guid.NewGuid() + ".dat");
            var source = World.Create(WorldConfig.Default1024);
            var target = default(World);
            var targetId = -1;
            try
            {
                var entity = source.Entity(new Stab1 { V = 42 });
                source.Update();
                var entityId = entity.id;
                await source.SaveToFileAsync(path);
                source.Dispose();
                target = World.Create(WorldConfig.Default16);
                targetId = target.Id;
                target = await World.LoadAsync(path, target);
                AssertCurrentPointer(target, targetId);
                Assert.AreEqual(42, target.GetEntity(entityId).Get<Stab1>().V);
                target.GetEntity(entityId).Add(new Stab2 { V = 17 });
                target.Update();
                Assert.AreEqual(17, target.GetEntity(entityId).Get<Stab2>().V);
                target.Dispose();
                targetId = -1;
            }
            finally
            {
                // Never dereference the caller's stale arena if the assertion fails.
                if (targetId >= 0 && World.Get(targetId).IsAlive) World.Get(targetId).Dispose();
                if (source.IsAlive) source.Dispose();
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static unsafe void AssertCurrentPointer(World world, int id)
        {
            Assert.IsTrue(world.unsafeWorldPtr.cached == World.Get(id).unsafeWorldPtr.cached,
                "Async load must give the caller the relocated world before it accesses or disposes the arena.");
        }
    }
}