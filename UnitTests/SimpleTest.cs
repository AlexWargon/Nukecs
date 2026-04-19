using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
    public struct C1Test
    {
        public int one;
    }

    public struct C2Test
    {
        public int one;
        public int two;
    }
    [TestFixture]
    public class SimpleTest
    {
        [Test]
        public void SimpleEntityCreation()
        {
            var world = World.Create(WorldConfig.Default256);
            var entity = world.Entity();
            Assert.IsTrue(entity.IsValid());
            world.Dispose();
        }
        [Test]
        public void QTest()
        {
            var wTest = new WTest(32);
            ref var q = ref wTest.GetQ<(C1Test, C2Test)>(3);
            q = new QueryTest<(C1Test, C2Test)>(32);
            foreach (ref var data in q)
            {
                data.Item1.one = 100;
                data.Item2.two = 32;
                data.Item2.one = 100;
            }
            ref var q2 = ref wTest.GetQ<(C1Test, C2Test)>(3);
            foreach (ref var data in q2.par_iter())
            {
                Assert.AreEqual(data.Item1.one, 100);
                Assert.AreEqual(data.Item2.two, 32);
                Assert.AreEqual(data.Item2.one, 100);
            }
            q2.Dispose();
            wTest.Dispose();
        }
    }
}