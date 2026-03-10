namespace Silt.Metrics;

/// <summary>
/// Aggregates per-chunk geometry and memory statistics.
/// </summary>
public sealed class ChunkStats
{
    public int SampleCount { get; private set; }
    
    // Vertex counts
    public long TotalVertices { get; private set; }
    public int MinVertices { get; private set; } = int.MaxValue;
    public int MaxVertices { get; private set; } = int.MinValue;
    
    // Triangle counts
    public long TotalTriangles { get; private set; }
    public int MinTriangles { get; private set; } = int.MaxValue;
    public int MaxTriangles { get; private set; } = int.MinValue;
    
    // Mesh data size (VBO + EBO in bytes)
    public long TotalMeshBytes { get; private set; }
    public long MinMeshBytes { get; private set; } = long.MaxValue;
    public long MaxMeshBytes { get; private set; } = long.MinValue;
    
    // Voxel data size per chunk (constant based on chunk dimensions)
    public int VoxelDataBytesPerChunk { get; private set; }
    
    // Total chunk count in the world
    public int TotalChunkCount { get; private set; }
    

    public void Reset()
    {
        SampleCount = 0;
        
        TotalVertices = 0;
        MinVertices = int.MaxValue;
        MaxVertices = int.MinValue;
        
        TotalTriangles = 0;
        MinTriangles = int.MaxValue;
        MaxTriangles = int.MinValue;
        
        TotalMeshBytes = 0;
        MinMeshBytes = long.MaxValue;
        MaxMeshBytes = long.MinValue;
        
        VoxelDataBytesPerChunk = 0;
        TotalChunkCount = 0;
    }


    public void SetChunkInfo(int totalChunkCount, int voxelDataBytesPerChunk)
    {
        TotalChunkCount = totalChunkCount;
        VoxelDataBytesPerChunk = voxelDataBytesPerChunk;
    }


    public void AddSample(int vertexCount, int triangleCount, long meshBytes)
    {
        if (vertexCount < 0 || triangleCount < 0 || meshBytes < 0)
            return;

        SampleCount++;
        
        TotalVertices += vertexCount;
        MinVertices = Math.Min(MinVertices, vertexCount);
        MaxVertices = Math.Max(MaxVertices, vertexCount);
        
        TotalTriangles += triangleCount;
        MinTriangles = Math.Min(MinTriangles, triangleCount);
        MaxTriangles = Math.Max(MaxTriangles, triangleCount);
        
        TotalMeshBytes += meshBytes;
        MinMeshBytes = Math.Min(MinMeshBytes, meshBytes);
        MaxMeshBytes = Math.Max(MaxMeshBytes, meshBytes);
    }


    public string FormatInvariant(string keyPrefix)
    {
        long totalVertices = SampleCount > 0 ? TotalVertices : 0;
        int minVertices = SampleCount > 0 ? MinVertices : 0;
        int maxVertices = SampleCount > 0 ? MaxVertices : 0;
        
        long totalTriangles = SampleCount > 0 ? TotalTriangles : 0;
        int minTriangles = SampleCount > 0 ? MinTriangles : 0;
        int maxTriangles = SampleCount > 0 ? MaxTriangles : 0;
        
        long totalMeshBytes = SampleCount > 0 ? TotalMeshBytes : 0;
        long minMeshBytes = SampleCount > 0 ? MinMeshBytes : 0;
        long maxMeshBytes = SampleCount > 0 ? MaxMeshBytes : 0;

        long totalVoxelBytes = VoxelDataBytesPerChunk * TotalChunkCount;
        
        return $"{keyPrefix}_count_total={TotalChunkCount}\n" +
               $"{keyPrefix}_sample_count={SampleCount}\n" +
               $"{keyPrefix}_vertices_total={totalVertices}\n" +
               $"{keyPrefix}_vertices_min={minVertices}\n" +
               $"{keyPrefix}_vertices_max={maxVertices}\n" +
               $"{keyPrefix}_triangles_total={totalTriangles}\n" +
               $"{keyPrefix}_triangles_min={minTriangles}\n" +
               $"{keyPrefix}_triangles_max={maxTriangles}\n" +
               $"{keyPrefix}_mesh_data_bytes_total={totalMeshBytes}\n" +
               $"{keyPrefix}_mesh_data_bytes_min={minMeshBytes}\n" +
               $"{keyPrefix}_mesh_data_bytes_max={maxMeshBytes}\n" +
               $"{keyPrefix}_voxel_data_bytes_total={totalVoxelBytes}\n" +
               $"{keyPrefix}_data_bytes_total={totalMeshBytes + totalVoxelBytes}\n";
    }
}

