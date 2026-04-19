using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using PCG.Core;

namespace PCG.Modules.Environment.Rendering
{
    public static class ProceduralMeshBuilder
    {
        private const float WallHeight = 1.0f;
        private const float FloorHeight = 0.2f;
        private const float CellSize = 1.0f;

        private static readonly Color WallColor = new Color(0f, 0.121f, 0.247f);
        private static readonly Color FloorColor = new Color(0.968f, 0.905f, 0.807f);

        /// <summary>
        /// Generates meshes in a hyper-optimized way using the Job System and Burst Compiler (Zero GC)
        /// </summary>
        /// <param name="map"></param>
        public static (Mesh floorMesh, Mesh wallMesh) BuildMesh(MapData map)
        {
            // Estimation of the maximum size to avoid resizing arrays (Zero GC)
            int maxFaces = map.Width * map.Height * 6; // In the worst case (a loose cube), 6 faces
            int maxVertices = maxFaces * 4;
            int maxTriangles = maxFaces * 6;

            // Allocation of temporary NativeLists (they live only during the execution of this method)
            NativeList<Vector3> floorVerts = new NativeList<Vector3>(maxVertices, Allocator.TempJob);
            NativeList<int> floorTris = new NativeList<int>(maxTriangles, Allocator.TempJob);
            NativeList<Color> floorColors = new NativeList<Color>(maxVertices, Allocator.TempJob);

            NativeList<Vector3> wallVerts = new NativeList<Vector3>(maxVertices, Allocator.TempJob);
            NativeList<int> wallTris = new NativeList<int>(maxTriangles, Allocator.TempJob);
            NativeList<Color> wallColors = new NativeList<Color>(maxVertices, Allocator.TempJob);

            // 1. Configure the Job
            MeshBuilderJob job = new MeshBuilderJob
            {
                Width = map.Width,
                Height = map.Height,
                Grid = map.Grid,

                WallHeight = WallHeight,
                FloorHeight = FloorHeight,
                CellSize = CellSize,

                WallColor = WallColor,
                FloorColor = FloorColor,

                FloorVertices = floorVerts,
                FloorTriangles = floorTris,
                FloorColors = floorColors,

                WallVertices = wallVerts,
                WallTriangles = wallTris,
                WallColors = wallColors
            };

            // 2. Execute the Job and wait for it to finish
            job.Schedule().Complete();

            // 3. Dump the native data to the Mesh
            Mesh floorMesh = new Mesh { name = "PCG_Floor_Mesh" };
            floorMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            floorMesh.SetVertices(floorVerts.AsArray());
            floorMesh.SetColors(floorColors.AsArray());
            floorMesh.SetIndices(floorTris.AsArray(), MeshTopology.Triangles, 0);
            floorMesh.RecalculateNormals();

            Mesh wallMesh = new Mesh { name = "PCG_Wall_Mesh" };
            wallMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            wallMesh.SetVertices(wallVerts.AsArray());
            wallMesh.SetColors(wallColors.AsArray());
            wallMesh.SetIndices(wallTris.AsArray(), MeshTopology.Triangles, 0);
            wallMesh.RecalculateNormals();

            // 4. Free the memory of the pointers (Fundamental to avoid Memory Leaks)
            floorVerts.Dispose();
            floorTris.Dispose();
            floorColors.Dispose();

            wallVerts.Dispose();
            wallTris.Dispose();
            wallColors.Dispose();

            return (floorMesh, wallMesh);
        }

        // ==========================================
        // BURST COMPILED JOB
        // ==========================================

        [BurstCompile(CompileSynchronously = true)]
        private struct MeshBuilderJob : IJob
        {
            [ReadOnly] public int Width;
            [ReadOnly] public int Height;
            [ReadOnly] public NativeArray<CellType> Grid;

            [ReadOnly] public float WallHeight;
            [ReadOnly] public float FloorHeight;
            [ReadOnly] public float CellSize;

            [ReadOnly] public Color WallColor;
            [ReadOnly] public Color FloorColor;

            // Output arrays
            public NativeList<Vector3> FloorVertices;
            public NativeList<int> FloorTriangles;
            public NativeList<Color> FloorColors;

            public NativeList<Vector3> WallVertices;
            public NativeList<int> WallTriangles;
            public NativeList<Color> WallColors;

            public void Execute()
            {
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        int index = GetIndex(x, y);
                        CellType cell = Grid[index];

                        float posX = x * CellSize;
                        float posZ = y * CellSize;

                        if (cell == CellType.Floor)
                        {
                            // Top face of the floor
                            AddQuad(FloorVertices, FloorTriangles, FloorColors, FloorColor,
                                new Vector3(posX, FloorHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, FloorHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, FloorHeight, posZ),
                                new Vector3(posX, FloorHeight, posZ));
                        }
                        else if (cell == CellType.Wall)
                        {
                            // Top face of the wall
                            AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                new Vector3(posX, WallHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, WallHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, WallHeight, posZ),
                                new Vector3(posX, WallHeight, posZ));

                            // Analysis of neighbors to draw side faces (Only if it touches the floor or boundary)

                            // North (y + 1)
                            if (NeedsFace(x, y + 1, cell))
                            {
                                AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                    new Vector3(posX + CellSize, WallHeight, posZ + CellSize),
                                    new Vector3(posX, WallHeight, posZ + CellSize),
                                    new Vector3(posX, FloorHeight, posZ + CellSize),
                                    new Vector3(posX + CellSize, FloorHeight, posZ + CellSize));
                            }

                            // South (y - 1)
                            if (NeedsFace(x, y - 1, cell))
                            {
                                AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                    new Vector3(posX, WallHeight, posZ),
                                    new Vector3(posX + CellSize, WallHeight, posZ),
                                    new Vector3(posX + CellSize, FloorHeight, posZ),
                                    new Vector3(posX, FloorHeight, posZ));
                            }

                            // East (x + 1)
                            if (NeedsFace(x + 1, y, cell))
                            {
                                AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                    new Vector3(posX + CellSize, WallHeight, posZ),
                                    new Vector3(posX + CellSize, WallHeight, posZ + CellSize),
                                    new Vector3(posX + CellSize, FloorHeight, posZ + CellSize),
                                    new Vector3(posX + CellSize, FloorHeight, posZ));
                            }

                            // West (x - 1)
                            if (NeedsFace(x - 1, y, cell))
                            {
                                AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                    new Vector3(posX, WallHeight, posZ + CellSize),
                                    new Vector3(posX, WallHeight, posZ),
                                    new Vector3(posX, FloorHeight, posZ),
                                    new Vector3(posX, FloorHeight, posZ + CellSize));
                            }
                        }
                    }
                }
            }

            /// <summary>
            /// Evaluates if a face needs to be drawn based on its neighboring cell.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            /// <param name="currentType"></param>
            private bool NeedsFace(int x, int y, CellType currentType)
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                {
                    return true; // Map boundaries
                }

                CellType neighborType = Grid[GetIndex(x, y)];
                if (currentType == CellType.Wall && neighborType == CellType.Floor)
                {
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Converts 2D grid coordinates into a 1D array index.
            /// </summary>
            /// <param name="x"></param>
            /// <param name="y"></param>
            private int GetIndex(int x, int y)
            {
                return (y * Width) + x;
            }

            /// <summary>
            /// Adds a quad (two triangles) to the corresponding native lists.
            /// </summary>
            /// <param name="verts"></param>
            /// <param name="tris"></param>
            /// <param name="cols"></param>
            /// <param name="c"></param>
            /// <param name="tl"></param>
            /// <param name="tr"></param>
            /// <param name="br"></param>
            /// <param name="bl"></param>
            private void AddQuad(NativeList<Vector3> verts, NativeList<int> tris, NativeList<Color> cols, Color c,
                Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl)
            {
                int index = verts.Length;

                verts.Add(tl);
                verts.Add(tr);
                verts.Add(br);
                verts.Add(bl);

                cols.Add(c);
                cols.Add(c);
                cols.Add(c);
                cols.Add(c);

                tris.Add(index + 0);
                tris.Add(index + 1);
                tris.Add(index + 2);

                tris.Add(index + 0);
                tris.Add(index + 2);
                tris.Add(index + 3);
            }
        }
    }
}