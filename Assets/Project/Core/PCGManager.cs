using PCG.Environment;
using UnityEngine;
using Unity.Collections;
using PCG.Rendering;
using System.Diagnostics;
using System;
using Unity.Mathematics;
using Unity.VisualScripting;

namespace PCG.Core
{
    public enum GenerationAlgorithm
    {
        Maze_Backtracker,
        Dungeon_BSP
    }
    
    [DisallowMultipleComponent] // With this, only 1 manager per GameObject is allowed
    public class PCGManager : MonoBehaviour
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

        // This method generates a level
        [ContextMenu("Generate Level")] // It allows to play from inspector without actually playing
        public void GenerateLevel()
        {
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
                UnityEngine.Debug.LogError("PCGManager config is missing!");
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

        // This method is called both when game's closed and scene's been changed. It is mandatory to clean up NativeArrays here
        private void OnDestroy()
        {
            CleanMemory();
        }
        
        // This method is called when a script is deactivated or Unity is recompiling
        private void OnDisable()
        {
            CleanMemory();
        }

        // This method cleans up every array and rubbish
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