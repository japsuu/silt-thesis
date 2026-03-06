namespace Silt.Core.Utils;

public static class MathUtil
{
    public static float DegreesToRadians(float degrees)
    {
        return MathF.PI / 180f * degrees;
    }
    
    
    public static float RadiansToDegrees(float radians)
    {
        return 180f / MathF.PI * radians;
    }
}