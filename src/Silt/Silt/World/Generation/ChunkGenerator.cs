namespace Silt.World.Generation;

/// <summary>
/// Responsible for generating voxel data for a chunk based on procedural algorithms.
/// </summary>
public static class ChunkGenerator
{
    private static readonly FastNoiseLite _fnl = new(1357);


    public static void ConfigureNoise(float frequency)
    {
        _fnl.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        _fnl.SetFrequency(frequency);
    }
    
    
    public static void GenerateChunk(Chunk chunk)
    {
        for (int x = 0; x < Chunk.SIZE; x++)
        {
            float worldX = chunk.WorldPosition.X + x;
            for (int y = 0; y < Chunk.SIZE; y++)
            {
                float worldY = chunk.WorldPosition.Y + y;
                int id = 1 + y % 7;
                for (int z = 0; z < Chunk.SIZE; z++)
                {
                    float worldZ = chunk.WorldPosition.Z + z;
                    
                    float noiseValue = _fnl.GetNoise(worldX, worldY, worldZ);
                    bool solid = noiseValue > 0f;
                    int vid = solid ? id : 0;
                    int idx = Chunk.Idx(x, y, z);
                    chunk.VoxelIds[idx] = vid;
                    chunk.VoxelData1[idx] = 0;
                    chunk.VoxelData2[idx] = 0;
                    chunk.VoxelData3[idx] = 0;
                }
            }
        }
    }
}