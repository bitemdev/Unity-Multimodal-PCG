using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using PCG.Core;
using PCG.Modules.Environment;

namespace PCG.Modules.Entities
{
    public class EntityManager : MonoBehaviour
    {
        [Header("References")] 
        [SerializeField] private EnvironmentManager _environmentManager;

        [Header("Manual Offsets")]
        [SerializeField] private float _globalHeightOffset = 0.0f;

        [Header("Prefabs / Pools")] 
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _exitPrefab;
        [SerializeField] private EntityPool _enemyPool;
        [SerializeField] private EntityPool _objectPool;

        private GameObject _currentPlayer;
        private GameObject _currentExit;
        
        // Tracks all entities spawned in the current session to manage their save states
        private List<EntityIdentifier> _currentLevelEntities = new List<EntityIdentifier>();

        private void OnEnable()
        {
            if (_environmentManager != null)
            {
                _environmentManager.OnLevelGenerated += OnLevelGeneratedHandler;
            }
        }
        
        private void OnDisable()
        {
            if (_environmentManager != null)
            {
                _environmentManager.OnLevelGenerated -= OnLevelGeneratedHandler;
            }
        }

        /// <summary>
        /// This method runs when the level is generated. It spawns all the entities
        /// </summary>
        /// <param name="spawnPoints"></param>
        private void OnLevelGeneratedHandler(NativeList<SpawnPoint> spawnPoints)
        {
            _enemyPool.DeactivateAll();
            _objectPool.DeactivateAll();
            _currentLevelEntities.Clear();
            
            int entityIdCounter = 0;

            foreach (SpawnPoint point in spawnPoints)
            {
                Vector3 basePosition = new Vector3(point.Coordinate.x, 0, point.Coordinate.y);
                Quaternion rotation = Quaternion.Euler(0, point.RotationY, 0);

                switch (point.Type)
                {
                    case EntityType.Start:
                        PlacePlayer(basePosition, rotation);
                        break;

                    case EntityType.Exit:
                        PlaceExit(basePosition, rotation);
                        break;

                    case EntityType.Enemy:
                        GameObject enemy = _enemyPool.Get();
                        PlaceAndAdjust(enemy, basePosition, rotation);
                        RegisterEntity(enemy, entityIdCounter++, EntityType.Enemy);
                        break;

                    case EntityType.Object:
                        GameObject obj = _objectPool.Get();
                        PlaceAndAdjust(obj, basePosition, rotation);
                        RegisterEntity(obj, entityIdCounter++, EntityType.Object);
                        break;
                }
            }
        }
        
        /// <summary>
        /// Attaches or updates an EntityIdentifier to keep track of this specific instance.
        /// </summary>
        private void RegisterEntity(GameObject go, int id, EntityType type)
        {
            EntityIdentifier ident = go.GetComponent<EntityIdentifier>();
            
            // If the user forgot to attach the script to the prefab, add it safely at runtime
            if (ident == null)
            {
                ident = go.AddComponent<EntityIdentifier>();
            }

            ident.ID = id;
            ident.Type = type;
            ident.IsActiveState = true; // Reset state in case it was pulled from a dirty pool
            
            _currentLevelEntities.Add(ident);
        }
        
        /// <summary>
        /// Extracts dynamic state (player transform, alive/dead entities) into the save data.
        /// </summary>
        public void FillSaveData(SaveData data)
        {
            if (_currentPlayer != null)
            {
                data.Player.Position = _currentPlayer.transform.position;
                data.Player.Rotation = _currentPlayer.transform.rotation;
            }

            data.Entities.Clear();
            foreach (var entity in _currentLevelEntities)
            {
                if (entity == null) continue;

                EntitySaveData entityData = new EntitySaveData();
                entityData.ID = entity.ID;
                entityData.EntityType = (int)entity.Type;
                
                // Entity is considered active if it hasn't been explicitly marked as dead AND is physically active
                entityData.IsActive = entity.IsActiveState && entity.gameObject.activeInHierarchy;
                
                if (entityData.IsActive)
                {
                    entityData.Position = entity.transform.position;
                    entityData.Rotation = entity.transform.rotation;
                }

                data.Entities.Add(entityData);
            }
        }

        /// <summary>
        /// Injects loaded data back into the scene, positioning player and handling entity states.
        /// </summary>
        public void LoadSaveData(SaveData data)
        {
            if (_currentPlayer != null)
            {
                CharacterController cc = _currentPlayer.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;

                _currentPlayer.transform.position = data.Player.Position;
                _currentPlayer.transform.rotation = data.Player.Rotation;

                if (cc) cc.enabled = true;
            }

            // Create a quick lookup dictionary for performance
            Dictionary<int, EntitySaveData> savedEntities = new Dictionary<int, EntitySaveData>();
            foreach (var e in data.Entities) savedEntities[e.ID] = e;

            // Apply loaded state to the freshly spawned entities
            foreach (var currentEntity in _currentLevelEntities)
            {
                if (savedEntities.TryGetValue(currentEntity.ID, out EntitySaveData savedData))
                {
                    if (!savedData.IsActive)
                    {
                        currentEntity.IsActiveState = false;
                        currentEntity.gameObject.SetActive(false); // Hide if it was dead/looted
                    }
                    else
                    {
                        currentEntity.IsActiveState = true;
                        currentEntity.gameObject.SetActive(true);

                        // Safe teleportation if the entity is an AI agent
                        UnityEngine.AI.NavMeshAgent agent = currentEntity.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null && agent.isOnNavMesh)
                        {
                            agent.Warp(savedData.Position);
                        }
                        else
                        {
                            currentEntity.transform.position = savedData.Position;
                        }
                        
                        currentEntity.transform.rotation = savedData.Rotation;
                    }
                }
            }
        }

        /// <summary>
        /// This method is an auxiliary one which simply spawns the player
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        private void PlacePlayer(Vector3 position, Quaternion rotation)
        {
            if (_currentPlayer == null)
            {
                _currentPlayer = Instantiate(_playerPrefab, position, rotation);
            }
            
            PlaceAndAdjust(_currentPlayer, position, rotation);
        }
        
        /// <summary>
        /// This method is an auxiliary one which simply spawns the exit
        /// </summary>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        private void PlaceExit(Vector3 position, Quaternion rotation)
        {
            if (_currentExit == null)
            {
                _currentExit = Instantiate(_exitPrefab, position, rotation);
            }
            
            PlaceAndAdjust(_currentExit, position, rotation);
        }

        /// <summary>
        /// This method is an auxiliary one which helps position the entities correctly above the floor
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="targetPos"></param>
        /// <param name="rotation"></param>
        private void PlaceAndAdjust(GameObject entity, Vector3 targetPos, Quaternion rotation)
        {
            // Temporarily at 0
            entity.transform.position = targetPos;
            entity.transform.rotation = rotation;

            Collider col = entity.GetComponent<Collider>();
            
            if (col == null)
            {
                col = entity.GetComponentInChildren<Collider>(); // If no collider, it searches within its children
            }

            if (col != null)
            {
                Physics.SyncTransforms(); // Force update physics just in case

                float bottomY = col.bounds.min.y; // The minimum point

                // It calculates the remaining difference to 0
                float offsetToGround = 0f - bottomY;

                entity.transform.position += new Vector3(0, offsetToGround + _globalHeightOffset, 0); // Apply
            }
        }
    }
}