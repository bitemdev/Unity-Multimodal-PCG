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
                        break;

                    case EntityType.Object:
                        GameObject obj = _objectPool.Get();
                        PlaceAndAdjust(obj, basePosition, rotation);
                        break;
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