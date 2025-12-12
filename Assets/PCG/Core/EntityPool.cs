using UnityEngine;
using System.Collections.Generic;

namespace PCG.Core
{
    public class EntityPool : MonoBehaviour
    {
        [Header("Settings")] 
        [SerializeField] private GameObject _prefab;
        [SerializeField] private EntityType _entityType;
        [SerializeField] private PCGConfiguration _config;

        private Queue<GameObject> _pool = new Queue<GameObject>();
        private List<GameObject> _activeObjects = new List<GameObject>();
        
        /// <summary>
        /// This method returns the front object of the pool, activating it and inserting it into the active objects list
        /// </summary>
        public GameObject Get()
        {
            if (_pool.Count == 0)
            {
                CreateNewInstance();
            }

            GameObject current = _pool.Dequeue();
            current.SetActive(true);
            _activeObjects.Add(current);

            return current;
        }

        /// <summary>
        /// This method deactivates every object instantiated and saves it into the pool again
        /// </summary>
        public void DeactivateAll()
        {
            foreach (GameObject obj in _activeObjects)
            {
                obj.SetActive(false);
                obj.transform.SetParent(transform); // Sort hierarchy
                _pool.Enqueue(obj);
            }

            _activeObjects.Clear();
        }

        private void Awake()
        {
            if (_entityType == EntityType.Object)
            {
                for (int i = 0; i < _config.InitialObjectCount; i++)
                {
                    CreateNewInstance();
                }
            }
            else if (_entityType == EntityType.Enemy)
            {
                for (int i = 0; i < _config.InitialEnemyCount; i++)
                {
                    CreateNewInstance();
                }
            }
        }

        /// <summary>
        /// This method creates a new GameObject and inserts it into the object pool
        /// </summary>
        private void CreateNewInstance()
        {
            GameObject obj = Instantiate(_prefab, transform);
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }
    }
}