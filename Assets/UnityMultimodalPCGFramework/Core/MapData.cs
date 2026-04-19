using System;
using UnityEngine;
using Unity.Collections;

namespace PCG.Core
{
    // It is used struct over class, as classes generate garbage. Structs live in the stack (rapid memory) and are passed as value
    public struct MapData : IDisposable // IDisposable is the interface that forces this script to implement the Dispose() method, cleaning up memory
    {
        public NativeArray<CellType> Grid; // NativeArray is way more optimised than List, as it in reality is a C++ array and can be accessed from multiple threads
        public readonly int Width;
        public readonly int Height;
        
        // This constructor method initialises the map
        public
            MapData(int width, int height,
                Allocator allocator) // Allocator.Persistent means it will live in memory until it is said not to
        {
            Width = width;
            Height = height;
            Grid = new NativeArray<CellType>(width * height, allocator); // As the size of the collection is known, array is better than List -> List is used when size is unknown
        }

        // This method returns the position in the 1D grid (as it receives 2D parameters). Mathematically, row * totalWidth + column
        public int GetIndex(int x, int y)
        {
            return (y * Width) + x;
        }

        // Mandatory method which manually liberates memory space
        public void Dispose()
        {
            if (Grid.IsCreated)
            {
                Grid.Dispose();
            }
        }
    }
}
