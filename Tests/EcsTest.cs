using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using Wargon.Nukecs;

public class EcsTest : MonoBehaviour
{
    // Start is called before the first frame update
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
        using var world = World.Create();

        var e = world.CreateEntity();
        e.Add(new HP {
            value = 556
        });
        e.Add(new Player());
        e.Add(new Speed());
        Debug.Log($"{e.Get<HP>().value}");
        Debug.Log($"{e.Has<Speed>()}");
        Debug.Log($"{e.Has<HP>()}");
        Debug.Log($"{e.Has<Money>()}");
        Debug.Log($"{e.Has<Player>()}");

    }

    // Update is called once per frame
    void Update()
    {
        
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