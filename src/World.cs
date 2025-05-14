//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■
//
//
//
//
//■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■■

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Wargon.Nukecs
{
    public unsafe partial struct World : IDisposable
    {
        internal ptr<WorldUnsafe> UnsafeWorldPtr;

        public WorldUnsafe* UnsafeWorld
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => UnsafeWorldPtr.Ptr;
        }
        internal ref WorldUnsafe UnsafeWorldRef => ref UnsafeWorldPtr.Ref;
        public int Id => UnsafeWorld->Id;
        public bool IsAlive => UnsafeWorld != null;
        public WorldConfig Config => UnsafeWorld->config;
        public Allocator Allocator => UnsafeWorld->Allocator;
        public UnityAllocatorHandler AllocatorHandler => UnsafeWorld->AllocatorHandler;
        public int LastDestroyedEntity => UnsafeWorld->lastDestroyedEntity;
        public int EntitiesAmount => UnsafeWorld->entitiesAmount;
        internal ref EntityCommandBuffer ECB => ref UnsafeWorld->ECB;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref EntityCommandBuffer GetEcbVieContext(UpdateContext context)
        {
            return ref UnsafeWorld->EntityCommandBuffer;
        }

        internal UpdateContext CurrentContext
        {
            get => UnsafeWorld->CurrentContext;
        }

        public ref JobHandle DependenciesUpdate => ref UnsafeWorld->systemsUpdateJobDependencies;
        public ref JobHandle DependenciesFixedUpdate => ref UnsafeWorld->systemsFixedUpdateJobDependencies;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref GenericPool GetPool<T>() where T : unmanaged
        {
            return ref UnsafeWorld->GetPool<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity()
        {
            return UnsafeWorld->CreateEntity();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity SpawnPrefab(in Entity prefab)
        {
            return UnsafeWorld->SpawnPrefab(in prefab);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity<T1>(in T1 c1) where T1 : unmanaged, IComponent
        {
            return UnsafeWorld->CreateEntity(in c1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity<T1>() where T1 : unmanaged, IComponent
        {
            return UnsafeWorld->CreateEntity(default(T1));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Entity Entity<T1, T2>(in T1 c1, in T2 c2)
            where T1 : unmanaged, IComponent
            where T2 : unmanaged, IComponent
        {
            return UnsafeWorld->CreateEntity(in c1, in c2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref Entity GetEntity(int id)
        {
            return ref UnsafeWorld->GetEntity(id);
        }

        public Query Query(bool withDefaultNoneTypes = true)
        {
            return new Query(UnsafeWorld->CreateQueryPtr(withDefaultNoneTypes));
        }

        /// <summary>
        ///     Update dirty entities and queries
        /// </summary>
        public void Update()
        {
            UnsafeWorld->ECB.Playback(ref this);
        }

        public ref T GetSingleton<T>() where T : unmanaged, IComponent
        {
            return ref UnsafeWorld->GetPool<T>().GetSingleton<T>();
        }
    }

    public struct WorldConfig
    {
        public int StartEntitiesAmount;
        public int StartPoolSize;
        public int StartComponentsAmount;
        public Allocator WorldAllocator => Allocator.Persistent;

        public static WorldConfig Default16 => new()
        {
            StartPoolSize = 16,
            StartEntitiesAmount = 16,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default => new()
        {
            StartPoolSize = 64,
            StartEntitiesAmount = 64,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default256 => new()
        {
            StartPoolSize = 256,
            StartEntitiesAmount = 256,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default1024 => new()
        {
            StartPoolSize = 1025,
            StartEntitiesAmount = 1025,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default6144 => new()
        {
            StartPoolSize = 6144,
            StartEntitiesAmount = 6144,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default16384 => new()
        {
            StartPoolSize = 16385,
            StartEntitiesAmount = 16385,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default65536 => new()
        {
            StartPoolSize = 65536,
            StartEntitiesAmount = 65536,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default163840 => new()
        {
            StartPoolSize = 163841,
            StartEntitiesAmount = 163841,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default256000 => new()
        {
            StartPoolSize = 256001,
            StartEntitiesAmount = 256001,
            StartComponentsAmount = 32
        };

        public static WorldConfig Default_1_000_000 => new()
        {
            StartPoolSize = 1_000_001,
            StartEntitiesAmount = 1_000_001,
            StartComponentsAmount = 32
        };
    }

    public unsafe struct Locking : IDisposable
    {
        private NativeReference<int> _locks;

        public static Locking Create(Allocator allocator)
        {
            return new Locking
            {
                _locks = new NativeReference<int>(0, allocator)
            };
        }

        public void Lock()
        {
            while (Interlocked.CompareExchange(ref *_locks.GetUnsafePtrWithoutChecks(), 1, 0) != 0) Common.Pause();
        }

        public void Unlock()
        {
            _locks.Value = 0;
        }

        public void Dispose()
        {
            _locks.Dispose();
        }
    }

    public unsafe struct WorldLock
    {
        public int locks;
        internal World.WorldUnsafe* world;
        public bool IsLocked => locks > 0;
        public bool IsMerging => locks < 0;

        public void Lock()
        {
            if (IsMerging) return;
            locks++;
        }

        public void Unlock()
        {
            if (IsMerging) return;
            locks--;
            if (locks == 0)
            {
                locks = -1;
                world->ECB.Playback(world);
                locks = 0;
            }
        }
    }
}