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
                Vector3 position = new Vector3(point.Coordinate.x, 0, point.Coordinate.y);
                Quaternion rotation = Quaternion.Euler(0, point.RotationY, 0);

                switch (point.Type)
                {
                    case EntityType.Start:
                        PlacePlayer(position, rotation);
                        break;
                    case EntityType.Enemy:
                        GameObject enemy = _enemyPool.Get();
                        enemy.transform.position = position;
                        enemy.transform.rotation = rotation;
                        break;
                    case EntityType.Object:
                        GameObject obj = _objectPool.Get();
                        obj.transform.position = position;
                        obj.transform.rotation = rotation;
                        break;
                    case EntityType.Exit:
                        PlaceExit(position, rotation);
                        break;
                }
            }
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
            else
            {
                _currentExit.transform.position = position;
                _currentExit.transform.rotation = rotation;
            }
        }
    }
}
