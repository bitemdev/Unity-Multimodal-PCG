using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PCG.Core
{
    public static class MapAnalyzer
    {
        // This method implements an optimised 2-Pass BFS, assuring the maximum playability as the spawn and the exit are in opposite positions. It returns an optimised list containing the player spawn point and the exit
        public static NativeList<SpawnPoint> GetOptimalSpawnPoints(MapData map, Allocator allocator)
        {
            NativeList<SpawnPoint> results = new NativeList<SpawnPoint>(allocator);

            int2 randomFloor = FindFirstFloor(map); // Random floor to start, better if it's the centre
            if (randomFloor.x == -1) // No map
            {
                return results;
            }

            int2 startNode = RunFloodFill(map, randomFloor, out int maxDistA); // Search for the start
            int2 endNode = RunFloodFill(map, startNode, out int maxDistB); // From start, search for opposite point (exit)

            // Save results
            results.Add(new SpawnPoint { Coordinate = startNode, Type = EntityType.Start });
            results.Add(new SpawnPoint { Coordinate = endNode, Type = EntityType.Exit });

            Debug.Log($"[MapAnalyzer] Maximum distance from start to exit: {maxDistB} steps.");

            return results;
        }

        // This method runs an optimised version of BFS, returning both the farthest point and the distance it takes to go from start till there
        private static int2 RunFloodFill(MapData map, int2 startPos, out int maxDistanceFound)
        {
            NativeArray<int> distances = new NativeArray<int>(map.Grid.Length, Allocator.Temp); // Temp because it only lives within the execution of this method
            NativeQueue<int2> queue = new NativeQueue<int2>(Allocator.Temp);

            ArrayInit(distances, -1); 

            int startIndex = map.GetIndex(startPos.x, startPos.y);
            distances[startIndex] = 0;
            queue.Enqueue(startPos);

            int2 furthestNode = startPos;
            int maxDist = 0;

            int2[] directions = { new int2(0, 1), new int2(0, -1), new int2(1, 0), new int2(-1, 0) };

            while (queue.TryDequeue(out int2 current))
            {
                int currIndex = map.GetIndex(current.x, current.y);
                int currentDist = distances[currIndex];

                if (currentDist > maxDist)
                {
                    maxDist = currentDist;
                    furthestNode = current;
                }

                for (int i = 0; i < 4; i++)
                {
                    int2 neighbour = current + directions[i];
                    if (neighbour.x < 0 || neighbour.y < 0 || neighbour.x >= map.Width || neighbour.y >= map.Height)
                    {
                        continue;
                    }

                    int neighbourIndex = map.GetIndex(neighbour.x, neighbour.y);
                    
                    if (map.Grid[neighbourIndex] == CellType.Floor && distances[neighbourIndex] == -1)
                    {
                        distances[neighbourIndex] = currentDist + 1;
                        queue.Enqueue(neighbour);
                    }
                }
            }

            distances.Dispose();
            queue.Dispose();
            
            maxDistanceFound = maxDist;
            return furthestNode;
        }

        // This method initialises an array with a given value
        private static void ArrayInit(NativeArray<int> array, int value)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }

        // This method searches from the centre out for the first walkable floor
        private static int2 FindFirstFloor(MapData map)
        {
            int centerX = map.Width / 2;
            int centerY = map.Height / 2;

            if (map.Grid[map.GetIndex(centerX, centerY)] == CellType.Floor)
            {
                return new int2(centerX, centerY);
            }

            for (int i = 0; i < map.Grid.Length; i++)
            {
                if (map.Grid[i] == CellType.Floor)
                {
                    return new int2(i % map.Width, i / map.Width);
                }
            }
            return new int2(-1, -1);
        }
    }
}