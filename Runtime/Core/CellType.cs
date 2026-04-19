namespace PCG.Core
{
    public enum CellType : byte // It inherits from "byte" so it can be stored with only 1 byte in memory (and not 4 bytes as for "int")
    {
        Empty = 0,
        Wall = 1,
        Floor = 2
    }
}