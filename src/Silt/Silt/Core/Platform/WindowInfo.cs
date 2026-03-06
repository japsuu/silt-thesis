using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Silt.Core.Platform;

public struct WindowResizeEventArgs
{
    public int Width { get; init; }
    public int Height { get; init; }
    public float AspectRatio { get; init; }
}

/// <summary>
/// Contains information about the application window.
/// </summary>
public static class WindowInfo
{
    /// <summary>
    /// Width of the client window.
    /// </summary>
    public static int ClientWidth { get; private set; }
    
    /// <summary>
    /// Height of the client window.
    /// </summary>
    public static int ClientHeight { get; private set; }
    
    /// <summary>
    /// Aspect ratio of the client window.
    /// </summary>
    public static float ClientAspectRatio { get; private set; }
    
    public static event Action<WindowResizeEventArgs>? ClientResized;
    
    
    public static void Initialize(IWindow window)
    {
        OnWindowResized(window.Size);
        window.Resize += OnWindowResized;
    }


    private static void OnWindowResized(Vector2D<int> size)
    {
        ClientWidth = size.X;
        ClientHeight = size.Y;
        ClientAspectRatio = ClientHeight == 0 ? 0 : (float)ClientWidth / ClientHeight;
        ClientResized?.Invoke(new WindowResizeEventArgs
        {
            Width = ClientWidth,
            Height = ClientHeight,
            AspectRatio = ClientAspectRatio
        });
    }
}