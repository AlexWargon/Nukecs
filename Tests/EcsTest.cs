using Unity.Burst;
using UnityEngine;
using Wargon.Nukecs;

public unsafe class EcsTest : MonoBehaviour
{
    private World world;
    private Systems systems;
    void Start() {
        // var mask = new DynamicBitmask(128);
        //
        //
        // mask.Add(5);
        //
        // mask.Add(123);
        //
        // Debug.Log(mask.Has(5));
        // Debug.Log(mask.Has(124));
        // Debug.Log(mask.Has(123));
        // Debug.Log(mask.Has(1));
        // mask.Dispose();
        world = World.Create();
        systems = new Systems(ref world);
        systems.Add<TestSystem>().Add<TestSystem2>();
        var e = world.CreateEntity();
        
        e.Add(new Money {
            amount = 1000
        });
        e.Add(new Player());
        // Debug.Log($"{e.Get<HP>().value}");
        // Debug.Log($"{e.Has<Speed>()}");
        // Debug.Log($"{e.Has<HP>()}");
        // Debug.Log($"{e.Has<Money>()}");
        // Debug.Log($"{e.Has<Player>()}");

    }

    // Update is called once per frame
    void Update()
    {
        systems.OnUpdate(Time.deltaTime);
    }

    private void OnDestroy()
    {
        world.Dispose();
    }
}
[BurstCompile]
public struct TestSystem : ISystem, ICreate
{
    private Query _query;
    public void OnCreate(ref World world)
    {
        _query = world.CreateQuery().With<Money>();
        Debug.Log(_query.Count);
    }
    [BurstCompile]
    public void OnUpdate(float deltaTime)
    {
        for (var i = 0; i < _query.Count; i++)
        {
            ref var e = ref _query.GetEntity(i);
            ref var money = ref e.Get<Money>();
            money.amount++;
            Debug.Log(money.amount);
            if (money.amount >= 3_000)
            {
                Debug.Log($"YOU ARE MILLINER {money.amount}");
                e.Remove<Money>();
            }
        }
    }
}
[BurstCompile]
public struct TestSystem2 : ISystem, ICreate
{
    private Query _query;
    public void OnCreate(ref World world)
    {
        _query = world.CreateQuery().None<Money>().With<Player>();
    }
    [BurstCompile]
    public void OnUpdate(float deltaTime)
    {
        if (_query.Count > 0)
        {
            Debug.Log("WORK");
        }
    }
}
public struct HP : IComponent {
    public int value;
}

public struct Player : IComponent {
    
}

public struct Speed : IComponent {
    
}

public struct Money : IComponent {
    public int amount;
}
public struct Dead : IComponent {}