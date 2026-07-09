using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct GraphPosition : IComponent
    {
        public float X;
        public float Y;
    }

    public struct GraphVelocity : IComponent
    {
        public float X;
        public float Y;
    }

    public struct GraphHealth : IComponent
    {
        public float Value;
    }

    public struct GraphDamage : IComponent
    {
        public float Amount;
    }

    public struct GraphScore : IComponent
    {
        public float Value;
    }

    public struct GraphTagA : IComponent
    {
        public byte _;
    }

    public struct GraphTagB : IComponent
    {
        public byte _;
    }

    public struct GraphMarkerC : IComponent
    {
        public byte _;
    }

    public struct GraphMarkerD : IComponent
    {
        public byte _;
    }

    public struct GraphExtra : IComponent
    {
        public float Value;
    }

    public static class GraphDependencySystems
    {
        [System]
        public static void MovementSystem(
            ref Query<GraphPosition, GraphVelocity> query, ref State state)
        {
            foreach (var (pos, vel) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
            }
        }

        [System]
        public static void DamageSystem(
            ref Query<GraphDamage, GraphHealth> query)
        {
            foreach (var (dmg, hp) in query)
            {
                hp.Get.Value -= dmg.Read.Amount;
            }
        }

        [System]
        public static void ScoreSystem(
            ref Query<Entity, GraphScore> query)
        {
            foreach (var (e, score) in query)
            {
                score.Get.Value += 1f;
            }
        }

        [System]
        public static void IndependentSystemA(
            ref Query<GraphTagA> query)
        {
            foreach (var tag in query)
            {
            }
        }

        [System]
        public static void IndependentSystemB(
            ref Query<GraphTagB> query)
        {
            foreach (var tag in query)
            {
            }
        }

        [System]
        public static void ReadPositionSystem(
            ref Query<Entity, GraphPosition> query)
        {
            foreach (var (e, pos) in query)
            {
                var x = pos.Read.X;
            }
        }

        [System]
        public static void AddExtraToTagA(
            ref Query<Entity, GraphTagA> query)
        {
            foreach (var (e, _) in query)
            {
                e.Add(new GraphExtra { Value = 1f });
            }
        }

        [System]
        public static void AddExtraToTagB(
            ref Query<Entity, GraphTagB> query)
        {
            foreach (var (e, _) in query)
            {
                e.Add(new GraphExtra { Value = 2f });
            }
        }

        [System]
        public static void ModifyExtraOnC(
            ref Query<GraphMarkerC, GraphExtra> query)
        {
            foreach (var (c, extra) in query)
            {
                extra.Get.Value += 10f;
            }
        }

        [System]
        public static void ModifyExtraOnD(
            ref Query<GraphMarkerD, GraphExtra> query)
        {
            foreach (var (d, extra) in query)
            {
                extra.Get.Value += 20f;
            }
        }
    }

    [TestFixture]
    public class DependencyGraphTests
    {
        [SetUp]
        public void SetUp()
        {
            World.DisposeStatic();
        }

        [TearDown]
        public void TearDown()
        {
            World.DisposeStatic();
        }

        [Test]
        public void DependencyGraph_DependentSystems_ExecuteInOrder()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.DamageSystem, Threads.Main);
            systems.Add(GraphDependencySystems.ReadPositionSystem, Threads.Main);

            var arch = world.GetArchetype(typeof(GraphDamage), typeof(GraphHealth), typeof(GraphPosition));
            var entity = arch.CreateEntity();
            entity.Get<GraphDamage>().Amount = 10f;
            entity.Get<GraphHealth>().Value = 100f;
            entity.Get<GraphPosition>().X = 5f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(90f, entity.Get<GraphHealth>().Value, "Health reduced by damage");
            Assert.AreEqual(5f, entity.Get<GraphPosition>().X, "Position unchanged (read only)");

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_IndependentSystems_CanRunInParallel()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.IndependentSystemA, Threads.Parallel);
            systems.Add(GraphDependencySystems.IndependentSystemB, Threads.Parallel);

            var queryA = world.Query().With<GraphTagA>();
            var queryB = world.Query().With<GraphTagB>();

            var archA = world.GetArchetype(typeof(GraphTagA));
            var archB = world.GetArchetype(typeof(GraphTagB));
            const int count = 100;
            archA.BatchCreateEntity(count);
            archB.BatchCreateEntity(count);
            world.Update();

            Assert.AreEqual(count, queryA.Count, "All TagA entities exist before systems");
            Assert.AreEqual(count, queryB.Count, "All TagB entities exist before systems");

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(count, queryA.Count, "All TagA entities exist after systems");
            Assert.AreEqual(count, queryB.Count, "All TagB entities exist after systems");

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_SameComponentConflict_Serializes()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.DamageSystem, Threads.Parallel);
            systems.Add(GraphDependencySystems.MovementSystem, Threads.Parallel);

            var arch = world.GetArchetype(
                typeof(GraphDamage), typeof(GraphHealth),
                typeof(GraphPosition), typeof(GraphVelocity));
            var entity = arch.CreateEntity();
            entity.Get<GraphDamage>().Amount = 5f;
            entity.Get<GraphHealth>().Value = 50f;
            entity.Get<GraphVelocity>().X = 10f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(45f, entity.Get<GraphHealth>().Value, "Damage applied");
            Assert.AreEqual(10f, entity.Get<GraphPosition>().X, "Position updated by velocity");

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_BackwardCompatible_SequentialExecution()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);

            systems.Add(ChainSystems.AccelerationToVelocity, Threads.Main);
            systems.Add(ChainSystems.VelocityToPosition, Threads.Main);
            systems.Add(ChainSystems.PositionToDistance, Threads.Main);

            var arch = world.GetArchetype(
                typeof(ChainAcceleration), typeof(ChainVelocity),
                typeof(ChainPosition), typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 10f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(10f, entity.Get<ChainVelocity>().X);
            Assert.AreEqual(10f, entity.Get<ChainPosition>().X);
            Assert.AreEqual(200f, entity.Get<ChainDistance>().Value);

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_GraphRebuiltOnSystemAdd()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.MovementSystem, Threads.Main);
            world.Update();

            systems.OnUpdate(1f, 1f);

            systems.Add(GraphDependencySystems.DamageSystem, Threads.Main);
            systems.OnUpdate(1f, 2f);

            var arch = world.GetArchetype(typeof(GraphPosition), typeof(GraphVelocity));
            var entity = arch.CreateEntity();
            entity.Get<GraphVelocity>().X = 5f;
            world.Update();

            systems.OnUpdate(1f, 3f);

            Assert.AreEqual(5f, entity.Get<GraphPosition>().X);

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_WriteWriteConflict_CreatesDependency()
        {
            var graph = new SystemDependencyGraph();
            var nodes = new SystemNode[]
            {
                new SystemNode { Index = 0, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(1, SystemAccessMode.Write) } } },
                new SystemNode { Index = 1, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(1, SystemAccessMode.Write) } } },
            };
            graph.Build(nodes);

            Assert.IsFalse(graph.HasCyclicDependency(), "No actual cycle - just a dependency");

            var groups = graph.GetExecutionGroups();
            Assert.IsNotNull(groups);
            Assert.AreEqual(2, groups.Length, "Conflicting systems in separate groups");
        }

        [Test]
        public void DependencyGraph_NoConflict_ParallelGroups()
        {
            var graph = new SystemDependencyGraph();
            var nodes = new SystemNode[]
            {
                new SystemNode { Index = 0, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(1, SystemAccessMode.Read) } } },
                new SystemNode { Index = 1, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(2, SystemAccessMode.Read) } } },
                new SystemNode { Index = 2, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(3, SystemAccessMode.Write) } } },
            };
            graph.Build(nodes);

            Assert.IsFalse(graph.HasCyclicDependency(), "No cyclic dependency");

            var groups = graph.GetExecutionGroups();
            Assert.AreEqual(1, groups.Length, "All independent systems in one group");
            Assert.AreEqual(3, groups[0].Length, "All three systems in the group");
        }

        [Test]
        public void DependencyGraph_ECBConflict_SeparatesIntoGroups()
        {
            var graph = new SystemDependencyGraph();
            var nodes = new SystemNode[]
            {
                new SystemNode { Index = 0, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(1, SystemAccessMode.Write) }, UsesECB = true } },
                new SystemNode { Index = 1, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(2, SystemAccessMode.Write) }, UsesECB = true } },
            };
            graph.Build(nodes);

            var groups = graph.GetExecutionGroups();
            Assert.AreEqual(1, groups.Length, "ECB systems with different components can run in parallel");
        }

        [Test]
        public void DependencyGraph_ECBConflict_NoComponentInfo()
        {
            var graph = new SystemDependencyGraph();
            var nodes = new SystemNode[]
            {
                new SystemNode { Index = 0, Info = new SystemDependencyInfo { UsesECB = true } },
                new SystemNode { Index = 1, Info = new SystemDependencyInfo { Components = new[] { new ComponentAccess(1, SystemAccessMode.Write) }, UsesECB = true } },
            };
            graph.Build(nodes);

            var groups = graph.GetExecutionGroups();
            Assert.AreEqual(2, groups.Length, "ECB system with no component info serialized against other ECB systems");
        }

        [Test]
        public void DependencyGraph_ParallelECBSystems_NoCrash()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.AddExtraToTagA, Threads.Parallel);
            systems.Add(GraphDependencySystems.AddExtraToTagB, Threads.Parallel);
            var queryExtra = world.Query().With<GraphExtra>();
            var archA = world.GetArchetype(typeof(GraphTagA));
            var archB = world.GetArchetype(typeof(GraphTagB));
            const int count = 50;
            archA.BatchCreateEntity(count);
            archB.BatchCreateEntity(count);
            world.Update();

            systems.OnUpdate(1f, 1f);


            Assert.AreEqual(count * 2, queryExtra.Count, "Extras added by both ECB systems");

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_ParallelECBSystems_CorrectValues()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.ModifyExtraOnC, Threads.Parallel);
            systems.Add(GraphDependencySystems.ModifyExtraOnD, Threads.Parallel);

            var archC = world.GetArchetype(typeof(GraphMarkerC), typeof(GraphExtra));
            var archD = world.GetArchetype(typeof(GraphMarkerD), typeof(GraphExtra));
            var eC = archC.CreateEntity();
            var eD = archD.CreateEntity();
            eC.Get<GraphExtra>().Value = 0f;
            eD.Get<GraphExtra>().Value = 0f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(10f, eC.Get<GraphExtra>().Value, "C system added 10");
            Assert.AreEqual(20f, eD.Get<GraphExtra>().Value, "D system added 20");

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_MixedMainAndParallelECB_NoCrash()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.DamageSystem, Threads.Main);
            systems.Add(GraphDependencySystems.AddExtraToTagA, Threads.Parallel);
            systems.Add(GraphDependencySystems.AddExtraToTagB, Threads.Parallel);
            var queryExtra = world.Query().With<GraphExtra>();
            var archA = world.GetArchetype(typeof(GraphDamage), typeof(GraphHealth), typeof(GraphTagA));
            var archB = world.GetArchetype(typeof(GraphDamage), typeof(GraphHealth), typeof(GraphTagB));
            const int count = 20;
            archA.BatchCreateEntity(count);
            archB.BatchCreateEntity(count);
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(count * 2, queryExtra.Count, "Extras added after main + parallel systems");

            world.Dispose();
        }

        [Test]
        public void DependencyGraph_ParallelECB_StressTest()
        {
            var world = World.Create(WorldConfig.Default6144);
            var systems = new Systems(ref world);
            systems.UseDependencyGraph();

            systems.Add(GraphDependencySystems.AddExtraToTagA, Threads.Parallel);
            systems.Add(GraphDependencySystems.AddExtraToTagB, Threads.Parallel);
            systems.Add(GraphDependencySystems.ModifyExtraOnC, Threads.Parallel);
            systems.Add(GraphDependencySystems.ModifyExtraOnD, Threads.Parallel);

            var archA = world.GetArchetype(typeof(GraphTagA));
            var archB = world.GetArchetype(typeof(GraphTagB));
            var archC = world.GetArchetype(typeof(GraphMarkerC), typeof(GraphExtra));
            var archD = world.GetArchetype(typeof(GraphMarkerD), typeof(GraphExtra));
            const int count = 1000;
            archA.BatchCreateEntity(count);
            archB.BatchCreateEntity(count);
            archC.BatchCreateEntity(count);
            archD.BatchCreateEntity(count);
            world.Update();

            for (int i = 0; i < 10; i++)
            {
                systems.OnUpdate(1f, i);
            }

            world.Dispose();
        }
    }
}
