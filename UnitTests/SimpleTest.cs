using NUnit.Framework;

namespace Wargon.Nukecs.Tests
{
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