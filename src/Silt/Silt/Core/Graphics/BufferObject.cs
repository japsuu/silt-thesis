using Silk.NET.OpenGL;

namespace Silt.Core.Graphics;

/// <summary>
/// Represents a buffer object on the GPU.
/// </summary>
/// <typeparam name="T">The type of data stored in the buffer. Must be an unmanaged type.</typeparam>
public sealed class BufferObject<T> : GraphicsResource where T : unmanaged
{
    public uint DataLength { get; private set; }
    
    private readonly BufferTargetARB _bufferTarget;


    /// <summary>
    /// Creates a new buffer object.
    /// </summary>
    /// <param name="gl">The OpenGL context</param>
    /// <param name="data">The data to store in the buffer</param>
    /// <param name="target">The type of buffer to create</param>
    public unsafe BufferObject(GL gl, ReadOnlySpan<T> data, BufferTargetARB target) : base(gl)
    {
        _bufferTarget = target;
        Handle = Gl.GenBuffer();
        Bind();

        fixed (void* d = data)
        {
            uint dataLength = (uint)data.Length;
            Gl.BufferData(_bufferTarget, (nuint)(dataLength * sizeof(T)), d, BufferUsageARB.StaticDraw);
            DataLength = dataLength;
        }
    }


    /// <summary>
    /// Creates a new buffer object without any data.
    /// </summary>
    /// <param name="gl">The OpenGL context</param>
    /// <param name="target">The type of buffer to create</param>
    public BufferObject(GL gl, BufferTargetARB target) : base(gl)
    {
        _bufferTarget = target;
        Handle = Gl.GenBuffer();
    }


    /// <summary>
    /// Binds this buffer to the current OpenGL context.
    /// </summary>
    public void Bind()
    {
        Gl.BindBuffer(_bufferTarget, Handle);
    }


    /// <summary>
    /// Unbinds this buffer from the current OpenGL context.
    /// </summary>
    public void Unbind()
    {
        Gl.BindBuffer(_bufferTarget, 0);
    }
    
    
    /// <summary>
    /// Sets new data for this buffer by allocating new storage and copying the data. Old data will be discarded.
    /// </summary>
    /// <param name="data">The new data to store in the buffer</param>
    public unsafe void SetData(ReadOnlySpan<T> data)
    {
        Bind();
        fixed (void* d = data)
        {
            Gl.BufferData(_bufferTarget, (nuint)(data.Length * sizeof(T)), d, BufferUsageARB.StaticDraw);
        }

        DataLength = (uint)data.Length;
    }


    protected override void DisposeResources(bool manual)
    {
        Gl.DeleteBuffer(Handle);
    }
}