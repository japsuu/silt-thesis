using Silk.NET.OpenGL;
using Silt.Core.Graphics;
using Silt.Metrics;
using Silt.World.Meshing;

namespace Silt.World.Rendering;

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