using UnityEngine;
using Unity.Collections;
using System.Diagnostics;
using System;
using PCG.Core;
using PCG.Modules.Environment.Rendering;
using PCG.Modules.Tools;
using PCG.Modules.Entities;

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

        [Header("AI & Navigation")]
        [Tooltip("Script which creates the navigable mesh for AI.")]
        [SerializeField] private RuntimeNavMeshBuilder _navMeshBuilder;
        
        [Header("Settings")]
        [Tooltip("Algorithm used by the generation.")]
        [SerializeField] private GenerationAlgorithm _algorithmType;
        
        [Header("Visualization - Floor")]
        [SerializeField] private MeshFilter _floorMeshFilter;
        [SerializeField] private MeshRenderer _floorMeshRenderer;
        [SerializeField] private MeshCollider _floorMeshCollider;
        [Tooltip("If no material is assigned, it will use a debug color.")]
        [SerializeField] private Material _floorMaterial;
        
        [Header("Visualization - Rest")]
        [SerializeField] private MeshFilter _wallsMeshFilter;
        [SerializeField] private MeshRenderer _wallsMeshRenderer;
        [Tooltip("If no material is assigned, it will use a debug color.")]
        [SerializeField] private Material _wallsMaterial;

        private MapData _currentMap;
        private NativeList<SpawnPoint> _spawnPoints;
        private int _currentSeed;

        /// <summary>
        /// This method generates a full level using the config seed.
        /// </summary>
        [ContextMenu("Generate Level")] // It allows to play from inspector without actually playing
        public void GenerateLevel()
        {
            // -1 acts as a flag to use the default config seed
            GenerateLevelDeterministic(-1);
        }
        
        /// <summary>
        /// Generates the level. If forcedSeed is provided (not -1), it overrides the config.
        /// Essential for recreating exactly the same level when loading a save.
        /// </summary>
        public void GenerateLevelDeterministic(int forcedSeed)
        {
            if (!ValidateConfiguration())
            {
                UnityEngine.Debug.LogError("[EnvironmentManager] Generation Aborted: Invalid Configuration.");
                return;
            }

            ClearMemory();
            DestroyOldMesh(_floorMeshFilter);
            DestroyOldMesh(_wallsMeshFilter);

            // Determine which seed to use
            _currentSeed = forcedSeed == -1 ? _config.Seed : forcedSeed;

            IGeneratorStrategy strategy = GetStrategy(_algorithmType);
            
            Stopwatch sw = new Stopwatch();
            sw.Start();
            
            Vector2Int size = new Vector2Int(_config.Width, _config.Height);
            
            // Generate using the deterministic seed and expected parameters
            _currentMap = strategy.Generate(_currentSeed, size);
            _spawnPoints = MapAnalyzer.GetOptimalSpawnPoints(_currentMap, _config.InitialEnemyCount, _config.InitialObjectCount, Allocator.Persistent);
            
            long logicTime = sw.ElapsedMilliseconds; 
            
            (Mesh floorMesh, Mesh wallMesh) = ProceduralMeshBuilder.BuildMesh(_currentMap);
            
            sw.Stop();
            long renderTime = sw.ElapsedMilliseconds - logicTime; 
    
            AssignMesh(_floorMeshFilter, _floorMeshRenderer, floorMesh, _floorMaterial, "PCG_Floor");
            AssignMesh(_wallsMeshFilter, _wallsMeshRenderer, wallMesh, _wallsMaterial, "Default");
            
            if (_floorMeshCollider != null)
            {
                _floorMeshCollider.sharedMesh = floorMesh;
            }
            
            Physics.SyncTransforms();

            if (_navMeshBuilder != null)
            {
                _navMeshBuilder.BuildNavMesh();
            }
            else
            {
                UnityEngine.Debug.LogWarning("NavMeshBuilder not assigned. AI will not move.");
            }
            
            OnLevelGenerated?.Invoke(_spawnPoints);
    
            int totalVerts = floorMesh.vertexCount + wallMesh.vertexCount;
            string log = $"[PCG Stats] Map: {_config.Width}x{_config.Height} | ";
            log += $"Total: {sw.Elapsed.TotalMilliseconds:F2}ms (Logic: {logicTime}ms | Mesh: {renderTime}ms) | ";
            log += $"Vertices: {totalVerts} | Seed: {_currentSeed}";
    
            UnityEngine.Debug.Log(log);
        }
        
        /// <summary>
        /// This method destroys old meshes
        /// </summary>
        /// <param name="filter"></param>
        private void DestroyOldMesh(MeshFilter filter)
        {
            if (filter != null && filter.sharedMesh != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(filter.sharedMesh);
                }
                else
                {
                    DestroyImmediate(filter.sharedMesh); // If in editor
                }
                
                filter.sharedMesh = null;
            }
        }
        
        /// <summary>
        /// This method is a helper to assign mesh, update collider, set layer and set material
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="renderer"></param>
        /// <param name="newMesh"></param>
        /// <param name="userMaterial"></param>
        /// <param name="layerName"></param>
        private void AssignMesh(MeshFilter filter, MeshRenderer renderer, Mesh newMesh, Material userMaterial, string layerName)
        {
            if (filter == null || renderer == null)
            {
                return;
            }

            filter.mesh = newMesh;
            
            if (userMaterial != null)
            {
                renderer.material = userMaterial;
            }
            else
            {
                Material debugMat = new Material(Shader.Find("Sprites/Default"));
                debugMat.name = "Debug Vertex Color Mat";
                renderer.material = debugMat;
            }

            int layer = LayerMask.NameToLayer(layerName);
            if (layer > -1)
            {
                filter.gameObject.layer = layer;
            }

            var col = filter.GetComponent<MeshCollider>();
            if (col == null)
            {
                col = filter.gameObject.AddComponent<MeshCollider>();
            }
            
            col.sharedMesh = newMesh;
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

            if (_floorMeshFilter == null || _floorMeshRenderer == null)
            {
                UnityEngine.Debug.LogError("Floor visual components missing via Inspector");
                return false;
            }
            
            if (_wallsMeshFilter == null || _wallsMeshRenderer == null)
            {
                UnityEngine.Debug.LogError("Walls visual components missing via Inspector");
                return false;
            }

            if (_navMeshBuilder == null)
            {
                _navMeshBuilder = GetComponent<RuntimeNavMeshBuilder>();

                if (_navMeshBuilder == null)
                {
                    UnityEngine.Debug.Log("[EnvironmentManager] RuntimeNavMeshBuilder missing! Enemies won't move.");
                }
            }

            return true;
        }

        /// <summary>
        /// This method receives a desired generation algorithm, and returns a generator strategy matching that generation algorithm
        /// </summary>
        /// <param name="type"></param>
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
        
        /// <summary>
        /// Collects current world state and saves it to disk.
        /// </summary>
        [ContextMenu("Save Game")]
        public void SaveCurrentGame()
        {
            if (!_currentMap.Grid.IsCreated)
            {
                UnityEngine.Debug.LogWarning("[EnvironmentManager] Generate a level first before saving.");
                return;
            }

            SaveData data = new SaveData();
            data.SaveName = "ManualSave";
            data.Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Store Generation Meta
            data.Generation.Seed = _currentSeed;
            data.Generation.Algorithm = _algorithmType.ToString();

            // Store Dynamic Entity State via EntityManager
            EntityManager entityManager = FindObjectOfType<EntityManager>();
            if (entityManager != null)
            {
                entityManager.FillSaveData(data);
            }

            SaveSystem.SaveGame(data);
        }

        /// <summary>
        /// Reads world state from disk and reconstructs the level deterministically.
        /// </summary>
        [ContextMenu("Load Game")]
        public void LoadLastGame()
        {
            SaveData data = SaveSystem.LoadGame();
            if (data == null) return;

            // Restore Algorithm setting
            if (Enum.TryParse(data.Generation.Algorithm, out GenerationAlgorithm algo))
            {
                _algorithmType = algo;
            }

            // Regenerate the map using the exact saved seed
            GenerateLevelDeterministic(data.Generation.Seed);

            // Reconstruct dynamic state (Player pos, dead enemies, etc.)
            EntityManager entityManager = FindObjectOfType<EntityManager>();
            if (entityManager != null)
            {
                entityManager.LoadSaveData(data);
            }
        }

        private void OnDestroy()
        {
            ClearMemory();
        }
        
        private void OnDisable()
        {
            ClearMemory();
        }

        /// <summary>
        /// This method cleans up every array and rubbish
        /// </summary>
        public void ClearMemory()
        {
            if (_currentMap.Grid.IsCreated)
            {
                _currentMap.Dispose();
            }

            if (_spawnPoints.IsCreated)
            {
                _spawnPoints.Dispose();
            }

            if (_floorMeshFilter.mesh != null)
            {
                DestroyImmediate(_floorMeshFilter.mesh);
            }

            if (_wallsMeshFilter.mesh != null)
            {
                DestroyImmediate(_wallsMeshFilter.mesh);
            }
            
            UnityEngine.Debug.Log("Map memory cleaned.");
        }
    }
}