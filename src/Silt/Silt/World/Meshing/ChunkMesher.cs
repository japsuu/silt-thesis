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

    // Greedy meshing scratch mask: stores the voxel ID of visible faces for the current slice.
    // 0 means no face (air or occluded). Positive value = voxel ID to merge.
    private static readonly int[] _mask = new int[Chunk.SIZE * Chunk.SIZE];


    public static VoxelMeshData MeshChunk(in MeshingInput input)
    {
        // Generate vertex and index data based on the voxel data in the chunk.
        // Optimizations:
        // - Neighbor-based face culling: skip faces between adjacent solid voxels (including across chunk boundaries)
        // - Greedy meshing with neighbor-based face culling:
        //   For each of the 6 face directions, we sweep through slices perpendicular to the face normal.
        //   For each slice we build a 2D mask of visible face voxel IDs, then greedily merge
        //   adjacent same-ID entries into larger rectangular quads.
        _vertexDataCount = 0;
        _vertexCount = 0;
        _indexDataCount = 0;

        Voxel[,,] voxels = input.Center.Voxels;
        float worldX = input.Center.WorldPosition.X;
        float worldY = input.Center.WorldPosition.Y;
        float worldZ = input.Center.WorldPosition.Z;

        // --- X axis faces (X+ and X-) ---
        // Slice perpendicular to X: iterate x from 0..SIZE, mask is [y, z]
        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0; // X+ or X-
            float nx = positive ? 1f : -1f;

            for (int x = 0; x < Chunk.SIZE; x++)
            {
                // Build mask for this slice
                for (int y = 0; y < Chunk.SIZE; y++)
                {
                    for (int z = 0; z < Chunk.SIZE; z++)
                    {
                        int voxelId = voxels[x, y, z].Id;
                        if (voxelId == 0)
                        {
                            _mask[y * Chunk.SIZE + z] = 0;
                            continue;
                        }

                        int adjX = positive ? x + 1 : x - 1;
                        Voxel[,,]? neighbour = positive ? input.XPos?.Voxels : input.XNeg?.Voxels;
                        bool visible = IsVoxelOccluder(adjX, y, z, voxels, neighbour);
                        _mask[y * Chunk.SIZE + z] = visible ? voxelId : 0;
                    }
                }

                // Greedy merge the mask
                GreedyMerge(Chunk.SIZE, Chunk.SIZE, (row, col, w, h, id) =>
                {
                    // row = y, col = z
                    (float r, float g, float b) = GetColorForId(id);
                    float px = worldX + x + (positive ? 1 : 0);
                    float py0 = worldY + row;
                    float py1 = worldY + row + h;
                    float pz0 = worldZ + col;
                    float pz1 = worldZ + col + w;

                    if (positive)
                    {
                        // X+ face: vertices wound CCW when viewed from +X
                        EmitQuad(
                            px, py0, pz1,
                            px, py0, pz0,
                            px, py1, pz0,
                            px, py1, pz1,
                            r, g, b, nx, 0, 0);
                    }
                    else
                    {
                        // X- face
                        EmitQuad(
                            px, py0, pz0,
                            px, py0, pz1,
                            px, py1, pz1,
                            px, py1, pz0,
                            r, g, b, nx, 0, 0);
                    }
                });
            }
        }

        // --- Y axis faces (Y+ and Y-) ---
        // Slice perpendicular to Y: iterate y from 0..SIZE, mask is [x, z]
        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            float ny = positive ? 1f : -1f;

            for (int y = 0; y < Chunk.SIZE; y++)
            {
                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    for (int z = 0; z < Chunk.SIZE; z++)
                    {
                        int voxelId = voxels[x, y, z].Id;
                        if (voxelId == 0)
                        {
                            _mask[x * Chunk.SIZE + z] = 0;
                            continue;
                        }

                        int adjY = positive ? y + 1 : y - 1;
                        Voxel[,,]? neighbour = positive ? input.YPos?.Voxels : input.YNeg?.Voxels;
                        bool visible = IsVoxelOccluder(x, adjY, z, voxels, neighbour);
                        _mask[x * Chunk.SIZE + z] = visible ? voxelId : 0;
                    }
                }

                GreedyMerge(Chunk.SIZE, Chunk.SIZE, (row, col, w, h, id) =>
                {
                    // row = x, col = z
                    (float r, float g, float b) = GetColorForId(id);
                    float py = worldY + y + (positive ? 1 : 0);
                    float px0 = worldX + row;
                    float px1 = worldX + row + h;
                    float pz0 = worldZ + col;
                    float pz1 = worldZ + col + w;

                    if (positive)
                    {
                        // Y+ face
                        EmitQuad(
                            px0, py, pz1,
                            px1, py, pz1,
                            px1, py, pz0,
                            px0, py, pz0,
                            r, g, b, 0, ny, 0);
                    }
                    else
                    {
                        // Y- face
                        EmitQuad(
                            px0, py, pz0,
                            px1, py, pz0,
                            px1, py, pz1,
                            px0, py, pz1,
                            r, g, b, 0, ny, 0);
                    }
                });
            }
        }

        // --- Z axis faces (Z+ and Z-) ---
        // Slice perpendicular to Z: iterate z from 0..SIZE, mask is [x, y]
        for (int face = 0; face < 2; face++)
        {
            bool positive = face == 0;
            float nz = positive ? 1f : -1f;

            for (int z = 0; z < Chunk.SIZE; z++)
            {
                for (int x = 0; x < Chunk.SIZE; x++)
                {
                    for (int y = 0; y < Chunk.SIZE; y++)
                    {
                        int voxelId = voxels[x, y, z].Id;
                        if (voxelId == 0)
                        {
                            _mask[x * Chunk.SIZE + y] = 0;
                            continue;
                        }

                        int adjZ = positive ? z + 1 : z - 1;
                        Voxel[,,]? neighbour = positive ? input.ZPos?.Voxels : input.ZNeg?.Voxels;
                        bool visible = IsVoxelOccluder(x, y, adjZ, voxels, neighbour);
                        _mask[x * Chunk.SIZE + y] = visible ? voxelId : 0;
                    }
                }

                GreedyMerge(Chunk.SIZE, Chunk.SIZE, (row, col, w, h, id) =>
                {
                    // row = x, col = y
                    (float r, float g, float b) = GetColorForId(id);
                    float pz = worldZ + z + (positive ? 1 : 0);
                    float px0 = worldX + row;
                    float px1 = worldX + row + h;
                    float py0 = worldY + col;
                    float py1 = worldY + col + w;

                    if (positive)
                    {
                        // Z+ face
                        EmitQuad(
                            px0, py0, pz,
                            px1, py0, pz,
                            px1, py1, pz,
                            px0, py1, pz,
                            r, g, b, 0, 0, nz);
                    }
                    else
                    {
                        // Z- face
                        EmitQuad(
                            px1, py0, pz,
                            px0, py0, pz,
                            px0, py1, pz,
                            px1, py1, pz,
                            r, g, b, 0, 0, nz);
                    }
                });
            }
        }

        // Return the generated mesh data for this chunk.
        ReadOnlySpan<float> vertices = new(_vertices, 0, _vertexDataCount);
        ReadOnlySpan<uint> indices = new(_indices, 0, _indexDataCount);
        return new VoxelMeshData(vertices, indices);
    }


    /// <summary>
    /// Greedy-merges the static <see cref="_mask"/> array (dimensions <paramref name="rows"/> × <paramref name="cols"/>).
    /// For each maximal rectangle of identical non-zero IDs found, <paramref name="emitQuad"/> is called
    /// with (startRow, startCol, width, height, voxelId). The mask entries are zeroed as they are consumed.
    /// </summary>
    private static void GreedyMerge(int rows, int cols, EmitQuadDelegate emitQuad)
    {
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; )
            {
                int idx = row * cols + col;
                int id = _mask[idx];
                if (id == 0)
                {
                    col++;
                    continue;
                }

                // Determine width: extend along the column axis while the ID matches
                int w = 1;
                while (col + w < cols && _mask[idx + w] == id)
                    w++;

                // Determine height: extend along the row axis while every cell in the strip matches
                int h = 1;
                bool fits = true;
                while (row + h < rows && fits)
                {
                    int rowIdx = (row + h) * cols + col;
                    for (int k = 0; k < w; k++)
                    {
                        if (_mask[rowIdx + k] != id)
                        {
                            fits = false;
                            break;
                        }
                    }
                    if (fits) h++;
                }

                // Zero out the merged region in the mask
                for (int dy = 0; dy < h; dy++)
                {
                    int rowIdx = (row + dy) * cols + col;
                    for (int dx = 0; dx < w; dx++)
                        _mask[rowIdx + dx] = 0;
                }

                emitQuad(row, col, w, h, id);

                col += w;
            }
        }
    }

    private delegate void EmitQuadDelegate(int row, int col, int w, int h, int voxelId);


    /// <summary>
    /// Emits a single quad (4 vertices, 6 indices) into the scratch buffers.
    /// Vertices are specified in CCW winding order as seen from the front face.
    /// </summary>
    private static void EmitQuad(
        float x0, float y0, float z0,
        float x1, float y1, float z1,
        float x2, float y2, float z2,
        float x3, float y3, float z3,
        float r, float g, float b,
        float nx, float ny, float nz)
    {
        int vi = _vertexDataCount;

        // Vertex 0
        _vertices[vi++] = x0; _vertices[vi++] = y0; _vertices[vi++] = z0;
        _vertices[vi++] = r;  _vertices[vi++] = g;  _vertices[vi++] = b;
        _vertices[vi++] = nx; _vertices[vi++] = ny; _vertices[vi++] = nz;

        // Vertex 1
        _vertices[vi++] = x1; _vertices[vi++] = y1; _vertices[vi++] = z1;
        _vertices[vi++] = r;  _vertices[vi++] = g;  _vertices[vi++] = b;
        _vertices[vi++] = nx; _vertices[vi++] = ny; _vertices[vi++] = nz;

        // Vertex 2
        _vertices[vi++] = x2; _vertices[vi++] = y2; _vertices[vi++] = z2;
        _vertices[vi++] = r;  _vertices[vi++] = g;  _vertices[vi++] = b;
        _vertices[vi++] = nx; _vertices[vi++] = ny; _vertices[vi++] = nz;

        // Vertex 3
        _vertices[vi++] = x3; _vertices[vi++] = y3; _vertices[vi++] = z3;
        _vertices[vi++] = r;  _vertices[vi++] = g;  _vertices[vi++] = b;
        _vertices[vi++] = nx; _vertices[vi++] = ny; _vertices[vi++] = nz;

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


    public static void SetupVertexAttributes(VertexArrayObject<float, uint> vao)
    {
        vao.SetVertexAttributePointer(0, 3, VertexAttribPointerType.Float, VERTEX_SIZE_ELEMENTS, 0); // position
        vao.SetVertexAttributePointer(1, 3, VertexAttribPointerType.Float, VERTEX_SIZE_ELEMENTS, 3); // color
        vao.SetVertexAttributePointer(2, 3, VertexAttribPointerType.Float, VERTEX_SIZE_ELEMENTS, 6); // normal
    }


    private static (float r, float g, float b) GetColorForId(int id)
    {
        return id switch
        {
            0 => throw new ArgumentException("Air voxels should never get meshed!"),
            1 => (1f, 0f, 0f),  // red
            2 => (0f, 1f, 0f),  // green
            3 => (0f, 0f, 1f),  // blue
            4 => (1f, 1f, 0f),  // yellow
            5 => (0f, 1f, 1f),  // cyan
            6 => (1f, 0f, 1f),  // magenta
            7 => (1f, 1f, 1f),  // white
            _ => throw new ArgumentException($"Unknown voxel ID: {id}")
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