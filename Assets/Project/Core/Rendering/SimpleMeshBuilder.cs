using System.Collections.Generic;
using PCG.Core;
using UnityEngine;

namespace PCG.Rendering
{
    static public class SimpleMeshBuilder
    {
        // This method generates a simple mesh from mapdata
        public static Mesh BuildMesh(MapData map, float cellSize)
        {
            // List is being used here because the number of vertices is unknown
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            float halfSize = cellSize * 0.5f; // Offset to center the cube

            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    int index = map.GetIndex(x, y);
                    CellType type = map.Grid[index];

                    if (type != CellType.Empty)
                    {
                        Vector3 center = new Vector3(x * cellSize, 0, y * cellSize);
                        
                        float height = (type == CellType.Wall) ? 1.0f : 0.2f;
                        float yPos = (type == CellType.Wall) ? 0.5f : 0.0f; // Walls up

                        Vector3 position = center + new Vector3(0, yPos, 0);
                        Vector3 size = new Vector3(cellSize, height, cellSize);

                        AddCube(position, size, vertices, triangles);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; 
            
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            
            // Normal calculations for the light
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        // This auxiliar method adds the 8 vertices and 12 triangles of a cube
        static private void AddCube(Vector3 position, Vector3 size, List<Vector3> vertices, List<int> triangles)
        {
            // Half's calculation
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            float z = size.z * 0.5f;

            int vIndex = vertices.Count; // Current index where new vertices start

            // Top
            vertices.Add(position + new Vector3(-x, y, -z)); // 0: Top-Left-Back
            vertices.Add(position + new Vector3(x, y, -z));  // 1: Top-Right-Back
            vertices.Add(position + new Vector3(x, y, z));   // 2: Top-Right-Fwd
            vertices.Add(position + new Vector3(-x, y, z));  // 3: Top-Left-Fwd
            
            // Bottom
            vertices.Add(position + new Vector3(-x, -y, -z)); // 4: Bottom-Left-Back
            vertices.Add(position + new Vector3(x, -y, -z));  // 5: Bottom-Right-Back
            vertices.Add(position + new Vector3(x, -y, z));   // 6: Bottom-Right-Fwd
            vertices.Add(position + new Vector3(-x, -y, z));  // 7: Bottom-Left-Fwd

            // Clockwise
            // Top
            AddQuad(triangles, vIndex, 0, 3, 2, 1); 
            
            // Bottom
            AddQuad(triangles, vIndex, 7, 6, 5, 4);

            // Front
            AddQuad(triangles, vIndex, 2, 3, 7, 6);
            
            // Back
            AddQuad(triangles, vIndex, 0, 1, 5, 4);
            
            // Right
            AddQuad(triangles, vIndex, 1, 2, 6, 5);
            
            // Left
            AddQuad(triangles, vIndex, 3, 0, 4, 7);
        }

        static private void AddQuad(List<int> triangles, int vIndex, int a, int b, int c, int d)
        {
            // First triangle
            triangles.Add(vIndex + a);
            triangles.Add(vIndex + b);
            triangles.Add(vIndex + c);
            
            // Second triangle
            triangles.Add(vIndex + c);
            triangles.Add(vIndex + d);
            triangles.Add(vIndex + a);
        }
    }
}