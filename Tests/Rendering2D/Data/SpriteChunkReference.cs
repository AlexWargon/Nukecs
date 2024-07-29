using System.Runtime.InteropServices;

namespace Wargon.Nukecs.Tests {
    [StructLayout(LayoutKind.Sequential)]
    public struct SpriteChunkReference : IComponent {
        public unsafe SpriteChunk* chunk;
        public unsafe ref SpriteChunk ChunkRef => ref *chunk;
    }
}