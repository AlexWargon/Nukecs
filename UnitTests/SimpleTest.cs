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
    }
}