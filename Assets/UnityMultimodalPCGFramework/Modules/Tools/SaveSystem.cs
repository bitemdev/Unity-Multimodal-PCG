using System.IO;
using UnityEngine;
using PCG.Core;

namespace PCG.Modules.Tools
{
    /// <summary>
    /// Static class responsible for reading and writing SaveData to the local disk.
    /// </summary>
    public static class SaveSystem
    {
        // Platform-independent path for saving files
        private static string SaveFolder => Path.Combine(Application.persistentDataPath, "PCG_Saves");

        /// <summary>
        /// Serializes and saves the game state to a JSON file.
        /// </summary>
        public static void SaveGame(SaveData data, string fileName = "QuickSave")
        {
            if (!Directory.Exists(SaveFolder))
            {
                Directory.CreateDirectory(SaveFolder);
            }

            string json = JsonUtility.ToJson(data, true); // true for pretty print
            string fullPath = Path.Combine(SaveFolder, fileName + ".json");
            
            File.WriteAllText(fullPath, json);
            UnityEngine.Debug.Log($"[SaveSystem] Game successfully saved at: {fullPath}");
        }

        /// <summary>
        /// Loads and deserializes the game state from a JSON file.
        /// </summary>
        public static SaveData LoadGame(string fileName = "QuickSave")
        {
            string fullPath = Path.Combine(SaveFolder, fileName + ".json");

            if (!File.Exists(fullPath))
            {
                UnityEngine.Debug.LogWarning($"[SaveSystem] Save file not found at: {fullPath}");
                return null;
            }

            string json = File.ReadAllText(fullPath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            
            UnityEngine.Debug.Log($"[SaveSystem] Game loaded: {data.SaveName} (Seed: {data.Generation.Seed})");
            return data;
        }
    }
}