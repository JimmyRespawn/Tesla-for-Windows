using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;

namespace TeslaMurphy.Services;

internal sealed class Model3DrivingLights
{
    private readonly List<(PBRMaterialCore Material, Color4 Original, int Side)> indicators = new();
    private readonly List<(PBRMaterialCore Material, Color4 Original)> brakes = new();
    private readonly List<(MeshNode Mesh, int Side)> rearSignals = new();
    public int IndicatorParts => indicators.Count + rearSignals.Count;
    public int BrakeParts => brakes.Count;

    public Model3DrivingLights(SceneNode car)
    {
        foreach (var mesh in Model3HelixSceneAssets.Traverse(car).OfType<MeshNode>().ToArray())
        {
            if (mesh.Material is not PBRMaterialCore original || mesh.Geometry is not MeshGeometry3D geometry) continue;
            bool indicator = mesh.Name.StartsWith("Front_Indicator", StringComparison.Ordinal) || mesh.Name.StartsWith("turn_indicat_", StringComparison.Ordinal);
            bool brake = mesh.Name.StartsWith("rear_lights", StringComparison.Ordinal) || mesh.Name.StartsWith("light_breake", StringComparison.Ordinal) || mesh.Name.StartsWith("breake_int", StringComparison.Ordinal);
            if (!indicator && !brake) continue;
            // The original front signals share a material. Give each mesh its
            // own copy so one side never lights the other side accidentally.
            var material = new PBRMaterialCore
            {
                Name = original.Name + "_Driving_" + mesh.Name,
                AlbedoColor = original.AlbedoColor, AlbedoMap = original.AlbedoMap,
                NormalMap = original.NormalMap, EmissiveMap = original.EmissiveMap,
                EmissiveColor = original.EmissiveColor, MetallicFactor = original.MetallicFactor,
                RoughnessFactor = original.RoughnessFactor, ClearCoatStrength = original.ClearCoatStrength,
                ClearCoatRoughness = original.ClearCoatRoughness,
                RenderEnvironmentMap = original.RenderEnvironmentMap, RenderShadowMap = original.RenderShadowMap
            };
            mesh.Material = material;
            if (indicator) indicators.Add((material, material.EmissiveColor, geometry.Positions.Average(p => p.Z) > 0 ? 1 : -1));
            if (brake) brakes.Add((material, material.EmissiveColor));
            if (!mesh.Name.StartsWith("light_breake", StringComparison.Ordinal) || mesh.Parent is not GroupNode parent) continue;
            // Reuse the model's existing thin rear LED strip as two separate
            // amber overlays; no texture or model file is modified.
            foreach (int side in new[] { -1, 1 })
            {
                var strip = new MeshGeometry3D { Positions = geometry.Positions, Normals = geometry.Normals, TextureCoordinates = geometry.TextureCoordinates, Indices = new() };
                for (int i = 0; i + 2 < geometry.Indices.Count; i += 3)
                {
                    int a = geometry.Indices[i], b = geometry.Indices[i+1], c = geometry.Indices[i+2];
                    if (side * geometry.Positions[a].Z < .3f || side * geometry.Positions[b].Z < .3f || side * geometry.Positions[c].Z < .3f) continue;
                    strip.Indices.Add(a); strip.Indices.Add(b); strip.Indices.Add(c);
                }
                if (strip.Indices.Count == 0) continue;
                var overlay = new MeshNode
                {
                    Name = side > 0 ? "Driving_Rear_Left_Signal" : "Driving_Rear_Right_Signal",
                    Geometry = strip, ModelMatrix = mesh.ModelMatrix * Matrix4x4.CreateTranslation(.002f, 0, 0),
                    Material = new PBRMaterialCore { AlbedoColor = new Color4(1,.30f,.015f,1), EmissiveColor = new Color4(2.2f,.67f,.04f,1), RoughnessFactor = .4f, MetallicFactor = 0 },
                    Visible = false, IsThrowingShadow = false, IsHitTestVisible = false, CullMode = mesh.CullMode
                };
                parent.AddChildNode(overlay);
                rearSignals.Add((overlay, side));
            }
        }
    }

    public void Apply(Model3DriveDynamics dynamics, bool lockFlash)
    {
        foreach (var light in indicators)
            light.Material.EmissiveColor = lockFlash || dynamics?.SignalOn == true && dynamics.Signal == light.Side
                ? new Color4(2.2f,.67f,.04f,1) : light.Original;
        foreach (var light in brakes)
            light.Material.EmissiveColor = dynamics?.BrakeLights == true ? new Color4(3.2f,.025f,.01f,1) : light.Original;
        foreach (var light in rearSignals)
            light.Mesh.Visible = lockFlash || dynamics?.SignalOn == true && dynamics.Signal == light.Side;
    }
}
