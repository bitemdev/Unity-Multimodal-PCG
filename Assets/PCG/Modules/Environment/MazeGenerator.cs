using PCG.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace PCG.Modules.Environment
{
    public class MazeGenerator : IGeneratorStrategy
    {
        /// <summary>
        /// This method prepares the data, schedules the Burst-compiled job, and waits for its completion.
        /// </summary>
        public MapData Generate(int seed, Vector2Int size)
        {
            UnityEngine.Debug.Log($"[MazeGenerator] Starting Burst-Compiled Maze with Seed: {seed}");

            // 1. Create the MapData container. Allocator.Persistent so it survives the job and the frame.
            MapData map = new MapData(size.x, size.y, Allocator.Persistent);

            // 2. Initialise the grid with walls before passing it to the job.
            // (We could do this in the job, but doing it here is fine or using a simple memset)
            for (int i = 0; i < map.Grid.Length; i++)
            {
                map.Grid[i] = CellType.Wall;
            }

            // 3. Configure the Job
            MazeJob mazeJob = new MazeJob
            {
                Seed = (uint)seed,
                Width = size.x,
                Height = size.y,
                Grid = map.Grid // We pass the native array pointer to the job
            };

            // 4. Schedule the job on a worker thread and immediately wait for it to finish (Complete).
            // This blocks the main thread for a microsecond, but thanks to Burst, it's virtually instant.
            JobHandle handle = mazeJob.Schedule();
            handle.Complete();

            return map;
        }

        /// <summary>
        /// The Burst-compiled job that executes the Iterative Backtracker algorithm purely in native code.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)] // Forces Unity to compile it immediately
        private struct MazeJob : IJob
        {
            public uint Seed;
            public int Width;
            public int Height;
            
            public NativeArray<CellType> Grid;

            public void Execute()
            {
                Random rng = new Random(Seed);

                // Local allocations using Allocator.Temp (extremely fast memory allocation that lives only for this frame)
                NativeList<int> stack = new NativeList<int>(Allocator.Temp);
                NativeArray<bool> visited = new NativeArray<bool>(Width * Height, Allocator.Temp);

                // Start from odd coordinates to ensure valid walls around
                int startX = 1;
                int startY = 1;
                
                int startIndex = GetIndex(startX, startY);
                visited[startIndex] = true;
                Grid[startIndex] = CellType.Floor;
                stack.Add(startIndex);

                // Native array for directions to avoid GC allocation inside the job loop
                NativeArray<int2> directions = new NativeArray<int2>(4, Allocator.Temp);
                directions[0] = new int2(0, 1);  // North
                directions[1] = new int2(0, -1); // South
                directions[2] = new int2(1, 0);  // East
                directions[3] = new int2(-1, 0); // West

                NativeArray<int> dirIndexes = new NativeArray<int>(4, Allocator.Temp);
                for (int i = 0; i < 4; i++) dirIndexes[i] = i;

                while (stack.Length > 0)
                {
                    int currentIndex = stack[stack.Length - 1];
                    int currentX = currentIndex % Width;
                    int currentY = currentIndex / Width;

                    // Shuffle directions natively
                    for (int i = 3; i > 0; i--)
                    {
                        int j = rng.NextInt(i + 1);
                        int temp = dirIndexes[i];
                        dirIndexes[i] = dirIndexes[j];
                        dirIndexes[j] = temp;
                    }

                    bool foundNeighbor = false;

                    for (int i = 0; i < 4; i++)
                    {
                        int2 dir = directions[dirIndexes[i]];
                        
                        // We check the cell that is 2 steps away
                        int targetX = currentX + (dir.x * 2);
                        int targetY = currentY + (dir.y * 2);
                        
                        // Carve intermediate cell
                        int wallX = currentX + dir.x; 
                        int wallY = currentY + dir.y;
                        
                        // Check if inside the map with a 1 cell margin border
                        if (targetX > 0 && targetX < Width - 1 && targetY > 0 && targetY < Height - 1) 
                        {
                            int targetIndex = GetIndex(targetX, targetY);

                            if (!visited[targetIndex])
                            {
                                visited[targetIndex] = true;
                                Grid[targetIndex] = CellType.Floor;
                                
                                int wallIndex = GetIndex(wallX, wallY);
                                Grid[wallIndex] = CellType.Floor; // Carve path

                                stack.Add(targetIndex);
                                foundNeighbor = true;
                                break;
                            }
                        }
                    }

                    // If no neighbour has been found, it backtracks
                    if (!foundNeighbor) 
                    {
                        stack.RemoveAt(stack.Length - 1);
                    }
                }

                // Dispose temp arrays to free up stack memory immediately
                stack.Dispose();
                visited.Dispose();
                directions.Dispose();
                dirIndexes.Dispose();
            }

            // Helper method internal to the Job
            private int GetIndex(int x, int y)
            {
                return (y * Width) + x;
            }
        }
    }
}