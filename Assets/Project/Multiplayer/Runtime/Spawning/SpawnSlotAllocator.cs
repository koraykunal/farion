using System.Collections.Generic;

namespace Farion.Multiplayer.Spawning
{
    public sealed class SpawnSlotAllocator
    {
        readonly int capacity;
        readonly Dictionary<int, int> slotsByConnection = new();

        public SpawnSlotAllocator(int capacity)
        {
            this.capacity = capacity > 0 ? capacity : 1;
        }

        public int Count => slotsByConnection.Count;

        public bool TryGetReserved(int connectionId, out int slot)
        {
            return slotsByConnection.TryGetValue(connectionId, out slot);
        }

        public bool TryReserve(int connectionId, out int slot)
        {
            if (slotsByConnection.TryGetValue(connectionId, out slot))
            {
                return true;
            }

            for (slot = 0; slot < capacity; slot++)
            {
                if (!slotsByConnection.ContainsValue(slot))
                {
                    slotsByConnection.Add(connectionId, slot);
                    return true;
                }
            }

            slot = -1;
            return false;
        }

        public void Release(int connectionId)
        {
            slotsByConnection.Remove(connectionId);
        }

        public void Reset()
        {
            slotsByConnection.Clear();
        }
    }
}
