using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silt.World.Generation;

namespace Silt.World;

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
                    Chunk chunk = new(new Vector3D<int>(x, y, z), gl, this);
                    Chunks[index] = chunk;
                }
            }
        }
    }
    
    
    public void GenerateAllChunks()
    {
        foreach (Chunk chunk in Chunks)
            ChunkGenerator.GenerateChunk(chunk);
        
        foreach (Chunk chunk in Chunks)
            chunk.UpdateMeshAfterGeneration();
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
    
    
    /// <summary>
    /// Returns the chunk at the given chunk-coordinate position, or null if outside the loaded world area.
    /// </summary>
    public Chunk? TryGetChunkAtPosition(int chunkX, int chunkY, int chunkZ)
    {
        if (chunkX < -_worldRadiusChunks || chunkX >= _worldRadiusChunks ||
            chunkY < -_worldRadiusChunks || chunkY >= _worldRadiusChunks ||
            chunkZ < -_worldRadiusChunks || chunkZ >= _worldRadiusChunks)
        {
            return null;
        }
        
        int index = GetChunkIndex(chunkX, chunkY, chunkZ);
        return Chunks[index];
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