
![logo-no-background](https://github.com/AlexWargon/Nukecs/assets/37613162/827d5e54-82ff-45d5-af2f-bac06fabc2ec)

### <img src="https://github.com/AlexWargon/Nukecs/assets/37613162/553b8223-c304-4429-8def-96e2830d5ca7" width=2% height=2%> NUKECS is a fast c# entity component system that uses burst and job systems by default.

### Entity Job System
```cs
[BurstCompile]
public struct ExampleSystem1 : IEntityJobSystem
{
	public SystemMode Mode => SystemMode.Parallel;
	public Query GetQuery(ref world) => world.CreateQuery()
					.With<Component1>()
					.With<Component2>()
					.None<Component3>();
 	public void OnUpdate(ref Entity entity, float deltaTime)
	{
 		ref var comp1 = ref entity.Get<Component1>();
		ref var comp2 = ref entity.Get<Component2>();
		entity.Has<Component3>() // return false in this case
		entity.Remove<Component1>();
 	}
}
```
### Job System
```cs
[BurstCompile]
public struct TestSystem2 : IJobSystem, ICreate {
	private Query _query;

	public void OnCreate(ref World world) {
	        _query = world.CreateQuery().None<Component1>().With<Component2>();
	}

	public void OnUpdate(ref World world, float deltaTime)
	{
		if (_query.Count > 0) 
		{
			Debug.Log("WORK");
		}
	}
}
```
