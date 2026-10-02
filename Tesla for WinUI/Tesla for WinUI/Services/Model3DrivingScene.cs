using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;

namespace TeslaMurphy.Services;

internal sealed class Model3DrivingScene
{
    private readonly List<(MeshNode Node, Matrix4x4 Base, Vector3 Pivot)> wheels = new();
    private readonly GroupNode markings = new() { Name = "Moving_Road_Markings", IsHitTestVisible = false };
    private readonly GroupNode scenery = new() { Name = "Moving_Roadside", IsHitTestVisible = false };
    private readonly List<(GroupNode Node, float Start, float Lane, float Relative)> traffic = new();
    private readonly Model3TrafficAssets trafficAssets;
    private readonly SceneNode car;
    private readonly Matrix4x4 carBase;
    private readonly GroupNode body = new() { Name = "Driving_Body_Suspension", IsHitTestVisible = false };
    private readonly GroupNode steering = new() { Name = "Driving_Steering_Wheel", IsHitTestVisible = false };
    internal int TrafficTrianglesPerVehicle => trafficAssets.TrianglesPerVehicle;
    public GroupNode Road { get; } = new() { Name = "Driving_Road", Visible = false, IsHitTestVisible = false };
    public int RotatingParts => wheels.Count;
    private readonly Model3DriveEnvironment environment;
    public Model3RoadKind RoadKind => environment.Kind;
    public Model3WeatherKind WeatherKind => environment.Weather;
    public bool IsWet => environment.IsWet;
    public bool WeatherAnimating => environment.WeatherAnimating;
    public void SetEnvironment(Model3RoadKind road, Model3WeatherKind weather) => environment.Set(road, weather);

    public Model3DrivingScene(SceneNode car,string trafficDirectory)
    {
        this.car = car;
        carBase = car.ModelMatrix;
        car.UpdateAllTransformMatrix();
        trafficAssets = new Model3TrafficAssets(trafficDirectory);
        foreach (var source in Model3HelixSceneAssets.Traverse(car).OfType<MeshNode>().ToArray())
        {
            if (!source.Name.StartsWith("wheels", StringComparison.Ordinal) || source.Geometry is not MeshGeometry3D original || source.Parent is not GroupNode parent) continue;
            // Offset red brake calipers must remain fixed, unlike the rims/discs.
            if (source.Material is PBRMaterialCore material && material.Name == "wheels.0") continue;
            for (int side = -1; side <= 1; side += 2)
            {
                var geometry = new MeshGeometry3D
                {
                    Positions = original.Positions, Normals = original.Normals,
                    TextureCoordinates = original.TextureCoordinates,
                    Tangents = original.Tangents, BiTangents = original.BiTangents,
                    Colors = original.Colors, Indices = new()
                };
                var indices = geometry.Indices;
                for (int i = 0; i + 2 < original.Indices.Count; i += 3)
                {
                    var a = original.Indices[i]; var b = original.Indices[i + 1]; var c = original.Indices[i + 2];
                    var z = original.Positions[a].Z + original.Positions[b].Z + original.Positions[c].Z;
                    if ((z >= 0 ? 1 : -1) != side) continue;
                    indices.Add(a); indices.Add(b); indices.Add(c);
                }
                if (indices.Count == 0) continue;
                var node = new MeshNode
                {
                    Name = source.Name + (side > 0 ? "_DriveLeft" : "_DriveRight"),
                    Geometry = geometry, Material = source.Material, ModelMatrix = source.ModelMatrix,
                    CullMode = source.CullMode, IsTransparent = source.IsTransparent,
                    IsThrowingShadow = source.IsThrowingShadow, IsHitTestVisible = false
                };
                parent.AddChildNode(node);
                var front = original.Positions[indices[0]].X < 0;
                wheels.Add((node, source.ModelMatrix, new Vector3(front ? -1.55968f : 1.30041f, .34737f, 0)));
            }
            source.Visible = false;
        }
        if (wheels.Count != 24) throw new InvalidOperationException("Unexpected Model 3 wheel layout.");

        // Keep wheel contact points on the road; only the sprung body pitches
        // and rolls. Existing door/window/trunk hinges remain inside this group.
        var container = Model3HelixSceneAssets.Traverse(car).OfType<GroupNode>().First(n => n.Items.Any(c => c.Name.StartsWith("wheels", StringComparison.Ordinal)));
        foreach (var node in container.Items.ToArray())
        {
            if (node.Name.StartsWith("wheels", StringComparison.Ordinal) || node.Name.StartsWith("hub_", StringComparison.Ordinal)) continue;
            container.RemoveChildNode(node);
            body.AddChildNode(node);
        }
        container.AddChildNode(body);
        car.UpdateAllTransformMatrix();
        if (car is GroupNode root && container != root)
        {
            SceneNode branch = container;
            while (branch.Parent != root && branch.Parent != null) branch = branch.Parent;
            Matrix4x4.Invert(body.TotalModelMatrix, out var inverseBody);
            foreach (var node in root.Items.Where(n => n != branch).ToArray())
            {
                var world = node.TotalModelMatrix;
                root.RemoveChildNode(node); node.ModelMatrix = world * inverseBody; body.AddChildNode(node);
            }
        }
        foreach (var node in body.Items.Where(n => n.Name.StartsWith("movsteer_1.0_", StringComparison.Ordinal)).ToArray())
        {
            body.RemoveChildNode(node); steering.AddChildNode(node);
        }
        body.AddChildNode(steering);
        car.UpdateAllTransformMatrix();

        var surface = new MeshGeometry3D { Positions = new(), Normals = new(), Indices = new() };
        AddQuad(surface, -240, 240, -5.5f, 5.5f, -.010f);
        Road.AddChildNode(new MeshNode
        {
            Name = "Road_Surface", Geometry = surface,
            Material = new PBRMaterialCore { AlbedoColor = new Color4(.055f,.065f,.078f,1), RoughnessFactor = .98f, MetallicFactor = 0, RenderShadowMap = true, RenderEnvironmentMap = false },
            IsThrowingShadow = false, IsHitTestVisible = false
        });
        var paint = new MeshGeometry3D { Positions = new(), Normals = new(), Indices = new() };
        for (int segment = -22; segment <= 22; segment++)
        foreach (var z in new[] { 1.85f }) AddQuad(paint, segment * 12, segment * 12 + 4, z - .045f, z + .045f, -.009f);
        markings.AddChildNode(new MeshNode
        {
            Name = "Lane_Dashes", Geometry = paint,
            Material = new DiffuseMaterialCore { DiffuseColor = new Color4(.75f,.78f,.80f,1) },
            IsThrowingShadow = false, IsHitTestVisible = false
        });
        Road.AddChildNode(markings);
        BuildEnvironment();
        environment = new Model3DriveEnvironment(Road, scenery, markings);
        Apply(0, 0);
    }

    public void Apply(float angle, float offset, double distance = 0, Model3DriveDynamics dynamics = null, double weatherTime = 0)
    {
        car.ModelMatrix = carBase * (dynamics?.TravelPose ?? Matrix4x4.Identity);
        body.ModelMatrix = dynamics?.BodyPose ?? Matrix4x4.Identity;
        var steeringPivot = new Vector3(-.57f,.84f,.40f);
        float curveSteering = dynamics != null && environment.Kind == Model3RoadKind.Curved ? .0208f : 0;
        steering.ModelMatrix = Matrix4x4.CreateTranslation(-steeringPivot) *
            Matrix4x4.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1,.15f,0)), -((dynamics?.SteeringAngle ?? 0) + curveSteering) * 9) * Matrix4x4.CreateTranslation(steeringPivot);
        foreach (var wheel in wheels)
            wheel.Node.ModelMatrix = Matrix4x4.CreateTranslation(-wheel.Pivot) * Matrix4x4.CreateRotationZ(angle) * Matrix4x4.CreateTranslation(wheel.Pivot) * wheel.Base;
        markings.ModelMatrix = Matrix4x4.CreateTranslation(offset, 0, 0);
        scenery.ModelMatrix = Matrix4x4.CreateTranslation((float)(distance % 24), 0, 0);
        foreach (var car in traffic)
        {
            var x = (float)(((car.Start + distance * car.Relative + 120) % 240 + 240) % 240 - 120);
            car.Node.ModelMatrix = (car.Relative > 1 ? Matrix4x4.CreateRotationY(MathF.PI) : Matrix4x4.Identity)
                * (environment.Kind == Model3RoadKind.Curved
                    ? Matrix4x4.CreateRotationY(-x / Model3DriveEnvironmentMath.CurveRadius) * Matrix4x4.CreateTranslation(Model3DriveEnvironmentMath.CurvePoint(new Vector3(x,0,car.Lane)))
                    : Matrix4x4.CreateTranslation(x,0,car.Lane));
        }
        environment.Apply(distance, offset, weatherTime);
    }

    public bool LaneClear(int lane, double distance)
        => !traffic.Any(t => Math.Abs(t.Lane - lane * Model3DriveDynamics.LaneWidth) < .4 &&
            TrafficX(t.Start, distance, t.Relative) > -14 && TrafficX(t.Start, distance, t.Relative) < 10);

    private static double TrafficX(float start, double distance, float relative)
        => ((start + distance * relative + 120) % 240 + 240) % 240 - 120;

    public void SetLightTheme(bool light) => environment.SetLightTheme(light);

    private static MeshGeometry3D EmptyMesh() => new() { Positions = new(), Normals = new(), Indices = new() };

    private static MeshNode AddMesh(GroupNode parent, string name, MeshGeometry3D geometry, Color4 color)
    {
        var mesh = new MeshNode { Name = name, Geometry = geometry,
            Material = new DiffuseMaterialCore { DiffuseColor = color },
            IsHitTestVisible = false, IsThrowingShadow = true };
        parent.AddChildNode(mesh);
        return mesh;
    }

    private void BuildEnvironment()
    {
        var ground = EmptyMesh();
        AddQuad(ground, -300, 300, -300, -5.5f, -.035f);
        AddQuad(ground, -300, 300, 5.5f, 300, -.035f);
        AddMesh(Road, "Roadside_Ground", ground, new Color4(.075f,.11f,.095f,1)).IsThrowingShadow = false;
        var edges = EmptyMesh();
        foreach (var z in new[] { -5.25f, 5.25f }) AddQuad(edges, -240, 240, z-.06f, z+.06f, -.008f);
        AddMesh(Road, "Road_Edge_Lines", edges, new Color4(.85f,.86f,.83f,1)).IsThrowingShadow = false;
        var divider = EmptyMesh();
        foreach(var z in new[]{-1.92f,-1.78f}) AddQuad(divider,-240,240,z-.035f,z+.035f,-.008f);
        AddMesh(Road,"Opposing_Traffic_Divider",divider,new Color4(.85f,.64f,.18f,1)).IsThrowingShadow = false;
        var rails = EmptyMesh(); var trunks = EmptyMesh(); var foliage = EmptyMesh(); var lights = EmptyMesh();
        for (int i = -10; i <= 10; i++)
        foreach (var side in new[] { -1, 1 })
        {
            float x = i*24, z = side*6.2f;
            Box(rails, new Vector3(x,.65f,z), new Vector3(24,.18f,.12f));
            for (int p=0;p<4;p++) Box(rails,new Vector3(x-10+p*6,.35f,z),new Vector3(.1f,.7f,.12f));
            Box(trunks,new Vector3(x+7,1.1f,side*10),new Vector3(.28f,2.2f,.28f));
            // Two tapered crown tiers keep trees inexpensive and readable in motion.
            Prism(foliage,new Vector3(x+7,2.8f,side*10),new Vector3(2.7f,2.8f,2.7f),.05f);
            Prism(foliage,new Vector3(x+7,4,side*10),new Vector3(2,2.6f,2),.03f);
            Box(rails,new Vector3(x,2.7f,side*7.2f),new Vector3(.13f,5.4f,.13f));
            Box(rails,new Vector3(x,5.35f,side*6.3f),new Vector3(.14f,.12f,1.9f));
            Box(lights,new Vector3(x,5.25f,side*5.5f),new Vector3(.48f,.08f,.5f));
        }
        AddMesh(scenery,"Guardrails_And_Lamp_Posts",rails,new Color4(.42f,.46f,.49f,1));
        AddMesh(scenery,"Tree_Trunks",trunks,new Color4(.25f,.19f,.13f,1));
        AddMesh(scenery,"Tree_Crowns",foliage,new Color4(.18f,.32f,.24f,1));
        var lamps = AddMesh(scenery,"Lamp_Soft_Lights",lights,new Color4(.95f,.92f,.75f,1));
        lamps.Material = new PBRMaterialCore { AlbedoColor = new Color4(.8f,.8f,.7f,1),
            EmissiveColor = new Color4(.6f,.57f,.4f,1), MetallicFactor = 0, RoughnessFactor = 1 };
        Road.AddChildNode(scenery);
        // All traffic is local simulation. Separate lanes and spaced fixed phases
        // avoid collisions; recycle well beyond the close viewing area.
        AddTraffic(-26, 0, 0, "Sedan");
        AddTraffic(-46, 3.6f, -.18f, "SUV");
        AddTraffic(30, 3.6f, -.18f, "Sedan");
        AddTraffic(-74, -3.6f, 1.85f, "Hatchback");
        AddTraffic(48, -3.6f, 1.85f, "Sport");
    }

    private void AddTraffic(float start, float lane, float relative, string kind)
    {
        var group = trafficAssets.Create("Simulated_Traffic_" + traffic.Count, kind);
        traffic.Add((group,start,lane,relative)); Road.AddChildNode(group);
    }

    private static void Box(MeshGeometry3D g, Vector3 center, Vector3 size) => Prism(g,center,size,1);
    private static void Prism(MeshGeometry3D g, Vector3 center, Vector3 size, float taper)
    {
        var p = new Vector3[8];
        for(int i=0;i<8;i++)
        {
            bool top=(i&4)!=0; float scale=top?taper:1;
            p[i]=center+new Vector3((i&1)==0?-size.X*.5f*scale:size.X*.5f*scale,
                top?size.Y*.5f:-size.Y*.5f,(i&2)==0?-size.Z*.5f*scale:size.Z*.5f*scale);
        }
        foreach(var face in new[]{new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,1,5,4},new[]{2,6,7,3},new[]{0,4,6,2},new[]{1,3,7,5}})
            Face(g,p[face[3]],p[face[2]],p[face[1]],p[face[0]]);
    }
    private static void Face(MeshGeometry3D g,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        int index=g.Positions.Count;
        var n=Vector3.Cross(b-a,c-a); n=n.LengthSquared()>1e-10f?Vector3.Normalize(n):Vector3.UnitZ;
        foreach(var p in new[]{a,b,c,d}) { g.Positions.Add(p);g.Normals.Add(n); }
        foreach(var v in new[]{0,1,2,0,2,3})g.Indices.Add(index+v);
    }

    private static void AddQuad(MeshGeometry3D geometry, float x0, float x1, float z0, float z1, float y)
    {
        int index = geometry.Positions.Count;
        geometry.Positions.Add(new Vector3(x0,y,z0)); geometry.Positions.Add(new Vector3(x0,y,z1));
        geometry.Positions.Add(new Vector3(x1,y,z1)); geometry.Positions.Add(new Vector3(x1,y,z0));
        for (int i=0; i<4; i++) geometry.Normals.Add(Vector3.UnitY);
        foreach (var vertex in new[] { 0,1,2,0,2,3 }) geometry.Indices.Add(index + vertex);
    }
}
