using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using PCG.Core;

namespace PCG.Modules.Environment.Rendering
{
    public static class ProceduralMeshBuilder
    {
        // Valores constantes
        private const float WallHeight = 1.0f;
        private const float FloorHeight = 0.2f;
        private const float CellSize = 1.0f;
        
        // Colores
        private static readonly Color WallColor = new Color(0f, 0.121f, 0.247f);
        private static readonly Color FloorColor = new Color(0.968f, 0.905f, 0.807f);

        /// <summary>
        /// Genera mallas de forma hiper-optimizada usando Jobs y Burst Compiler (Zero GC)
        /// </summary>
        public static (Mesh floorMesh, Mesh wallMesh) BuildMesh(MapData map)
        {
            // Estimación del tamaño máximo para evitar redimensionar arrays (Zero GC)
            int maxFaces = map.Width * map.Height * 6; // En el peor de los casos (un cubo suelto), 6 caras
            int maxVertices = maxFaces * 4;
            int maxTriangles = maxFaces * 6;

            // Alocación de NativeLists temporales (viven solo durante la ejecución de este método)
            NativeList<Vector3> floorVerts = new NativeList<Vector3>(maxVertices, Allocator.TempJob);
            NativeList<int> floorTris = new NativeList<int>(maxTriangles, Allocator.TempJob);
            NativeList<Color> floorColors = new NativeList<Color>(maxVertices, Allocator.TempJob);

            NativeList<Vector3> wallVerts = new NativeList<Vector3>(maxVertices, Allocator.TempJob);
            NativeList<int> wallTris = new NativeList<int>(maxTriangles, Allocator.TempJob);
            NativeList<Color> wallColors = new NativeList<Color>(maxVertices, Allocator.TempJob);

            // 1. Configuramos el Job
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

            // 2. Ejecutamos el Job y esperamos a que termine
            job.Schedule().Complete();

            // 3. Volcamos los datos nativos a la Malla
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

            // 4. Liberamos la memoria de los punteros (Fundamental para no tener Memory Leaks)
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

            // Arrays de salida
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
                            // Cara superior del Suelo
                            AddQuad(FloorVertices, FloorTriangles, FloorColors, FloorColor,
                                new Vector3(posX, FloorHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, FloorHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, FloorHeight, posZ),
                                new Vector3(posX, FloorHeight, posZ));
                        }
                        else if (cell == CellType.Wall)
                        {
                            // Cara superior del Muro
                            AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                new Vector3(posX, WallHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, WallHeight, posZ + CellSize),
                                new Vector3(posX + CellSize, WallHeight, posZ),
                                new Vector3(posX, WallHeight, posZ));

                            // Análisis de vecinos para dibujar caras laterales (Solo si toca con suelo o límite)
                            
                            // Norte (y + 1)
                            if (NeedsFace(x, y + 1, cell))
                            {
                                AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                    new Vector3(posX + CellSize, WallHeight, posZ + CellSize),
                                    new Vector3(posX, WallHeight, posZ + CellSize),
                                    new Vector3(posX, FloorHeight, posZ + CellSize),
                                    new Vector3(posX + CellSize, FloorHeight, posZ + CellSize));
                            }

                            // Sur (y - 1)
                            if (NeedsFace(x, y - 1, cell))
                            {
                                AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                    new Vector3(posX, WallHeight, posZ),
                                    new Vector3(posX + CellSize, WallHeight, posZ),
                                    new Vector3(posX + CellSize, FloorHeight, posZ),
                                    new Vector3(posX, FloorHeight, posZ));
                            }

                            // Este (x + 1)
                            if (NeedsFace(x + 1, y, cell))
                            {
                                AddQuad(WallVertices, WallTriangles, WallColors, WallColor,
                                    new Vector3(posX + CellSize, WallHeight, posZ),
                                    new Vector3(posX + CellSize, WallHeight, posZ + CellSize),
                                    new Vector3(posX + CellSize, FloorHeight, posZ + CellSize),
                                    new Vector3(posX + CellSize, FloorHeight, posZ));
                            }

                            // Oeste (x - 1)
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

            private bool NeedsFace(int x, int y, CellType currentType)
            {
                if (x < 0 || x >= Width || y < 0 || y >= Height)
                {
                    return true; // Limites del mapa
                }

                CellType neighborType = Grid[GetIndex(x, y)];
                if (currentType == CellType.Wall && neighborType == CellType.Floor)
                {
                    return true;
                }

                return false;
            }

            private int GetIndex(int x, int y)
            {
                return (y * Width) + x;
            }

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