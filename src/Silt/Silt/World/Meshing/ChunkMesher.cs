using System.Numerics;
using Silk.NET.OpenGL;
using Silt.Core.Graphics;

namespace Silt.World.Meshing;

/// <summary>
/// Contains the vertex and index data for rendering a chunk of voxels.
/// Produced by the meshing system based on the voxel data in a chunk.
/// Each vertex is a single packed uint.
/// </summary>
public readonly ref struct VoxelMeshData(ReadOnlySpan<uint> vertices, ReadOnlySpan<uint> indices)
{
    public readonly ReadOnlySpan<uint> Vertices = vertices;
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
    public const int VERTEX_SIZE_ELEMENTS = 1;
    public const int VERTEX_ELEMENT_SIZE_BYTES = sizeof(uint);
    public const int INDEX_ELEMENT_SIZE_BYTES = sizeof(uint);

    // Normal direction indices (must match shader lookup table)
    private const int NORMAL_X_POS = 0;
    private const int NORMAL_X_NEG = 1;
    private const int NORMAL_Y_POS = 2;
    private const int NORMAL_Y_NEG = 3;
    private const int NORMAL_Z_POS = 4;
    private const int NORMAL_Z_NEG = 5;

    // Worst case: all voxels are solid and no face culling
    private const int VOXELS_PER_CHUNK = Chunk.SIZE * Chunk.SIZE * Chunk.SIZE;
    private const int FACES_PER_VOXEL = 6;
    private const int VERTICES_PER_FACE = 4;
    private const int INDICES_PER_FACE = 6;

    private const int MAX_FACES_PER_CHUNK = VOXELS_PER_CHUNK * FACES_PER_VOXEL;
    private const int MAX_VERTICES_PER_CHUNK = MAX_FACES_PER_CHUNK * VERTICES_PER_FACE;
    private const int MAX_INDICES_PER_CHUNK = MAX_FACES_PER_CHUNK * INDICES_PER_FACE;

    /// <summary>Maximum voxel ID supported (IDs 1..MAX_VOXEL_ID).</summary>
    private const int MAX_VOXEL_ID = 7;

    // Meshing "scratch buffers" to avoid allocations during meshing.
    // We can use a single static buffer since meshing is currently single-threaded, and we process one chunk at a time.
    private static readonly uint[] _vertices = new uint[MAX_VERTICES_PER_CHUNK * VERTEX_SIZE_ELEMENTS];
    private static readonly uint[] _indices = new uint[MAX_INDICES_PER_CHUNK];
    private static int _vertexDataCount;
    private static uint _vertexCount;
    private static int _indexDataCount;

    /// <summary>
    /// Per-axis solid bitmasks for all slices.
    /// Layout: _solidSliceMasks[slice * Chunk.SIZE + row] = bitmask of solid voxels along the column axis.
    /// Each bit in the uint represents one voxel position along the column axis (0 = air, 1 = solid).
    /// Reused per axis (cleared and rebuilt for X, Y, Z in turn).
    /// </summary>
    private static readonly uint[] _solidSliceMasks = new uint[Chunk.SIZE * Chunk.SIZE];

    /// <summary>
    /// Per-voxel-type binary face masks for one slice.
    /// Layout: _faceMasks[voxelId * Chunk.SIZE + row] = bitmask of visible faces along the column axis.
    /// Cleared and rebuilt for every slice.
    /// </summary>
    private static readonly uint[] _faceMasks = new uint[(MAX_VOXEL_ID + 1) * Chunk.SIZE];


    /// <summary>
    /// Packs a chunk-local vertex position, color index, and normal index into a single uint.
    /// </summary>
    private static uint PackVertex(int x, int y, int z, int colorIndex, int normalIndex)
    {
        // Bits 0–5:   local X position (0–32)
        // Bits 6–11:  local Y position (0–32)
        // Bits 12–17: local Z position (0–32)
        // Bits 18–20: color index      (0–6, maps to voxel ID 1–7)
        // Bits 21–23: normal index     (0–5: +X, −X, +Y, −Y, +Z, −Z)
        return (uint)x
               | ((uint)y << 6)
               | ((uint)z << 12)
               | ((uint)colorIndex << 18)
               | ((uint)normalIndex << 21);
    }


    public static VoxelMeshData MeshChunk(in MeshingInput input)
    {
        // Binary meshing approach:
        // 1. For each axis, precompute solid bitmasks for all slices (one uint per row, one bit per column voxel).
        // 2. For each face direction (+/-), for each slice:
        //    a. Bitwise face culling: visible = solidMask & ~neighborMask  (AND-NOT)
        //    b. Distribute visible bits into per-voxel-type face masks
        //    c. Binary greedy merge: use TrailingZeroCount for O(1) run detection,
        //       extend runs vertically by masking against subsequent rows
        _vertexDataCount = 0;
        _vertexCount = 0;
        _indexDataCount = 0;

        int[] ids = input.Center.VoxelIds;

        // X AXIS FACES (X+ and X-)
        // Solid mask layout: _solidSliceMasks[x * SIZE + y], bits represent z positions
        Array.Clear(_solidSliceMasks, 0, _solidSliceMasks.Length);
        for (int x = 0; x < Chunk.SIZE; x++)
            for (int y = 0; y < Chunk.SIZE; y++)
                for (int z = 0; z < Chunk.SIZE; z++)
                    if (ids[Chunk.Idx(x, y, z)] != 0)
                        _solidSliceMasks[x * Chunk.SIZE + y] |= 1u << z;

        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            int normalIdx = positive ? NORMAL_X_POS : NORMAL_X_NEG;
            int[]? neighbourIds = positive ? input.XPos?.VoxelIds : input.XNeg?.VoxelIds;

            for (int x = 0; x < Chunk.SIZE; x++)
            {
                Array.Clear(_faceMasks, 0, _faceMasks.Length);

                int adjX = positive ? x + 1 : x - 1;
                bool isBoundary = adjX < 0 || adjX >= Chunk.SIZE;

                for (int y = 0; y < Chunk.SIZE; y++)
                {
                    uint solidMask = _solidSliceMasks[x * Chunk.SIZE + y];
                    if (solidMask == 0) continue;

                    // Bitwise face culling: get neighbor solid mask for the adjacent slice
                    uint neighborMask;
                    if (!isBoundary)
                    {
                        neighborMask = _solidSliceMasks[adjX * Chunk.SIZE + y];
                    }
                    else if (neighbourIds != null)
                    {
                        int wrappedX = positive ? 0 : Chunk.SIZE - 1;
                        neighborMask = 0;
                        for (int z = 0; z < Chunk.SIZE; z++)
                            if (neighbourIds[Chunk.Idx(wrappedX, y, z)] != 0)
                                neighborMask |= 1u << z;
                    }
                    else
                    {
                        neighborMask = 0; // World boundary: all faces visible
                    }

                    // Visible faces: solid AND neighbor is air (bitwise AND-NOT)
                    uint visibleMask = solidMask & ~neighborMask;

                    // Distribute visible bits into per-type face masks using bit extraction
                    while (visibleMask != 0)
                    {
                        int z = BitOperations.TrailingZeroCount(visibleMask);
                        visibleMask &= visibleMask - 1; // clear lowest set bit
                        int id = ids[Chunk.Idx(x, y, z)];
                        _faceMasks[id * Chunk.SIZE + y] |= 1u << z;
                    }
                }

                // Binary greedy merge and emit quads
                int sliceX = x;
                BinaryGreedyMerge((row, startBit, width, height, id) =>
                {
                    // row = y, startBit = z
                    int colorIdx = id - 1;
                    int px = sliceX + (positive ? 1 : 0);
                    int py0 = row;
                    int py1 = row + height;
                    int pz0 = startBit;
                    int pz1 = startBit + width;

                    if (positive)
                    {
                        // X+ face
                        EmitQuad(
                            px, py0, pz1,
                            px, py0, pz0,
                            px, py1, pz0,
                            px, py1, pz1,
                            colorIdx, normalIdx);
                    }
                    else
                    {
                        // X- face
                        EmitQuad(
                            px, py0, pz0,
                            px, py0, pz1,
                            px, py1, pz1,
                            px, py1, pz0,
                            colorIdx, normalIdx);
                    }
                });
            }
        }

        // Y AXIS FACES (Y+ and Y-)
        // Solid mask layout: _solidSliceMasks[y * SIZE + x], bits represent z positions
        Array.Clear(_solidSliceMasks, 0, _solidSliceMasks.Length);
        for (int y = 0; y < Chunk.SIZE; y++)
            for (int x = 0; x < Chunk.SIZE; x++)
                for (int z = 0; z < Chunk.SIZE; z++)
                    if (ids[Chunk.Idx(x, y, z)] != 0)
                        _solidSliceMasks[y * Chunk.SIZE + x] |= 1u << z;

        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            int normalIdx = positive ? NORMAL_Y_POS : NORMAL_Y_NEG;
            int[]? neighbourIds = positive ? input.YPos?.VoxelIds : input.YNeg?.VoxelIds;

            for (int y = 0; y < Chunk.SIZE; y++)
            {
                Array.Clear(_faceMasks, 0, _faceMasks.Length);

                int adjY = positive ? y + 1 : y - 1;
                bool isBoundary = adjY < 0 || adjY >= Chunk.SIZE;

                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    uint solidMask = _solidSliceMasks[y * Chunk.SIZE + x];
                    if (solidMask == 0) continue;

                    uint neighborMask;
                    if (!isBoundary)
                    {
                        neighborMask = _solidSliceMasks[adjY * Chunk.SIZE + x];
                    }
                    else if (neighbourIds != null)
                    {
                        int wrappedY = positive ? 0 : Chunk.SIZE - 1;
                        neighborMask = 0;
                        for (int z = 0; z < Chunk.SIZE; z++)
                            if (neighbourIds[Chunk.Idx(x, wrappedY, z)] != 0)
                                neighborMask |= 1u << z;
                    }
                    else
                    {
                        neighborMask = 0;
                    }

                    uint visibleMask = solidMask & ~neighborMask;

                    while (visibleMask != 0)
                    {
                        int z = BitOperations.TrailingZeroCount(visibleMask);
                        visibleMask &= visibleMask - 1;
                        int id = ids[Chunk.Idx(x, y, z)];
                        _faceMasks[id * Chunk.SIZE + x] |= 1u << z;
                    }
                }

                int sliceY = y;
                BinaryGreedyMerge((row, startBit, width, height, id) =>
                {
                    // row = x, startBit = z
                    int colorIdx = id - 1;
                    int py = sliceY + (positive ? 1 : 0);
                    int px0 = row;
                    int px1 = row + height;
                    int pz0 = startBit;
                    int pz1 = startBit + width;

                    if (positive)
                    {
                        // Y+ face
                        EmitQuad(
                            px0, py, pz1,
                            px1, py, pz1,
                            px1, py, pz0,
                            px0, py, pz0,
                            colorIdx, normalIdx);
                    }
                    else
                    {
                        // Y- face
                        EmitQuad(
                            px0, py, pz0,
                            px1, py, pz0,
                            px1, py, pz1,
                            px0, py, pz1,
                            colorIdx, normalIdx);
                    }
                });
            }
        }

        // Z AXIS FACES (Z+ and Z-)
        // Solid mask layout: _solidSliceMasks[z * SIZE + x], bits represent y positions
        Array.Clear(_solidSliceMasks, 0, _solidSliceMasks.Length);
        for (int z = 0; z < Chunk.SIZE; z++)
            for (int x = 0; x < Chunk.SIZE; x++)
                for (int y = 0; y < Chunk.SIZE; y++)
                    if (ids[Chunk.Idx(x, y, z)] != 0)
                        _solidSliceMasks[z * Chunk.SIZE + x] |= 1u << y;

        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            int normalIdx = positive ? NORMAL_Z_POS : NORMAL_Z_NEG;
            int[]? neighbourIds = positive ? input.ZPos?.VoxelIds : input.ZNeg?.VoxelIds;

            for (int z = 0; z < Chunk.SIZE; z++)
            {
                Array.Clear(_faceMasks, 0, _faceMasks.Length);

                int adjZ = positive ? z + 1 : z - 1;
                bool isBoundary = adjZ < 0 || adjZ >= Chunk.SIZE;

                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    uint solidMask = _solidSliceMasks[z * Chunk.SIZE + x];
                    if (solidMask == 0) continue;

                    uint neighborMask;
                    if (!isBoundary)
                    {
                        neighborMask = _solidSliceMasks[adjZ * Chunk.SIZE + x];
                    }
                    else if (neighbourIds != null)
                    {
                        int wrappedZ = positive ? 0 : Chunk.SIZE - 1;
                        neighborMask = 0;
                        for (int y = 0; y < Chunk.SIZE; y++)
                            if (neighbourIds[Chunk.Idx(x, y, wrappedZ)] != 0)
                                neighborMask |= 1u << y;
                    }
                    else
                    {
                        neighborMask = 0;
                    }

                    uint visibleMask = solidMask & ~neighborMask;

                    while (visibleMask != 0)
                    {
                        int y = BitOperations.TrailingZeroCount(visibleMask);
                        visibleMask &= visibleMask - 1;
                        int id = ids[Chunk.Idx(x, y, z)];
                        _faceMasks[id * Chunk.SIZE + x] |= 1u << y;
                    }
                }

                int sliceZ = z;
                BinaryGreedyMerge((row, startBit, width, height, id) =>
                {
                    // row = x, startBit = y
                    int colorIdx = id - 1;
                    int pz = sliceZ + (positive ? 1 : 0);
                    int px0 = row;
                    int px1 = row + height;
                    int py0 = startBit;
                    int py1 = startBit + width;

                    if (positive)
                    {
                        // Z+ face
                        EmitQuad(
                            px0, py0, pz,
                            px1, py0, pz,
                            px1, py1, pz,
                            px0, py1, pz,
                            colorIdx, normalIdx);
                    }
                    else
                    {
                        // Z- face
                        EmitQuad(
                            px1, py0, pz,
                            px0, py0, pz,
                            px0, py1, pz,
                            px1, py1, pz,
                            colorIdx, normalIdx);
                    }
                });
            }
        }

        // Return the generated mesh data for this chunk.
        ReadOnlySpan<uint> vertices = new(_vertices, 0, _vertexDataCount);
        ReadOnlySpan<uint> indices = new(_indices, 0, _indexDataCount);
        return new VoxelMeshData(vertices, indices);
    }


    /// <summary>
    /// Binary greedy merge: iterates all per-type face masks in <see cref="_faceMasks"/>,
    /// using bitwise operations to find maximal rectangular regions of same-type visible faces.
    /// 
    /// For each voxel type, scans rows of the face mask. Within each row, uses
    /// <see cref="BitOperations.TrailingZeroCount(int)"/> to locate the first set bit (start of a run)
    /// and the first clear bit after it (end of the run) in O(1). The resulting run mask is then
    /// tested against subsequent rows to extend the rectangle vertically, again using a single
    /// bitwise AND + compare per row.
    /// 
    /// Merged bits are cleared from the masks as they are consumed.
    /// </summary>
    private static void BinaryGreedyMerge(EmitBinaryQuadDelegate emitQuad)
    {
        for (int id = 1; id <= MAX_VOXEL_ID; id++)
        {
            int maskBase = id * Chunk.SIZE;

            for (int row = 0; row < Chunk.SIZE; row++)
            {
                ref uint rowMask = ref _faceMasks[maskBase + row];
                while (rowMask != 0)
                {
                    // Find the first set bit (start of a horizontal run)
                    int startBit = BitOperations.TrailingZeroCount(rowMask);

                    // Count consecutive set bits (width of the run)
                    // Shift so the run starts at bit 0, invert, then count trailing zeros of the inverted value
                    int width = BitOperations.TrailingZeroCount(~(rowMask >> startBit));

                    // Build a bitmask covering the run
                    // width==32: (1u << 32) is undefined in C# (shift mod 32 = no-op)
                    uint runMask = width >= 32
                        ? uint.MaxValue
                        : ((1u << width) - 1) << startBit;

                    // Extend vertically: check subsequent rows for the same run pattern
                    int height = 1;
                    while (row + height < Chunk.SIZE &&
                           (_faceMasks[maskBase + row + height] & runMask) == runMask)
                    {
                        _faceMasks[maskBase + row + height] &= ~runMask;
                        height++;
                    }

                    rowMask &= ~runMask;

                    emitQuad(row, startBit, width, height, id);
                }
            }
        }
    }

    private delegate void EmitBinaryQuadDelegate(int row, int startBit, int width, int height, int voxelId);


    /// <summary>
    /// Emits a single quad (4 vertices, 6 indices) into the scratch buffers.
    /// Vertices are specified in CCW winding order as seen from the front face.
    /// Each vertex is packed into a single uint via <see cref="PackVertex"/>.
    /// </summary>
    private static void EmitQuad(
        int x0, int y0, int z0,
        int x1, int y1, int z1,
        int x2, int y2, int z2,
        int x3, int y3, int z3,
        int colorIndex, int normalIndex)
    {
        int vi = _vertexDataCount;

        _vertices[vi++] = PackVertex(x0, y0, z0, colorIndex, normalIndex);
        _vertices[vi++] = PackVertex(x1, y1, z1, colorIndex, normalIndex);
        _vertices[vi++] = PackVertex(x2, y2, z2, colorIndex, normalIndex);
        _vertices[vi++] = PackVertex(x3, y3, z3, colorIndex, normalIndex);

        _vertexDataCount = vi;

        // Two triangles: 0-1-2 and 0-2-3
        int ii = _indexDataCount;
        _indices[ii++] = _vertexCount;
        _indices[ii++] = _vertexCount + 1;
        _indices[ii++] = _vertexCount + 2;
        _indices[ii++] = _vertexCount;
        _indices[ii++] = _vertexCount + 2;
        _indices[ii++] = _vertexCount + 3;
        _indexDataCount = ii;

        _vertexCount += 4;
    }


    public static void SetupVertexAttributes(VertexArrayObject<uint, uint> vao)
    {
        vao.SetVertexAttributeIPointer(0, 1, VertexAttribIType.UnsignedInt, 1, 0);
    }
}
