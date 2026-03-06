using Silk.NET.OpenGL;
using Silt.World.Rendering;

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