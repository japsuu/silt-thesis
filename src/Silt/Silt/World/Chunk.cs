using System.Diagnostics;
using System.Runtime.CompilerServices;
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
    
    /// <summary>Size of the voxel data for an entire chunk in bytes (SoA: Id + Data1 + Data2 + Data3)</summary>
    public const int VOXEL_DATA_BYTES_PER_CHUNK =
        SIZE * SIZE * SIZE * (sizeof(int) + sizeof(float) + sizeof(int) + sizeof(int));
    
    public readonly Vector3D<int> Position;
    private readonly ChunkManager _chunkManager;
    public readonly Vector3D<int> WorldPosition;
    
    // SoA (Structure of Arrays) voxel storage — split by field for cache efficiency.
    // Meshing only reads VoxelIds; keeping the other fields separate avoids polluting cache lines.
    public readonly int[] VoxelIds;
    public readonly float[] VoxelData1;
    public readonly int[] VoxelData2;
    public readonly int[] VoxelData3;
    
    private readonly ChunkRenderer _renderer;
    
    
    /// <summary>
    /// Computes the flat array index for a voxel at (x, y, z) using X-major layout.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Idx(int x, int y, int z) => x * SIZE * SIZE + y * SIZE + z;
    
    
    public Chunk(Vector3D<int> position, GL gl, ChunkManager chunkManager)
    {
        Position = position;
        _chunkManager = chunkManager;
        WorldPosition = position * SIZE;
        VoxelIds = new int[SIZE * SIZE * SIZE];
        VoxelData1 = new float[SIZE * SIZE * SIZE];
        VoxelData2 = new int[SIZE * SIZE * SIZE];
        VoxelData3 = new int[SIZE * SIZE * SIZE];
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