using PCG.Core;
using Unity.Collections;
using UnityEngine;
using Random = Unity.Mathematics.Random; // Unity.Mathematics is faster

namespace PCG.Environment
{
    public class MazeGenerator : IGeneratorStrategy
    {
        static private readonly Vector2Int[] Directions = new Vector2Int[]
        {
            new Vector2Int(0, 1),  // North
            new Vector2Int(0, -1), // South
            new Vector2Int(1, 0),  // East
            new Vector2Int(-1, 0)  // West
        }; // These arrays will not change, thus they are static and readonly

        // This method implements an optimised Iterative Backtracker algorithm for mazes
        public MapData Generate(int seed, Vector2Int size)
        {
            Debug.Log($"[MazeGenerator] Starting with Seed: {seed}");

            MapData map = new MapData(size.x, size.y, Allocator.Persistent); // Data container creation
            Random rng = new Random((uint)seed);
            
            // At first, everything's wall
            for (int i = 0; i < map.Grid.Length; i++)
            {
                map.Grid[i] = CellType.Wall;
            }

            NativeList<int> stack = new NativeList<int>(Allocator.Temp); // It saves the path and goes back
            NativeArray<bool> visited = new NativeArray<bool>(size.x * size.y, Allocator.Temp); // To keep track of visited cells
            
            NativeArray<int> dirIndexes = new NativeArray<int>(4, Allocator.Temp);
            for (int i = 0; i < 4; i++)
            {
                dirIndexes[i] = i;
            }

            int startX = 1;
            int startY = 1;
            int startIndex = map.GetIndex(startX, startY);
            stack.Add(startIndex);
            visited[startIndex] = true;
            map.Grid[startIndex] = CellType.Floor; // Initial cell is floor

            while (stack.Length > 0)
            {
                int currentIndex = stack[stack.Length - 1];
                
                // 1D to 2D to calculate neighbours
                int currentX = currentIndex % size.x;
                int currentY = currentIndex / size.x;

                bool foundNeighbor = false;
                
                // Local Fisher-Yates Shuffle
                for (int i = 0; i < 4; i++)
                {
                    int r = rng.NextInt(i, 4);
                    int temp = dirIndexes[r];
                    dirIndexes[r] = dirIndexes[i];
                    dirIndexes[i] = temp;
                }
                
                for (int i = 0; i < 4; i++)
                {
                    int dirIdx = dirIndexes[i];
                    Vector2Int dir = Directions[dirIdx];
                    
                    int targetX = currentX + (dir.x * 2); // Jump 2 cells
                    int targetY = currentY + (dir.y * 2);
                    
                    int wallX = currentX + dir.x; // Carve intermediate cell
                    int wallY = currentY + dir.y;
                    
                    if (targetX > 0 && targetX < size.x - 1 && targetY > 0 && targetY < size.y - 1) // Check if inside the map with a 1 cell margin border
                    {
                        int targetIndex = map.GetIndex(targetX, targetY);

                        if (!visited[targetIndex])
                        {
                            visited[targetIndex] = true;
                            map.Grid[targetIndex] = CellType.Floor;
                            
                            int wallIndex = map.GetIndex(wallX, wallY);
                            map.Grid[wallIndex] = CellType.Floor; // Hacemos pasillo

                            stack.Add(targetIndex);
                            foundNeighbor = true;
                            break;
                        }
                    }
                }

                if (!foundNeighbor) // If no neighbour has been found, it backtracks
                {
                    stack.RemoveAt(stack.Length - 1);
                }
            }

            stack.Dispose(); // Clean stack
            visited.Dispose(); // Clean visited
            dirIndexes.Dispose(); // Clean dirIndexes

            return map; // Map is not visited because it is needed in the return
        }
    }
}