namespace Wargon.Nukecs
{
    using System.Collections.Generic;

    public class SystemsGroup {
        internal System.Collections.Generic.List<ISystemRunner> runners = new System.Collections.Generic.List<ISystemRunner>();
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
        public SystemsGroup Add<T>() where T : struct, IJobSystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            runners.Add(new JobSystemRunner<T> {
                System = system,
                EcbJob = default
            });
            return this;
        }

        public SystemsGroup Add<T>(bool dymmy = false) where T : struct, IEntityJobSystem {
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
            runner.Query = runner.System.GetQuery(ref world);
            runners.Add(runner);
            return this;
        }
        
        public SystemsGroup Add<T>(short dymmy = 1) where T : struct, IQueryJobSystem {
            T system = default;
            if (system is IOnCreate s) {
                s.OnCreate(ref world);
                system = (T) s;
            }
            var runner = new QueryJobSystemRunner<T> {
                System = system,
                Mode = system.Mode,
                EcbJob = default
            };
            runner.Query = runner.System.GetQuery(ref world);
            runners.Add(runner);
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
            runners.Add(runner);
            return this;
        }
    }
}