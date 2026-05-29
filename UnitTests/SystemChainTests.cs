using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct ChainAcceleration : IComponent
    {
        public float Value;
    }

    public struct ChainVelocity : IComponent
    {
        public float X;
        public float Y;
    }

    public struct ChainPosition : IComponent
    {
        public float X;
        public float Y;
    }

    public struct ChainDistance : IComponent
    {
        public float Value;
    }

    public struct ChainDamageEvent : IComponent
    {
        public int Amount;
    }

    public struct ChainHealth : IComponent
    {
        public int Value;
    }

    public struct ChainDeadFlag : IComponent
    {
        public bool IsDead;
    }

    public struct ChainBaseScore : IComponent
    {
        public int Value;
    }

    public struct ChainMultiplier : IComponent
    {
        public float Value;
    }

    public struct ChainFinalScore : IComponent
    {
        public int Value;
    }

    public struct ChainRank : IComponent
    {
        public int Value;
    }

    public struct ChainInput : IComponent
    {
        public float MoveX;
        public float MoveY;
    }

    public struct ChainSpeed : IComponent
    {
        public float Value;
    }

    public struct ChainAirborneTag : IComponent
    {
        public byte _;
    }

    public static class ChainSystems
    {
        [System]
        public static void AccelerationToVelocity(
            ref Query<ChainAcceleration, ChainVelocity> query, ref State state)
        {
            foreach (var (acc, vel) in query)
            {
                vel.Get.X += acc.Read.Value * state.Time.DeltaTime;
                vel.Get.Y += acc.Read.Value * state.Time.DeltaTime;
            }
        }

        [System]
        public static void VelocityToPosition(
            ref Query<ChainVelocity, ChainPosition> query, ref State state)
        {
            foreach (var (vel, pos) in query)
            {
                pos.Get.X += vel.Read.X * state.Time.DeltaTime;
                pos.Get.Y += vel.Read.Y * state.Time.DeltaTime;
            }
        }

        [System]
        public static void PositionToDistance(
            ref Query<ChainPosition, ChainDistance> query)
        {
            foreach (var (pos, dist) in query)
            {
                var x = pos.Read.X;
                var y = pos.Read.Y;
                dist.Get.Value = x * x + y * y;
            }
        }

        [System]
        public static void ApplyDamage(
            ref Query<ChainDamageEvent, ChainHealth> query)
        {
            foreach (var (dmg, hp) in query)
            {
                hp.Get.Value -= dmg.Read.Amount;
            }
        }

        [System]
        public static void CheckDeath(
            ref Query<ChainHealth, ChainDeadFlag> query)
        {
            foreach (var (hp, dead) in query)
            {
                if (hp.Read.Value <= 0)
                    dead.Get.IsDead = true;
            }
        }

        [System]
        public static void ClampDeadHealth(
            ref Query<ChainHealth, ChainDeadFlag> query)
        {
            foreach (var (hp, dead) in query)
            {
                if (dead.Read.IsDead && hp.Read.Value < 0)
                    hp.Get.Value = 0;
            }
        }

        [System]
        public static void ApplyMultiplier(
            ref Query<ChainBaseScore, ChainMultiplier> query)
        {
            foreach (var (baseScore, mult) in query)
            {
                baseScore.Get.Value = (int)(baseScore.Read.Value * mult.Read.Value);
            }
        }

        [System]
        public static void ApplyBonus(
            ref Query<ChainBaseScore, ChainFinalScore> query)
        {
            foreach (var (baseScore, final) in query)
            {
                final.Get.Value = baseScore.Read.Value;
                if (final.Read.Value > 100)
                    final.Get.Value += 50;
            }
        }

        [System]
        public static void AssignRank(
            ref Query<ChainFinalScore, ChainRank> query)
        {
            foreach (var (score, rank) in query)
            {
                var v = score.Read.Value;
                if (v >= 300) rank.Get.Value = 3;
                else if (v >= 150) rank.Get.Value = 2;
                else rank.Get.Value = 1;
            }
        }

        [System]
        public static void InputToVelocity(
            ref Query<ChainInput, ChainSpeed, ChainVelocity, None<ChainAirborneTag>> query)
        {
            foreach (var (inp, spd, vel, _) in query)
            {
                vel.Get.X = inp.Read.MoveX * spd.Read.Value;
                vel.Get.Y = inp.Read.MoveY * spd.Read.Value;
            }
        }
    }

    [TestFixture]
    public class SystemChainTests
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
        public void Chain_AccelerationVelocityPositionDistance_Main()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.AccelerationToVelocity, Threads.Main);
            systems.Add(ChainSystems.VelocityToPosition, Threads.Main);
            systems.Add(ChainSystems.PositionToDistance, Threads.Main);
            var arch = world.GetArchetype(
                typeof(ChainAcceleration),
                typeof(ChainVelocity),
                typeof(ChainPosition),
                typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 10;

            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(10f, entity.Get<ChainVelocity>().X, "S1: vel X = acc*dt = 10*1");
            Assert.AreEqual(10f, entity.Get<ChainVelocity>().Y, "S1: vel Y");
            Assert.AreEqual(10f, entity.Get<ChainPosition>().X, "S2: pos X = vel*dt = 10*1");
            Assert.AreEqual(10f, entity.Get<ChainPosition>().Y, "S2: pos Y");
            Assert.AreEqual(200f, entity.Get<ChainDistance>().Value, "S3: dist = 10*10+10*10");

            systems.OnUpdate(1f, 2f);

            Assert.AreEqual(20f, entity.Get<ChainVelocity>().X, "Tick2: vel X = 10+10");
            Assert.AreEqual(20f, entity.Get<ChainVelocity>().Y, "Tick2: vel Y");
            Assert.AreEqual(30f, entity.Get<ChainPosition>().X, "Tick2: pos X = 10+20");
            Assert.AreEqual(30f, entity.Get<ChainPosition>().Y, "Tick2: pos Y");
            Assert.AreEqual(1800f, entity.Get<ChainDistance>().Value, "Tick2: dist = 30*30+30*30");

            world.Dispose();
        }

        [Test]
        public void Chain_AccelerationVelocityPositionDistance_Single()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.AccelerationToVelocity, Threads.Single);
            systems.Add(ChainSystems.VelocityToPosition, Threads.Single);
            systems.Add(ChainSystems.PositionToDistance, Threads.Single);
            var arch = world.GetArchetype(
                typeof(ChainAcceleration),
                typeof(ChainVelocity),
                typeof(ChainPosition),
                typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 5;

            world.Update();

            systems.OnUpdate(2f, 2f);

            Assert.AreEqual(10f, entity.Get<ChainVelocity>().X, "S1: vel X = 5*2");
            Assert.AreEqual(10f, entity.Get<ChainVelocity>().Y, "S1: vel Y");
            Assert.AreEqual(20f, entity.Get<ChainPosition>().X, "S2: pos X = 10*2");
            Assert.AreEqual(20f, entity.Get<ChainPosition>().Y, "S2: pos Y");
            Assert.AreEqual(800f, entity.Get<ChainDistance>().Value, "S3: dist = 20*20+20*20");

            world.Dispose();
        }

        [Test]
        public void Chain_AccelerationVelocityPositionDistance_Parallel()
        {
            var world = World.Create(WorldConfig.Default1024);
            var query = world.Query().With<ChainDistance>();
            var systems = new Systems(ref world);
            
            systems.Add(ChainSystems.AccelerationToVelocity);
            systems.Add(ChainSystems.VelocityToPosition);
            systems.Add(ChainSystems.PositionToDistance);
            var arch = world.GetArchetype(
                typeof(ChainAcceleration),
                typeof(ChainVelocity),
                typeof(ChainPosition),
                typeof(ChainDistance));
            const int count = 500;
            var entities = arch.BatchCreateEntity(count);
            
            for (int i = 0; i < count; i++)
            {
                entities[i].Set(new ChainAcceleration { Value = 3f });
            }
            world.Update();

            systems.OnUpdate(1f, 1f);

            int checkedCount = 0;
            foreach (ref var e in query)
            {
                Assert.AreEqual(3f, e.Get<ChainVelocity>().X, "Parallel: vel X");
                Assert.AreEqual(3f, e.Get<ChainVelocity>().Y, "Parallel: vel Y");
                Assert.AreEqual(3f, e.Get<ChainPosition>().X, "Parallel: pos X");
                Assert.AreEqual(3f, e.Get<ChainPosition>().Y, "Parallel: pos Y");
                Assert.AreEqual(18f, e.Get<ChainDistance>().Value, "Parallel: dist = 3*3+3*3");
                checkedCount++;
            }
            Assert.AreEqual(count, checkedCount, "All entities checked");

            world.Dispose();
        }

        [Test]
        public void Chain_DamageDeathClamp()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.ApplyDamage, Threads.Main);
            systems.Add(ChainSystems.CheckDeath, Threads.Main);
            systems.Add(ChainSystems.ClampDeadHealth, Threads.Main);

            var damageArch = world.GetArchetype(
                typeof(ChainDamageEvent), typeof(ChainHealth), typeof(ChainDeadFlag));
            var e1 = damageArch.CreateEntity();
            e1.Get<ChainDamageEvent>().Amount = 30;
            e1.Get<ChainHealth>().Value = 50;
            var e2 = damageArch.CreateEntity();
            e2.Get<ChainDamageEvent>().Amount = 60;
            e2.Get<ChainHealth>().Value = 50;
            var e3 = damageArch.CreateEntity();
            e3.Get<ChainDamageEvent>().Amount = 10;
            e3.Get<ChainHealth>().Value = 50;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(20, e1.Get<ChainHealth>().Value, "e1 hp = 50-30 = 20");
            Assert.AreEqual(0, e2.Get<ChainHealth>().Value, "e2 hp clamped from -10 to 0 by S3");
            Assert.AreEqual(40, e3.Get<ChainHealth>().Value, "e3 hp = 50-10 = 40");

            Assert.IsFalse(e1.Get<ChainDeadFlag>().IsDead, "e1 NOT dead (hp=20)");
            Assert.IsTrue(e2.Get<ChainDeadFlag>().IsDead, "e2 IS dead (hp was -10)");
            Assert.IsFalse(e3.Get<ChainDeadFlag>().IsDead, "e3 NOT dead (hp=40)");

            world.Dispose();
        }

        [Test]
        public void Chain_MultipleDamageTicks_KillOverTime()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.ApplyDamage, Threads.Main);
            systems.Add(ChainSystems.CheckDeath, Threads.Main);
            systems.Add(ChainSystems.ClampDeadHealth, Threads.Main);

            var damageArch = world.GetArchetype(
                typeof(ChainDamageEvent), typeof(ChainHealth), typeof(ChainDeadFlag));
            var entity = damageArch.CreateEntity();
            entity.Get<ChainDamageEvent>().Amount = 30;
            entity.Get<ChainHealth>().Value = 100;
            world.Update();

            systems.OnUpdate(1f, 1f);
            Assert.AreEqual(70, entity.Get<ChainHealth>().Value, "Tick1: hp=100-30=70");
            Assert.IsFalse(entity.Get<ChainDeadFlag>().IsDead, "Tick1: not dead");

            systems.OnUpdate(1f, 2f);
            Assert.AreEqual(40, entity.Get<ChainHealth>().Value, "Tick2: hp=70-30=40");
            Assert.IsFalse(entity.Get<ChainDeadFlag>().IsDead, "Tick2: not dead");

            systems.OnUpdate(1f, 3f);
            Assert.AreEqual(10, entity.Get<ChainHealth>().Value, "Tick3: hp=40-30=10");
            Assert.IsFalse(entity.Get<ChainDeadFlag>().IsDead, "Tick3: not dead");

            systems.OnUpdate(1f, 4f);
            Assert.AreEqual(0, entity.Get<ChainHealth>().Value, "Tick4: hp clamped from -20 to 0");
            Assert.IsTrue(entity.Get<ChainDeadFlag>().IsDead, "Tick4: dead!");

            world.Dispose();
        }

        [Test]
        public void Chain_ScoreMultiplierBonusRank()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.ApplyMultiplier, Threads.Main);
            systems.Add(ChainSystems.ApplyBonus, Threads.Main);
            systems.Add(ChainSystems.AssignRank, Threads.Main);

            var scoreArch = world.GetArchetype(
                typeof(ChainBaseScore), typeof(ChainMultiplier),
                typeof(ChainFinalScore), typeof(ChainRank));
            var e1 = scoreArch.CreateEntity();
            e1.Get<ChainBaseScore>().Value = 100;
            e1.Get<ChainMultiplier>().Value = 2f;
            var e2 = scoreArch.CreateEntity();
            e2.Get<ChainBaseScore>().Value = 50;
            e2.Get<ChainMultiplier>().Value = 1f;
            var e3 = scoreArch.CreateEntity();
            e3.Get<ChainBaseScore>().Value = 200;
            e3.Get<ChainMultiplier>().Value = 3f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(200, e1.Get<ChainBaseScore>().Value, "e1 base = 100*2");
            Assert.AreEqual(250, e1.Get<ChainFinalScore>().Value, "e1 final = 200+50 bonus");
            Assert.AreEqual(2, e1.Get<ChainRank>().Value, "e1 rank=2 (250>=150, <300)");

            Assert.AreEqual(50, e2.Get<ChainBaseScore>().Value, "e2 base = 50*1");
            Assert.AreEqual(50, e2.Get<ChainFinalScore>().Value, "e2 final = 50 (no bonus)");
            Assert.AreEqual(1, e2.Get<ChainRank>().Value, "e2 rank=1 (50<150)");

            Assert.AreEqual(600, e3.Get<ChainBaseScore>().Value, "e3 base = 200*3");
            Assert.AreEqual(650, e3.Get<ChainFinalScore>().Value, "e3 final = 600+50 bonus");
            Assert.AreEqual(3, e3.Get<ChainRank>().Value, "e3 rank=3 (650>=300)");

            world.Dispose();
        }

        [Test]
        public void Chain_InputSpeedVelocityPositionDistance_WithFilter()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.InputToVelocity, Threads.Main);
            systems.Add(ChainSystems.VelocityToPosition, Threads.Main);
            systems.Add(ChainSystems.PositionToDistance, Threads.Main);

            var groundedArch = world.GetArchetype(
                typeof(ChainInput), typeof(ChainSpeed), typeof(ChainVelocity),
                typeof(ChainPosition), typeof(ChainDistance));
            var grounded = groundedArch.CreateEntity();
            grounded.Get<ChainInput>().MoveX = 1f;
            grounded.Get<ChainSpeed>().Value = 5f;

            var airborneArch = world.GetArchetype(
                typeof(ChainInput), typeof(ChainSpeed), typeof(ChainVelocity),
                typeof(ChainPosition), typeof(ChainDistance), typeof(ChainAirborneTag));
            var airborne = airborneArch.CreateEntity();
            airborne.Get<ChainInput>().MoveX = 1f;
            airborne.Get<ChainSpeed>().Value = 5f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(5f, grounded.Get<ChainVelocity>().X, "Grounded: vel X = 1*5");
            Assert.AreEqual(0f, grounded.Get<ChainVelocity>().Y, "Grounded: vel Y = 0*5");
            Assert.AreEqual(5f, grounded.Get<ChainPosition>().X, "Grounded: pos X = 5*1");
            Assert.AreEqual(0f, grounded.Get<ChainPosition>().Y, "Grounded: pos Y = 0");
            Assert.AreEqual(25f, grounded.Get<ChainDistance>().Value, "Grounded: dist = 5*5+0");

            Assert.AreEqual(0f, airborne.Get<ChainVelocity>().X, "Airborne: vel X unchanged (filtered out)");
            Assert.AreEqual(0f, airborne.Get<ChainVelocity>().Y, "Airborne: vel Y unchanged");
            Assert.AreEqual(0f, airborne.Get<ChainPosition>().X, "Airborne: pos X unchanged");
            Assert.AreEqual(0f, airborne.Get<ChainPosition>().Y, "Airborne: pos Y unchanged");
            Assert.AreEqual(0f, airborne.Get<ChainDistance>().Value, "Airborne: dist = 0");

            world.Dispose();
        }

        [Test]
        public void Chain_MultipleEntities_IndependentDataFlow()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.AccelerationToVelocity, Threads.Main);
            systems.Add(ChainSystems.VelocityToPosition, Threads.Main);
            systems.Add(ChainSystems.PositionToDistance, Threads.Main);

            var arch = world.GetArchetype(
                typeof(ChainAcceleration), typeof(ChainVelocity),
                typeof(ChainPosition), typeof(ChainDistance));
            var fast = arch.CreateEntity();
            fast.Get<ChainAcceleration>().Value = 20f;
            var slow = arch.CreateEntity();
            slow.Get<ChainAcceleration>().Value = 5f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(20f, fast.Get<ChainVelocity>().X, "Fast vel X");
            Assert.AreEqual(5f, slow.Get<ChainVelocity>().X, "Slow vel X");
            Assert.AreEqual(20f, fast.Get<ChainPosition>().X, "Fast pos X");
            Assert.AreEqual(5f, slow.Get<ChainPosition>().X, "Slow pos X");
            Assert.AreEqual(800f, fast.Get<ChainDistance>().Value, "Fast dist = 20*20+20*20");
            Assert.AreEqual(50f, slow.Get<ChainDistance>().Value, "Slow dist = 5*5+5*5");

            world.Dispose();
        }

        [Test]
        public void Chain_ZeroAcceleration_NoMovement()
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
            entity.Get<ChainPosition>().X = 5f;
            entity.Get<ChainPosition>().Y = 3f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(0f, entity.Get<ChainVelocity>().X, "Vel X stays 0");
            Assert.AreEqual(0f, entity.Get<ChainVelocity>().Y, "Vel Y stays 0");
            Assert.AreEqual(5f, entity.Get<ChainPosition>().X, "Pos X stays 5");
            Assert.AreEqual(3f, entity.Get<ChainPosition>().Y, "Pos Y stays 3");
            Assert.AreEqual(34f, entity.Get<ChainDistance>().Value, "Dist = 5*5+3*3 = 34");

            world.Dispose();
        }

        [Test]
        public void Chain_NegativeAcceleration_Decelerates()
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
            entity.Get<ChainAcceleration>().Value = -2f;
            entity.Get<ChainVelocity>().X = 10f;
            entity.Get<ChainVelocity>().Y = 10f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(8f, entity.Get<ChainVelocity>().X, "Vel X = 10+(-2)*1 = 8");
            Assert.AreEqual(8f, entity.Get<ChainVelocity>().Y, "Vel Y = 10+(-2)*1 = 8");
            Assert.AreEqual(8f, entity.Get<ChainPosition>().X, "Pos X += 8*1");
            Assert.AreEqual(8f, entity.Get<ChainPosition>().Y, "Pos Y += 8*1");
            Assert.AreEqual(128f, entity.Get<ChainDistance>().Value, "Dist = 8*8+8*8 = 128");

            world.Dispose();
        }

        [Test]
        public void Chain_AccelerationVelocityPosition_ISystem()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add<ChainAccelerationSystem>();
            systems.Add<ChainVelocityToPositionSystem>();
            systems.Add<ChainPositionToDistanceSystem>();

            var arch = world.GetArchetype(
                typeof(ChainAcceleration), typeof(ChainVelocity),
                typeof(ChainPosition), typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 10f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(10f, entity.Get<ChainVelocity>().X, "ISystem: vel X");
            Assert.AreEqual(10f, entity.Get<ChainVelocity>().Y, "ISystem: vel Y");
            Assert.AreEqual(10f, entity.Get<ChainPosition>().X, "ISystem: pos X");
            Assert.AreEqual(10f, entity.Get<ChainPosition>().Y, "ISystem: pos Y");
            Assert.AreEqual(200f, entity.Get<ChainDistance>().Value, "ISystem: dist");

            world.Dispose();
        }

        [Test]
        public void Chain_MixedThreadModes_AllSystemsExecuteInOrder()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.AccelerationToVelocity, Threads.Main);
            systems.Add(ChainSystems.VelocityToPosition, Threads.Single);
            systems.Add(ChainSystems.PositionToDistance, Threads.Parallel);

            var arch = world.GetArchetype(
                typeof(ChainAcceleration), typeof(ChainVelocity),
                typeof(ChainPosition), typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 4f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(4f, entity.Get<ChainVelocity>().X, "Mixed: vel X");
            Assert.AreEqual(4f, entity.Get<ChainPosition>().X, "Mixed: pos X");
            Assert.AreEqual(32f, entity.Get<ChainDistance>().Value, "Mixed: dist = 4*4+4*4");

            world.Dispose();
        }

        [Test]
        public void Chain_FullPipeline_DamageDeathClamp_MultipleEntities()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.Add(ChainSystems.ApplyDamage, Threads.Main);
            systems.Add(ChainSystems.CheckDeath, Threads.Main);
            systems.Add(ChainSystems.ClampDeadHealth, Threads.Main);

            var damageArch = world.GetArchetype(
                typeof(ChainDamageEvent), typeof(ChainHealth), typeof(ChainDeadFlag));
            var hero = damageArch.CreateEntity();
            hero.Get<ChainDamageEvent>().Amount = 0;
            hero.Get<ChainHealth>().Value = 100;
            var enemy1 = damageArch.CreateEntity();
            enemy1.Get<ChainDamageEvent>().Amount = 25;
            enemy1.Get<ChainHealth>().Value = 20;
            var enemy2 = damageArch.CreateEntity();
            enemy2.Get<ChainDamageEvent>().Amount = 50;
            enemy2.Get<ChainHealth>().Value = 50;
            var boss = damageArch.CreateEntity();
            boss.Get<ChainDamageEvent>().Amount = 10;
            boss.Get<ChainHealth>().Value = 200;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(100, hero.Get<ChainHealth>().Value, "Hero takes 0 damage");
            Assert.IsFalse(hero.Get<ChainDeadFlag>().IsDead, "Hero alive");

            Assert.AreEqual(0, enemy1.Get<ChainHealth>().Value, "Enemy1 hp clamped from -5 to 0");
            Assert.IsTrue(enemy1.Get<ChainDeadFlag>().IsDead, "Enemy1 dead");

            Assert.AreEqual(0, enemy2.Get<ChainHealth>().Value, "Enemy2 hp = 50-50 = 0 (not clamped, already 0)");
            Assert.IsTrue(enemy2.Get<ChainDeadFlag>().IsDead, "Enemy2 dead (hp<=0)");

            Assert.AreEqual(190, boss.Get<ChainHealth>().Value, "Boss hp = 200-10 = 190");
            Assert.IsFalse(boss.Get<ChainDeadFlag>().IsDead, "Boss alive");

            world.Dispose();
        }

        [Test]
        public void Chain_AddSystems_BareArgs_DefaultParallel()
        {
            var world = World.Create(WorldConfig.Default1024);
            var systems = new Systems(ref world);
            systems.AddSystems(SystemPath.Update,
                ChainSystems.AccelerationToVelocity,
                ChainSystems.VelocityToPosition,
                ChainSystems.PositionToDistance);

            var arch = world.GetArchetype(
                typeof(ChainAcceleration),
                typeof(ChainVelocity),
                typeof(ChainPosition),
                typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 4f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(4f, entity.Get<ChainVelocity>().X, "AddSystems bare: vel X");
            Assert.AreEqual(4f, entity.Get<ChainPosition>().X, "AddSystems bare: pos X");
            Assert.AreEqual(32f, entity.Get<ChainDistance>().Value, "AddSystems bare: dist = 4*4+4*4");

            world.Dispose();
        }

        [Test]
        public void Chain_AddSystems_TupleArgs_CustomThreads()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddSystems(SystemPath.Update,
                (ChainSystems.AccelerationToVelocity, Threads.Main),
                (ChainSystems.VelocityToPosition, Threads.Main),
                (ChainSystems.PositionToDistance, Threads.Main));

            var arch = world.GetArchetype(
                typeof(ChainAcceleration),
                typeof(ChainVelocity),
                typeof(ChainPosition),
                typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 10f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(10f, entity.Get<ChainVelocity>().X, "AddSystems tuple: vel X = 10*1");
            Assert.AreEqual(10f, entity.Get<ChainPosition>().X, "AddSystems tuple: pos X = 10*1");
            Assert.AreEqual(200f, entity.Get<ChainDistance>().Value, "AddSystems tuple: dist = 10*10*2");

            world.Dispose();
        }

        [Test]
        public void Chain_AddSystems_MixedBareAndTuple()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddSystems(SystemPath.Update,
                ChainSystems.AccelerationToVelocity,
                (ChainSystems.VelocityToPosition, Threads.Main),
                ChainSystems.PositionToDistance);

            var arch = world.GetArchetype(
                typeof(ChainAcceleration),
                typeof(ChainVelocity),
                typeof(ChainPosition),
                typeof(ChainDistance));
            var entity = arch.CreateEntity();
            entity.Get<ChainAcceleration>().Value = 4f;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(4f, entity.Get<ChainVelocity>().X, "AddSystems mixed: vel X");
            Assert.AreEqual(4f, entity.Get<ChainPosition>().X, "AddSystems mixed: pos X");
            Assert.AreEqual(32f, entity.Get<ChainDistance>().Value, "AddSystems mixed: dist = 4*4+4*4");

            world.Dispose();
        }

        [Test]
        public void Chain_AddSystems_DamageDeathClamp()
        {
            var world = World.Create(WorldConfig.Default256);
            var systems = new Systems(ref world);
            systems.AddSystems(SystemPath.Update,
                (ChainSystems.ApplyDamage, Threads.Main),
                (ChainSystems.CheckDeath, Threads.Main),
                (ChainSystems.ClampDeadHealth, Threads.Main));

            var damageArch = world.GetArchetype(
                typeof(ChainDamageEvent), typeof(ChainHealth), typeof(ChainDeadFlag));
            var entity = damageArch.CreateEntity();
            entity.Get<ChainDamageEvent>().Amount = 60;
            entity.Get<ChainHealth>().Value = 50;
            world.Update();

            systems.OnUpdate(1f, 1f);

            Assert.AreEqual(0, entity.Get<ChainHealth>().Value, "AddSystems damage: hp clamped to 0");
            Assert.IsTrue(entity.Get<ChainDeadFlag>().IsDead, "AddSystems damage: dead");

            world.Dispose();
        }

        [Test]
        public void Chain_FractionalDeltaTime_AccumulatesCorrectly()
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

            systems.OnUpdate(0.5f, 0.5f);

            Assert.AreEqual(5f, entity.Get<ChainVelocity>().X, "Tick1: vel = 10*0.5");
            Assert.AreEqual(2.5f, entity.Get<ChainPosition>().X, "Tick1: pos = 5*0.5");
            Assert.AreEqual(12.5f, entity.Get<ChainDistance>().Value, "Tick1: dist = 2.5*2.5*2");

            systems.OnUpdate(0.5f, 1f);

            Assert.AreEqual(10f, entity.Get<ChainVelocity>().X, "Tick2: vel = 5+10*0.5 = 10");
            Assert.AreEqual(7.5f, entity.Get<ChainPosition>().X, "Tick2: pos = 2.5+10*0.5 = 7.5");
            Assert.AreEqual(112.5f, entity.Get<ChainDistance>().Value, "Tick2: dist = 7.5*7.5*2");

            world.Dispose();
        }
    }

    public struct ChainAccelerationSystem : ISystem, IOnCreate
    {
        private Query query;

        public void OnCreate(ref World world)
        {
            query = world.Query().With<ChainAcceleration>().With<ChainVelocity>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var e in query)
            {
                ref var acc = ref e.Get<ChainAcceleration>();
                ref var vel = ref e.Get<ChainVelocity>();
                vel.X += acc.Value * state.Time.DeltaTime;
                vel.Y += acc.Value * state.Time.DeltaTime;
            }
        }
    }

    public struct ChainVelocityToPositionSystem : ISystem, IOnCreate
    {
        private Query query;

        public void OnCreate(ref World world)
        {
            query = world.Query().With<ChainVelocity>().With<ChainPosition>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var e in query)
            {
                ref var vel = ref e.Get<ChainVelocity>();
                ref var pos = ref e.Get<ChainPosition>();
                pos.X += vel.X * state.Time.DeltaTime;
                pos.Y += vel.Y * state.Time.DeltaTime;
            }
        }
    }

    public struct ChainPositionToDistanceSystem : ISystem, IOnCreate
    {
        private Query query;

        public void OnCreate(ref World world)
        {
            query = world.Query().With<ChainPosition>().With<ChainDistance>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var e in query)
            {
                ref var pos = ref e.Get<ChainPosition>();
                ref var dist = ref e.Get<ChainDistance>();
                dist.Value = pos.X * pos.X + pos.Y * pos.Y;
            }
        }
    }
}
