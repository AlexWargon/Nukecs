namespace Wargon.Nukecs
{
    internal class SystemClassDestroyer : ISystemDestroyer
    {
        private IOnDestroy system;

        internal SystemClassDestroyer(IOnDestroy system)
        {
            this.system = system;
        }
        public void Destroy(ref World world)
        {
            system.OnDestroy(ref world);
        }
    }
}