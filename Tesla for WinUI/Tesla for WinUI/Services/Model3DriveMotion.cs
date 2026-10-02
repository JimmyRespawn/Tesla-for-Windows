using System;

namespace TeslaMurphy.Services;

// Local visual simulation; has no vehicle API or session dependency.
internal sealed class Model3DriveMotion
{
    public bool Running { get; set; }
    public double RequestedSpeed { get; set; } = 60;
    public double Speed { get; private set; }
    public float WheelAngle { get; private set; }
    public float RoadOffset { get; private set; }
    public double Distance { get; private set; }
    public bool BrakeHeld { get; set; }
    public double Acceleration { get; private set; }
    public void Step(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        seconds = Math.Min(seconds, .1);
        var target = Running && !BrakeHeld && double.IsFinite(RequestedSpeed) ? Math.Clamp(RequestedSpeed, 0, 120) : 0;
        var previous = Speed;
        Speed += (target - Speed) * (1 - Math.Exp(-(BrakeHeld ? 4.5 : 2.2) * seconds));
        if (target == 0 && Speed < .05) Speed = 0;
        Acceleration = (Speed - previous) / 3.6 / seconds;
        var distance = (previous + Speed) * .5 / 3.6 * seconds;
        Distance += distance;
        // The vehicle faces -X; the road moves towards +X, wheels turn about +Z.
        WheelAngle = (WheelAngle + (float)(distance / .347)) % (MathF.PI * 2);
        RoadOffset = (RoadOffset + (float)distance) % 12;
    }
    public void Reset() { Running = BrakeHeld = false; Speed = Acceleration = 0; WheelAngle = RoadOffset = 0; Distance = 0; }
}

