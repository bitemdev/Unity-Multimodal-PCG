using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using PCG.Core;

namespace PCG.Modules.Environment
{
    public static class MapAnalyzer
    {
        /// <summary>
        /// Analyses the map to find the optimal spawn points using the Burst Compiler for maximum performance.
        /// </summary>
        public static NativeList<SpawnPoint> GetOptimalSpawnPoints(MapData map, int enemyCount, int objectCount, Allocator allocator)
        {
            NativeList<SpawnPoint> results = new NativeList<SpawnPoint>(allocator);
            
            // 1. Find Start and Exit points (Simple sequential search, fast enough on main thread)
            var endPoints = GetStartAndExit(map);
            int2 startPos = endPoints.start;
            int2 exitPos = endPoints.exit;

            results.Add(new SpawnPoint(startPos, EntityType.Start, 0));
            results.Add(new SpawnPoint(exitPos, EntityType.Exit, 0));

            // Occupied positions to avoid overlapping entities
            NativeList<int2> occupiedPositions = new NativeList<int2>(allocator);
            occupiedPositions.Add(startPos);
            occupiedPositions.Add(exitPos);

            // 2. Schedule Burst Jobs for heavy grid analysis
            NativeList<int2> deadEnds = new NativeList<int2>(allocator);
            NativeList<int2> freeFloors = new NativeList<int2>(allocator);

            // Job to find dead ends (corridors with 3 walls)
            FindDeadEndsJob deadEndsJob = new FindDeadEndsJob
            {
                Width = map.Width,
                Height = map.Height,
                Grid = map.Grid,
                DeadEnds = deadEnds
            };
            
            // Job to find all available floor cells
            FindFreeFloorsJob freeFloorsJob = new FindFreeFloorsJob
            {
                Width = map.Width,
                Height = map.Height,
                Grid = map.Grid,
                FreeFloors = freeFloors
            };

            // Schedule both jobs to run in parallel on worker threads
            JobHandle deadEndsHandle = deadEndsJob.Schedule();
            JobHandle freeFloorsHandle = freeFloorsJob.Schedule();
            
            // Wait for both jobs to finish before proceeding
            JobHandle.CompleteAll(ref deadEndsHandle, ref freeFloorsHandle);
            
            // --- SHUFFLE LISTS TO AVOID LINEAR PLACEMENT ---
            Shuffle(deadEnds);
            Shuffle(freeFloors);

            // --- LOOT PLACEMENT ---
            int itemsPlaced = 0;
            
            // Priority 1: Place loot in dead ends
            for (int i = 0; i < deadEnds.Length; i++)
            {
                if (itemsPlaced >= objectCount) break;
                
                int2 pos = deadEnds[i];
                if (IsOccupied(pos, occupiedPositions)) continue;

                results.Add(new SpawnPoint(pos, EntityType.Object, 0));
                occupiedPositions.Add(pos);
                itemsPlaced++;
            }
            
            // Priority 2: Fallback to random free floors if not enough dead ends exist
            if (itemsPlaced < objectCount)
            {
                for (int i = 0; i < freeFloors.Length; i++)
                {
                    if (itemsPlaced >= objectCount) break;
                    
                    int2 pos = freeFloors[i];
                    if (IsOccupied(pos, occupiedPositions)) continue;

                    results.Add(new SpawnPoint(pos, EntityType.Object, 0));
                    occupiedPositions.Add(pos);
                    itemsPlaced++;
                }
            }

            // --- ENEMY PLACEMENT ---
            int enemiesPlaced = 0;
            
            for (int i = 0; i < freeFloors.Length; i++)
            {
                if (enemiesPlaced >= enemyCount) break;

                int2 pos = freeFloors[i];
                if (IsOccupied(pos, occupiedPositions)) continue;

                // Random rotation facing one of the 4 cardinal directions
                float randomRot = UnityEngine.Random.Range(0, 4) * 90f;
                results.Add(new SpawnPoint(pos, EntityType.Enemy, randomRot));
                occupiedPositions.Add(pos);
                enemiesPlaced++;
            }

            // Clean up memory
            deadEnds.Dispose();
            freeFloors.Dispose();
            occupiedPositions.Dispose();

            return results;
        }

        // ==========================================
        // BURST COMPILED JOBS
        // ==========================================

        [BurstCompile(CompileSynchronously = true)]
        private struct FindDeadEndsJob : IJob
        {
            [ReadOnly] public int Width;
            [ReadOnly] public int Height;
            [ReadOnly] public NativeArray<CellType> Grid;
            
            public NativeList<int2> DeadEnds;

            public void Execute()
            {
                NativeArray<int2> directions = new NativeArray<int2>(4, Allocator.Temp);
                directions[0] = new int2(0, 1);
                directions[1] = new int2(0, -1);
                directions[2] = new int2(1, 0);
                directions[3] = new int2(-1, 0);

                for (int x = 1; x < Width - 1; x++)
                {
                    for (int y = 1; y < Height - 1; y++)
                    {
                        if (Grid[(y * Width) + x] == CellType.Wall) continue;

                        int wallCount = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            int2 dir = directions[i];
                            if (Grid[((y + dir.y) * Width) + (x + dir.x)] == CellType.Wall)
                            {
                                wallCount++;
                            }
                        }

                        if (wallCount >= 3)
                        {
                            DeadEnds.Add(new int2(x, y));
                        }
                    }
                }
                
                directions.Dispose();
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct FindFreeFloorsJob : IJob
        {
            [ReadOnly] public int Width;
            [ReadOnly] public int Height;
            [ReadOnly] public NativeArray<CellType> Grid;
            
            public NativeList<int2> FreeFloors;

            public void Execute()
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        if (Grid[(y * Width) + x] == CellType.Floor)
                        {
                            FreeFloors.Add(new int2(x, y));
                        }
                    }
                }
            }
        }

        // ==========================================
        // HELPER METHODS
        // ==========================================

        private static bool IsOccupied(int2 pos, NativeList<int2> occupied)
        {
            for (int i = 0; i < occupied.Length; i++)
            {
                if (occupied[i].Equals(pos)) return true;
            }
            return false;
        }

        private static (int2 start, int2 exit) GetStartAndExit(MapData map)
        {
            int2 start = int2.zero; 
            int2 exit = int2.zero;
            
            for (int i = 0; i < map.Grid.Length; i++) 
            {
                if (map.Grid[i] == CellType.Floor) 
                {
                    start = new int2(i % map.Width, i / map.Width);
                    break;
                }
            }
            
            for (int i = map.Grid.Length - 1; i >= 0; i--) 
            {
                if (map.Grid[i] == CellType.Floor) 
                {
                    exit = new int2(i % map.Width, i / map.Width);
                    break;
                }
            }
            
            return (start, exit);
        }

        private static void Shuffle<T>(NativeList<T> list) where T : unmanaged
        {
            var rng = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
            for (int i = list.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}