// ReSharper disable InconsistentNaming
namespace Wargon.Nukecs
{
    public static class _systems_internal
    {
        public static void add_runner(Systems systems, int path, ISystemRunner runner)
        {
            switch (path)
            {
                case SystemPath.Start:
                    add_on_start(systems, runner);
                    break;
                case SystemPath.Update:
                    add_on_update(systems, runner);
                    break;
                case SystemPath.FixedUpdate:
                    add_on_fixed_update(systems, runner);
                    break;
                case SystemPath.Destroy:
                    add_on_destroy(systems, runner);
                    break;
            }
        }

        public static void add_on_start(Systems systems, ISystemRunner runner)
        {
            systems.onStart.Add(runner);
        }
        public static void add_on_update(Systems systems, ISystemRunner runner)
        {
            systems.onUpdate.Add(runner);
        }
        public static void add_on_fixed_update(Systems systems, ISystemRunner runner)
        {
            systems.onFixedUpdate.Add(runner);
        }
        public static void add_on_destroy(Systems systems, ISystemRunner runner)
        {
            systems.onDestroy.Add(runner);
        }
    }
}