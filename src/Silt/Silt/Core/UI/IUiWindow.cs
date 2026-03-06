using ImGuiNET;

namespace Silt.Core.UI;

/// <summary>
/// Represents an ImGui-powered debug window/panel.
/// </summary>
public interface IUiWindow
{
    string Title { get; }
    
    ImGuiWindowFlags Flags { get; }

    /// <summary>
    /// Whether the window is currently visible/open.
    /// Implementations should pass this by ref to ImGui.Begin(...).
    /// </summary>
    bool IsOpen { get; set; }

    /// <summary>
    /// Called once after the ImGui context is created.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Called once per frame before <see cref="Draw"/>.
    /// Keep this allocation-free.
    /// </summary>
    void Update(double deltaTime);

    /// <summary>
    /// Called once per frame to emit ImGui widgets.
    /// </summary>
    void Draw(double deltaTime);
}
