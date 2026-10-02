using System;
using System.Linq;
using System.Numerics;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model.Scene;

namespace TeslaMurphy.Services;

internal sealed class Model3TrunkAnimation
{
    private readonly GroupNode hinge;
    private readonly Matrix4x4 closed;
    private readonly float angle;
    private float target;
    public bool IsOpen => target > 0;
    public bool IsFullyOpen => target == 1;
    public float Progress { get; private set; }
    public Model3TrunkAnimation(SceneNode car, bool front = false)
    {
        if (car is not GroupNode root) throw new InvalidOperationException("Trunk requires a group root.");
        car.UpdateAllTransformMatrix();
        if (!Matrix4x4.Invert(car.TotalModelMatrix, out var inverseCar))
            throw new InvalidOperationException("Trunk root transform is singular.");
        var pivot = Matrix4x4.CreateTranslation(front ? new Vector3(-1.182f, .909f, 0) : new Vector3(1.505f, 1.225f, 0));
        Matrix4x4.Invert(pivot, out var inversePivot);
        hinge = new GroupNode { Name = front ? "Front_Trunk_Hinge" : "Rear_Trunk_Hinge", ModelMatrix = pivot * inverseCar };
        angle = front ? -1.05f : 1.22f;
        // Only the bonnet and its emblem move. The cowl/frunk plastic stays fixed.
        bool Matches(SceneNode n) => front ? n.Name.StartsWith("bonnet_", StringComparison.Ordinal)
            || n.Name.Contains("_bonnet_", StringComparison.Ordinal) : n.Name.StartsWith("boot_", StringComparison.Ordinal)
            || n.Name.Contains("_boot_", StringComparison.Ordinal)
            || n.Name.StartsWith("platnomor_", StringComparison.Ordinal);
        var parts = Model3HelixSceneAssets.Traverse(car).Where(Matches)
            .Where(n => n.Parent == null || !Matches(n.Parent)).ToArray();
        if (parts.Length < (front ? 2 : 8)) throw new InvalidOperationException("Trunk parts are missing.");
        foreach (var part in parts)
        {
            var world = part.TotalModelMatrix;
            if (part.Parent is not GroupNode parent) throw new InvalidOperationException("Trunk parent is not a group.");
            parent.RemoveChildNode(part);
            part.ModelMatrix = world * inversePivot;
            hinge.AddChildNode(part);
        }
        root.AddChildNode(hinge);
        closed = hinge.ModelMatrix;
        car.UpdateAllTransformMatrix();
    }
    public void SetOpen(bool open) => SetOpening(open ? 1 : 0);
    public void SetOpening(float fraction)
    {
        if (!float.IsFinite(fraction)) return;
        target = Math.Clamp(fraction, 0, 1);
    }
    public bool Step(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return false;
        if (Progress == target) return false;
        Progress += (target - Progress) * (float)(1 - Math.Exp(-6 * Math.Min(seconds, .1)));
        if (Math.Abs(target - Progress) < .0005f) Progress = target;
        hinge.ModelMatrix = Matrix4x4.CreateRotationZ(angle * Progress) * closed;
        return true;
    }
}
