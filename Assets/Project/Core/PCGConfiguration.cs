using UnityEngine;

namespace PCG.Core
{
    [CreateAssetMenu(fileName = "NewPCGConfig", menuName = "PCG/Configuration")]
    public class PCGConfiguration : ScriptableObject
    {
        public int Seed => _seed;
        public int Width => _width;
        public int Height => _height;
        public int EnemyCount => _enemyCount;
        public int ObjectCount => _objectCount;
        
        [Header("General Settings")]
        [SerializeField] private int _seed;

        [Header("Dimensions")]
        [SerializeField, Range(10, 500)] private int _width = 50;
        [SerializeField, Range(10, 500)] private int _height = 50;
        
        [Header("Entities")] 
        [SerializeField] private int _enemyCount = 10;
        [SerializeField] private int _objectCount = 6;
    }
}
