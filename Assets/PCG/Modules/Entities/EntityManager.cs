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
        /// This method repositions every entity at the start of the level
        /// </summary>
        private void OnLevelGeneratedHandler(NativeList<SpawnPoint> spawnPoints)
        {
            // Clean previous
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
                        enemy.transform.position = AdjustYPosition(enemy, basePosition);
                        enemy.transform.rotation = rotation;
                        break;
                    case EntityType.Object:
                        GameObject obj = _objectPool.Get();
                        obj.transform.position = AdjustYPosition(obj, basePosition);
                        obj.transform.rotation = rotation;
                        break;
                }
            }
        }
        
        /// <summary>
        /// This method calculates the correct Y position so the object rests perfectly on the floor, regardless of where its Pivot is (Center or Feet).
        /// </summary>
        private Vector3 AdjustYPosition(GameObject entity, Vector3 gridPosition)
        {
            float actualFloorY = 0f;
    
            RaycastHit hit;
            Vector3 origin = new Vector3(gridPosition.x, 10f, gridPosition.z);
    
            if (Physics.Raycast(origin, Vector3.down, out hit, 20f))
            {
                actualFloorY = hit.point.y;
            }
            else
            {
                actualFloorY = 0.2f; 
            }

            float pivotOffset = 0f;
            Collider col = entity.GetComponent<Collider>();
    
            if (col != null)
            {
                Physics.SyncTransforms(); // Force physics update, just in case
        
                float distPivotToBottom = entity.transform.position.y - col.bounds.min.y;

                if (distPivotToBottom > 0)
                {
                    pivotOffset = distPivotToBottom;
                }
            }

            return new Vector3(gridPosition.x, actualFloorY + pivotOffset + 0.001f, gridPosition.z);
        }

        /// <summary>
        /// This method instantiates/places the player once the level is generated
        /// </summary>
        private void PlacePlayer(Vector3 position, Quaternion rotation)
        {
            if (_currentPlayer == null)
            {
                _currentPlayer = Instantiate(_playerPrefab, position, rotation);
            }
            else
            {
                CharacterController characterController = _currentPlayer.GetComponent<CharacterController>();

                if (characterController) // If player has CharacterController, it needs to be inactive in order to tp it
                {
                    characterController.enabled = false;
                }

                _currentPlayer.transform.position = position;
                _currentPlayer.transform.position = AdjustYPosition(_currentPlayer, position);
                _currentPlayer.transform.rotation = rotation;
                
                if (characterController)
                {
                    characterController.enabled = true;
                }
            }
        }
        
        /// <summary>
        /// This method instantiates/places the exit once the level is generated
        /// </summary>
        private void PlaceExit(Vector3 position, Quaternion rotation)
        {
            if (_currentExit == null)
            {
                _currentExit = Instantiate(_exitPrefab, position, rotation);
            }
            
            _currentExit.transform.position = AdjustYPosition(_currentExit, position);
            _currentExit.transform.rotation = rotation;
        }
    }
}
