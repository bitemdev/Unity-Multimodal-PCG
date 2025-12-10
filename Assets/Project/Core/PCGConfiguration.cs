using UnityEngine;

namespace PCG.Core
{
    [CreateAssetMenu(fileName = "NewPCGConfig", menuName = "PCG/PCG Configuration")]
    public class PCGConfiguration : ScriptableObject
    {
        public int Seed => _seed;
        public int Width => _width;
        public int Height => _height;
        public int InitialEnemyCount => _initialEnemyCount;
        public int InitialObjectCount => _initialObjectCount;
        
        [Header("General Settings")]
        [SerializeField] private int _seed;

        [Header("Dimensions")]
        [SerializeField, Range(10, 500)] private int _width = 50;
        [SerializeField, Range(10, 500)] private int _height = 50;
        
        [Header("Entities")] 
        [SerializeField] private int _initialEnemyCount = 10;
        [SerializeField] private int _initialObjectCount = 6;
    }
}
