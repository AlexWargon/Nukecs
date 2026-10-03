using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct Qi4A : IComponent { public int Value; }
    public struct Qi4B : IComponent { public int Value; }
    public struct Qi4C : IComponent { public int Value; }
    public struct Qi4D : IComponent { public int Value; }
    public struct Qi4Tag : IComponent { }
    public struct Qi4Pool : IPoolComponent { public int Value; }

    public struct Qi4Result : IRes
    {
        public int Count;
        public int Sum;
        public void OnCreate(ref World world) { }
        public void OnUpdate(ref World world) { }
    }

    public static class QueryIter4Systems
    {
        [System]
        public static void Dense(ref Query<Qi4A, Qi4B, Qi4C, Qi4D> query, ref Res<Qi4Result> result)
        {
            foreach (var (a, b, c, d) in query.iter())
            {
                a.Get.Value += b.Read.Value;
                c.Get.Value += d.Read.Value;
                result.Ref.Count++;
            }
        }

        [System]
        public static void NoneTag(
            ref Query<Qi4A, Qi4B, Qi4C, None<Qi4Tag>> query,
            ref Res<Qi4Result> result)
        {
            foreach (var (a, b, c, _) in query.iter())
            {
                result.Ref.Count++;
                result.Ref.Sum += a.Read.Value + b.Read.Value + c.Read.Value;
            }
        }

        [System]
        public static void WithPool(
            ref Query<Qi4A, Qi4B, Qi4C, Qi4Pool> query,
            ref Res<Qi4Result> result)
        {
            foreach (var (a, b, c, pool) in query.iter())
            {
                result.Ref.Count++;
                result.Ref.Sum += a.Read.Value + b.Read.Value + c.Read.Value + pool.Read.Value;
            }
        }

        [System]
        public static void WithTag(
            ref Query<Qi4A, Qi4B, Qi4C, Qi4Tag> query,
            ref Res<Qi4Result> result)
        {
            foreach (var (a, b, c, _) in query.iter())
            {
                result.Ref.Count++;
                result.Ref.Sum += a.Read.Value + b.Read.Value + c.Read.Value;
            }
        }

    }

    [TestFixture]
    public class QueryIter4Tests
    {
        [Test]
        public void DenseInline_IteratesAndMutatesEveryRow()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new Qi4Result());
            var systems = new Systems(ref world).Add(QueryIter4Systems.Dense, Threads.Main);

            var first = Create(ref world,
                new Qi4A { Value = 1 }, new Qi4B { Value = 10 },
                new Qi4C { Value = 100 }, new Qi4D { Value = 1000 });
            var second = Create(ref world,
                new Qi4A { Value = 2 }, new Qi4B { Value = 20 },
                new Qi4C { Value = 200 }, new Qi4D { Value = 2000 });
            second.Add<Qi4Tag>(); // same inline storage, different logical archetype
            world.Update();

            systems.OnUpdate(0.016f, 0f);

            Assert.AreEqual(2, new Res<Qi4Result>().Ref.Count);
            Assert.AreEqual(11, first.Get<Qi4A>().Value);
            Assert.AreEqual(1100, first.Get<Qi4C>().Value);
            Assert.AreEqual(22, second.Get<Qi4A>().Value);
            Assert.AreEqual(2200, second.Get<Qi4C>().Value);
            world.Dispose();
        }

        [Test]
        public void NoneTag_SparseRows_UsesCorrectPhysicalRows()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new Qi4Result());
            var systems = new Systems(ref world).Add(QueryIter4Systems.NoneTag, Threads.Main);

            Create(ref world, new Qi4A { Value = 1 }, new Qi4B { Value = 10 }, new Qi4C { Value = 100 });
            var excluded = Create(ref world, new Qi4A { Value = 2 }, new Qi4B { Value = 20 }, new Qi4C { Value = 200 });
            excluded.Add<Qi4Tag>();
            Create(ref world, new Qi4A { Value = 3 }, new Qi4B { Value = 30 }, new Qi4C { Value = 300 });
            world.Update();

            systems.OnUpdate(0.016f, 0f);

            Assert.AreEqual(2, new Res<Qi4Result>().Ref.Count);
            Assert.AreEqual(444, new Res<Qi4Result>().Ref.Sum);
            world.Dispose();
        }

        [Test]
        public void PoolAndTagSlots_StayOnGeneralPath()
        {
            var world = World.Create(WorldConfig.Default256);
            world.AddRes(new Qi4Result());
            var poolSystems = new Systems(ref world).Add(QueryIter4Systems.WithPool, Threads.Main);
            var tagSystems = new Systems(ref world).Add(QueryIter4Systems.WithTag, Threads.Main);

            var tagged = Create(ref world,
                new Qi4A { Value = 1 }, new Qi4B { Value = 10 }, new Qi4C { Value = 100 },
                new Qi4Pool { Value = 1000 });
            tagged.Add<Qi4Tag>();
            Create(ref world,
                new Qi4A { Value = 2 }, new Qi4B { Value = 20 }, new Qi4C { Value = 200 },
                new Qi4Pool { Value = 2000 });
            world.Update();

            poolSystems.OnUpdate(0.016f, 0f);
            Assert.AreEqual(2, new Res<Qi4Result>().Ref.Count);
            Assert.AreEqual(3333, new Res<Qi4Result>().Ref.Sum);

            new Res<Qi4Result>().Ref = default;
            tagSystems.OnUpdate(0.016f, 0f);
            Assert.AreEqual(1, new Res<Qi4Result>().Ref.Count);
            Assert.AreEqual(111, new Res<Qi4Result>().Ref.Sum);
            world.Dispose();
        }

        private static Entity Create(ref World world, Qi4A a, Qi4B b, Qi4C c)
        {
            var entity = world.Entity();
            entity.Add(a);
            entity.Add(b);
            entity.Add(c);
            return entity;
        }

        private static Entity Create(ref World world, Qi4A a, Qi4B b, Qi4C c, Qi4D d)
        {
            var entity = Create(ref world, a, b, c);
            entity.Add(d);
            return entity;
        }

        private static Entity Create(ref World world, Qi4A a, Qi4B b, Qi4C c, Qi4Pool pool)
        {
            var entity = Create(ref world, a, b, c);
            entity.Add(pool);
            return entity;
        }
    }
}
