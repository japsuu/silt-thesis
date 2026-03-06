namespace Silt.Core.CameraManagement;

/// <summary>
/// Manages the main camera and its controller.
/// </summary>
public static class CameraManager
{
    /// <summary>
    /// The currently active camera controller.
    /// </summary>
    public static ICameraController? ActiveController { get; private set; }

    /// <summary>
    /// The main camera used for rendering.
    /// </summary>
    public static Camera MainCamera { get; private set; } = null!;


    public static void Initialize(Camera mainCamera)
    {
        MainCamera = mainCamera;
    }


    public static void Initialize(Camera mainCamera, ICameraController controller)
    {
        MainCamera = mainCamera;
        SetActiveController(controller);
    }


    /// <summary>
    /// Sets the active camera controller.
    /// </summary>
    /// <param name="controller">The controller to make active.</param>
    public static void SetActiveController(ICameraController controller)
    {
        ActiveController = controller;
    }


    /// <summary>
    /// Updates the active camera controller.
    /// </summary>
    /// <param name="deltaTime">The time elapsed since the last frame.</param>
    public static void Update(double deltaTime)
    {
        ActiveController?.Update(MainCamera, deltaTime);
    }
}