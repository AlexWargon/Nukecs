using Unity.Jobs;

namespace Wargon.Nukecs
{
    /// <summary>
    /// <code>
    /// Dependencies
    /// World
    /// Time
    /// </code>
    /// </summary>
    public struct State
    {
        public JobHandle Dependencies;
        public World World;
        public TimeData Time;
    }
}