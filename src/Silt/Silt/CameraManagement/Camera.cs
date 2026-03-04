using System.Numerics;
using Silt.Platform;
using Silt.Utils;

namespace Silt.CameraManagement;

/// <summary>
/// Represents a 3D camera with position, orientation, and projection settings.
/// </summary>
public class Camera
{
    /// <summary>
    /// Position in world space.
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Front vector of the camera.
    /// </summary>
    public Vector3 Front { get; private set; } = -Vector3.UnitZ;

    /// <summary>
    /// Up vector of the camera.
    /// </summary>
    public Vector3 Up { get; private set; } = Vector3.UnitY;

    /// <summary>
    /// Right vector of the camera.
    /// </summary>
    public Vector3 Right { get; private set; } = Vector3.UnitX;

    /// <summary>
    /// Pitch in degrees.
    /// </summary>
    public float Pitch
    {
        get;
        set
        {
            // Clamp to avoid flipping
            field = Math.Clamp(value, -89.0f, 89.0f);
            UpdateDirectionVectors();
        }
    }

    /// <summary>
    /// Yaw in degrees.
    /// </summary>
    public float Yaw
    {
        get;
        set
        {
            field = value;
            UpdateDirectionVectors();
        }
    } = -90f; // Start facing down the -Z axis

    /// <summary>
    /// Vertical field of view in degrees.
    /// </summary>
    public float Fov
    {
        get;
        set => field = Math.Clamp(value, 1.0f, 90.0f);
    } = 45f;

    /// <summary>
    /// Near clipping plane distance.
    /// </summary>
    public float NearPlane { get; set; } = 0.1f;

    /// <summary>
    /// Far clipping plane distance.
    /// </summary>
    public float FarPlane { get; set; } = 1000.0f;


    /// <param name="position">The initial position of the camera.</param>
    public Camera(Vector3 position)
    {
        Position = position;
        UpdateDirectionVectors();
    }


    /// <summary>
    /// Calculates the view matrix for this camera.
    /// </summary>
    /// <returns>The view matrix.</returns>
    public Matrix4x4 GetViewMatrix() => Matrix4x4.CreateLookAt(Position, Position + Front, Up);


    /// <summary>
    /// Calculates the projection matrix for this camera.
    /// </summary>
    /// <returns>The projection matrix.</returns>
    public Matrix4x4 GetProjectionMatrix() => Matrix4x4.CreatePerspectiveFieldOfView(MathUtil.DegreesToRadians(Fov), WindowInfo.ClientAspectRatio, NearPlane, FarPlane);
    
    
    public void LookAt(Vector3 target)
    {
        Vector3 direction = Vector3.Normalize(target - Position);
        Pitch = MathUtil.RadiansToDegrees(MathF.Asin(direction.Y));
        Yaw = MathUtil.RadiansToDegrees(MathF.Atan2(direction.Z, direction.X));
    }


    private void UpdateDirectionVectors()
    {
        Vector3 newFront;
        newFront.X = MathF.Cos(MathUtil.DegreesToRadians(Yaw)) * MathF.Cos(MathUtil.DegreesToRadians(Pitch));
        newFront.Y = MathF.Sin(MathUtil.DegreesToRadians(Pitch));
        newFront.Z = MathF.Sin(MathUtil.DegreesToRadians(Yaw)) * MathF.Cos(MathUtil.DegreesToRadians(Pitch));

        Front = Vector3.Normalize(newFront);
        Right = Vector3.Normalize(Vector3.Cross(Front, Vector3.UnitY));
        Up = Vector3.Normalize(Vector3.Cross(Right, Front));
    }
}