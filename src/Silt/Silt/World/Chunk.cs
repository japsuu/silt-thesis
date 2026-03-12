using System.Diagnostics;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silt.Metrics;
using Silt.World.Meshing;
using Silt.World.Rendering;

namespace Silt.World;

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
    private readonly ChunkManager _chunkManager;
    public readonly Vector3D<int> WorldPosition;
    public readonly Voxel[,,] Voxels;
    
    private readonly ChunkRenderer _renderer;
    
    
    public Chunk(Vector3D<int> position, GL gl, ChunkManager chunkManager)
    {
        Position = position;
        _chunkManager = chunkManager;
        WorldPosition = position * SIZE;
        Voxels = new Voxel[SIZE, SIZE, SIZE];
        _renderer = new ChunkRenderer(gl);
    }
    
    
    /// <summary>
    /// Same as <see cref="UpdateMesh"/> but called only once after chunk generation to capture per-chunk geometry statistics without the overhead of timing.
    /// </summary>
    public void UpdateMeshAfterGeneration()
    {
        MeshingInput input = GetMeshingInput();
        VoxelMeshData meshData = ChunkMesher.MeshChunk(input);
        _renderer.UpdateMeshData(meshData);
        
        // Record per-chunk geometry statistics
        int vertexCount = meshData.Vertices.Length / ChunkMesher.VERTEX_SIZE_ELEMENTS;
        int triangleCount = meshData.Indices.Length / 3;
        long meshBytes = meshData.Vertices.Length * ChunkMesher.VERTEX_ELEMENT_SIZE_BYTES
                         + meshData.Indices.Length * ChunkMesher.INDEX_ELEMENT_SIZE_BYTES;
        PerfMonitor.AddChunkStatsSample(vertexCount, triangleCount, meshBytes);
    }
    
    
    /// <summary>
    /// Same as <see cref="UpdateMesh"/> but also measures the time taken to generate the mesh and updates performance metrics.
    /// </summary>
    public void UpdateMeshTimed()
    {
        long startTicks = Stopwatch.GetTimestamp();

        MeshingInput input = GetMeshingInput();
        VoxelMeshData meshData = ChunkMesher.MeshChunk(input);
        _renderer.UpdateMeshData(meshData);

        long endTicks = Stopwatch.GetTimestamp();
        double ms = (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;
        PerfMonitor.AddChunkMeshingSample(ms);
    }
    
    
    /// <summary>
    /// Generates vertex and index data for this chunk based on its voxel data, and updates the renderer's buffers.
    /// Should be called whenever the voxel data changes and the mesh needs to be regenerated.
    /// </summary>
    public void UpdateMesh()
    {
        MeshingInput input = GetMeshingInput();
        VoxelMeshData meshData = ChunkMesher.MeshChunk(input);
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
    
    
    private MeshingInput GetMeshingInput()
    {
        return new MeshingInput(
            this,
            _chunkManager.TryGetChunkAtPosition(Position.X + 1, Position.Y, Position.Z),
            _chunkManager.TryGetChunkAtPosition(Position.X - 1, Position.Y, Position.Z),
            _chunkManager.TryGetChunkAtPosition(Position.X, Position.Y + 1, Position.Z),
            _chunkManager.TryGetChunkAtPosition(Position.X, Position.Y - 1, Position.Z),
            _chunkManager.TryGetChunkAtPosition(Position.X, Position.Y, Position.Z + 1),
            _chunkManager.TryGetChunkAtPosition(Position.X, Position.Y, Position.Z - 1)
        );
    }
}