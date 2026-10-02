using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model.Scene;

namespace TeslaMurphy.Services;

// Window transforms only; the controller owns vehicle command acknowledgements.
internal sealed class Model3WindowAnimation
{
    private readonly Dictionary<string, Window> windows = new(StringComparer.Ordinal);

    public Model3WindowAnimation(SceneNode car)
    {
        car.UpdateAllTransformMatrix();
        var meshes = Model3HelixSceneAssets.Traverse(car).OfType<MeshNode>().ToArray();
        foreach (var id in new[] { "lf", "rf", "lr", "rr" })
        {
            var node = meshes.Single(n => n.Name == "door_" + id + "_glass.0_0");
            var vertices = node.Geometry?.Positions ?? throw new InvalidOperationException("Window geometry is missing.");
            var parent = node.Parent ?? throw new InvalidOperationException("Window parent is missing.");
            var points = vertices.Select(p => Vector3.Transform(p, node.TotalModelMatrix)).ToArray();
            var height = points.Max(p => p.Y) - points.Min(p => p.Y);
            if (!Matrix4x4.Invert(parent.TotalModelMatrix, out var inverseParent))
                throw new InvalidOperationException("Window parent transform is singular.");
            // Lower the top edge below the original belt line. Map world-down
            // into parent space, retaining the source geometry and closed pose.
            var travel = Vector3.TransformNormal(new Vector3(0, -height - .04f, 0), inverseParent);
            windows.Add(id, new Window(node, travel));
        }
    }

    public bool IsLowered(string id) => windows[id].Target > 0;
    public float Progress(string id) => windows[id].Amount;
    public void SetLowered(string id, bool lowered) => windows[id].Target = lowered ? 1 : 0;
    public void SetOpenFlag(string id, bool open) => windows[id].Target = open ? .12f : 0;
    public void SetAllVented(bool vented)
    {
        foreach (var window in windows.Values) window.Target = vented ? .12f : 0;
    }
    public void SetAllLowered(bool lowered)
    {
        foreach (var window in windows.Values) window.Target = lowered ? 1 : 0;
    }

    // Returns true on every changed frame, including the final exact pose.
    public bool Step(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return false;
        var blend = (float)(1 - Math.Exp(-8 * Math.Min(seconds, .1)));
        bool changed = false;
        foreach (var window in windows.Values)
        {
            var target = window.Target;
            if (window.Amount == target) continue;
            window.Amount += (target - window.Amount) * blend;
            if (Math.Abs(target - window.Amount) < .0005f) window.Amount = target;
            window.Node.ModelMatrix = window.Closed * Matrix4x4.CreateTranslation(window.Travel * window.Amount);
            // A fully lowered sheet is inside the door. Hide it there so it
            // cannot protrude through the simplified inner door lining.
            window.Node.Visible = window.Visible && window.Amount < 1;
            changed = true;
        }
        return changed;
    }

    private sealed class Window
    {
        public readonly MeshNode Node;
        public readonly Matrix4x4 Closed;
        public readonly Vector3 Travel;
        public readonly bool Visible;
        public float Target;
        public float Amount;
        public Window(MeshNode node, Vector3 travel)
        {
            Node = node;
            Closed = node.ModelMatrix;
            Travel = travel;
            Visible = node.Visible;
        }
    }
}
