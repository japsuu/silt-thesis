using System.Numerics;
using Serilog;
using Silk.NET.OpenGL;
using Silt.Core.CameraManagement;
using Shader = Silt.Core.Graphics.Shader;

namespace Silt.World.Rendering;

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
    private readonly int _uChunkPos;


    public VoxelWorldRenderer(GL gl, ChunkManager chunkManager)
    {
        _gl = gl;
        _chunkManager = chunkManager;

        _chunkShader = new Shader(_gl, "voxel_chunk", "assets/voxel_chunk.vert", "assets/voxel_chunk.frag");
        _uMatView = _chunkShader.GetUniformLocation("u_mat_view");
        _uMatProj = _chunkShader.GetUniformLocation("u_mat_proj");
        _uChunkPos = _chunkShader.GetUniformLocation("u_chunk_pos");
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
        {
            _chunkShader.SetUniform(_uChunkPos, chunk.WorldPosition.X, chunk.WorldPosition.Y, chunk.WorldPosition.Z);
            chunk.Draw();
        }
    }


    public void Dispose()
    {
        _chunkShader.Dispose();
    }
}