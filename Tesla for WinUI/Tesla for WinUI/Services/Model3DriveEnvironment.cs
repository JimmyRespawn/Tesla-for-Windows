using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;
using SharpDX.Direct3D11;

namespace TeslaMurphy.Services;

// Prebuild variants once. Animation updates matrices; no per-frame mesh creation.
internal sealed class Model3DriveEnvironment
{
    private readonly GroupNode plain = new() { Name = "Straight_Road_Infrastructure" };
    private readonly GroupNode city = new() { Name = "City_Environment", Visible = false };
    private readonly GroupNode tunnel = new() { Name = "Tunnel_Environment", Visible = false };
    private readonly GroupNode curve = new() { Name = "Curved_Road_Infrastructure", Visible = false };
    private readonly GroupNode rain = new() { Name = "Driving_Rain", Visible = false, IsHitTestVisible = false };
    private readonly GroupNode wet = new() { Name = "Driving_Puddles", Visible = false, IsHitTestVisible = false };
    private readonly GroupNode highway, markings, curveMarkings, curveScenery;
    private readonly MeshNode surface;
    private readonly List<MeshNode> grounds = new();
    private readonly List<GroupNode> rainLayers = new();
    private bool light;
    public Model3RoadKind Kind { get; private set; }
    public Model3WeatherKind Weather { get; private set; }
    public bool IsWet => Weather == Model3WeatherKind.Rain && Kind != Model3RoadKind.Tunnel;
    public bool WeatherAnimating => rain.Visible;

    public Model3DriveEnvironment(GroupNode road, GroupNode highway, GroupNode markings)
    {
        this.highway = highway; this.markings = markings;
        surface = road.Items.OfType<MeshNode>().Single(n=>n.Name=="Road_Surface");
        foreach (var node in road.Items.Where(n=>n!=highway && !n.Name.StartsWith("Simulated_Traffic_",StringComparison.Ordinal)).ToArray())
        {
            road.RemoveChildNode(node); plain.AddChildNode(node);
        }
        road.AddChildNode(plain);
        var material = (PBRMaterialCore)surface.Material;
        curve.AddChildNode(Mesh("Curved_Road_Surface", Arc(-5.5f,5.5f,-.010f),material));
        var curveGround = Arc(-90,-5.5f,-.035f); Append(curveGround,Arc(5.5f,90,-.035f));
        var originalGround = Model3HelixSceneAssets.Traverse(plain).OfType<MeshNode>().Single(n=>n.Name=="Roadside_Ground");
        var curvedGroundNode=Mesh("Curved_Roadside_Ground",curveGround,originalGround.Material);
        curve.AddChildNode(curvedGroundNode); grounds.Add(originalGround); grounds.Add(curvedGroundNode);
        foreach(var edge in Model3HelixSceneAssets.Traverse(plain).OfType<MeshNode>().Where(n=>n.Name is "Road_Edge_Lines" or "Opposing_Traffic_Divider"))
        {
            var geometry = Empty();
            var bands = edge.Name=="Road_Edge_Lines" ? new[]{(-5.31f,-5.19f),(5.19f,5.31f)} : new[]{(-1.955f,-1.885f),(-1.815f,-1.745f)};
            foreach(var band in bands) Append(geometry,Arc(band.Item1,band.Item2,-.008f));
            curve.AddChildNode(Mesh("Curved_"+edge.Name,geometry,edge.Material));
        }
        curveMarkings=new GroupNode{Name="Curved_Lane_Markings"};
        var dashes=Empty();
        for(int i=0;i<72;i++) Quad(dashes, i*12,i*12+4,1.805f,1.895f,-.009f, true);
        curveMarkings.AddChildNode(Mesh("Curved_Lane_Dashes",dashes,markings.Items.OfType<MeshNode>().Single().Material));
        curve.AddChildNode(curveMarkings);
        curveScenery=new GroupNode{Name="Curved_Roadside"};
        foreach(var node in Model3HelixSceneAssets.Traverse(highway).OfType<MeshNode>())
        {
            var source=(MeshGeometry3D)node.Geometry;
            var mapped=new MeshGeometry3D{Positions=new(),Normals=new(),Indices=source.Indices};
            for(int i=0;i<source.Positions.Count;i++)
            {
                var p=source.Positions[i]; mapped.Positions.Add(Model3DriveEnvironmentMath.CurvePoint(p));
                mapped.Normals.Add(Vector3.TransformNormal(source.Normals[i],Matrix4x4.CreateRotationY(-p.X/Model3DriveEnvironmentMath.CurveRadius)));
            }
            curveScenery.AddChildNode(Mesh("Curved_"+node.Name,mapped,node.Material));
        }
        curve.AddChildNode(curveScenery);
        BuildCity(); BuildTunnel(); BuildRain(); BuildPuddles();
        road.AddChildNode(city); road.AddChildNode(tunnel); road.AddChildNode(curve); road.AddChildNode(rain); road.AddChildNode(wet);
        Set(Model3RoadKind.Highway,Model3WeatherKind.Clear);
    }

    public void Set(Model3RoadKind kind, Model3WeatherKind weather)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(weather)) return;
        Kind=kind; Weather=weather;
        plain.Visible=kind!=Model3RoadKind.Curved;
        highway.Visible=kind==Model3RoadKind.Highway;
        city.Visible=kind==Model3RoadKind.City;
        tunnel.Visible=kind==Model3RoadKind.Tunnel;
        curve.Visible=kind==Model3RoadKind.Curved;
        rain.Visible=wet.Visible=IsWet;
        foreach(var node in wet.Items) node.Visible=Kind==Model3RoadKind.Curved ? node.Name=="Curved_Wet_Road_Puddles" : node.Name=="Wet_Road_Puddles";
        SetLightTheme(light);
    }

    public void SetLightTheme(bool value)
    {
        light=value;
        var road=(PBRMaterialCore)surface.Material;
        road.AlbedoColor=IsWet ? light ? new Color4(.13f,.16f,.19f,.88f) : new Color4(.045f,.055f,.065f,.88f)
            : light ? new Color4(.22f,.24f,.27f,1) : new Color4(.055f,.065f,.078f,1);
        road.RoughnessFactor=IsWet ? .38f : .98f;
        road.MetallicFactor=0;
        road.ReflectanceFactor=.03f;
        road.ClearCoatStrength=IsWet ? .20f : 0;
        road.ClearCoatRoughness=.30f;
        // The studio's rectangular softboxes belong on the car; on an open
        // road they otherwise create a bright artificial band across the lane.
        road.RenderEnvironmentMap=false;
        surface.IsTransparent=IsWet;
        curve.Items.OfType<MeshNode>().Single(n=>n.Name=="Curved_Road_Surface").IsTransparent=IsWet;
        foreach(var ground in grounds)
        {
            var material=(DiffuseMaterialCore)ground.Material;
            material.DiffuseColor=Kind is Model3RoadKind.City or Model3RoadKind.Tunnel
                ? light ? new Color4(.28f,.30f,.33f,1) : new Color4(.045f,.052f,.063f,1)
                : light ? new Color4(.47f,.55f,.46f,1) : new Color4(.075f,.11f,.095f,1);
        }
        foreach(var node in Model3HelixSceneAssets.Traverse(city).OfType<MeshNode>())
            if(node.Name=="City_Windows" && node.Material is PBRMaterialCore windows)
                windows.EmissiveColor=light ? new Color4(.12f,.16f,.21f,1) : new Color4(.8f,.64f,.36f,1);
    }

    public void Apply(double distance, float roadOffset, double weatherTime)
    {
        city.ModelMatrix=tunnel.ModelMatrix=Matrix4x4.CreateTranslation((float)(distance%24),0,0);
        curveMarkings.ModelMatrix=Model3DriveEnvironmentMath.CurveAdvance(roadOffset);
        curveScenery.ModelMatrix=Model3DriveEnvironmentMath.CurveAdvance(distance%24);
        wet.ModelMatrix=Kind==Model3RoadKind.Curved ? Model3DriveEnvironmentMath.CurveAdvance(distance%24) : Matrix4x4.CreateTranslation((float)(distance%24),0,0);
        for(int i=0;i<rainLayers.Count;i++)
            rainLayers[i].ModelMatrix=Matrix4x4.CreateTranslation(0,-Model3DriveEnvironmentMath.RainOffset(weatherTime,i),0);
    }

    private void BuildCity()
    {
        var stone=Empty(); var glass=Empty(); var sidewalks=Empty(); var poles=Empty(); var lamps=Empty();
        for(int i=-10;i<=10;i++)foreach(int side in new[]{-1,1})
        {
            float x=i*24,z=side*13, height=7+((i+11)*7%5)*2;
            Box(stone,new Vector3(x+4,height*.5f,z),new Vector3(15,height,8));
            Box(sidewalks,new Vector3(x,.08f,side*6.7f),new Vector3(24,.16f,2.4f));
            Box(poles,new Vector3(x,3,side*7.4f),new Vector3(.14f,6,.14f));
            Box(lamps,new Vector3(x,5.9f,side*7.4f),new Vector3(.6f,.12f,.6f));
            for(int row=0;row<height/2-1;row++)for(int col=0;col<5;col++)
                Box(glass,new Vector3(x-2+col*2.6f,1.7f+row*2,side*8.98f),new Vector3(1.2f,1.1f,.06f));
        }
        city.AddChildNode(Mesh("City_Buildings",stone,new PBRMaterialCore{AlbedoColor=new Color4(.33f,.38f,.45f,1),RoughnessFactor=.87f,MetallicFactor=0}));
        city.AddChildNode(Mesh("City_Windows",glass,new PBRMaterialCore{AlbedoColor=new Color4(.15f,.22f,.30f,1),RoughnessFactor=.22f,MetallicFactor=.3f,RenderEnvironmentMap=true}));
        city.AddChildNode(Mesh("City_Sidewalks",sidewalks,new DiffuseMaterialCore{DiffuseColor=new Color4(.46f,.47f,.49f,1)}));
        city.AddChildNode(Mesh("City_Lamp_Posts",poles,new DiffuseMaterialCore{DiffuseColor=new Color4(.30f,.33f,.37f,1)}));
        city.AddChildNode(Mesh("City_Lamps",lamps,new PBRMaterialCore{AlbedoColor=new Color4(.85f,.80f,.63f,1),EmissiveColor=new Color4(1,.8f,.5f,1)}));
    }

    private void BuildTunnel()
    {
        var shell=Empty();var ribs=Empty();var lamps=Empty();var strips=Empty();
        Box(shell,new Vector3(0,4,8.5f),new Vector3(520,8,.4f));
        Box(shell,new Vector3(0,4,-8.5f),new Vector3(520,8,.4f));
        Box(shell,new Vector3(0,8.15f,0),new Vector3(520,.3f,17.4f));
        for(int i=-11;i<=11;i++)
        {
            float x=i*24;
            Box(ribs,new Vector3(x,4,8.25f),new Vector3(.45f,8,.18f));
            Box(ribs,new Vector3(x,4,-8.25f),new Vector3(.45f,8,.18f));
            Box(ribs,new Vector3(x,7.93f,0),new Vector3(.45f,.18f,16.5f));
            foreach(float z in new[]{-4f,4f})Box(lamps,new Vector3(x,7.8f,z),new Vector3(5,.08f,.28f));
        }
        foreach(float z in new[]{-8.22f,8.22f})Box(strips,new Vector3(0,.85f,z),new Vector3(520,.10f,.04f));
        tunnel.AddChildNode(Mesh("Tunnel_Walls_And_Roof",shell,new PBRMaterialCore{AlbedoColor=new Color4(.36f,.38f,.40f,1),RoughnessFactor=.85f,MetallicFactor=0}));
        tunnel.AddChildNode(Mesh("Tunnel_Ribs",ribs,new DiffuseMaterialCore{DiffuseColor=new Color4(.20f,.23f,.27f,1)}));
        tunnel.AddChildNode(Mesh("Tunnel_Ceiling_Lights",lamps,new PBRMaterialCore{AlbedoColor=new Color4(1,.85f,.61f,1),EmissiveColor=new Color4(2,1.65f,1.15f,1)}));
        tunnel.AddChildNode(Mesh("Tunnel_Guide_Lights",strips,new PBRMaterialCore{AlbedoColor=new Color4(.22f,.50f,.72f,1),EmissiveColor=new Color4(.2f,.55f,.9f,1)}));
        foreach(float x in new[]{-24f,0f,24f})
            tunnel.AddChildNode(new PointLightNode{Name="Tunnel_Point_Light",Position=new Vector3(x,7.2f,1.8f),Color=new Color4(.70f,.60f,.44f,1),Range=40,Attenuation=new Vector3(1,.04f,.006f)});
    }

    private void BuildRain()
    {
        var random=new Random(3048);
        var material=new DiffuseMaterialCore{DiffuseColor=new Color4(.75f,.85f,.96f,.24f)};
        for(int layer=0;layer<4;layer++)
        {
            var geometry=Empty();
            for(int i=0;i<90;i++)
            {
                float x=(float)random.NextDouble()*70-45, z=(float)random.NextDouble()*26-11;
                if(Math.Abs(x)<3.2f && z>-2.1f && z<5.8f)continue;
                float y=(float)random.NextDouble()*9+9, length=.26f+(float)random.NextDouble()*.25f;
                var p=new Vector3(x,y,z); var tail=p+new Vector3(.08f,-length,.02f);
                Face(geometry,p-new Vector3(.008f,0,0),p+new Vector3(.008f,0,0),tail+new Vector3(.008f,0,0),tail-new Vector3(.008f,0,0));
                Face(geometry,p-new Vector3(0,0,.008f),p+new Vector3(0,0,.008f),tail+new Vector3(0,0,.008f),tail-new Vector3(0,0,.008f));
            }
            var group=new GroupNode{Name="Rain_Layer_"+layer,IsHitTestVisible=false};
            var node=Mesh("Rain_Streaks_"+layer,geometry,material);node.IsTransparent=true;node.IsThrowingShadow=false;node.CullMode=CullMode.None;
            group.AddChildNode(node);rain.AddChildNode(group);rainLayers.Add(group);
        }
    }

    private void BuildPuddles()
    {
        var g=Empty(); var random=new Random(808);
        for(int i=-11;i<=11;i++)
        {
            float x=i*24+4,z=(float)random.NextDouble()*7-3.5f;
            int start=g.Positions.Count;
            g.Positions.Add(new Vector3(x+3,-.006f,z));g.Normals.Add(Vector3.UnitY);
            for(int j=0;j<16;j++)
            {
                float angle=j*MathF.PI/8, radius=.88f+(float)random.NextDouble()*.12f;
                g.Positions.Add(new Vector3(x+3+3*radius*MathF.Cos(angle),-.006f,z+.65f*radius*MathF.Sin(angle)));g.Normals.Add(Vector3.UnitY);
            }
            for(int j=0;j<16;j++){g.Indices.Add(start);g.Indices.Add(start+1+(j+1)%16);g.Indices.Add(start+1+j);}
        }
        var node=Mesh("Wet_Road_Puddles",g,new PBRMaterialCore{AlbedoColor=new Color4(.15f,.20f,.26f,.12f),RoughnessFactor=.22f,MetallicFactor=0,ReflectanceFactor=.03f,ClearCoatStrength=.20f,ClearCoatRoughness=.25f,RenderEnvironmentMap=false});
        node.IsTransparent=true;node.IsThrowingShadow=false;wet.AddChildNode(node);
        var mapped=Empty();
        foreach(var p in g.Positions)mapped.Positions.Add(Model3DriveEnvironmentMath.CurvePoint(p));
        foreach(var n in g.Normals)mapped.Normals.Add(n);
        mapped.Indices=g.Indices;
        var curved=Mesh("Curved_Wet_Road_Puddles",mapped,node.Material);curved.IsTransparent=true;wet.AddChildNode(curved);
    }

    private static MeshNode Mesh(string name,MeshGeometry3D geometry,MaterialCore material)
        => new(){Name=name,Geometry=geometry,Material=material,IsHitTestVisible=false,IsThrowingShadow=false,CullMode=CullMode.None};
    private static MeshGeometry3D Empty()=>new(){Positions=new(),Normals=new(),Indices=new()};
    private static void Append(MeshGeometry3D to,MeshGeometry3D from)
    {
        int offset=to.Positions.Count;
        foreach(var p in from.Positions)to.Positions.Add(p);foreach(var n in from.Normals)to.Normals.Add(n);
        foreach(var i in from.Indices)to.Indices.Add(i+offset);
    }
    private static MeshGeometry3D Arc(float z0,float z1,float y)
    {
        var g=Empty();for(int i=0;i<288;i++)Quad(g,i*3,i*3+3,z0,z1,y,true);return g;
    }
    private static void Quad(MeshGeometry3D g,float x0,float x1,float z0,float z1,float y,bool curved)
    {
        var a=new Vector3(x0,y,z0);var b=new Vector3(x0,y,z1);var c=new Vector3(x1,y,z1);var d=new Vector3(x1,y,z0);
        if(curved){a=Model3DriveEnvironmentMath.CurvePoint(a);b=Model3DriveEnvironmentMath.CurvePoint(b);c=Model3DriveEnvironmentMath.CurvePoint(c);d=Model3DriveEnvironmentMath.CurvePoint(d);}
        Face(g,a,b,c,d);
    }
    private static void Box(MeshGeometry3D g,Vector3 center,Vector3 size)
    {
        var p=new Vector3[8];for(int i=0;i<8;i++)p[i]=center+new Vector3((i&1)==0?-size.X*.5f:size.X*.5f,(i&4)==0?-size.Y*.5f:size.Y*.5f,(i&2)==0?-size.Z*.5f:size.Z*.5f);
        foreach(var f in new[]{new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,1,5,4},new[]{2,6,7,3},new[]{0,4,6,2},new[]{1,3,7,5}})Face(g,p[f[3]],p[f[2]],p[f[1]],p[f[0]]);
    }
    private static void Face(MeshGeometry3D g,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        int i=g.Positions.Count;var n=Vector3.Normalize(Vector3.Cross(b-a,c-a));
        foreach(var p in new[]{a,b,c,d}){g.Positions.Add(p);g.Normals.Add(n);}foreach(var v in new[]{0,1,2,0,2,3})g.Indices.Add(i+v);
    }
}
