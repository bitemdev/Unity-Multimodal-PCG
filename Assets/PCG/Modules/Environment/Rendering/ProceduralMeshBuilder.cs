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

        // Auxiliary class to help manage every mesh list
        private class MeshBuffer
        {
            public List<Vector3> Vertices = new List<Vector3>();
            public List<int> Triangles = new List<int>();
            public List<Color> Colors = new List<Color>();
        }
        
        /// <summary>
        /// This method generates two optimised meshes based on MapData (one for the walkable floor, and the rest).
        /// </summary>
        /// <param name="map"></param>
        public static (Mesh floorMesh, Mesh wallMesh) BuildMesh(MapData map)
        {
            MeshBuffer floorBuffer = new MeshBuffer();
            MeshBuffer wallBuffer = new MeshBuffer();
            
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    int index = map.GetIndex(x, y);
                    CellType currentCell = map.Grid[index];

                    if (currentCell == CellType.Empty)
                    {
                        continue;
                    }

                    Vector3 pos = new Vector3(x * CellSize, 0, y * CellSize);
                    
                    BuildCell(x, y, currentCell, pos, map, floorBuffer, wallBuffer);
                }
            }

            return (CreateMeshFromBuffer(floorBuffer), CreateMeshFromBuffer(wallBuffer));
        }
        
        /// <summary>
        /// This method creates an optimised mesh based on a mesh list buffer.
        /// </summary>
        /// <param name="buffer"></param>
        private static Mesh CreateMeshFromBuffer(MeshBuffer buffer)
        {
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            
            mesh.vertices = buffer.Vertices.ToArray();
            mesh.triangles = buffer.Triangles.ToArray();
            mesh.colors = buffer.Colors.ToArray();
            
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }

        /// <summary>
        /// This method generates a single cell based on its info.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="type"></param>
        /// <param name="pos"></param>
        /// <param name="map"></param>
        /// <param name="floorBuffer"></param>
        /// <param name="wallBuffer"></param>
        private static void BuildCell(int x, int y, CellType type, Vector3 pos, MapData map, MeshBuffer floorBuffer, MeshBuffer wallBuffer)
        {
            float height = (type == CellType.Wall) ? WallHeight : FloorHeight; // If it's wall then WallHeight, otherwise it's floor minimum height
            float yCenter = (type == CellType.Wall) ? WallHeight / 2 : FloorHeight / 2;
            Color cellColor = (type == CellType.Wall) ? WallColor : FloorColor;
            
            Vector3 center = pos + new Vector3(0, yCenter, 0);
            Vector3 size = new Vector3(CellSize, height, CellSize);
            
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            float hz = size.z * 0.5f;

            // Faces always go to wallBuffer
            if (ShouldDrawFace(x, y + 1, type, map)) // Z+ Face Culling
            {
                AddQuad(wallBuffer, WallColor, 
                    center + new Vector3(hx, hy, hz),
                    center + new Vector3(-hx, hy, hz),
                    center + new Vector3(-hx, -hy, hz),
                    center + new Vector3(hx, -hy, hz));
            }

            if (ShouldDrawFace(x, y - 1, type, map)) // Z- Face Culling
            {
                AddQuad(wallBuffer, WallColor, 
                    center + new Vector3(-hx, hy, -hz),
                    center + new Vector3(hx, hy, -hz),
                    center + new Vector3(hx, -hy, -hz),
                    center + new Vector3(-hx, -hy, -hz));
            }

            if (ShouldDrawFace(x + 1, y, type, map)) // X+ Face Culling
            {
                AddQuad(wallBuffer, WallColor, 
                    center + new Vector3(hx, hy, -hz),
                    center + new Vector3(hx, hy, hz),
                    center + new Vector3(hx, -hy, hz),
                    center + new Vector3(hx, -hy, -hz));
            }

            if (ShouldDrawFace(x - 1, y, type, map)) // X- Face Culling
            {
                AddQuad(wallBuffer, WallColor, 
                    center + new Vector3(-hx, hy, hz),
                    center + new Vector3(-hx, hy, -hz),
                    center + new Vector3(-hx, -hy, -hz),
                    center + new Vector3(-hx, -hy, hz));
            }

            MeshBuffer targetBuffer = type == CellType.Wall ? wallBuffer : floorBuffer;

            // Y+ is always rendered
            AddQuad(targetBuffer, cellColor,
                center + new Vector3(-hx, hy, hz),
                center + new Vector3(hx, hy, hz),
                center + new Vector3(hx, hy, -hz),
                center + new Vector3(-hx, hy, -hz)
            );

            // Y- is never rendered
        }

        /// <summary>
        /// This method determines whether a face should be rendered or not based on optimisation rules.
        /// </summary>
        /// <param name="neighborX"></param>
        /// <param name="neighborY"></param>
        /// <param name="currentType"></param>
        /// <param name="map"></param>
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
        /// This method generates faces.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="c"></param>
        /// <param name="tl"></param>
        /// <param name="tr"></param>
        /// <param name="br"></param>
        /// <param name="bl"></param>
        private static void AddQuad(MeshBuffer buffer, Color c, Vector3 tl, Vector3 tr, Vector3 br, Vector3 bl)
        {
            int index = buffer.Vertices.Count;
            
            buffer.Vertices.Add(tl);
            buffer.Vertices.Add(tr);
            buffer.Vertices.Add(br);
            buffer.Vertices.Add(bl);
            
            buffer.Colors.Add(c);
            buffer.Colors.Add(c);
            buffer.Colors.Add(c);
            buffer.Colors.Add(c);

            buffer.Triangles.Add(index + 0);
            buffer.Triangles.Add(index + 1);
            buffer.Triangles.Add(index + 2);
            
            buffer.Triangles.Add(index + 0);
            buffer.Triangles.Add(index + 2);
            buffer.Triangles.Add(index + 3);
        }
    }
}