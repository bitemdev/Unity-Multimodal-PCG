using System;
using System.Collections.Generic;
using UnityEngine;

namespace PCG.Core
{
    /// <summary>
    /// Core data structure for saving the game state. 
    /// It must be serializable to be converted to JSON.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public string SaveName;
        public string Timestamp;

        public GenerationSaveData Generation;
        public PlayerSaveData Player;
        public List<EntitySaveData> Entities;

        public SaveData()
        {
            Generation = new GenerationSaveData();
            Player = new PlayerSaveData();
            Entities = new List<EntitySaveData>();
        }
    }

    [Serializable]
    public class GenerationSaveData
    {
        public int Seed;
        public string Algorithm;
    }

    [Serializable]
    public class PlayerSaveData
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    [Serializable]
    public class EntitySaveData
    {
        public int ID;
        public int EntityType; // Casted to int to avoid JSON serialization issues with Enums
        public bool IsActive;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}