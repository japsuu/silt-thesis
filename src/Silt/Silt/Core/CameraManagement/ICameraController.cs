namespace Silt.Core.CameraManagement;

/// <summary>
/// Defines the contract for a camera controller.
/// </summary>
public interface ICameraController
{
    /// <summary>
    /// Updates the camera state based on the controller logic.
    /// </summary>
    /// <param name="camera">The camera instance being controlled.</param>
    /// <param name="deltaTime">The time elapsed since the last frame.</param>
    public void Update(Camera camera, double deltaTime);
}