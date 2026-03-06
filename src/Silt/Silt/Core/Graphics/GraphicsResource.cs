using Silk.NET.OpenGL;

namespace Silt.Core.Graphics;

/// <summary>
/// Represents a graphics resource that is managed by the GPU.
/// </summary>
public abstract class GraphicsResource : IDisposable
{
    /// <summary>
    /// The handle to the underlying OpenGL object.
    /// </summary>
    public uint Handle { get; protected init; }

    /// <summary>
    /// The OpenGL context.
    /// </summary>
    protected readonly GL Gl;
    
    /// <summary>
    /// True if this resource has already been disposed of.
    /// </summary>
    protected bool IsDisposed { get; private set; }


    /// <param name="gl">The OpenGL context.</param>
    protected GraphicsResource(GL gl)
    {
        Gl = gl;
    }
    
    
    /// <summary>
    /// Called by the garbage collector.
    /// A call to <see cref="Dispose"/> prevents this destructor from being called.
    /// </summary>
    ~GraphicsResource()
    {
        Dispose(false);
    }


    /// <summary>
    /// Releases any unmanaged resources used by the graphics resource.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        // Take this object off the finalization queue to prevent the destructor from being called.
        GC.SuppressFinalize(this);
    }


    private void Dispose(bool manual)
    {
        // Safely handle multiple calls to dispose
        if (IsDisposed)
            return;

        IsDisposed = true;
        DisposeResources(manual);
    }


    /// <summary>
    /// Override to release all resources owned by this object.
    /// Guaranteed to be called only once.
    /// </summary>
    protected abstract void DisposeResources(bool manual);
}