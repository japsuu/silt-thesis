namespace Silt.World;

/// <summary>
/// Represents a single voxel in the world.
/// Contains an ID for the voxel type and additional per-cell state.
/// </summary>
public readonly struct Voxel(int id, float data1, int data2, int data3)
{
    public readonly int Id = id;
    public readonly float Data1 = data1;
    public readonly int Data2 = data2;
    public readonly int Data3 = data3;
}