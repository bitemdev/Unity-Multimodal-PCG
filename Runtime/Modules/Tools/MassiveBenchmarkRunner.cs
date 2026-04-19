using System.Collections;
using UnityEngine;
using PCG.Modules.Environment;

namespace PCG.Modules.Tools
{
    /// <summary>
    /// Automates the benchmarking process by running massive amounts of procedural generations
    /// across different algorithms and sizes to gather statistically significant data.
    /// </summary>
    [RequireComponent(typeof(EnvironmentManager))]
    public class MassiveBenchmarkRunner : MonoBehaviour
    {
        [Header("Benchmark Settings")]
        [Tooltip("Number of generations per configuration to get an accurate average.")]
        [SerializeField] private int _iterationsPerConfig = 10;

        [Tooltip("The map sizes to test (e.g., 20, 50, 100, 150).")]
        [SerializeField] private int[] _mapSizesToTest = { 20, 50, 100, 150 };

        private EnvironmentManager _envManager;

        private void Awake()
        {
            _envManager = GetComponent<EnvironmentManager>();
        }

        /// <summary>
        /// Trigger this method via Context Menu to start the massive data collection.
        /// </summary>
        [ContextMenu("Run Massive Benchmark")]
        public void RunMassiveBenchmark()
        {
            StartCoroutine(BenchmarkCoroutine());
        }

        private IEnumerator BenchmarkCoroutine()
        {
            UnityEngine.Debug.Log("<color=cyan>[MassiveBenchmark] Starting automated data collection...</color>");
            float startTime = Time.realtimeSinceStartup;
            int totalRuns = 0;

            // Extract the PCGConfiguration to temporarily override its values
            // Note: In a strict architecture, we might use reflection or a specific public method, 
            // but we assume the config is accessible or we alter the manager state.

            // Loop through algorithms
            GenerationAlgorithm[] algorithms = { GenerationAlgorithm.Maze_Backtracker, GenerationAlgorithm.Dungeon_BSP };

            foreach (GenerationAlgorithm algo in algorithms)
            {
                // We use reflection here just to forcibly change the private algorithm field for the test
                var algoField = typeof(EnvironmentManager).GetField("_algorithmType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (algoField != null) algoField.SetValue(_envManager, algo);

                // Loop through map sizes
                foreach (int size in _mapSizesToTest)
                {
                    // Forcibly change config size (Warning: this modifies the ScriptableObject temporarily)
                    var configField = typeof(EnvironmentManager).GetField("_config", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    PCG.Core.PCGConfiguration config = (PCG.Core.PCGConfiguration)configField.GetValue(_envManager);

                    var widthField = typeof(PCG.Core.PCGConfiguration).GetField("_width", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var heightField = typeof(PCG.Core.PCGConfiguration).GetField("_height", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (widthField != null) widthField.SetValue(config, size);
                    if (heightField != null) heightField.SetValue(config, size);

                    UnityEngine.Debug.Log($"[MassiveBenchmark] Testing {algo} at Size {size}x{size} for {_iterationsPerConfig} iterations...");

                    // Loop through iterations for statistical average
                    for (int i = 0; i < _iterationsPerConfig; i++)
                    {
                        // Pass -1 to use a random seed each time
                        _envManager.GenerateLevelDeterministic(-1);
                        totalRuns++;

                        // Yield to next frame to prevent Unity from freezing and crashing
                        yield return null;
                    }
                }
            }

            float totalTime = Time.realtimeSinceStartup - startTime;
            UnityEngine.Debug.Log($"<color=green>[MassiveBenchmark] Finished! Executed {totalRuns} generations in {totalTime:F2} seconds.</color>");
            UnityEngine.Debug.Log("Check your persistentDataPath for the GenerationBenchmark.csv file.");
        }
    }
}