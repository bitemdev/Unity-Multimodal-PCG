using Unity.Mathematics;

namespace PCG.Core
{
    public struct SpawnPoint
    {
        public int2 Coordinate;
        public EntityType Type;
        public float RotationY;
        
        public SpawnPoint(int2 coordinate, EntityType type, float rotationY)
        {
            Coordinate = coordinate;
            Type = type;
            RotationY = rotationY;
        }
    }

    public enum EntityType
    {
        Start,
        Enemy,
        Object,
        Exit
    }
}