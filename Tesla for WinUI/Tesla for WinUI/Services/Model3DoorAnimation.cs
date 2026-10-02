using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model.Scene;

namespace TeslaMurphy.Services;

// Runtime hinges keep the original meshes, glass and front-mirror subtrees.
// These targets are local demonstration state, never vehicle commands.
internal sealed class Model3DoorAnimation
{
    private readonly Dictionary<string, Door> doors = new(StringComparer.Ordinal);
    public Model3DoorAnimation(SceneNode car)
    {
        if (car is not GroupNode root) throw new InvalidOperationException("Door animation requires a group root.");
        car.UpdateAllTransformMatrix();
        if (!Matrix4x4.Invert(car.TotalModelMatrix, out var inverseCar))
            throw new InvalidOperationException("Car transform is singular.");
        var original = Model3HelixSceneAssets.Traverse(car).ToArray();
        foreach (var id in new[] { "lf", "rf", "lr", "rr" })
        {
            bool left = id[0] == 'l', front = id[1] == 'f';
            var pivot = new Vector3(front ? -1.025f : .13f, .65f, (left ? 1 : -1) * (front ? .80f : .85f));
            var worldHinge = Matrix4x4.CreateTranslation(pivot);
            Matrix4x4.Invert(worldHinge, out var inverseHinge);
            var hinge = new GroupNode { Name = "Door_" + id + "_Hinge", ModelMatrix = worldHinge * inverseCar };
            bool Matches(SceneNode node) => node.Name.StartsWith("door_" + id + "_", StringComparison.Ordinal)
                || front && (node.Name == (left ? "Mirror_Left_Hinge" : "Mirror_Right_Hinge")
                    || node.Name.StartsWith("Mirror_Stem_door_" + id + "_", StringComparison.Ordinal));
            // Move only the highest matching part; duplicate Assimp group/mesh
            // names must not cause the same geometry to be reparented twice.
            var parts = original.Where(Matches).Where(n => n.Parent == null || !Matches(n.Parent)).ToArray();
            if (parts.Length < 8) throw new InvalidOperationException("Door parts are missing: " + id);
            foreach (var part in parts)
            {
                var closedWorld = part.TotalModelMatrix;
                if (part.Parent is not GroupNode parent) throw new InvalidOperationException("Door part parent is not a group.");
                parent.RemoveChildNode(part);
                part.ModelMatrix = closedWorld * inverseHinge;
                hinge.AddChildNode(part);
            }
            root.AddChildNode(hinge);
            doors.Add(id, new Door(hinge, left ? -1.05f : 1.05f));
        }
        car.UpdateAllTransformMatrix();
    }
    public bool IsOpen(string id) => doors[id].Open;
    public float Progress(string id) => doors[id].Amount;
    public void SetOpen(string id, bool open) => doors[id].Open = open;
    public void SetAllOpen(bool open)
    {
        foreach (var door in doors.Values) door.Open = open;
    }
    public bool Step(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return false;
        var blend = (float)(1 - Math.Exp(-8 * Math.Min(seconds, .1)));
        bool changed = false;
        foreach (var door in doors.Values)
        {
            var target = door.Open ? 1f : 0f;
            if (door.Amount == target) continue;
            door.Amount += (target - door.Amount) * blend;
            if (Math.Abs(target - door.Amount) < .0005f) door.Amount = target;
            door.Hinge.ModelMatrix = Matrix4x4.CreateRotationY(door.Angle * door.Amount) * door.Closed;
            changed = true;
        }
        return changed;
    }
    private sealed class Door
    {
        public readonly GroupNode Hinge;
        public readonly Matrix4x4 Closed;
        public readonly float Angle;
        public bool Open;
        public float Amount;
        public Door(GroupNode hinge, float angle) { Hinge = hinge; Closed = hinge.ModelMatrix; Angle = angle; }
    }
}
