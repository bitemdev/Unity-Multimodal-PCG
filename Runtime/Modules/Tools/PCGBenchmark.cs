using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace PCG.Modules.Tools
{
    /// <summary>
    /// Professional profiling tool designed to track execution time and memory allocation
    /// during the procedural generation process. Outputs data to a CSV file for academic analysis.
    /// </summary>
    public class PCGBenchmark
    {
        private Stopwatch _globalTimer;
        private Stopwatch _phaseTimer;

        private Dictionary<string, double> _phaseTimes;
        private string _currentPhase;
        private long _initialMemory;

        private int _seed;
        private int _width;
        private int _height;
        private string _algorithm;

        // Platform-independent path for saving benchmark reports
        private static string ReportFolder => Path.Combine(Application.persistentDataPath, "PCG_Benchmarks");
        private static string ReportFilePath => Path.Combine(ReportFolder, "GenerationBenchmark.csv");

        public PCGBenchmark(int seed, int width, int height, string algorithm)
        {
            _seed = seed;
            _width = width;
            _height = height;
            _algorithm = algorithm;

            _globalTimer = new Stopwatch();
            _phaseTimer = new Stopwatch();
            _phaseTimes = new Dictionary<string, double>();

            // Force Garbage Collection before starting to get an accurate baseline memory reading
            GC.Collect();
            GC.WaitForPendingFinalizers();
            _initialMemory = GC.GetTotalMemory(true);

            _globalTimer.Start();
        }

        /// <summary>
        /// Starts timing a specific generation phase.
        /// </summary>
        public void StartPhase(string phaseName)
        {
            _currentPhase = phaseName;
            _phaseTimer.Restart();
        }

        /// <summary>
        /// Stops the current phase timer and records the elapsed time.
        /// </summary>
        public void StopPhase()
        {
            _phaseTimer.Stop();
            if (!string.IsNullOrEmpty(_currentPhase))
            {
                _phaseTimes[_currentPhase] = _phaseTimer.Elapsed.TotalMilliseconds;
            }
        }

        /// <summary>
        /// Finishes the benchmark, logs the results to the Unity Console, and appends them to a CSV file.
        /// </summary>
        public void FinishAndExport()
        {
            _globalTimer.Stop();

            // Calculate memory difference
            long finalMemory = GC.GetTotalMemory(false);
            long allocatedMemoryBytes = finalMemory - _initialMemory;
            double allocatedMemoryMB = allocatedMemoryBytes / (1024.0 * 1024.0);

            // 1. Console Logging
            StringBuilder log = new StringBuilder();
            log.AppendLine($"<b>[PCG Benchmark Report]</b> Seed: {_seed} | Algorithm: {_algorithm} | Size: {_width}x{_height}");
            log.AppendLine($"Total Time: {_globalTimer.Elapsed.TotalMilliseconds:F2} ms");
            log.AppendLine($"Allocated Memory (GC): {Mathf.Max(0, (float)allocatedMemoryMB):F4} MB");
            log.AppendLine("--- Phases ---");

            foreach (var phase in _phaseTimes)
            {
                log.AppendLine($"- {phase.Key}: {phase.Value:F2} ms");
            }

            UnityEngine.Debug.Log(log.ToString());

            // 2. CSV Exporting
            ExportToCSV(allocatedMemoryMB);
        }

        private void ExportToCSV(double allocatedMemoryMB)
        {
            bool writeHeaders = false;

            if (!Directory.Exists(ReportFolder))
            {
                Directory.CreateDirectory(ReportFolder);
            }

            if (!File.Exists(ReportFilePath))
            {
                writeHeaders = true;
            }

            using (StreamWriter writer = new StreamWriter(ReportFilePath, true))
            {
                if (writeHeaders)
                {
                    // Define CSV columns based on tracked phases
                    string headers = "Timestamp,Seed,Algorithm,Width,Height,TotalTime(ms),MemoryAllocated(MB)";
                    foreach (var phase in _phaseTimes.Keys)
                    {
                        headers += $",{phase}(ms)";
                    }
                    writer.WriteLine(headers);
                }

                // Write data row
                string row = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss},{_seed},{_algorithm},{_width},{_height},{_globalTimer.Elapsed.TotalMilliseconds:F2},{Mathf.Max(0, (float)allocatedMemoryMB):F4}";
                foreach (var phase in _phaseTimes.Values)
                {
                    row += $",{phase:F2}";
                }

                writer.WriteLine(row);
            }

            UnityEngine.Debug.Log($"[PCG Benchmark] Data appended to CSV at: {ReportFilePath}");
        }
    }
}