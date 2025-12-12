using System.Collections.Generic;
using PCG.Core;
using UnityEngine;

namespace PCG.Modules.Environment.Rendering
{
    public static class ProceduralMeshBuilder
    {
        // Const values to optimise
        private const float WallHeight = 1.0f;
        private const float FloorHeight = 0.2f;
        private const float CellSize = 1.0f;
        
        // Debug colors
         private static readonly Color WallColor = new Color(0f, 0.121f, 0.247f);
         private static readonly Color FloorColor = new Color(0.968f, 0.905f, 0.807f);
        
        /// <summary>
        /// This method generates an optimised mesh based on MapData
        /// </summary>
        public static Mesh BuildMesh(MapData map)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            List<Color> colors = new List<Color>();
            
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    int index = map.GetIndex(x, y);
                    CellType currentCell = map.Grid[index];

                    if (currentCell == CellType.Empty) // If cell empty, go to next one (CPU saving)
                    {
                        continue;
                    }

                    Vector3 pos = new Vector3(x * CellSize, 0, y * CellSize);
                    
                    BuildCell(x, y, currentCell, pos, map, vertices, triangles, colors);
                }
            }

            Mesh mesh = new Mesh(); // Final mesh
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // This allows more than 65k vertices, for big maps
            
            // It used normal Lists because the mesh builder only accepts these, and since it is only created at the very beginning, it is okay to wait for the C# garbage collector
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.colors = colors.ToArray();
            
            // Calculate normals for the light
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// This method generates a single cell based on its info
        /// </summary>
        private static void BuildCell(int x, int y, CellType type, Vector3 pos, MapData map, List<Vector3> verts, List<int> tris, List<Color> colors)
        {
            float height = (type == CellType.Wall) ? WallHeight : FloorHeight; // If it's wall then WallHeight, otherwise it's floor minimum height
            float yCenter = (type == CellType.Wall) ? WallHeight / 2 : FloorHeight / 2;
            Color cellColor = (type == CellType.Wall) ? WallColor : FloorColor;
            
            Vector3 center = pos + new Vector3(0, yCenter, 0);
            Vector3 size = new Vector3(CellSize, height, CellSize);
            
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            if (ShouldDrawFace(x, y + 1, type, map)) // Z+ Face culling
            {
                // Clockwise
                AddQuad(verts, tris, colors, cellColor, 
                    center + new Vector3(hx, hy, hz),   // Top Right (2)
                    center + new Vector3(-hx, hy, hz),  // Top Left (3)
                    center + new Vector3(-hx, -hy, hz), // Bottom Left (7)
                    center + new Vector3(hx, -hy, hz)   // Bottom Right (6)
                );
            }

            if (ShouldDrawFace(x, y - 1, type, map)) // Z- Face culling
            {
                // Clockwise
                AddQuad(verts, tris, colors, cellColor,
                    center + new Vector3(-hx, hy, -hz), // Top Left (0)
                    center + new Vector3(hx, hy, -hz),  // Top Right (1)
                    center + new Vector3(hx, -hy, -hz), // Bottom Right (5)
                    center + new Vector3(-hx, -hy, -hz) // Bottom Left (4)
                );
            }

            if (ShouldDrawFace(x + 1, y, type, map)) // X+ Face culling
            {
                // Clockwise
                AddQuad(verts, tris, colors, cellColor,
                    center + new Vector3(hx, hy, -hz),  // Top Left (1)
                    center + new Vector3(hx, hy, hz),   // Top Right (2)
                    center + new Vector3(hx, -hy, hz),  // Bottom Right (6)
                    center + new Vector3(hx, -hy, -hz)  // Bottom Left (5)
                );
            }

            if (ShouldDrawFace(x - 1, y, type, map)) // X- Face culling
            {
                // Clockwise
                AddQuad(verts, tris, colors, cellColor,
                    center + new Vector3(-hx, hy, hz),  // Top Left (3)
                    center + new Vector3(-hx, hy, -hz), // Top Right (0)
                    center + new Vector3(-hx, -hy, -hz),// Bottom Right (4)
                    center + new Vector3(-hx, -hy, hz)  // Bottom Left (7)
                );
            }

            // Y+ is always rendered
            AddQuad(verts, tris, colors, cellColor,
                center + new Vector3(-hx, hy, hz),  // 3
                center + new Vector3(hx, hy, hz),   // 2
                center + new Vector3(hx, hy, -hz),  // 1
                center + new Vector3(-hx, hy, -hz)  // 0
            );

            // Y- is never rendered
        }

        /// <summary>
        /// This method determines whether a face should be rendered or not based on optimisation rules
        /// </summary>
        private static bool ShouldDrawFace(int neighborX, int neighborY, CellType currentType, MapData map)
        {
            if (neighborX < 0 || neighborX >= map.Width || neighborY < 0 || neighborY >= map.Height) // If neighbour is out of the map, it's a border -> DRAW
            {
                return true;
            }

            int neighborIndex = map.GetIndex(neighborX, neighborY);
            CellType neighborType = map.Grid[neighborIndex];

            if (neighborType == CellType.Empty) // If it's empty -> DRAW
            {
                return true;
            }

            if (currentType == CellType.Wall && neighborType == CellType.Floor) // If it's a wall and its neighbour is floor -> DRAW (because wall is taller than floor)
            {
                return true;
            }

            return false; // Wall-Wall, Floor-Floor, Floor-Wall -> HIDE
        }

        /// <summary>
        /// This method generates faces
        /// </summary>
        private static void AddQuad(List<Vector3> verts, List<int> tris, List<Color> colors, Color c, Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl)
        {
            int index = verts.Count;
            
            // 4 vertices
            verts.Add(tl);
            verts.Add(tr);
            verts.Add(br);
            verts.Add(bl);
            
            colors.Add(c);
            colors.Add(c);
            colors.Add(c);
            colors.Add(c);

            // First triangle
            tris.Add(index + 0);
            tris.Add(index + 1);
            tris.Add(index + 2);
            
            // Second triangle
            tris.Add(index + 0);
            tris.Add(index + 2);
            tris.Add(index + 3);
        }
    }
}