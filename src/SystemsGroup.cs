namespace Wargon.Nukecs
{
    using System.Collections.Generic;

    public class SystemsGroup {
        internal List<ISystemRunner> runners = new ();
        internal List<ISystemRunner> fixedRunners = new ();
        internal List<ISystemRunner> mainThreadRunners = new ();
        internal List<ISystemRunner> mainThreadFixedRunners = new ();
        protected string name;
        public string Name => name;
        internal World world;
        public SystemsGroup(ref World world){
            this.world = world;
            this.name = "";
        }
        public SystemsGroup(ref World world,string name){
            this.world = world;
            this.name = name;
        }

        public unsafe SystemsGroup Add<T>(bool dymmy = false) where T : struct, IEntityJobSystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            var runner = new EntityJobSystemRunner<T> {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref world).InternalPointer;
            if (system is IFixed)
            {
                fixedRunners.Add(runner);
            }
            else
            {
                runners.Add(runner);
            }

            return this;
        }


        public SystemsGroup Add<T>(int dymmy = 1) where T : struct, ISystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            var runner = new SystemMainThreadRunnerStruct<T> {
                System = system,
                EcbJob = default
            };
            if (system is IFixed)
            {
                mainThreadFixedRunners.Add(runner);
            }
            else
            if (system is IJobRunner)
            {
                runners.Add(runner);
            }
            else
            {
                mainThreadRunners.Add(runner);
            }
            return this;
        }
        public SystemsGroup Add<T>(byte dymmy = 1) where T : class, ISystem, new() {
            T system = new T();
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            
            var runner = new SystemMainThreadRunnerClass<T> {
                System = system,
                EcbJob = default
            };
            if (system is IFixed)
            {
                mainThreadFixedRunners.Add(runner);
            }
            else
            if (system is IJobRunner)
            {
                runners.Add(runner);
            }
            else
            {
                mainThreadRunners.Add(runner);
            }
            return this;
        }
    }
}