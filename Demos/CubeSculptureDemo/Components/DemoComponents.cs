using System.Runtime.InteropServices;
using Unity.Mathematics;
using Wargon.Nukecs.Transforms;

namespace Wargon.Nukecs.Demos.CubeSculpture
{
    public enum CubeState
    {
        Free,
        Swarm,
        Assemble,
        Assembled
    }

    public struct Velocity : IComponent
    {
        public float3 Value;
    }

    public struct CubeStateTag : IComponent
    {
        public CubeState Value;
    }

    public struct AssembledTag : IComponent
    {
        
    }
    public struct SculptureSlotIndex : IComponent
    {
        public int Value;
    }

    public struct FormationOffset : IComponent
    {
        public float3 Value;
    }

    public struct AnimationPhase : IComponent
    {
        public float Time;
    }

    public struct ConfigData : IRes
    {
        public int TargetCount;
        public float CubeScale;
        public int SpawnBatchSize;
        public float timer;
        public float spawnTime;
        public void Init(ref World world) { }
        public void Update(ref World world) { }
    }

    public struct SculptureData : IRes
    {
        public int TransitionCounter;
        public int SlotCounter;

        public void Init(ref World world) { }
        public void Update(ref World world) { }
    }

    public struct CycleData : IRes
    {
        public int CycleIndex;
        public float AssembledTimer;
        public float AssembledDuration;
        public int AssembledCount;
        public int SwarmFormation;
        public int SculptureShape;
        public bool Disassembling;
        public float DisassembleTimer;

        public void Init(ref World world)
        {
            AssembledDuration = 3f;
        }

        public void Update(ref World world) { }
    }
}
