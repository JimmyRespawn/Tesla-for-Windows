using System;
using System.Numerics;

namespace TeslaMurphy.Services;

// All values are local animation state, independent of the connected vehicle.
internal sealed class Model3DriveDynamics
{
    internal const float LaneWidth = 3.6f;
    private double elapsed, laneTime, duration;
    private float startOffset, previousLateralSpeed;
    public int Lane { get; private set; }
    public int TargetLane { get; private set; }
    public int Signal { get; private set; } // +1 left, -1 right, 0 off
    public bool ChangingLane { get; private set; }
    public bool SignalOn => Signal != 0 && elapsed % .7 < .35;
    public bool BrakeLights { get; private set; }
    public float LateralOffset { get; private set; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public float Roll { get; private set; }
    public float SteeringAngle { get; private set; }
    public Matrix4x4 TravelPose => Matrix4x4.CreateRotationY(Yaw) * Matrix4x4.CreateTranslation(0, 0, LateralOffset);
    public Matrix4x4 BodyPose => Matrix4x4.CreateTranslation(0, -.52f, 0) *
        Matrix4x4.CreateRotationZ(Pitch) * Matrix4x4.CreateRotationX(Roll) * Matrix4x4.CreateTranslation(0, .52f, 0);
    public bool IsAnimating => ChangingLane || Signal != 0 || Math.Abs(Pitch) > .0001 || Math.Abs(Roll) > .0001 || Math.Abs(SteeringAngle) > .0001;

    public void ToggleSignal(int direction)
    {
        if (ChangingLane || direction is not (-1 or 1)) return;
        Signal = Signal == direction ? 0 : direction;
        elapsed = 0;
    }

    public bool CanChangeLane(int direction, double speed, bool running, bool braking)
        => direction is -1 or 1 && !ChangingLane && running && !braking && double.IsFinite(speed) && speed >= 8 && Lane + direction is >= 0 and <= 1;

    public bool ChangeLane(int direction, double speed, bool running, bool braking)
    {
        if (!CanChangeLane(direction, speed, running, braking)) return false;
        TargetLane = Lane + direction;
        startOffset = LateralOffset;
        duration = Math.Max(3.2, LaneWidth * 1.875 / (speed / 3.6 * Math.Tan(.12)));
        laneTime = elapsed = 0;
        Signal = direction;
        ChangingLane = true;
        return true;
    }

    public void Step(double seconds, double speed, double acceleration, bool braking)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        seconds = Math.Min(seconds, .1);
        speed = double.IsFinite(speed) ? Math.Max(0, speed) : 0;
        acceleration = double.IsFinite(acceleration) ? acceleration : 0;
        elapsed += seconds;
        float lateralSpeed = 0;
        if (ChangingLane && speed >= 3)
        {
            laneTime += seconds;
            var t = Math.Clamp((laneTime - .35) / duration, 0, 1);
            var smooth = t * t * t * (10 + t * (-15 + 6 * t));
            var difference = TargetLane * LaneWidth - startOffset;
            LateralOffset = startOffset + difference * (float)smooth;
            lateralSpeed = difference * (float)(30 * t * t * (1 - t) * (1 - t) / duration);
            if (t >= 1) { Lane = TargetLane; ChangingLane = false; Signal = 0; }
        }
        var lateralAcceleration = (lateralSpeed - previousLateralSpeed) / seconds;
        previousLateralSpeed = lateralSpeed;
        Yaw = (float)Math.Clamp(Math.Atan2(lateralSpeed, Math.Max(1, speed / 3.6)), -.12, .12);
        var blend = (float)(1 - Math.Exp(-7 * seconds));
        Pitch += ((float)Math.Clamp(-acceleration * .005, -.035, .035) - Pitch) * blend;
        Roll += ((float)Math.Clamp(-lateralAcceleration * .009, -.04, .04) - Roll) * blend;
        var steering = (float)Math.Clamp(Math.Atan(2.86 * lateralAcceleration / Math.Max(9, speed * speed / 12.96)), -.075, .075);
        SteeringAngle += (steering - SteeringAngle) * blend;
        if (Math.Abs(Pitch) < .0001) Pitch = 0;
        if (Math.Abs(Roll) < .0001) Roll = 0;
        if (Math.Abs(SteeringAngle) < .0001) SteeringAngle = 0;
        BrakeLights = braking || acceleration < -.45 && speed > .1;
    }

    public void Reset()
    {
        elapsed = laneTime = duration = 0;
        Lane = TargetLane = Signal = 0;
        ChangingLane = BrakeLights = false;
        startOffset = previousLateralSpeed = LateralOffset = Yaw = Pitch = Roll = SteeringAngle = 0;
    }
}
