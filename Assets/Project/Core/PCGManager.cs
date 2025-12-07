using PCG.Environment;
using UnityEngine;
using Unity.Collections;

namespace PCG.Core
{
    public enum GenerationAlgorithm
    {
        Maze_Backtracker,
        Dungeon_BSP
    }
    
    [DisallowMultipleComponent] // With this, only 1 manager per gameobject is allowed
    public class PCGManager : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Configuration file with base parameters.")]
        [SerializeField] private PCGConfiguration _config;
        
        [Header("Settings")]
        [Tooltip("Algorithm used by the generation.")]
        [SerializeField] private GenerationAlgorithm _algorithmType;

        private MapData _currentMap;

        // This method generates a level
        [ContextMenu("Generate Level")] // It allows to play from inspector without actually playing
        public void GenerateLevel()
        {
            if (_currentMap.Grid.IsCreated) // If a map is already created, it shall be cleaned up to prevent memory leaks
            {
                _currentMap.Dispose();
            }

            if (_config == null)
            {
                Debug.LogError("PCGManager config is missing!");
                return;
            }

            IGeneratorStrategy strategy = GetStrategy(_algorithmType);
            Vector2Int size = new Vector2Int(_config.Width, _config.Height);
            _currentMap = strategy.Generate(_config.Seed, size);
            
            Debug.Log($"Generated: {_algorithmType}");
        }

        private IGeneratorStrategy GetStrategy(GenerationAlgorithm type)
        {
            switch (type)
            {
                case GenerationAlgorithm.Maze_Backtracker:
                    return new MazeGenerator();
                case GenerationAlgorithm.Dungeon_BSP:
                    return new MazeGenerator();
                default:
                    return new MazeGenerator();
            }
        }

        // This method is called both when game's closed and scene's been changed. It is mandatory to clean up NativeArrays here
        private void OnDestroy()
        {
            if (_currentMap.Grid.IsCreated)
            {
                _currentMap.Dispose();
                Debug.Log("Map memory cleaned.");
            }
        }
        
        // This method is called when a script is deactivated or Unity is recompiling
        private void OnDisable()
        {
            if (_currentMap.Grid.IsCreated)
            {
                _currentMap.Dispose();
                Debug.Log("Map memory cleaned.");
            }
        }
        
        // This Unity event permits to draw visual debug elements in the Scene View.
        // It is extremely useful to verify the logic without instantiating GameObjects (which is slow).
        private void OnDrawGizmos()
        {
            if (!_currentMap.Grid.IsCreated)
            {
                return;
            }

            Vector3 cellSize = new Vector3(1f, 0.1f, 1f);

            for (int x = 0; x < _currentMap.Width; x++)
            {
                for (int y = 0; y < _currentMap.Height; y++)
                {
                    int index = _currentMap.GetIndex(x, y);
                    CellType cell = _currentMap.Grid[index];
                    
                    Vector3 pos = new Vector3(x, 0, y);

                    if (cell == CellType.Floor)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawCube(pos, cellSize);
                    }
                    else if (cell == CellType.Wall)
                    {
                        Gizmos.color = Color.black;
                        Gizmos.DrawCube(pos, cellSize);
                    }
                }
            }
        }
    }
}