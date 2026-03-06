using System.Numerics;

namespace Silt.Core;

/// <summary>
/// Represents a 3D transformation with position, rotation, and scale.
/// </summary>
public class Transform
{
    public Vector3 Position { get; set; } = new(0, 0, 0);
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public float Scale { get; set; } = 1f;

    // The order of matrix multiplication is important here.
    public Matrix4x4 ModelMatrix => Matrix4x4.Identity * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateTranslation(Position);
}