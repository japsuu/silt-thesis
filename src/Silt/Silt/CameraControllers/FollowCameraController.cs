using System.Numerics;
using Silt.Core;
using Silt.Core.CameraManagement;
using Silt.Core.Utils;

namespace Silt.CameraControllers;

/// <summary>
/// A camera controller that follows a target transform.
/// </summary>
public class FollowCameraController : ICameraController
{
    /// <summary>
    /// The transform that the camera will follow.
    /// </summary>
    public Transform Target { get; set; }

    /// <summary>
    /// The offset from the target's position.
    /// </summary>
    public Vector3 Offset { get; set; }


    /// <param name="target">The initial target to follow.</param>
    /// <param name="offset">The offset from the target.</param>
    public FollowCameraController(Transform target, Vector3 offset)
    {
        Target = target;
        Offset = offset;
    }


    public void Update(Camera camera, double deltaTime)
    {
        camera.Position = Target.Position + Offset;

        // Make the camera look at the target
        Vector3 direction = Vector3.Normalize(Target.Position - camera.Position);
        camera.Yaw = MathUtil.RadiansToDegrees(MathF.Atan2(direction.Z, direction.X));
        camera.Pitch = MathUtil.RadiansToDegrees(MathF.Asin(direction.Y));
    }
}