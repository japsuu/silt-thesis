using Silk.NET.OpenGL;
using Silk.NET.Windowing;

namespace Silt.Core.SceneManagement;

/// <summary>
/// Represents an in-game scene.
/// </summary>
public abstract class Scene
{
    protected readonly GL GL;
    protected readonly IWindow Window;


    protected Scene(GL gl, IWindow window)
    {
        GL = gl;
        Window = window;
    }


    /// <summary>
    /// Called when the scene is loaded.
    /// </summary>
    public abstract void Load();
    
    /// <summary>
    /// Called when the scene is unloaded.
    /// </summary>
    public abstract void Unload();
    
    /// <summary>
    /// Called from the update loop.
    /// </summary>
    public abstract void Update(double deltaTime);
    
    /// <summary>
    /// Called from the fixed update loop.
    /// </summary>
    public abstract void FixedUpdate(double deltaTime);
    
    /// <summary>
    /// Called when the scene is rendered.
    /// </summary>
    public abstract void Render(double deltaTime);
}