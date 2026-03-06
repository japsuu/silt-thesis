using System.Diagnostics;
using System.Numerics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silt.CameraManagement;
using Silt.Graphics;
using Silt.Metrics;
using Silt.World.Generation;
using Shader = Silt.Graphics.Shader;

namespace Silt.World;

/// <summary>
/// Represents the entire voxel world, containing multiple chunks of voxels.
/// </summary>
public sealed class VoxelWorld : IDisposable
{
    public readonly ChunkManager ChunkManager;
    private readonly VoxelWorldRenderer _renderer;


    public VoxelWorld(GL gl, int worldRadiusChunks)
    {
        ChunkManager = new ChunkManager(worldRadiusChunks, gl);
        _renderer = new VoxelWorldRenderer(gl, ChunkManager);
    }


    public void Generate()
    {
        ChunkManager.GenerateAllChunks();
    }


    public void Draw()
    {
        _renderer.Draw();
    }


    public void Update(double deltaTime)
    {
        // In future update any dynamic rendering resources (e.g. culling, LOD)
    }


    public void Dispose()
    {
        ChunkManager.Dispose();
    }
}

/// <summary>
/// Responsible for rendering the voxel world, including managing rendering resources and issuing draw calls.
/// </summary>
public sealed class VoxelWorldRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly ChunkManager _chunkManager;
    private readonly Shader _chunkShader;
    private readonly int _uMatView;
    private readonly int _uMatProj;


    public VoxelWorldRenderer(GL gl, ChunkManager chunkManager)
    {
        _gl = gl;
        _chunkManager = chunkManager;

        _chunkShader = new Shader(_gl, "voxel_chunk", "assets/voxel_chunk.vert", "assets/voxel_chunk.frag");
        _uMatView = _chunkShader.GetUniformLocation("u_mat_view");
        _uMatProj = _chunkShader.GetUniformLocation("u_mat_proj");
    }


    public void Draw()
    {
        Matrix4x4 view = CameraManager.MainCamera.GetViewMatrix();
        Matrix4x4 proj = CameraManager.MainCamera.GetProjectionMatrix();

        _chunkShader.Use();
        _chunkShader.SetUniform(_uMatView, view);
        _chunkShader.SetUniform(_uMatProj, proj);

        // Iterate over visible chunks and draw them.
        foreach (Chunk chunk in _chunkManager.Chunks)
            chunk.Draw();
    }


    public void Dispose()
    {
        _chunkShader.Dispose();
    }
}

/// <summary>
/// Responsible for loading, unloading, and storage of voxel chunks.
/// Responsible for providing access to voxel data for rendering systems.
/// For simplicity, we'll load a fixed area of chunks around the world origin.
/// </summary>
public sealed class ChunkManager : IDisposable
{
    // flattened 3D array of chunks, indexed by (x + y * sizeX + z * sizeX * sizeY)
    public readonly Chunk[] Chunks;
    
    private readonly int _worldRadiusChunks;
    private readonly int _worldSizeChunks;


    /// <param name="worldRadiusChunks">
    /// Half-extent in chunks around the origin boundary.
    /// A value of 2 loads a 4x4x4 area with chunk coordinates in [-2, 1] (no single center chunk).
    /// </param>
    /// <param name="gl">OpenGL context for creating rendering resources for chunks</param>
    public ChunkManager(int worldRadiusChunks, GL gl)
    {
        if (worldRadiusChunks <= 0)
            throw new ArgumentOutOfRangeException(nameof(worldRadiusChunks), "worldRadiusChunks must be > 0 for an even-sized world.");

        _worldRadiusChunks = worldRadiusChunks;
        _worldSizeChunks = worldRadiusChunks * 2;
        int totalChunks = _worldSizeChunks * _worldSizeChunks * _worldSizeChunks;
        Chunks = new Chunk[totalChunks];
        
        // Chunk coordinates are [-radius, radius-1].
        for (int x = -worldRadiusChunks; x < worldRadiusChunks; x++)
        {
            for (int y = -worldRadiusChunks; y < worldRadiusChunks; y++)
            {
                for (int z = -worldRadiusChunks; z < worldRadiusChunks; z++)
                {
                    int index = GetChunkIndex(x, y, z);
                    Chunk chunk = new(new Vector3D<int>(x, y, z), gl);
                    Chunks[index] = chunk;
                }
            }
        }
    }
    
    
    public void GenerateAllChunks()
    {
        foreach (Chunk chunk in Chunks)
        {
            ChunkGenerator.GenerateChunk(chunk);
            chunk.UpdateMeshTimed();
        }
    }
    
    
    public Chunk GetChunkAtPosition(int chunkX, int chunkY, int chunkZ)
    {
        if (chunkX < -_worldRadiusChunks || chunkX >= _worldRadiusChunks ||
            chunkY < -_worldRadiusChunks || chunkY >= _worldRadiusChunks ||
            chunkZ < -_worldRadiusChunks || chunkZ >= _worldRadiusChunks)
        {
            throw new ArgumentException($"Chunk position ({chunkX}, {chunkY}, {chunkZ}) is out of bounds of the loaded world area.");
        }
        
        int index = GetChunkIndex(chunkX, chunkY, chunkZ);
        return Chunks[index] ?? throw new ArgumentException($"No chunk found at position ({chunkX}, {chunkY}, {chunkZ})");
    }
    
    
    public Chunk GetChunkAtWorldPosition(Vector3D<int> worldPos)
    {
        // Convert world position to chunk coordinates (floor division so negatives map correctly).
        int chunkX = FloorDiv(worldPos.X, Chunk.SIZE);
        int chunkY = FloorDiv(worldPos.Y, Chunk.SIZE);
        int chunkZ = FloorDiv(worldPos.Z, Chunk.SIZE);

        if (chunkX < -_worldRadiusChunks || chunkX >= _worldRadiusChunks ||
            chunkY < -_worldRadiusChunks || chunkY >= _worldRadiusChunks ||
            chunkZ < -_worldRadiusChunks || chunkZ >= _worldRadiusChunks)
        {
            throw new ArgumentException($"World position {worldPos} is out of bounds of the loaded world area.");
        }
        
        int index = GetChunkIndex(chunkX, chunkY, chunkZ);
        return Chunks[index] ?? throw new ArgumentException($"No chunk found at world position {worldPos}");
    }
    
    
    public void Dispose()
    {
        foreach (Chunk chunk in Chunks)
        {
            chunk.Dispose();
        }
    }
    
    
    private int GetChunkIndex(int chunkX, int chunkY, int chunkZ)
    {
        return (chunkX + _worldRadiusChunks)
               + (chunkY + _worldRadiusChunks) * _worldSizeChunks
               + (chunkZ + _worldRadiusChunks) * _worldSizeChunks * _worldSizeChunks;
    }

    private static int FloorDiv(int value, int divisor)
    {
        // Equivalent to Math.Floor((double)value / divisor)
        int q = value / divisor;
        int r = value % divisor;
        if (r != 0 && ((r > 0) != (divisor > 0))) q--;
        return q;
    }
}

/// <summary>
/// Represents a cubic chunk of voxels in the world.
/// Contains voxel data and relevant metadata.
/// </summary>
public sealed class Chunk : IDisposable
{
    public const int SIZE = 32;
    
    /// <summary>Size of a single Voxel struct in bytes</summary>
    public const int VOXEL_SIZE_BYTES = sizeof(int) + sizeof(float) + sizeof(int) + sizeof(int);
    
    /// <summary>Size of the voxel data for an entire chunk in bytes</summary>
    public const int VOXEL_DATA_BYTES_PER_CHUNK = SIZE * SIZE * SIZE * VOXEL_SIZE_BYTES;
    
    public readonly Vector3D<int> Position;
    public readonly Vector3D<int> WorldPosition;
    public readonly Voxel[,,] Voxels;
    
    private readonly ChunkRenderer _renderer;
    
    
    public Chunk(Vector3D<int> position, GL gl)
    {
        Position = position;
        WorldPosition = position * SIZE;
        Voxels = new Voxel[SIZE, SIZE, SIZE];
        _renderer = new ChunkRenderer(gl);
    }
    
    
    /// <summary>
    /// Same as <see cref="UpdateMesh"/> but also measures the time taken to generate the mesh and updates performance metrics.
    /// </summary>
    public void UpdateMeshTimed()
    {
        long startTicks = Stopwatch.GetTimestamp();

        VoxelMeshData meshData = ChunkMesher.MeshChunk(this);
        _renderer.UpdateMeshData(meshData);

        long endTicks = Stopwatch.GetTimestamp();
        double ms = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
        PerfMonitor.AddChunkMeshingSample(ms);
        
        // Record per-chunk geometry statistics
        int vertexCount = meshData.Vertices.Length / ChunkMesher.VERTEX_SIZE_ELEMENTS;
        int triangleCount = meshData.Indices.Length / 3;
        long meshBytes = meshData.Vertices.Length * ChunkMesher.VERTEX_ELEMENT_SIZE_BYTES
                         + meshData.Indices.Length * ChunkMesher.INDEX_ELEMENT_SIZE_BYTES;
        PerfMonitor.AddChunkStatsSample(vertexCount, triangleCount, meshBytes);
    }
    
    
    /// <summary>
    /// Generates vertex and index data for this chunk based on its voxel data, and updates the renderer's buffers.
    /// Should be called whenever the voxel data changes and the mesh needs to be regenerated.
    /// </summary>
    public void UpdateMesh()
    {
        VoxelMeshData meshData = ChunkMesher.MeshChunk(this);
        _renderer.UpdateMeshData(meshData);
    }
    
    
    public void Draw()
    {
        _renderer.Draw();
    }


    public void Dispose()
    {
        _renderer.Dispose();
    }
}

/// <summary>
/// Responsible for maintaining and updating rendering resources for a chunk of voxels.
/// </summary>
public sealed class ChunkRenderer : IDisposable
{
    private readonly GL _gl;
    private readonly VertexArrayObject<float, uint> _vao;
    private readonly BufferObject<float> _vbo;
    private readonly BufferObject<uint> _ebo;


    public ChunkRenderer(GL gl)
    {
        _gl = gl;
        _vbo = new BufferObject<float>(_gl, BufferTargetARB.ArrayBuffer);
        _ebo = new BufferObject<uint>(_gl, BufferTargetARB.ElementArrayBuffer);
        _vao = new VertexArrayObject<float, uint>(_gl, _vbo, _ebo);
        ChunkMesher.SetupVertexAttributes(_vao);

        // Cleanup
        _vao.Unbind();
    }


    public void UpdateMeshData(VoxelMeshData meshData)
    {
        _vao.Bind();
        
        // Update buffers with new data
        _vbo.SetData(meshData.Vertices);
        _ebo.SetData(meshData.Indices);
        
        _vao.Unbind();
    }
    
    
    public unsafe void Draw()
    {
        if (_ebo.DataLength == 0)
            return;

        // Bind, issue draw call
        _vao.Bind();
        _gl.DrawElements(PrimitiveType.Triangles, _ebo.DataLength, DrawElementsType.UnsignedInt, null);

        // Update performance metrics
        int triangles = (int)(_ebo.DataLength / 3);
        int vertices = (int)(_vbo.DataLength / ChunkMesher.VERTEX_SIZE_ELEMENTS);
        PerfMonitor.AddTriangles(triangles);
        PerfMonitor.AddVertices(vertices);
        PerfMonitor.AddDrawCalls(1);
    }


    public void Dispose()
    {
        _vao.Dispose();
        _vbo.Dispose();
        _ebo.Dispose();
    }
}

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
    

    public static VoxelMeshData MeshChunk(Chunk chunk)
    {
        // Generate vertex and index data based on the voxel data in the chunk.
        // For now just create a simple cube for each non-air voxel, without any optimization.
        _vertexDataCount = 0;
        _vertexCount = 0;
        _indexDataCount = 0;

        for (int x = 0; x < Chunk.SIZE; x++)
        {
            for (int y = 0; y < Chunk.SIZE; y++)
            {
                for (int z = 0; z < Chunk.SIZE; z++)
                {
                    Voxel voxel = chunk.Voxels[x, y, z];
                    if (voxel.Id == 0)
                        continue;

                    (float r, float g, float b) = GetColorForVoxel(voxel);

                    // Each vertex has 9 floats: position (3), color (3), and normal (3)
                    float baseX = chunk.WorldPosition.X + x;
                    float baseY = chunk.WorldPosition.Y + y;
                    float baseZ = chunk.WorldPosition.Z + z;

                    // Z+ face
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

                    // Z- face
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

                    // X+ face
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

                    // X- face
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

                    // Y+ face
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

                    // Y- face
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
}

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