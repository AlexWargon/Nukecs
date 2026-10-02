using System;
using NUnit.Framework;
using UnityEngine;

namespace Wargon.Nukecs.Tests
{
    public class LoadPrefabSource : ScriptableObject, ICustomConvertor
    {
        public int Conversions;
        public void Convert(ref World world, ref Entity entity)
        {
            Conversions++;
            entity.Add(new PositionTest { X = 91 });
        }
    }
    [TestFixture]
    public class PrefabMapLoadTests
    {
        private LoadPrefabSource source;
        [SetUp] public void SetUp()
        {
            World.DisposeStatic();
            source = ScriptableObject.CreateInstance<LoadPrefabSource>();
            source.name = "LoadPrefabSource";
        }
        [TearDown] public void TearDown()
        {
            World.DisposeStatic();
            UnityEngine.Object.DestroyImmediate(source);
        }
        [Test]
        public void Reload_RebindsCachedPrefabWithoutReconversion()
        {
            var world = World.Create(WorldConfig.Default256);
            var savedPrefab = EntityPrefabMap.GetOrCreatePrefab(source, ref world);
            var saved = world.Serialize();
            savedPrefab.DestroyNow();
            var replacement = EntityPrefabMap.GetOrCreatePrefab(source, ref world);
            Assert.AreNotEqual(savedPrefab, replacement);
            world.Deserialize(saved);
            Assert.IsFalse(replacement.IsValid());
            var restored = EntityPrefabMap.GetOrCreatePrefab(source, ref world);
            Assert.AreEqual(savedPrefab, restored);
            Assert.AreEqual(2, source.Conversions);
            Assert.AreEqual(restored, EntityPrefabMap.GetPrefab(source.GetInstanceID()));
            var spawned = EntityPrefabMap.Spawn(source.GetInstanceID());
            Assert.AreEqual(91, spawned.Get<PositionTest>().X);
        }
        [Test]
        public void RestartAndLoad_RebindsStartupPrefabWithoutReconversion()
        {
            var world = World.Create(WorldConfig.Default256);
            var savedPrefab = EntityPrefabMap.GetOrCreatePrefab(source, ref world);
            var saved = world.Serialize();
            World.DisposeStatic();
            world = World.Create(WorldConfig.Default16);
            var startup = EntityPrefabMap.GetOrCreatePrefab(source, ref world);
            world.Deserialize(saved);
            Assert.IsFalse(startup.IsValid());
            var restored = EntityPrefabMap.GetOrCreatePrefab(source, ref world);
            Assert.AreEqual(savedPrefab, restored);
            Assert.AreEqual(2, source.Conversions);
            Assert.AreEqual(1, world.Query(false).With<IsPrefab>().With<Name>().Count);
        }
        [Test]
        public void SameSourceInTwoWorlds_DoesNotReturnOtherWorldsPrefab()
        {
            var first = World.Create(WorldConfig.Default256);
            var second = World.Create(WorldConfig.Default256);
            var a = EntityPrefabMap.GetOrCreatePrefab(source, ref first);
            var b = EntityPrefabMap.GetOrCreatePrefab(source, ref second);
            Assert.AreNotEqual(a, b);
            Assert.AreEqual(a, EntityPrefabMap.GetOrCreatePrefab(source, ref first));
            Assert.AreEqual(2, source.Conversions);
        }
        [Test]
        public void AmbiguousLoadedNames_AreRejectedInsteadOfSelectingAnEntity()
        {
            var world = World.Create(WorldConfig.Default256);
            EntityPrefabMap.GetOrCreatePrefab(source, ref world);
            var duplicate = world.Entity();
            duplicate.Add(new IsPrefab());
            duplicate.Add(new Name(source.name));
            world.Update();
            EntityPrefabMap.Dispose();
            Assert.Throws<InvalidOperationException>(() => EntityPrefabMap.GetOrCreatePrefab(source, ref world));
        }
    }
}
