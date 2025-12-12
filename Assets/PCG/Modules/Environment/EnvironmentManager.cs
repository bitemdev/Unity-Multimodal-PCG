using UnityEngine;
using Unity.Collections;
using System.Diagnostics;
using System;
using PCG.Core;
using PCG.Modules.Environment.Rendering;

namespace PCG.Modules.Environment
{
    public enum GenerationAlgorithm
    {
        Maze_Backtracker,
        Dungeon_BSP
    }
    
    [DisallowMultipleComponent] // With this, only 1 manager per GameObject is allowed
    public class EnvironmentManager : MonoBehaviour
    {
        public event Action<NativeList<SpawnPoint>> OnLevelGenerated;
        
        [Header("Dependencies")]
        [Tooltip("Configuration file with base parameters.")]
        [SerializeField] private PCGConfiguration _config;
        
        [Header("Settings")]
        [Tooltip("Algorithm used by the generation.")]
        [SerializeField] private GenerationAlgorithm _algorithmType;
        
        [Header("Visualization")]
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;

        private MapData _currentMap;
        private NativeList<SpawnPoint> _spawnPoints;

        /// <summary>
        /// This method generates a full level
        /// </summary>
        [ContextMenu("Generate Level")] // It allows to play from inspector without actually playing
        public void GenerateLevel()
        {
            if (!ValidateConfiguration())
            {
                UnityEngine.Debug.LogError("[EnvironmentManager] Generation Aborted: Invalid Configuration.");
            }
            
            if (_currentMap.Grid.IsCreated) // If a map is already created, it shall be cleaned up to prevent memory leaks
            {
                _currentMap.Dispose();
            }

            if (_spawnPoints.IsCreated)
            {
                _spawnPoints.Dispose();
            }

            if (_config == null)
            {
                UnityEngine.Debug.LogError("PCGConfiguration config is missing!");
                return;
            }

            IGeneratorStrategy strategy = GetStrategy(_algorithmType);
            
            Stopwatch sw = new Stopwatch();
            sw.Start();
            
            Vector2Int size = new Vector2Int(_config.Width, _config.Height);
            _currentMap = strategy.Generate(_config.Seed, size);
            _spawnPoints = MapAnalyzer.GetOptimalSpawnPoints(_currentMap, Allocator.Persistent);
            
            MapAnalyzer.FindCellCandidates(
                _currentMap, 
                ref _spawnPoints, 
                _config.InitialEnemyCount, 
                _config.InitialObjectCount, 
                (uint)_config.Seed
            );
            
            long logicTime = sw.ElapsedMilliseconds; // Captured logic time (array data generation time)
            
            Mesh levelMesh = ProceduralMeshBuilder.BuildMesh(_currentMap);
            
            sw.Stop();
            long renderTime = sw.ElapsedMilliseconds - logicTime; // Visual delta
    
            _meshFilter.mesh = levelMesh;
            
            OnLevelGenerated?.Invoke(_spawnPoints);
    
            string log = $"[PCG Stats] Map: {_config.Width}x{_config.Height} | ";
            log += $"Total: {sw.Elapsed.TotalMilliseconds:F2}ms (Logic: {logicTime}ms | Mesh: {renderTime}ms) | ";
            log += $"Vertices: {levelMesh.vertexCount} | Triangles: {levelMesh.triangles.Length / 3}";
    
            UnityEngine.Debug.Log(log);
        }

        /// <summary>
        /// This method checks if configuration values are valid and protects from null, crashes, etc.
        /// </summary>
        private bool ValidateConfiguration()
        {
            if (_config == null)
            {
                UnityEngine.Debug.LogError("Config is null.");
                return false;
            }

            if (_config.Width < 10 || _config.Height < 10)
            {
                UnityEngine.Debug.LogError($"Map size too small ({_config.Width}x{_config.Height}). Minimum is 10x10.");
                return false;
            }

            if (_config.PentatonicScale.Length == 0)
            {
                UnityEngine.Debug.LogError("Music Scale is empty. Audio generation will crash.");
                return false;
            }

            if (_meshFilter == null || _meshRenderer == null)
            {
                UnityEngine.Debug.LogWarning("Visual components missing via Inspector. Trying to GetComponent...");
                _meshFilter = GetComponent<MeshFilter>();
                _meshRenderer = GetComponent<MeshRenderer>();

                if (_meshFilter == null || _meshRenderer == null)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// This method receives a desired generation algorithm, and returns a generator strategy matching that generation algorithm
        /// </summary>
        private IGeneratorStrategy GetStrategy(GenerationAlgorithm type)
        {
            switch (type)
            {
                case GenerationAlgorithm.Maze_Backtracker:
                    return new MazeGenerator();
                case GenerationAlgorithm.Dungeon_BSP:
                    return new DungeonGenerator();
                default:
                    return new MazeGenerator();
            }
        }

        private void OnDestroy()
        {
            CleanMemory();
        }
        
        private void OnDisable()
        {
            CleanMemory();
        }

        /// <summary>
        /// This method cleans up every array and rubbish
        /// </summary>
        private void CleanMemory()
        {
            if (_currentMap.Grid.IsCreated)
            {
                _currentMap.Dispose();
            }

            if (_spawnPoints.IsCreated)
            {
                _spawnPoints.Dispose();
            }
            
            UnityEngine.Debug.Log("Map memory cleaned.");
        }
    }
}