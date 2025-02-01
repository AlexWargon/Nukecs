using System.Runtime.InteropServices;
using UnityEngine.Serialization;

namespace Wargon.Nukecs.Tests {
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteChunkReference : IComponent {
        public unsafe SpriteChunk* chunk;
        public int instanceId;
        [FormerlySerializedAs("shader")] public int achetypeIndex;
        public unsafe ref SpriteChunk ChunkRef => ref *chunk;
    }
}