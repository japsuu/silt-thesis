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
    /// Reused per axis (derived from <see cref="_solidSliceMasksBase"/> for Y and Z axes).
    /// </summary>
    private static readonly uint[] _solidSliceMasks = new uint[Chunk.SIZE * Chunk.SIZE];

    /// <summary>
    /// Persistent copy of the X-axis solid bitmasks, used to derive Y and Z axis masks via transpose.
    /// Layout: _solidSliceMasksBase[x * SIZE + y] = bitmask of solid voxels along the z axis.
    /// Built once per <see cref="MeshChunk"/> call; the Y-axis and Z-axis masks are then computed
    /// from this buffer using index transpose and bit transpose respectively, avoiding redundant
    /// voxel array scans.
    /// </summary>
    private static readonly uint[] _solidSliceMasksBase = new uint[Chunk.SIZE * Chunk.SIZE];

    /// <summary>
    /// Pre-computed boundary bitmasks for the 6 neighbor faces.
    /// Layout: _neighbourBoundaryMasks[faceIndex * Chunk.SIZE + row] = bitmask of solid voxels
    /// in the adjacent boundary slice of the neighboring chunk.
    /// Built once per <see cref="MeshChunk"/> call. If the neighbor is null (world edge), the
    /// corresponding segment remains zero.
    /// Face indices reuse the NORMAL_* constants (0–5).
    /// </summary>
    private static readonly uint[] _neighbourBoundaryMasks = new uint[6 * Chunk.SIZE];

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

        // Pre-compute boundary bitmasks for all 6 neighbor faces.
        // Moves scattered neighbor reads out of the hot face-culling loop into a single pass.
        PrecomputeBoundaryMasks(in input);

        // X AXIS FACES (X+ and X-)
        // Solid mask layout: _solidSliceMasks[x * SIZE + y], bits represent z positions
        Array.Clear(_solidSliceMasks, 0, _solidSliceMasks.Length);
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int y = 0; y < Chunk.SIZE; y++)
            {
                for (int z = 0; z < Chunk.SIZE; z++)
                {
                    if (ids[Chunk.Idx(x, y, z)] != 0)
                        _solidSliceMasks[x * Chunk.SIZE + y] |= 1u << z;
                }
            }
        }

        // Persist X-axis masks for later transposition to Y and Z axes
        Array.Copy(_solidSliceMasks, _solidSliceMasksBase, _solidSliceMasks.Length);

        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            int normalIdx = positive ? NORMAL_X_POS : NORMAL_X_NEG;

            for (int x = 0; x < Chunk.SIZE; x++)
            {
                Array.Clear(_faceMasks, 0, _faceMasks.Length);

                int adjX = positive ? x + 1 : x - 1;
                bool isBoundary = adjX < 0 || adjX >= Chunk.SIZE;

                for (int y = 0; y < Chunk.SIZE; y++)
                {
                    uint solidMask = _solidSliceMasks[x * Chunk.SIZE + y];
                    if (solidMask == 0)
                        continue;

                    // Bitwise face culling: get neighbor solid mask for the adjacent slice
                    uint neighborMask = !isBoundary
                        ? _solidSliceMasks[adjX * Chunk.SIZE + y]
                        // Pre-computed boundary mask (zero if no neighbor = world edge)
                        : _neighbourBoundaryMasks[normalIdx * Chunk.SIZE + y];

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

                BinaryGreedyMerge(0, positive, x);
            }
        }

        // Y AXIS FACES (Y+ and Y-)
        // Derive Y-axis masks from X-axis via index transpose:
        // X-axis: [x * SIZE + y] bits=z -> Y-axis: [y * SIZE + x] bits=z
        for (int y = 0; y < Chunk.SIZE; y++)
        {
            for (int x = 0; x < Chunk.SIZE; x++)
                _solidSliceMasks[y * Chunk.SIZE + x] = _solidSliceMasksBase[x * Chunk.SIZE + y];
        }

        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            int normalIdx = positive ? NORMAL_Y_POS : NORMAL_Y_NEG;

            for (int y = 0; y < Chunk.SIZE; y++)
            {
                Array.Clear(_faceMasks, 0, _faceMasks.Length);

                int adjY = positive ? y + 1 : y - 1;
                bool isBoundary = adjY < 0 || adjY >= Chunk.SIZE;

                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    uint solidMask = _solidSliceMasks[y * Chunk.SIZE + x];
                    if (solidMask == 0)
                        continue;

                    uint neighborMask = !isBoundary
                        ? _solidSliceMasks[adjY * Chunk.SIZE + x]
                        // Pre-computed boundary mask (zero if no neighbor = world edge)
                        : _neighbourBoundaryMasks[normalIdx * Chunk.SIZE + x];

                    uint visibleMask = solidMask & ~neighborMask;

                    while (visibleMask != 0)
                    {
                        int z = BitOperations.TrailingZeroCount(visibleMask);
                        visibleMask &= visibleMask - 1;
                        int id = ids[Chunk.Idx(x, y, z)];
                        _faceMasks[id * Chunk.SIZE + x] |= 1u << z;
                    }
                }

                BinaryGreedyMerge(1, positive, y);
            }
        }

        // Z AXIS FACES (Z+ and Z-)
        // Derive Z-axis masks from X-axis via bit transpose (no voxel array access needed):
        // X-axis: [x * SIZE + y] bits=z -> Z-axis: [z * SIZE + x] bits=y
        // For each (x, y), extract set bits z from the X-axis mask and scatter them as bit y
        // into the Z-axis mask at [z * SIZE + x]. Operates on 4 KiB of L1-resident data.
        Array.Clear(_solidSliceMasks, 0, _solidSliceMasks.Length);
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int y = 0; y < Chunk.SIZE; y++)
            {
                uint mask = _solidSliceMasksBase[x * Chunk.SIZE + y];
                uint yBit = 1u << y;
                while (mask != 0)
                {
                    int z = BitOperations.TrailingZeroCount(mask);
                    mask &= mask - 1; // clear lowest set bit
                    _solidSliceMasks[z * Chunk.SIZE + x] |= yBit;
                }
            }
        }

        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            int normalIdx = positive ? NORMAL_Z_POS : NORMAL_Z_NEG;

            for (int z = 0; z < Chunk.SIZE; z++)
            {
                Array.Clear(_faceMasks, 0, _faceMasks.Length);

                int adjZ = positive ? z + 1 : z - 1;
                bool isBoundary = adjZ < 0 || adjZ >= Chunk.SIZE;

                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    uint solidMask = _solidSliceMasks[z * Chunk.SIZE + x];
                    if (solidMask == 0)
                        continue;

                    uint neighborMask = !isBoundary
                        ? _solidSliceMasks[adjZ * Chunk.SIZE + x]
                        // Pre-computed boundary mask (zero if no neighbor = world edge)
                        : _neighbourBoundaryMasks[normalIdx * Chunk.SIZE + x];


                    uint visibleMask = solidMask & ~neighborMask;

                    while (visibleMask != 0)
                    {
                        int y = BitOperations.TrailingZeroCount(visibleMask);
                        visibleMask &= visibleMask - 1;
                        int id = ids[Chunk.Idx(x, y, z)];
                        _faceMasks[id * Chunk.SIZE + x] |= 1u << y;
                    }
                }

                BinaryGreedyMerge(2, positive, z);
            }
        }

        // Return the generated mesh data for this chunk.
        ReadOnlySpan<uint> vertices = new(_vertices, 0, _vertexDataCount);
        ReadOnlySpan<uint> indices = new(_indices, 0, _indexDataCount);
        return new VoxelMeshData(vertices, indices);
    }


    /// <summary>
    /// Pre-computes boundary bitmasks for all 6 neighbor faces into <see cref="_neighbourBoundaryMasks"/>.
    /// For each face direction, reads the adjacent boundary slice of the neighboring chunk (if present)
    /// and builds a uint bitmask per row matching the layout expected by the per-axis face-culling loops.
    /// If the neighbor is null (world edge), the corresponding segment stays zero (all faces visible).
    /// </summary>
    private static void PrecomputeBoundaryMasks(in MeshingInput input)
    {
        Array.Clear(_neighbourBoundaryMasks, 0, _neighbourBoundaryMasks.Length);

        const int s = Chunk.SIZE;

        // X+ boundary (face at x=SIZE-1 looking towards neighbor XPos slice x=0)
        // Layout: [y] bits=z
        if (input.XPos != null)
        {
            int[] nIds = input.XPos.VoxelIds;
            const int baseIdx = NORMAL_X_POS * s;
            for (int y = 0; y < s; y++)
            {
                for (int z = 0; z < s; z++)
                {
                    if (nIds[Chunk.Idx(0, y, z)] != 0)
                        _neighbourBoundaryMasks[baseIdx + y] |= 1u << z;
                }
            }
        }

        // X- boundary (face at x=0 looking towards neighbor XNeg slice x=SIZE-1)
        // Layout: [y] bits=z
        if (input.XNeg != null)
        {
            int[] nIds = input.XNeg.VoxelIds;
            const int baseIdx = NORMAL_X_NEG * s;
            for (int y = 0; y < s; y++)
            {
                for (int z = 0; z < s; z++)
                {
                    if (nIds[Chunk.Idx(s - 1, y, z)] != 0)
                        _neighbourBoundaryMasks[baseIdx + y] |= 1u << z;
                }
            }
        }

        // Y+ boundary (face at y=SIZE-1 looking towards neighbor YPos slice y=0)
        // Layout: [x] bits=z
        if (input.YPos != null)
        {
            int[] nIds = input.YPos.VoxelIds;
            const int baseIdx = NORMAL_Y_POS * s;
            for (int x = 0; x < s; x++)
            {
                for (int z = 0; z < s; z++)
                {
                    if (nIds[Chunk.Idx(x, 0, z)] != 0)
                        _neighbourBoundaryMasks[baseIdx + x] |= 1u << z;
                }
            }
        }

        // Y- boundary (face at y=0 looking towards neighbor YNeg slice y=SIZE-1)
        // Layout: [x] bits=z
        if (input.YNeg != null)
        {
            int[] nIds = input.YNeg.VoxelIds;
            const int baseIdx = NORMAL_Y_NEG * s;
            for (int x = 0; x < s; x++)
            {
                for (int z = 0; z < s; z++)
                {
                    if (nIds[Chunk.Idx(x, s - 1, z)] != 0)
                        _neighbourBoundaryMasks[baseIdx + x] |= 1u << z;
                }
            }
        }

        // Z+ boundary (face at z=SIZE-1 looking towards neighbor ZPos slice z=0)
        // Layout: [x] bits=y
        if (input.ZPos != null)
        {
            int[] nIds = input.ZPos.VoxelIds;
            const int baseIdx = NORMAL_Z_POS * s;
            for (int x = 0; x < s; x++)
            {
                for (int y = 0; y < s; y++)
                {
                    if (nIds[Chunk.Idx(x, y, 0)] != 0)
                        _neighbourBoundaryMasks[baseIdx + x] |= 1u << y;
                }
            }
        }

        // Z- boundary (face at z=0 looking towards neighbor ZNeg slice z=SIZE-1)
        // Layout: [x] bits=y
        if (input.ZNeg != null)
        {
            int[] nIds = input.ZNeg.VoxelIds;
            const int baseIdx = NORMAL_Z_NEG * s;
            for (int x = 0; x < s; x++)
            {
                for (int y = 0; y < s; y++)
                {
                    if (nIds[Chunk.Idx(x, y, s - 1)] != 0)
                        _neighbourBoundaryMasks[baseIdx + x] |= 1u << y;
                }
            }
        }
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
    /// <param name="axis">0 = X, 1 = Y, 2 = Z</param>
    /// <param name="positive">true for the positive-direction face, false for negative</param>
    /// <param name="sliceCoord">The slice coordinate along the current axis</param>
    private static void BinaryGreedyMerge(int axis, bool positive, int sliceCoord)
    {
        int normalIdx = axis switch
        {
            0 => positive ? NORMAL_X_POS : NORMAL_X_NEG,
            1 => positive ? NORMAL_Y_POS : NORMAL_Y_NEG,
            _ => positive ? NORMAL_Z_POS : NORMAL_Z_NEG
        };
        int sliceOffset = positive ? 1 : 0;

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
                    while (row + height < Chunk.SIZE && (_faceMasks[maskBase + row + height] & runMask) == runMask)
                    {
                        _faceMasks[maskBase + row + height] &= ~runMask;
                        height++;
                    }

                    rowMask &= ~runMask;

                    // Emit quad
                    int colorIdx = id - 1;
                    switch (axis)
                    {
                        case 0:
                        {
                            int px = sliceCoord + sliceOffset;
                            int py0 = row;
                            int py1 = row + height;
                            int pz0 = startBit;
                            int pz1 = startBit + width;
                            if (positive)
                                EmitQuad(px, py0, pz1, px, py0, pz0, px, py1, pz0, px, py1, pz1, colorIdx, normalIdx);
                            else
                                EmitQuad(px, py0, pz0, px, py0, pz1, px, py1, pz1, px, py1, pz0, colorIdx, normalIdx);
                            break;
                        }
                        case 1:
                        {
                            int py = sliceCoord + sliceOffset;
                            int px0 = row;
                            int px1 = row + height;
                            int pz0 = startBit;
                            int pz1 = startBit + width;
                            if (positive)
                                EmitQuad(px0, py, pz1, px1, py, pz1, px1, py, pz0, px0, py, pz0, colorIdx, normalIdx);
                            else
                                EmitQuad(px0, py, pz0, px1, py, pz0, px1, py, pz1, px0, py, pz1, colorIdx, normalIdx);
                            break;
                        }
                        default:
                        {
                            int pz = sliceCoord + sliceOffset;
                            int px0 = row;
                            int px1 = row + height;
                            int py0 = startBit;
                            int py1 = startBit + width;
                            if (positive)
                                EmitQuad(px0, py0, pz, px1, py0, pz, px1, py1, pz, px0, py1, pz, colorIdx, normalIdx);
                            else
                                EmitQuad(px1, py0, pz, px0, py0, pz, px0, py1, pz, px1, py1, pz, colorIdx, normalIdx);
                            break;
                        }
                    }
                }
            }
        }
    }


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
        int colorIndex,
        int normalIndex)
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
