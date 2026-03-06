using System.Numerics;
using Silk.NET.Input;
using Silt.Core.Platform;

namespace Silt.Core.InputManagement;

public static class Cursor
{
    private static CursorMode _targetCursorMode = CursorMode.Normal;


    static Cursor()
    {
        Input.MouseAdded += OnMouseAdded;
    }
    
    
    /// <summary>
    /// The current cursor mode applied to all mice.
    /// </summary>
    public static CursorMode Mode
    {
        get => _targetCursorMode;
        set
        {
            _targetCursorMode = value;
            Vector2 center = new(WindowInfo.ClientWidth / 2f, WindowInfo.ClientHeight / 2f);
            foreach (IMouse mouse in Input.Mice)
            {
                if (value is CursorMode.Disabled or CursorMode.Raw)
                {
                    mouse.Position = center;
                }
                mouse.Cursor.CursorMode = _targetCursorMode;
            }
        }
    }
    
    
    /// <summary>
    /// Centers OS cursors to the middle of the client area and rebases delta.
    /// Call this right after switching to Disabled/Raw modes.
    /// </summary>
    public static void CenterCursor()
    {
        Vector2 center = new(WindowInfo.ClientWidth / 2f, WindowInfo.ClientHeight / 2f);
        foreach (IMouse mouse in Input.Mice)
        {
            mouse.Position = center;
        }
        Input.ForceMouseRebase(center);
    }


    private static void OnMouseAdded(IMouse mouse)
    {
        mouse.Cursor.CursorMode = _targetCursorMode;
    }
}