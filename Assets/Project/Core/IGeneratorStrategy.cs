using UnityEngine;

namespace PCG.Core
{
    public interface IGeneratorStrategy
    {
        MapData Generate(int seed, Vector2Int size);
    }
}
