using Wargon.Nukecs.Collections;

namespace Wargon.Nukecs
{
    public partial struct World
    {
#if NUKECS_DEBUG
        public struct ComponentChange
        {
            public int entityId;
            public int componentTypeIndex;
            public EntityCommandBuffer.ECBCommand.Type command; 
            public double timeStamp; // time from gameStart
        }
        public partial struct WorldUnsafe
        {
            internal MemoryList<ComponentChange> storyLog;
    
            // Index of the oldest element in the ring buffer
            private int changeStart;

            // Number of currently stored changes
            private int changeCount;
            private int logsTotalCount;
            internal void CreateStoryLogList(int capacity)
            {
                storyLog = new MemoryList<ComponentChange>(capacity, ref AllocatorRef, true);
                changeStart = 0;
                changeCount = 0;
                logsTotalCount = 0;
            }

            /// <summary>
            /// Adds a new component change record.
            /// Overwrites the oldest one if buffer is full.
            /// </summary>
            public void AddComponentChange(ComponentChange change)
            {
                if (changeCount < storyLog.Capacity)
                {
                    // Add to the end if there is space
                    int writeIndex = (changeStart + changeCount) % storyLog.Capacity;
                    storyLog[writeIndex] = change;
                    changeCount++;
                }
                else
                {
                    // Overwrite oldest entry
                    storyLog[changeStart] = change;
                    changeStart = (changeStart + 1) % storyLog.Capacity;
                }

                logsTotalCount++;
            }

            /// <summary>
            /// Returns the current number of stored changes.
            /// </summary>
            public int GetStoryLogCount()
            {
                return changeCount;
            }

            public int GetStoryLogHead()
            {
                return (changeStart + changeCount) % storyLog.Capacity;
            }

            public int GetTotalStoryLogCount()
            {
                return logsTotalCount;
            }
            
            /// <summary>
            /// Gets the change at the logical index (0 = oldest).
            /// </summary>
            public ComponentChange GetStoryLogAt(int index)
            {
                if (index < 0 || index >= changeCount)
                    throw new System.IndexOutOfRangeException();

                int actualIndex = (changeStart + index) % storyLog.Capacity;
                return storyLog[actualIndex];
            }
        }
#endif
    }
}