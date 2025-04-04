
![logo-no-background](https://github.com/AlexWargon/Nukecs/assets/37613162/827d5e54-82ff-45d5-af2f-bac06fabc2ec)

### <img src="https://github.com/AlexWargon/Nukecs/assets/37613162/553b8223-c304-4429-8def-96e2830d5ca7" width=2% height=2%> NUKECS is a fast c# entity component system.

### Very early version
### +++

- Use burst on default systems
- Jobs like systems schedule
- World seralisation
- Custom Allocator

### How to use:

Create class inherited from ```WordInstaller``` and drop it on scene
```cs
    public class TestRunner : WorldInstaller
    {
	// if you wnat use other world config
	protected override WorldConfig GetConfig() => new WorldConfig()
        {
            StartPoolSize = 100000, //start pool capacity
            StartEntitiesAmount = 100000 //start entities capacity
        };
	
	// use Udpate for exectuing all systems
        private void Update()
        {
            Systems.OnUpdate(Time.deltaTime, Time.time);
        }
	// add systems here
        protected override void OnWorldCreated(ref World world)
        {
            Systems
                .Add<MySystem1>()
                .Add<MySystem2>()
                .Add<MySystem3>()
                .Add<MySystem4>()
                
                ...
                
                .Add<MySystemN>()
                ;
        }
        
        //create entities here
        protected virtual void CreateEntities(ref World world)
        {
            var e = world.Entity();
            e.Add(new Player());
            e.Add(new Input());
            e.Add(new Speed{value = 4f});
            e.Add(new Body2D());
            e.Add(new Circle2D
            {
                radius = 1.3f,
                layer = CollisionLayer.Player,
                collideWith = CollisionLayer.Enemy | CollisionLayer.Player
            });
        }
	//if you need to cleare some data with world
	protected override void OnDestroy()
        {
            base.OnDestroy(); world will be disposed here
        }
    }
```

### Entity Job System
```cs
    [BurstCompile]
    public struct MoveBulletSystem : IEntityJobSystem
    {
        public SystemMode Mode => SystemMode.Parallel;
        public Query GetQuery(ref World world)
	{
            return world.Query().With<Bullet>().With<Transform>().With<Body2D>().With<Speed>().None<StaticTag>();
        }

        public void OnUpdate(ref Entity entity, ref State state)
	{
            ref var t = ref entity.Get<Transform>();
            ref var b = ref entity.Get<Body2D>();
            ref readonly var s = ref entity.Read<Speed>();
            t.Position = math.mul(t.Rotation, math.right()) * s.value * state.Time.DeltaTime;
        }
    }
```
### Job System
```cs
	[BurstCompile]
	public struct TestJobSystem : IJobSystem, ICreate
	{
		private Query _query;
		public void OnCreate(ref World world)
		{
			_query = world.Query().None<Component1>().With<Component2>();
		}
	
		public void OnUpdate(ref State state)
		{
			foreach (ref var entity in _query)
	                {
	                    ref var c = ref entity.Get<Component2>();
	                }
		}
	}
```
### ISystem
System for main thread. Can be struct or class
```cs

    public struct ClearTransformsSystem : ISystem, IOnCreate
    {
        private Query _query;
        public void OnCreate(ref World world)
        {
            query = world.Query(withDefaultNoneTypes : false)	// by default Query() use some default types for excluding from filtring,
		.With<DestroyEntity>()  			// but can this can be turned of just setting parametr to false
		.With<TransformRef>();
        }

        public void OnUpdate(ref State state)
        {
            foreach (ref var entity in _query)
            {
                UnityEngine.Object.Destroy(entity.Get<TransformRef>().Value.Value.gameObject);
            }
        }
    }
```
Also for every system can be used with some other interfaces: 

```ICreate```. ```IOnCreate(ref World world)``` will be called when system are added to list of systems

```IFixed``` For physics or other staff with fixed tickrate. System will be called every 0.016 seconds


### State
Just struct for accessing such things as World, Time and all dependencies chain
```cs
    public struct State
    {
        public JobHandle Dependencies;
        public World World;
        public TimeData Time;
    }
```
### World Serialization/Deserialization
```cs
    byte[] data = world.Serialize();
    
    world.Deserialize(data);
```
TODO:
- Update README
- Unity integration without tri inspector
- Generator for generic job systems
- Add sync of to network for entity commands
- Rework allocator
- Add support of managed components
