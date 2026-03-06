using Silk.NET.OpenGL;

namespace Silt.Core.Graphics;

/// <summary>
/// Represents a Vertex Array Object (VAO), encapsulates all the state needed to supply vertex data.
/// </summary>
public sealed class VertexArrayObject<TVertex, TIndex> : GraphicsResource
    where TVertex : unmanaged
    where TIndex : unmanaged
{
    public readonly uint VertexCount;
    public readonly uint IndexCount;


    /// <summary>
    /// Creates a new Vertex Array Object.
    /// </summary>
    /// <param name="gl">The OpenGL context</param>
    /// <param name="vbo">The vertex buffer object</param>
    /// <param name="ebo">The element buffer object</param>
    public VertexArrayObject(GL gl, BufferObject<TVertex> vbo, BufferObject<TIndex> ebo) : base(gl)
    {
        Handle = Gl.GenVertexArray();
        VertexCount = vbo.DataLength;
        IndexCount = ebo.DataLength;
        Bind();

        vbo.Bind();
        ebo.Bind();
    }

    
    /// <summary>
    /// Sets up a vertex attribute pointer.
    /// </summary>
    /// <param name="index">The index of the vertex attribute.</param>
    /// <param name="count">The number of components per generic vertex attribute.</param>
    /// <param name="type">The data type of each component in the array.</param>
    /// <param name="stride">The byte offset between consecutive generic vertex attributes.</param>
    /// <param name="offset">The offset of the first component of the first generic vertex attribute.</param>
    public unsafe void SetVertexAttributePointer(uint index, int count, VertexAttribPointerType type, uint stride, int offset)
    {
        Bind();
        Gl.VertexAttribPointer(index, count, type, false, stride * (uint) sizeof(TVertex), (void*)(offset * sizeof(TVertex)));
        Gl.EnableVertexAttribArray(index);
    }


    /// <summary>
    /// Binds this vertex array object to the current OpenGL context.
    /// </summary>
    public void Bind()
    {
        Gl.BindVertexArray(Handle);
    }


    /// <summary>
    /// Unbinds this vertex array object from the current OpenGL context.
    /// </summary>
    public void Unbind()
    {
        Gl.BindVertexArray(0);
    }


    protected override void DisposeResources(bool manual)
    {
        // The VAO does not own the VBO/EBO, so it should not dispose them.
        // The owner of the VBO/EBO is responsible for their disposal.
        Gl.DeleteVertexArray(Handle);
    }
}