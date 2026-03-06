using System.Globalization;

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
    public double AvgVertices => SampleCount > 0 ? (double)TotalVertices / SampleCount : 0;
    
    // Triangle counts
    public long TotalTriangles { get; private set; }
    public int MinTriangles { get; private set; } = int.MaxValue;
    public int MaxTriangles { get; private set; } = int.MinValue;
    public double AvgTriangles => SampleCount > 0 ? (double)TotalTriangles / SampleCount : 0;
    
    // Mesh data size (VBO + EBO in bytes)
    public long TotalMeshBytes { get; private set; }
    public long MinMeshBytes { get; private set; } = long.MaxValue;
    public long MaxMeshBytes { get; private set; } = long.MinValue;
    public double AvgMeshBytes => SampleCount > 0 ? (double)TotalMeshBytes / SampleCount : 0;
    
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
        double avgVertices = SampleCount > 0 ? AvgVertices : 0;
        int minVertices = SampleCount > 0 ? MinVertices : 0;
        int maxVertices = SampleCount > 0 ? MaxVertices : 0;
        
        double avgTriangles = SampleCount > 0 ? AvgTriangles : 0;
        int minTriangles = SampleCount > 0 ? MinTriangles : 0;
        int maxTriangles = SampleCount > 0 ? MaxTriangles : 0;
        
        double avgMeshBytes = SampleCount > 0 ? AvgMeshBytes : 0;
        long minMeshBytes = SampleCount > 0 ? MinMeshBytes : 0;
        long maxMeshBytes = SampleCount > 0 ? MaxMeshBytes : 0;

        return $"{keyPrefix}_total_count={TotalChunkCount}\n" +
               $"{keyPrefix}_sample_count={SampleCount}\n" +
               $"{keyPrefix}_vertices_avg={avgVertices.ToString("F2", CultureInfo.InvariantCulture)}\n" +
               $"{keyPrefix}_vertices_min={minVertices}\n" +
               $"{keyPrefix}_vertices_max={maxVertices}\n" +
               $"{keyPrefix}_triangles_avg={avgTriangles.ToString("F2", CultureInfo.InvariantCulture)}\n" +
               $"{keyPrefix}_triangles_min={minTriangles}\n" +
               $"{keyPrefix}_triangles_max={maxTriangles}\n" +
               $"{keyPrefix}_voxel_data_bytes={VoxelDataBytesPerChunk}\n" +
               $"{keyPrefix}_mesh_data_bytes_avg={avgMeshBytes.ToString("F2", CultureInfo.InvariantCulture)}\n" +
               $"{keyPrefix}_mesh_data_bytes_min={minMeshBytes}\n" +
               $"{keyPrefix}_mesh_data_bytes_max={maxMeshBytes}\n" +
               $"{keyPrefix}_total_memory_bytes_avg={(avgMeshBytes + VoxelDataBytesPerChunk).ToString("F2", CultureInfo.InvariantCulture)}\n";
    }
}

