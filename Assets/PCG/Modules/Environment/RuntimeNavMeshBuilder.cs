using UnityEngine;
using Unity.AI.Navigation;
using Unity.AI;
using UnityEngine.AI;

namespace PCG.Modules.Environment
{
    [RequireComponent(typeof(NavMeshSurface))]
    public class RuntimeNavMeshBuilder : MonoBehaviour
    {
        [SerializeField] private LayerMask _floorLayer;
        
        private NavMeshSurface _navMeshSurface;

        /// <summary>
        /// This method simply tells the NavMeshSurface to build the navigation mesh. It's an expensive method.
        /// </summary>
        public void BuildNavMesh()
        {
            _navMeshSurface.layerMask = _floorLayer;
            _navMeshSurface.BuildNavMesh();
        }

        /// <summary>
        /// This method updates the existent navigation mesh data instead of creating a new one.
        /// </summary>
        public void UpdateNavMeshData()
        {
            _navMeshSurface.UpdateNavMesh(_navMeshSurface.navMeshData);
        }

        private void Awake()
        {
            _navMeshSurface = GetComponent<NavMeshSurface>();
            
            _navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; // Use physics colliders, which are way more precise than code-generated meshes

            _navMeshSurface.agentTypeID = 0; // Default Humanoid
            _navMeshSurface.overrideVoxelSize = true;
            _navMeshSurface.voxelSize = 0.05f; // This allows a more precise detection, going through more thin spaces

            if (_floorLayer == 0) // Nothing's assigned
            {
                int floorLayer = LayerMask.NameToLayer("PCG_Floor");

                if (floorLayer != -1)
                {
                    _floorLayer = 1 << floorLayer; // It pushes "floorLayer" number of bits (in binary) to the left, basically it changes to floorLayer
                }
                else
                {
                    _floorLayer = LayerMask.GetMask("Default");
                }
            }

            _navMeshSurface.layerMask = _floorLayer;
        }
    }
}
