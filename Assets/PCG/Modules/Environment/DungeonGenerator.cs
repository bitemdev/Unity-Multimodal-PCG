using PCG.Core;
using Unity.Collections;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace PCG.Modules.Environment
{
    public class DungeonGenerator : IGeneratorStrategy
    {
        private const int MinNodeSize = 15; // Min space for Room + Walls
        private const int MinRoomSize = 6; // Min space for Room
        private const int RoomMargin = 2; // Margin between rooms -> Higher value, more separated rooms with longer corridors
        private const int MinCorridorWidth = 1;
        private const int MaxCorridorWidth = 3;

        // Struct is used because it does not generate garbage + is contained within the stack or nativearrays (way faster than classes)
        private struct BSPNode
        {
            public int ID; // List index
            public RectInt Bounds; // Total area
            public RectInt Room; // Carved room
            
            // Integers are way more optimised than references
            public int LeftChildIndex;
            public int RightChildIndex;
            public bool IsLeaf => LeftChildIndex == -1 && RightChildIndex == -1;
        }

        /// <summary>
        /// This method generates a dungeon map using an optimised modification of BSP (iterative + garbage collection)
        /// </summary>
        public MapData Generate(int seed, Vector2Int size)
        {
            Debug.Log($"[DungeonGenerator] Starting optimised BSP with Seed: {seed}");

            MapData map = new MapData(size.x, size.y, Allocator.Persistent);
            Random rng = new Random((uint)seed);

            for (int i = 0; i < map.Grid.Length; i++)
            {
                map.Grid[i] = CellType.Wall;
            }
            
            int estimatedCapacity = (size.x * size.y) / (MinNodeSize * MinNodeSize) * 2; // Worst case scenario capacity. Precalculating this makes the code faster, as it will not redimensionate memory
            NativeList<BSPNode> tree = new NativeList<BSPNode>(estimatedCapacity, Allocator.Temp); // Temp is used because it only lives during this method
            BSPNode root = new BSPNode
            {
                ID = 0,
                Bounds = new RectInt(2, 2, size.x - 4, size.y - 4), // Security margin for borders
                LeftChildIndex = -1, RightChildIndex = -1
            }; // Root node
            tree.Add(root);

            int processIndex = 0; // This simulates a Queue without actually deleting elements
            
            while (processIndex < tree.Length)
            {
                BSPNode currentNode = tree[processIndex]; // Copy of the current node

                ProcessSplit(ref tree, currentNode, ref rng); // Try to split space
                
                processIndex++; // Next node
            }

            // Organic room building
            for (int i = 0; i < tree.Length; i++)
            {
                BSPNode node = tree[i];
                if (node.IsLeaf)
                {
                    int maxW = node.Bounds.width - (RoomMargin * 2);
                    int maxH = node.Bounds.height - (RoomMargin * 2);

                    if (maxW < MinRoomSize)
                    {
                        maxW = MinRoomSize;
                    }

                    if (maxH < MinRoomSize)
                    {
                        maxH = MinRoomSize;
                    }

                    int w = rng.NextInt(MinRoomSize, maxW + 1);
                    int h = rng.NextInt(MinRoomSize, maxH + 1);

                    int freeX = maxW - w;
                    int freeY = maxH - h;
                    
                    int x = node.Bounds.x + RoomMargin + (freeX > 0 ? rng.NextInt(0, freeX + 1) : 0);
                    int y = node.Bounds.y + RoomMargin + (freeY > 0 ? rng.NextInt(0, freeY + 1) : 0);

                    node.Room = new RectInt(x, y, w, h);
                    tree[i] = node; // Struct update
                    
                    PaintRoom(map, node.Room);
                }
            }

            // Iterate backwards to let parent know where children are
            for (int i = tree.Length - 1; i >= 0; i--)
            {
                BSPNode node = tree[i];
                if (!node.IsLeaf)
                {
                    BSPNode childSource = rng.NextBool() ? tree[node.LeftChildIndex] : tree[node.RightChildIndex]; // Randomly decide if parent connects with left or right child
                    node.Room = childSource.Room;
                    tree[i] = node; // Struct update
                }
            }

            // Every parent knows where their children are at, now it connects brothers
            for (int i = 0; i < tree.Length; i++)
            {
                BSPNode node = tree[i];
                if (!node.IsLeaf)
                {
                    BSPNode left = tree[node.LeftChildIndex];
                    BSPNode right = tree[node.RightChildIndex];

                    ConnectRooms(map, left.Room, right.Room, ref rng);
                }
            }

            tree.Dispose(); // Clean up memory
            return map;
        }

        /// <summary>
        /// This method determines whether a given space can be split or not. If yes, it splits it
        /// </summary>
        private void ProcessSplit(ref NativeList<BSPNode> tree, BSPNode parent, ref Random rng)
        {
            bool canSplitH = parent.Bounds.height >= MinNodeSize * 2;
            bool canSplitV = parent.Bounds.width >= MinNodeSize * 2;

            if (!canSplitH && !canSplitV)
            {
                return;
            }

            bool splitH = rng.NextBool();
            if (parent.Bounds.width > parent.Bounds.height * 1.25f)
            {
                splitH = false;
            }
            else if (parent.Bounds.height > parent.Bounds.width * 1.25f)
            {
                splitH = true;
            }

            if (splitH && !canSplitH)
            {
                splitH = false;
            }

            if (!splitH && !canSplitV)
            {
                splitH = true;
            }

            RectInt rect1, rect2; // Children
            int splitPoint;

            if (splitH) // Y axis
            {
                splitPoint = rng.NextInt(parent.Bounds.y + MinNodeSize, parent.Bounds.y + parent.Bounds.height - MinNodeSize);
                rect1 = new RectInt(parent.Bounds.x, parent.Bounds.y, parent.Bounds.width, splitPoint - parent.Bounds.y);
                rect2 = new RectInt(parent.Bounds.x, splitPoint, parent.Bounds.width, parent.Bounds.y + parent.Bounds.height - splitPoint);
            }
            else // X axis
            {
                splitPoint = rng.NextInt(parent.Bounds.x + MinNodeSize, parent.Bounds.x + parent.Bounds.width - MinNodeSize);
                rect1 = new RectInt(parent.Bounds.x, parent.Bounds.y, splitPoint - parent.Bounds.x, parent.Bounds.height);
                rect2 = new RectInt(splitPoint, parent.Bounds.y, parent.Bounds.x + parent.Bounds.width - splitPoint, parent.Bounds.height);
            }

            // Create children and add them to list
            int idxL = tree.Length;
            tree.Add(new BSPNode { ID = idxL, Bounds = rect1, LeftChildIndex = -1, RightChildIndex = -1 });
            int idxR = tree.Length;
            tree.Add(new BSPNode { ID = idxR, Bounds = rect2, LeftChildIndex = -1, RightChildIndex = -1 });

            // Update parent with children indices
            parent.LeftChildIndex = idxL;
            parent.RightChildIndex = idxR;
            
            tree[parent.ID] = parent; // Update info for struct
        }

        /// <summary>
        /// This method creates a single room
        /// </summary>
        private void PaintRoom(MapData map, RectInt room)
        {
            for (int x = room.x; x < room.x + room.width; x++)
            {
                for (int y = room.y; y < room.y + room.height; y++)
                {
                    map.Grid[map.GetIndex(x, y)] = CellType.Floor;
                }
            }
        }

        /// <summary>
        /// This method connects two rooms
        /// </summary>
        private void ConnectRooms(MapData map, RectInt roomA, RectInt roomB, ref Random rng)
        {
            int corridorWidth = rng.NextInt(MinCorridorWidth, MaxCorridorWidth + 1);

            Vector2Int cA = new Vector2Int((int)roomA.center.x, (int)roomA.center.y);
            Vector2Int cB = new Vector2Int((int)roomB.center.x, (int)roomB.center.y);

            bool overlapX = (roomA.xMin < roomB.xMax - corridorWidth) && (roomB.xMin < roomA.xMax - corridorWidth);
            bool overlapY = (roomA.yMin < roomB.yMax - corridorWidth) && (roomB.yMin < roomA.yMax - corridorWidth);

            if (overlapX) // Vertical connection, search for common X to build corridor
            {
                int commonXMin = Mathf.Max(roomA.xMin, roomB.xMin);
                int commonXMax = Mathf.Min(roomA.xMax, roomB.xMax);
                
                int safeMin = commonXMin + 1; 
                int safeMax = commonXMax - corridorWidth - 1; 

                int xPos;
                if (safeMax < safeMin)
                {
                    xPos = (commonXMin + commonXMax) / 2; // If it's tight, go to centre
                }
                else
                {
                    xPos = rng.NextInt(safeMin, safeMax + 1);
                }

                // Connect closest Ys
                int startY = (cA.y < cB.y) ? roomA.yMax : roomB.yMax;
                int endY = (cA.y < cB.y) ? roomB.yMin : roomA.yMin;
                
                CarveTunnel(map, new Vector2Int(xPos, startY - 1), new Vector2Int(xPos, endY), corridorWidth);
            }
            else if (overlapY) // Horizontal connection, search for common Y to build corridor
            {
                int commonYMin = Mathf.Max(roomA.yMin, roomB.yMin);
                int commonYMax = Mathf.Min(roomA.yMax, roomB.yMax);

                int safeMin = commonYMin + 1;
                int safeMax = commonYMax - corridorWidth - 1;

                int yPos;
                if (safeMax < safeMin)
                {
                    yPos = (commonYMin + commonYMax) / 2;
                }
                else
                {
                    yPos = rng.NextInt(safeMin, safeMax + 1);
                }

                int startX = (cA.x < cB.x) ? roomA.xMax : roomB.xMax;
                int endX = (cA.x < cB.x) ? roomB.xMin : roomA.xMin;

                CarveTunnel(map, new Vector2Int(startX - 1, yPos), new Vector2Int(endX, yPos), corridorWidth);
            }
            else // L-connection
            {
                bool moveHorizontalFirst = rng.NextBool();

                if (Mathf.Abs(cA.x - cB.x) > Mathf.Abs(cA.y - cB.y) * 1.5f) // Horizontal distance is higher
                {
                    moveHorizontalFirst = true;
                }
                else if (Mathf.Abs(cA.y - cB.y) > Mathf.Abs(cA.x - cB.x) * 1.5f)
                {
                    moveHorizontalFirst = false;
                }

                Vector2Int elbow;

                if (moveHorizontalFirst) // From Left/Right A to Up/Down B
                {
                    int yA = GetRandomPointOnWall(roomA.yMin, roomA.yMax, corridorWidth, ref rng);
                    int xB = GetRandomPointOnWall(roomB.xMin, roomB.xMax, corridorWidth, ref rng);
                    
                    int xA = (cA.x < cB.x) ? roomA.xMax : roomA.xMin; 
                    int yB = (cA.y < cB.y) ? roomB.yMin : roomB.yMax;

                    xA = (cA.x < cB.x) ? xA - 1 : xA; 
                    elbow = new Vector2Int(xB, yA);
                    
                    // A -> Elbow (Horiz), Elbow -> B (Vert)
                    CarveTunnel(map, new Vector2Int(xA, yA), elbow, corridorWidth);
                    CarveTunnel(map, elbow, new Vector2Int(xB, yB), corridorWidth);
                }
                else // From Up/Down A to Left/Right B
                {
                    int xA = GetRandomPointOnWall(roomA.xMin, roomA.xMax, corridorWidth, ref rng);
                    int yB = GetRandomPointOnWall(roomB.yMin, roomB.yMax, corridorWidth, ref rng);

                    int yA = (cA.y < cB.y) ? roomA.yMax : roomA.yMin;
                    int xB = (cA.x < cB.x) ? roomB.xMin : roomB.xMax;
                    
                    yA = (cA.y < cB.y) ? yA - 1 : yA;
                    elbow = new Vector2Int(xA, yB);

                    CarveTunnel(map, new Vector2Int(xA, yA), elbow, corridorWidth);
                    CarveTunnel(map, elbow, new Vector2Int(xB, yB), corridorWidth);
                }
            }
        }

        /// <summary>
        /// This method returns a random coordinate in range with a minimum width
        /// </summary>
        private int GetRandomPointOnWall(int min, int max, int width, ref Random rng)
        {
            int safeMin = min + 1;
            int safeMax = max - width - 1;

            if (safeMax <= safeMin)
            {
                return (min + max) / 2; // No space, centre
            }
            
            return rng.NextInt(safeMin, safeMax + 1);
        }

        /// <summary>
        /// This method creates a single corridor given a start, an end and a width. It is optimised to not calculate limits every iteration
        /// </summary>
        private void CarveTunnel(MapData map, Vector2Int start, Vector2Int end, int width)
        {
            int xMin = Mathf.Min(start.x, end.x);
            int xMax = Mathf.Max(start.x, end.x);
            int yMin = Mathf.Min(start.y, end.y);
            int yMax = Mathf.Max(start.y, end.y);
            
            bool isHorizontal = (xMax - xMin) > (yMax - yMin);

            if (isHorizontal)
            {
                int drawXStart = xMin;
                int drawXEnd = xMax + (width / 2);
                drawXStart = Mathf.Max(1, drawXStart);
                drawXEnd = Mathf.Min(map.Width - 2, drawXEnd);
                
                for (int x = drawXStart; x <= drawXEnd; x++) 
                {
                    int drawYStart = yMin - (width / 2);
                    int drawYEnd = drawYStart + width;
                    
                    // Clamp Y
                    int finalYStart = Mathf.Max(1, drawYStart);
                    int finalYEnd = Mathf.Min(map.Height - 2, drawYEnd);

                    for (int y = finalYStart; y < finalYEnd; y++)
                    {
                        map.Grid[map.GetIndex(x, y)] = CellType.Floor; // Direct access to memory
                    }
                }
            }
            else // Vertical
            {
                int drawYStart = yMin;
                int drawYEnd = yMax + (width / 2);

                drawYStart = Mathf.Max(1, drawYStart);
                drawYEnd = Mathf.Min(map.Height - 2, drawYEnd);

                for (int y = drawYStart; y <= drawYEnd; y++)
                {
                    int drawXStart = xMin - (width / 2);
                    int drawXEnd = drawXStart + width;

                    int finalXStart = Mathf.Max(1, drawXStart);
                    int finalXEnd = Mathf.Min(map.Width - 2, drawXEnd);

                    for (int x = finalXStart; x < finalXEnd; x++)
                    {
                        map.Grid[map.GetIndex(x, y)] = CellType.Floor;
                    }
                }
            }
        }
    }
}