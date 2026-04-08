namespace Wargon.Nukecs
{
    internal class SystemDestroyer<T> : ISystemDestroyer where T : unmanaged, IOnDestroy
    {
        private T systemCopy;
        public SystemDestroyer(ref T system)
        {
            systemCopy = system;
        }
        public void Destroy(ref World world)
        {
            systemCopy.OnDestroy(ref world);
        }
    }
}