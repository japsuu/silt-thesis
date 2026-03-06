using ImGuiNET;

namespace Silt.Core.UI;

/// <summary>
/// Central UI registry and dispatch for all registered ImGui windows.
/// </summary>
public sealed class UiManager : IDisposable
{
    public static bool IsUiVisible { get; private set; } = true;
    public static bool WantCaptureMouse => _ioPointer.WantCaptureMouse;
    public static bool WantCaptureKeyboard => _ioPointer.WantCaptureKeyboard;

    private static ImGuiIOPtr _ioPointer;
    private readonly List<IUiWindow> _windows = new(8);


    public void Register(IUiWindow window)
    {
        if (_windows.Contains(window))
            return;

        _windows.Add(window);
    }


    public void Initialize()
    {
        foreach (IUiWindow w in _windows)
            w.Initialize();
        
        _ioPointer = ImGui.GetIO();
    }


    public static void ToggleUiVisibility() => IsUiVisible = !IsUiVisible;
    
    
    public static void SetUiVisibility(bool visible) => IsUiVisible = visible;


    public void Update(double deltaTime)
    {
        if (!IsUiVisible)
            return;

        foreach (IUiWindow w in _windows)
            w.Update(deltaTime);
    }
    
    
    public void Draw(double deltaTime)
    {
        EmitUi(deltaTime);
    }


    private void EmitUi(double deltaTime)
    {
        if (!IsUiVisible)
            return;
        
        // Fullscreen dockspace
        ImGui.DockSpaceOverViewport(0, ImGui.GetMainViewport(), ImGuiDockNodeFlags.PassthruCentralNode);

        DrawMainMenuBar();

        foreach (IUiWindow window in _windows)
        {
            if (!window.IsOpen)
                continue;

            bool open = window.IsOpen;
            if (ImGui.Begin(window.Title, ref open, window.Flags))
                window.Draw(deltaTime);

            ImGui.End();
            window.IsOpen = open;
        }
    }


    private void DrawMainMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
            return;

        if (ImGui.BeginMenu("Windows"))
        {
            foreach (IUiWindow window in _windows)
            {
                bool open = window.IsOpen;
                if (ImGui.MenuItem(window.Title, null, ref open))
                    window.IsOpen = open;
            }

            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }


    public void Dispose()
    {
        _windows.Clear();
    }
}