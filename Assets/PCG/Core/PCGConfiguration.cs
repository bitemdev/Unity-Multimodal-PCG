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
        public int BPM => _bpm;
        public float[] PentatonicScale => _pentatonicScale;
        
        [Header("PCG General Settings")]
        [Tooltip("Determinist generation's seed")]
        [SerializeField] private int _seed;

        [Header("Map Settings")]
        [Tooltip("Map's width expressed in number of cells")]
        [SerializeField, Range(10, 500)] private int _width = 50;
        [Tooltip("Map's height expressed in number of cells")]
        [SerializeField, Range(10, 500)] private int _height = 50;
        
        [Header("Entities Settings")] 
        [Tooltip("Number of enemies to generate")]
        [SerializeField] private int _initialEnemyCount = 10;
        [Tooltip("Number of objects (loot) to generate")]
        [SerializeField] private int _initialObjectCount = 6;
        
        [Header("Audio Settings")]
        [Tooltip("Music tempo")]
        [SerializeField, Range(60, 180)] private int _bpm = 120;
        [Tooltip("Base notes for procedural generation")]
        [SerializeField] private float[] _pentatonicScale = new float[]
        {
            65.41f, 77.78f, 87.31f, 98.00f, 116.54f,
            130.81f, 155.56f, 174.61f, 196.00f, 233.08f, 
            261.63f, 311.13f, 349.23f, 392.00f, 466.16f,
            523.25f
        }; // Base frequencies for a scale, Extended Scale with 3 Octaves

        // Security constants
        private const int MIN_SIZE = 20; // Less than 20x20 cells often break BSP
        private const int MAX_SIZE = 500; // More than 500x500 may cause lag on mobile
        private const int MIN_BPM = 40;
        private const int MAX_BPM = 240;

        /// <summary>
        /// This method is a Unity inner method who validates during execution
        /// </summary>
        private void OnValidate()
        {
            // Put width and height into bounds
            _width = Mathf.Clamp(_width, MIN_SIZE, MAX_SIZE);
            _height = Mathf.Clamp(_height, MIN_SIZE, MAX_SIZE);

            int totalCells = _width * _height;
            int estimatedFloor = totalCells / 2; // Approx half the map will be floor (estimation)
            int safeMargin = 20;
            int maxCapacity = Mathf.Max(0, estimatedFloor - safeMargin);

            if (_initialEnemyCount + _initialObjectCount > maxCapacity) // Overflow, priority to enemies over objects
            {
                if (_initialEnemyCount > maxCapacity)
                {
                    _initialEnemyCount = maxCapacity;
                    _initialObjectCount = 0;
                }
                else
                {
                    _initialObjectCount = maxCapacity - _initialEnemyCount;
                }
            }

            _bpm = Mathf.Clamp(_bpm, MIN_BPM, MAX_BPM);

            if (_pentatonicScale == null || _pentatonicScale.Length == 0)
            {
                _pentatonicScale = new float[]
                {
                    130.81f, 155.56f, 196.00f
                }; // Minimalistic fallback
            }
        }
    }
}
