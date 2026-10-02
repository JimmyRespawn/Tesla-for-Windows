using System;
using System.Numerics;

namespace TeslaMurphy.Services;

internal static class Model3CameraBounds
{
    internal static bool Clamp(ref Vector3 position, ref Vector3 look)
    {
        float distance = look.Length();
        if (!float.IsFinite(distance) || distance < .001f) return false;
        var target = position + look;
        float elevation = MathF.Asin(Math.Clamp(-look.Y / distance, -1, 1));
        float bounded = Math.Clamp(elevation, 3 * MathF.PI / 180, 85 * MathF.PI / 180);
        bool changed = MathF.Abs(bounded - elevation) > .00001f;
        if (changed)
        {
            var heading = new Vector3(look.X, 0, look.Z);
            heading = heading.LengthSquared() > .000001f ? Vector3.Normalize(heading) : Vector3.UnitZ;
            look = (heading * MathF.Cos(bounded) - Vector3.UnitY * MathF.Sin(bounded)) * distance;
            position = target - look;
        }
        if (position.Y < .15f)
        {
            position.Y = .15f;
            changed = true;
        }
        return changed;
    }
}
