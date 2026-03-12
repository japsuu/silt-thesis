using Silk.NET.OpenGL;
using Silt.Core.Graphics;

namespace Silt.World.Meshing;

/// <summary>
/// Contains the vertex and index data for rendering a chunk of voxels.
/// Produced by the meshing system based on the voxel data in a chunk.
/// </summary>
public readonly ref struct VoxelMeshData(ReadOnlySpan<float> vertices, ReadOnlySpan<uint> indices)
{
    public readonly ReadOnlySpan<float> Vertices = vertices;
    public readonly ReadOnlySpan<uint> Indices = indices;
}

/// <summary>
/// Input data for voxel meshing,
/// including the voxel data of the current chunk and its 6 immediate neighbors (or null if at world boundary).
/// </summary>
public readonly record struct MeshingInput(
    Chunk Center,
    Chunk? XPos,
    Chunk? XNeg,
    Chunk? YPos,
    Chunk? YNeg,
    Chunk? ZPos,
    Chunk? ZNeg);

/// <summary>
/// Responsible for converting voxel data in a chunk into vertex and index data for rendering.
/// Implements meshing algorithms to optimize the geometry.
/// </summary>
public static class ChunkMesher
{
    public const int VERTEX_SIZE_ELEMENTS = 9;
    public const int VERTEX_ELEMENT_SIZE_BYTES = sizeof(float);
    public const int INDEX_ELEMENT_SIZE_BYTES = sizeof(uint);
    
    // Worst case: all voxels are solid and no face culling
    private const int VOXELS_PER_CHUNK = Chunk.SIZE * Chunk.SIZE * Chunk.SIZE;
    private const int FACES_PER_VOXEL = 6;
    private const int VERTICES_PER_FACE = 4;
    private const int INDICES_PER_FACE = 6;

    private const int MAX_FACES_PER_CHUNK = VOXELS_PER_CHUNK * FACES_PER_VOXEL;
    private const int MAX_VERTICES_PER_CHUNK = MAX_FACES_PER_CHUNK * VERTICES_PER_FACE;
    private const int MAX_INDICES_PER_CHUNK = MAX_FACES_PER_CHUNK * INDICES_PER_FACE;

    // Meshing "scratch buffers" to avoid allocations during meshing.
    // We can use a single static buffer since meshing is currently single-threaded, and we process one chunk at a time.
    private static readonly float[] _vertices = new float[MAX_VERTICES_PER_CHUNK * VERTEX_SIZE_ELEMENTS];
    private static readonly uint[] _indices = new uint[MAX_INDICES_PER_CHUNK];
    private static int _vertexDataCount;
    private static uint _vertexCount;
    private static int _indexDataCount;
    

    public static VoxelMeshData MeshChunk(in MeshingInput input)
    {
        // Generate vertex and index data based on the voxel data in the chunk.
        // Optimizations:
        // - Neighbor-based face culling: skip a face if the adjacent voxel is solid.
        _vertexDataCount = 0;
        _vertexCount = 0;
        _indexDataCount = 0;

        Voxel[,,] voxels = input.Center.Voxels;

        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int y = 0; y < Chunk.SIZE; y++)
            {
                for (int z = 0; z < Chunk.SIZE; z++)
                {
                    Voxel voxel = voxels[x, y, z];
                    if (voxel.Id == 0)
                        continue;

                    (float r, float g, float b) = GetColorForVoxel(voxel);

                    // Each vertex has 9 floats: position (3), color (3), and normal (3)
                    float baseX = input.Center.WorldPosition.X + x;
                    float baseY = input.Center.WorldPosition.Y + y;
                    float baseZ = input.Center.WorldPosition.Z + z;

                    // Z+ face
                    if (IsVoxelOccluder(x, y, z + 1, voxels, input.ZPos?.Voxels))
                    {
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 1;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount + 3;
                        _vertexCount += 4;
                    }

                    // Z- face
                    if (IsVoxelOccluder(x, y, z - 1, voxels, input.ZNeg?.Voxels))
                    {
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 1;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount + 3;
                        _vertexCount += 4;
                    }

                    // X+ face
                    if (IsVoxelOccluder(x + 1, y, z, voxels, input.XPos?.Voxels))
                    {
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 1;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount + 3;
                        _vertexCount += 4;
                    }

                    // X- face
                    if (IsVoxelOccluder(x - 1, y, z, voxels, input.XNeg?.Voxels))
                    {
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 0;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 1;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount + 3;
                        _vertexCount += 4;
                    }

                    // Y+ face
                    if (IsVoxelOccluder(x, y + 1, z, voxels, input.YPos?.Voxels))
                    {
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY + 1;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = 1;
                        _vertices[_vertexDataCount++] = 0;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 1;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount + 3;
                        _vertexCount += 4;
                    }

                    // Y- face
                    if (IsVoxelOccluder(x, y - 1, z, voxels, input.YNeg?.Voxels))
                    {
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX + 1;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = baseX;
                        _vertices[_vertexDataCount++] = baseY;
                        _vertices[_vertexDataCount++] = baseZ + 1;
                        _vertices[_vertexDataCount++] = r;
                        _vertices[_vertexDataCount++] = g;
                        _vertices[_vertexDataCount++] = b;
                        _vertices[_vertexDataCount++] = 0;
                        _vertices[_vertexDataCount++] = -1;
                        _vertices[_vertexDataCount++] = 0;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 1;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount;
                        _indices[_indexDataCount++] = _vertexCount + 2;
                        _indices[_indexDataCount++] = _vertexCount + 3;
                        _vertexCount += 4;
                    }
                }
            }
        }
        
        // Return the generated mesh data for this chunk.
        ReadOnlySpan<float> vertices = new(_vertices, 0, _vertexDataCount);
        ReadOnlySpan<uint> indices = new(_indices, 0, _indexDataCount);
        return new VoxelMeshData(vertices, indices);
    }


    public static void SetupVertexAttributes(VertexArrayObject<float, uint> vao)
    {
        vao.SetVertexAttributePointer(0, 3, VertexAttribPointerType.Float, VERTEX_SIZE_ELEMENTS, 0); // position
        vao.SetVertexAttributePointer(1, 3, VertexAttribPointerType.Float, VERTEX_SIZE_ELEMENTS, 3); // color
        vao.SetVertexAttributePointer(2, 3, VertexAttribPointerType.Float, VERTEX_SIZE_ELEMENTS, 6); // normal
    }
    
    
    private static (float r, float g, float b) GetColorForVoxel(Voxel voxel)
    {
        return voxel.Id switch
        {
            0 => throw new ArgumentException("Air voxels should never get meshed!"),
            1 => (1f, 0f, 0f),  // red
            2 => (0f, 1f, 0f),  // green
            3 => (0f, 0f, 1f),  // blue
            4 => (1f, 1f, 0f),  // yellow
            5 => (0f, 1f, 1f),  // cyan
            6 => (1f, 0f, 1f),  // magenta
            7 => (1f, 1f, 1f),  // white
            _ => throw new ArgumentException($"Unknown voxel ID: {voxel.Id}")
        };
    }


    /// <param name="voxels">Voxel data of the current chunk</param>
    /// <param name="neighbourVoxels">Voxel data of the neighbor chunk in the occluder direction, or null at world boundary</param>
    /// <param name="x">Voxel X coordinate</param>
    /// <param name="y">Voxel Y coordinate</param>
    /// <param name="z">Voxel Z coordinate</param>
    private static bool IsVoxelOccluder(int x, int y, int z, Voxel[,,] voxels, Voxel[,,]? neighbourVoxels)
    {
        // Within chunk bounds: check the adjacent voxel directly
        if (x is >= 0 and < Chunk.SIZE && 
            y is >= 0 and < Chunk.SIZE &&
            z is >= 0 and < Chunk.SIZE)
            return voxels[x, y, z].Id == 0;

        if (neighbourVoxels == null)
            return true;

        // Wrap out-of-bounds coordinate to the opposite side of the neighbor chunk
        if (x >= Chunk.SIZE)
            x = 0;
        else if (x < 0)
            x = Chunk.SIZE - 1;
        if (y >= Chunk.SIZE)
            y = 0;
        else if (y < 0)
            y = Chunk.SIZE - 1;
        if (z >= Chunk.SIZE)
            z = 0;
        else if (z < 0)
            z = Chunk.SIZE - 1;

        return neighbourVoxels[x, y, z].Id == 0;
    }
}