using System;
using System.Numerics;

namespace TeslaMurphy.Services;

internal enum Model3RoadKind { Highway, City, Curved, Tunnel }
internal enum Model3WeatherKind { Clear, Rain }

internal static class Model3DriveEnvironmentMath
{
    // A closed circuit with a whole number of lane-marking periods.
    internal const float CurveRadius = 864f / (2 * MathF.PI);
    public static Vector3 CurvePoint(Vector3 point)
    {
        float angle = point.X / CurveRadius;
        return new Vector3((CurveRadius - point.Z) * MathF.Sin(angle), point.Y,
            CurveRadius * (1 - MathF.Cos(angle)) + point.Z * MathF.Cos(angle));
    }
    public static Matrix4x4 CurveAdvance(double distance)
    {
        float angle = (float)(distance % 864 / CurveRadius);
        return Matrix4x4.CreateTranslation(0,0,-CurveRadius) * Matrix4x4.CreateRotationY(-angle) * Matrix4x4.CreateTranslation(0,0,CurveRadius);
    }
    public static float RainOffset(double time, int layer)
        => (float)(((time * 6 + layer * 2.25) % 9 + 9) % 9);
}
