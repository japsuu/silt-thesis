using System.Numerics;
using Silk.NET.Input;
using Silt.Core.CameraManagement;
using Silt.Core.InputManagement;

namespace Silt.CameraControllers;

/// <summary>
/// A camera controller that allows free movement and rotation.
/// </summary>
public class FreeCameraController : ICameraController
{
    /// <summary>
    /// The speed at which the camera moves.
    /// </summary>
    public float MoveSpeed { get; set; } = 5f;

    /// <summary>
    /// The sensitivity of the mouse look.
    /// </summary>
    public float LookSensitivity { get; set; } = 0.1f;
    
    /// <summary>
    /// Whether to reproject movement vectors onto the ground plane, or use full camera-relative 3D movement.
    /// </summary>
    public bool ReprojectMovementToGround { get; set; } = true;

    private bool _isEnabled = false;


    public void Update(Camera camera, double deltaTime)
    {
        if (Input.WasKeyPressed(Key.Escape))
        {
            _isEnabled = !_isEnabled;
            Cursor.Mode = _isEnabled ? CursorMode.Raw : CursorMode.Normal;
        }

        if (!_isEnabled)
            return;
        
        // Movement
        float moveSpeed = MoveSpeed * (Input.IsKeyDown(Key.ShiftLeft) ? 10f : 1f);
        float moveAmount = (float)deltaTime * moveSpeed;

        Vector3 forward = ReprojectMovementToGround ? Vector3.Normalize(camera.Front with { Y = 0 }) : camera.Front;
        Vector3 right = ReprojectMovementToGround ? Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY)) : camera.Right;
        Vector3 up = ReprojectMovementToGround ? Vector3.UnitY : camera.Up;
        
        if (Input.IsKeyDown(Key.W))
            camera.Position += forward * moveAmount;
        if (Input.IsKeyDown(Key.S))
            camera.Position -= forward * moveAmount;
        if (Input.IsKeyDown(Key.A))
            camera.Position -= right * moveAmount;
        if (Input.IsKeyDown(Key.D))
            camera.Position += right * moveAmount;
        if (Input.IsKeyDown(Key.Space))
            camera.Position += up * moveAmount;
        if (Input.IsKeyDown(Key.ControlLeft))
            camera.Position -= up * moveAmount;

        // Look
        Vector2 mouseDelta = Input.MouseDelta;

        camera.Yaw += mouseDelta.X * LookSensitivity;
        camera.Pitch -= mouseDelta.Y * LookSensitivity; // Inverted Y

        // Zoom
        camera.Fov -= Input.MouseScrollDelta.Y;
    }
}