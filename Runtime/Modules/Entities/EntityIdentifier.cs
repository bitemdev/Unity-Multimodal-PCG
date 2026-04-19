using UnityEngine;
using PCG.Core;

namespace PCG.Modules.Entities
{
    /// <summary>
    /// Attached to instanced prefabs to give them a unique identity for the save system.
    /// </summary>
    public class EntityIdentifier : MonoBehaviour
    {
        [Tooltip("Unique ID assigned by the EntityManager upon spawning.")]
        public int ID;

        [Tooltip("The type of the entity (Enemy, Object).")]
        public EntityType Type;

        [Tooltip("Defines if the enemy is alive or the chest is unlooted.")]
        public bool IsActiveState = true;

        /// <summary>
        /// This method is called when the entity dies or the object is looted.
        /// </summary>
        public void MarkAsDeadOrLooted()
        {
            IsActiveState = false;
        }
    }
}