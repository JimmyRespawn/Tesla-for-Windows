using System;

namespace TeslaMurphy.Services;

// A local three-pulse illustration. Vehicle flash_lights has no persistent
// state to poll; only an acknowledged command starts this timeline.
internal sealed class Model3HeadlightFlash
{
    internal const double Duration = 1.2;
    private double started = double.NegativeInfinity;
    public void Start(double now)
    {
        if (double.IsFinite(now)) started = now;
    }
    public bool IsActive(double now) => double.IsFinite(now) && now >= started && now - started < Duration;
    public bool IsOn(double now) => IsActive(now) && (now - started) % .4 < .18;
    public void Reset() => started = double.NegativeInfinity;
}
