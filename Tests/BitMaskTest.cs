using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Wargon.Nukecs;

public unsafe class BitMaskTest : MonoBehaviour {
    private Bitmask1024 _bitmask1024;
    private HashSet<int> _hashSet;
    private BitmaskMax _bitmaskMax;
    private UnsafeHashSet<int> _unsafeHashSet;
    private DynamicBitmask _dynamicBitmask;
    private GenericPool* _poolPtr;
    void Start() {
        _poolPtr = GenericPool.CreatePtr<HP>(123, Allocator.Persistent);
        _bitmask1024 = default;
        _hashSet = new HashSet<int>();
        _bitmaskMax = BitmaskMax.New();
        _unsafeHashSet = new UnsafeHashSet<int>(1024, Allocator.Persistent);
        _dynamicBitmask = new DynamicBitmask(1024);
        Debug.Log(Bitmask1024.ArraySize);
        for (int i = 0; i < 12; i++) {
            if (_bitmask1024.Add(i)) {}
            if(_hashSet.Add(i)){}
            _bitmaskMax.Add(i);
            _unsafeHashSet.Add(i);
            _dynamicBitmask.Add(i);
        }
    }

    private void OnDestroy() {
        _bitmaskMax.Free();
        _unsafeHashSet.Dispose();
        _dynamicBitmask.Dispose();
        if (_poolPtr != null) {
            _poolPtr->Dispose();
            UnsafeUtility.Free(_poolPtr, Allocator.Persistent);
        }
    }

    private void Update() {
        
        var time = Time.realtimeSinceStartup;
        for (int i = 0; i < 100000; i++) {
            if(_bitmask1024.Has2(512)){}
            if(_bitmask1024.Has2(4)){}
        }

        var timeNew = Time.realtimeSinceStartup;
        Debug.Log($"Bitmask1024 Has {timeNew - time}");
        
        time = Time.realtimeSinceStartup;
        for (int i = 0; i < 100000; i++) {
            if(_hashSet.Contains(512)){}
            if(_hashSet.Contains(4)){}
        }
        timeNew = Time.realtimeSinceStartup;
        Debug.Log($"HashSet Contains {timeNew - time}");
        
        time = Time.realtimeSinceStartup;
        for (int i = 0; i < 100000; i++) {
            if(_bitmaskMax.Has(512)){}
            if(_bitmaskMax.Has(4)){}
        }
        timeNew = Time.realtimeSinceStartup;
        Debug.Log($"BitmaskMax Has {timeNew - time}");
        
        
        time = Time.realtimeSinceStartup;
        for (int i = 0; i < 100000; i++) {
            if(_unsafeHashSet.Contains(512)){}
            if(_unsafeHashSet.Contains(4)){}
        }
        timeNew = Time.realtimeSinceStartup;
        
        Debug.Log($"UnsafeHashSet Has {timeNew - time}");
        
        time = Time.realtimeSinceStartup;
        for (int i = 0; i < 100000; i++) {
            if(_dynamicBitmask.Has(512)){}
            if(_dynamicBitmask.Has(4)){}
        }
        timeNew = Time.realtimeSinceStartup;
        
        Debug.Log($"{nameof(DynamicBitmask)} Has {timeNew - time}");
        
        time = Time.realtimeSinceStartup;
        for (int i = 0; i < 100000; i++) {
            if(_poolPtr == null){}
            if(_poolPtr == null){}
        }
        timeNew = Time.realtimeSinceStartup;
        
        Debug.Log($"Pointer null check {timeNew - time}");
    }
    
}
