using PCG.Core;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace PCG.Modules.Environment
{
    public class DungeonGenerator : IGeneratorStrategy
    {
        private const int MinNodeSize = 15; // Min space for Room + Walls
        private const int MinRoomSize = 6; // Min space for Room
        private const int RoomMargin = 2; // Margin between rooms
        private const int MinCorridorWidth = 1;
        private const int MaxCorridorWidth = 3;

        /// <summary>
        /// This method generates a dungeon map using a Burst-Compiled, optimised BSP algorithm
        /// </summary>
        public MapData Generate(int seed, Vector2Int size)
        {
            Debug.Log($"[DungeonGenerator] Starting Burst-Compiled BSP with Seed: {seed}");

            MapData map = new MapData(size.x, size.y, Allocator.Persistent);

            // Init grid to walls
            for (int i = 0; i < map.Grid.Length; i++)
            {
                map.Grid[i] = CellType.Wall;
            }

            // Setup the Job
            DungeonJob job = new DungeonJob
            {
                Seed = (uint)seed,
                Width = size.x,
                Height = size.y,
                MinNodeSize = MinNodeSize,
                MinRoomSize = MinRoomSize,
                RoomMargin = RoomMargin,
                MinCorridorWidth = MinCorridorWidth,
                MaxCorridorWidth = MaxCorridorWidth,
                Grid = map.Grid
            };

            // Execute the Job on a worker thread and wait for completion
            job.Schedule().Complete();

            return map;
        }

        // ==========================================
        // BURST COMPILED JOB
        // ==========================================

        [BurstCompile(CompileSynchronously = true)]
        private struct DungeonJob : IJob
        {
            public uint Seed;
            public int Width;
            public int Height;
            public int MinNodeSize;
            public int MinRoomSize;
            public int RoomMargin;
            public int MinCorridorWidth;
            public int MaxCorridorWidth;

            public NativeArray<CellType> Grid;

            // Struct modified for Burst: int4 replaces RectInt (x, y, width, height)
            private struct BSPNode
            {
                public int ID; 
                public int4 Bounds; 
                public int4 Room; 
                
                public int LeftChildIndex;
                public int RightChildIndex;
                public bool IsLeaf => LeftChildIndex == -1 && RightChildIndex == -1;
            }

            public void Execute()
            {
                Random rng = new Random(Seed);

                // Use NativeList instead of C# List (Zero GC)
                NativeList<BSPNode> tree = new NativeList<BSPNode>(Allocator.Temp);
                NativeList<int> leafIndices = new NativeList<int>(Allocator.Temp);

                // Create root node
                BSPNode root = new BSPNode
                {
                    ID = 0,
                    Bounds = new int4(0, 0, Width, Height),
                    Room = new int4(0, 0, 0, 0),
                    LeftChildIndex = -1,
                    RightChildIndex = -1
                };

                tree.Add(root);
                leafIndices.Add(0);

                // 1. SPLIT PROCESS (Your iterative approach)
                bool didSplit = true;
                while (didSplit)
                {
                    didSplit = false;
                    
                    // We need to iterate over the current leaves, but adding new ones modifies the list.
                    // So we copy the current leaves to a temporary list for this pass.
                    NativeList<int> currentLeaves = new NativeList<int>(Allocator.Temp);
                    for (int i = 0; i < leafIndices.Length; i++)
                    {
                        currentLeaves.Add(leafIndices[i]);
                    }
                    
                    leafIndices.Clear(); // We will refill it with the new children (or keep the ones that couldn't split)

                    for (int i = 0; i < currentLeaves.Length; i++)
                    {
                        int currentIndex = currentLeaves[i];
                        BSPNode node = tree[currentIndex];
                        int4 b = node.Bounds;

                        bool canSplitH = b.w >= MinNodeSize * 2;
                        bool canSplitV = b.z >= MinNodeSize * 2;

                        if (!canSplitH && !canSplitV)
                        {
                            leafIndices.Add(currentIndex); // Kept as leaf
                            continue;
                        }

                        bool splitH = false;
                        if (canSplitH && canSplitV)
                        {
                            splitH = rng.NextFloat() > 0.5f;
                        }
                        else if (canSplitH)
                        {
                            splitH = true;
                        }

                        int maxSplit;
                        BSPNode leftNode = new BSPNode { LeftChildIndex = -1, RightChildIndex = -1, Room = new int4(0,0,0,0) };
                        BSPNode rightNode = new BSPNode { LeftChildIndex = -1, RightChildIndex = -1, Room = new int4(0,0,0,0) };

                        if (splitH)
                        {
                            maxSplit = b.w - MinNodeSize;
                            int splitPos = rng.NextInt(MinNodeSize, maxSplit);

                            leftNode.Bounds = new int4(b.x, b.y, b.z, splitPos);
                            rightNode.Bounds = new int4(b.x, b.y + splitPos, b.z, b.w - splitPos);
                        }
                        else
                        {
                            maxSplit = b.z - MinNodeSize;
                            int splitPos = rng.NextInt(MinNodeSize, maxSplit);

                            leftNode.Bounds = new int4(b.x, b.y, splitPos, b.w);
                            rightNode.Bounds = new int4(b.x + splitPos, b.y, b.z - splitPos, b.w);
                        }

                        // Add children to tree
                        leftNode.ID = tree.Length;
                        int leftIdx = tree.Length;
                        tree.Add(leftNode);
                        
                        rightNode.ID = tree.Length;
                        int rightIdx = tree.Length;
                        tree.Add(rightNode);

                        // Update parent
                        node.LeftChildIndex = leftIdx;
                        node.RightChildIndex = rightIdx;
                        tree[currentIndex] = node;

                        leafIndices.Add(leftIdx);
                        leafIndices.Add(rightIdx);
                        
                        didSplit = true;
                    }
                    
                    currentLeaves.Dispose();
                }

                // 2. CARVE ROOMS (Iterating only over leaves)
                for (int i = 0; i < leafIndices.Length; i++)
                {
                    int leafIdx = leafIndices[i];
                    BSPNode node = tree[leafIdx];
                    int4 b = node.Bounds;

                    int roomW = rng.NextInt(MinRoomSize, b.z - RoomMargin * 2);
                    int roomH = rng.NextInt(MinRoomSize, b.w - RoomMargin * 2);
                    int roomX = b.x + RoomMargin + rng.NextInt(0, b.z - roomW - RoomMargin * 2);
                    int roomY = b.y + RoomMargin + rng.NextInt(0, b.w - roomH - RoomMargin * 2);

                    node.Room = new int4(roomX, roomY, roomW, roomH);
                    tree[leafIdx] = node;

                    // Carve Room
                    for (int x = node.Room.x; x < node.Room.x + node.Room.z; x++)
                    {
                        for (int y = node.Room.y; y < node.Room.y + node.Room.w; y++)
                        {
                            if (x > 0 && x < Width - 1 && y > 0 && y < Height - 1)
                            {
                                Grid[(y * Width) + x] = CellType.Floor;
                            }
                        }
                    }
                }

                // 3. CREATE CORRIDORS (Iterating backwards to process bottom-up)
                for (int i = tree.Length - 1; i >= 0; i--)
                {
                    BSPNode node = tree[i];
                    if (!node.IsLeaf)
                    {
                        BSPNode leftChild = tree[node.LeftChildIndex];
                        BSPNode rightChild = tree[node.RightChildIndex];

                        int2 leftCenter = new int2(leftChild.Room.x + (leftChild.Room.z / 2), leftChild.Room.y + (leftChild.Room.w / 2));
                        int2 rightCenter = new int2(rightChild.Room.x + (rightChild.Room.z / 2), rightChild.Room.y + (rightChild.Room.w / 2));

                        int corridorWidth = rng.NextInt(MinCorridorWidth, MaxCorridorWidth + 1);

                        // Connect children
                        if (rng.NextFloat() > 0.5f)
                        {
                            CarveCorridor(leftCenter.x, leftCenter.y, rightCenter.x, leftCenter.y, corridorWidth);
                            CarveCorridor(rightCenter.x, leftCenter.y, rightCenter.x, rightCenter.y, corridorWidth);
                        }
                        else
                        {
                            CarveCorridor(leftCenter.x, leftCenter.y, leftCenter.x, rightCenter.y, corridorWidth);
                            CarveCorridor(leftCenter.x, rightCenter.y, rightCenter.x, rightCenter.y, corridorWidth);
                        }

                        // Pass room data up to parent
                        node.Room = leftChild.Room;
                        tree[i] = node;
                    }
                }

                tree.Dispose();
                leafIndices.Dispose();
            }

            // Burst-compatible corridor carving logic
            private void CarveCorridor(int x1, int y1, int x2, int y2, int width)
            {
                int minX = math.min(x1, x2);
                int maxX = math.max(x1, x2);
                int minY = math.min(y1, y2);
                int maxY = math.max(y1, y2);

                if (minX == maxX) // Vertical corridor
                {
                    int drawYStart = math.max(1, minY);
                    int drawYEnd = math.min(Height - 2, maxY + (width / 2));

                    for (int y = drawYStart; y <= drawYEnd; y++)
                    {
                        int drawXStart = math.max(1, minX - (width / 2));
                        int drawXEnd = math.min(Width - 2, drawXStart + width);

                        for (int x = drawXStart; x < drawXEnd; x++)
                        {
                            Grid[(y * Width) + x] = CellType.Floor;
                        }
                    }
                }
                else // Horizontal corridor
                {
                    int drawXStart = math.max(1, minX);
                    int drawXEnd = math.min(Width - 2, maxX + (width / 2));

                    for (int x = drawXStart; x <= drawXEnd; x++)
                    {
                        int drawYStart = math.max(1, minY - (width / 2));
                        int drawYEnd = math.min(Height - 2, drawYStart + width);

                        for (int y = drawYStart; y < drawYEnd; y++)
                        {
                            Grid[(y * Width) + x] = CellType.Floor;
                        }
                    }
                }
            }
        }
    }
}