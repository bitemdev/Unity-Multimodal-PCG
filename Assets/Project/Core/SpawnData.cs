using Unity.Mathematics;

namespace PCG.Core
{
    public struct SpawnPoint
    {
        public int2 Coordinate;
        public EntityType Type;
    }

    public enum EntityType
    {
        Start,
        Enemy,
        Object,
        Exit
    }
}