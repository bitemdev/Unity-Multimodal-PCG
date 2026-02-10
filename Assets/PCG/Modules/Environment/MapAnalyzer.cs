using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using PCG.Core;

namespace PCG.Modules.Environment
{
    public static class MapAnalyzer
    {
        /// <summary>
        /// This method analyses the map and gets the optimal spawn points to ensure good playability
        /// </summary>
        /// <param name="map"></param>
        /// <param name="enemyCount"></param>
        /// <param name="objectCount"></param>
        /// <param name="allocator"></param>
        /// <returns></returns>
        public static NativeList<SpawnPoint> GetOptimalSpawnPoints(MapData map, int enemyCount, int objectCount, Allocator allocator)
        {
            NativeList<SpawnPoint> results = new NativeList<SpawnPoint>(allocator);
            
            // Search for far away start and exit points
            var endPoints = GetStartAndExit(map);
            int2 startPos = endPoints.start;
            int2 exitPos = endPoints.exit;

            results.Add(new SpawnPoint(startPos, EntityType.Start, 0));
            results.Add(new SpawnPoint(exitPos, EntityType.Exit, 0));

            // Occupied positions to ensure not to overlap
            NativeList<int2> occupiedPositions = new NativeList<int2>(allocator);
            occupiedPositions.Add(startPos);
            occupiedPositions.Add(exitPos);
            
            NativeList<int2> deadEnds = GetDeadEnds(map, allocator);
            
            // Objects placement
            int itemsPlaced = 0;
            
            // Try to place at dead-ends (as they are the best positions)
            foreach (int2 deadEnd in deadEnds)
            {
                if (itemsPlaced >= objectCount)
                {
                    break;
                }

                if (IsOccupied(deadEnd, occupiedPositions))
                {
                    continue;
                }

                results.Add(new SpawnPoint(deadEnd, EntityType.Object, 0));
                occupiedPositions.Add(deadEnd);
                itemsPlaced++;
            }
            
            // If there are no dead-ends, place anywhere
            if (itemsPlaced < objectCount)
            {
                NativeList<int2> allFloors = GetFreeFloorCells(map, occupiedPositions, allocator);
                Shuffle(allFloors);

                int fallbackIndex = 0;
                while (itemsPlaced < objectCount && fallbackIndex < allFloors.Length)
                {
                    int2 pos = allFloors[fallbackIndex];
                    results.Add(new SpawnPoint(pos, EntityType.Object, 0));
                    occupiedPositions.Add(pos);
                    itemsPlaced++;
                    fallbackIndex++;
                }
                allFloors.Dispose();
            }

            // Enemies placement
            NativeList<int2> enemyCandidates = GetFreeFloorCells(map, occupiedPositions, allocator);
            Shuffle(enemyCandidates);

            int enemiesPlaced = 0;
            for (int i = 0; i < enemyCandidates.Length; i++)
            {
                if (enemiesPlaced >= enemyCount)
                {
                    break;
                }

                int2 pos = enemyCandidates[i];
                
                float randomRot = UnityEngine.Random.Range(0, 4) * 90f;
                results.Add(new SpawnPoint(pos, EntityType.Enemy, randomRot));
                enemiesPlaced++;
            }

            deadEnds.Dispose();
            occupiedPositions.Dispose();
            enemyCandidates.Dispose();

            return results;
        }
        
        /// <summary>
        /// This method is a helper one which returns whether a position is occupied or not
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="occupied"></param>
        /// <returns></returns>
        private static bool IsOccupied(int2 pos, NativeList<int2> occupied)
        {
            for (int i = 0; i < occupied.Length; i++)
            {
                if (occupied[i].Equals(pos))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// This method is a helper one which returns all the dead-ends within the map
        /// </summary>
        /// <param name="map"></param>
        /// <param name="allocator"></param>
        /// <returns></returns>
        private static NativeList<int2> GetDeadEnds(MapData map, Allocator allocator)
        {
            NativeList<int2> deadEnds = new NativeList<int2>(allocator);
            int2[] directions = { new int2(0, 1), new int2(0, -1), new int2(-1, 0), new int2(1, 0) };

            for (int x = 1; x < map.Width - 1; x++)
            {
                for (int y = 1; y < map.Height - 1; y++)
                {
                    if (map.Grid[map.GetIndex(x, y)] == CellType.Wall)
                    {
                        continue;
                    }

                    int wallCount = 0;
                    foreach (var dir in directions)
                    {
                        if (map.Grid[map.GetIndex(x + dir.x, y + dir.y)] == CellType.Wall)
                        {
                            wallCount++;
                        }
                    }

                    if (wallCount >= 3) // If it's a dead-end (3 walls surrounding the cell
                    {
                        deadEnds.Add(new int2(x, y));
                    }
                }
            }
            return deadEnds;
        }

        /// <summary>
        /// This method is a helper one which returns both the start and the exit of the map, ensuring they both are far away from each other
        /// </summary>
        /// <param name="map"></param>
        /// <returns></returns>
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

        /// <summary>
        /// This method is a helper one which returns free cells within the map
        /// </summary>
        /// <param name="map"></param>
        /// <param name="occupied"></param>
        /// <param name="allocator"></param>
        /// <returns></returns>
        private static NativeList<int2> GetFreeFloorCells(MapData map, NativeList<int2> occupied, Allocator allocator)
        {
            NativeList<int2> floors = new NativeList<int2>(allocator);
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    if (map.Grid[map.GetIndex(x, y)] == CellType.Floor)
                    {
                        int2 pos = new int2(x, y);
                        if (!IsOccupied(pos, occupied))
                        {
                            floors.Add(pos);
                        }
                    }
                }
            }
            
            return floors;
        }

        /// <summary>
        /// This method is a helper one which simply shuffles a list
        /// </summary>
        /// <param name="list"></param>
        /// <typeparam name="T"></typeparam>
        private static void Shuffle<T>(NativeList<T> list) where T : unmanaged
        {
            var rng = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
            for (int i = list.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}