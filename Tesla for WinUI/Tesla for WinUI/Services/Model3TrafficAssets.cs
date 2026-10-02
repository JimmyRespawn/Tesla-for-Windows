using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;

namespace TeslaMurphy.Services;

// Quaternius Cars Pack, CC0. See Assets/Traffic/CREDITS.md.
// FBX is imported once; texture-free geometry and neutral materials are shared
// across instances. No original-car geometry or materials are modified.
internal sealed class Model3TrafficAssets
{
    private readonly Dictionary<string,List<(string Name,MeshGeometry3D Geometry,PBRMaterialCore Material)>> templates = new();
    private readonly Dictionary<string,PBRMaterialCore> materials = new();
    internal int TrianglesPerVehicle => templates.Values.Max(t=>t.Sum(p=>p.Geometry.Indices.Count/3));
    internal IReadOnlyCollection<string> Kinds => templates.Keys;

    internal Model3TrafficAssets(string directory)
    {
        Load(directory,"Sedan","NormalCar1.fbx",4.5f);
        Load(directory,"Hatchback","NormalCar2.fbx",3.95f);
        Load(directory,"SUV","SUV.fbx",4.6f);
        Load(directory,"Sport","SportsCar2.fbx",4.4f);
    }

    internal GroupNode Create(string name,string kind)
    {
        var group=new GroupNode { Name=name,IsHitTestVisible=false };
        foreach(var part in templates[kind])
            group.AddChildNode(new MeshNode
            {
                Name="Traffic_"+part.Name,Geometry=part.Geometry,Material=part.Material,
                IsHitTestVisible=false,IsThrowingShadow=part.Name!="GroundShadow",
                IsTransparent=part.Name=="GroundShadow"
            });
        return group;
    }

    private void Load(string directory,string kind,string filename,float length)
    {
        using var importer=new Importer();
        importer.Configuration.ImportMaterialType=MaterialType.PBR;
        importer.Configuration.ImportAnimations=false;
        importer.Configuration.BuildOctree=false;
        var imported=importer.Load(Path.Combine(directory,filename)) ?? throw new InvalidDataException("Traffic FBX import failed: "+filename);
        try
        {
            imported.Root.UpdateAllTransformMatrix();
            var nodes=Model3HelixSceneAssets.Traverse(imported.Root).OfType<MeshNode>().ToArray();
            var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);
            foreach(var node in nodes)
            foreach(var p in ((MeshGeometry3D)node.Geometry).Positions)
            { var world=Vector3.Transform(p,node.TotalModelMatrix);min=Vector3.Min(min,world);max=Vector3.Max(max,world); }
            float scale=length/(max.Z-min.Z);
            // Source is Y-up, +Z forward, centimeter-scale. Normalize to meters
            // and -X forward while keeping the artist's vehicle proportions.
            var normalize=Matrix4x4.CreateTranslation(-(min.X+max.X)*.5f,-min.Y,-(min.Z+max.Z)*.5f)
                *Matrix4x4.CreateRotationY(-MathF.PI*.5f)*Matrix4x4.CreateScale(scale);
            var buckets=new Dictionary<string,MeshGeometry3D>();
            foreach(var node in nodes)
            {
                var source=(MeshGeometry3D)node.Geometry;
                var material=(PBRMaterialCore)node.Material;
                var category=material.Name switch
                {
                    "Windows"=>"Glass", "Black"=>"DarkTrim", "Grey"=>"Metal",
                    "Headlights"=>"Headlamp", "TailLights"=>"TailLamp", _=>"Body"
                };
                if(!buckets.TryGetValue(category,out var geometry)) buckets[category]=geometry=EmptyMesh();
                var temporary=Transform(source,node.TotalModelMatrix*normalize);
                SmoothNormals(temporary);
                int offset=geometry.Positions.Count;
                foreach(var p in temporary.Positions)geometry.Positions.Add(p);
                foreach(var n in temporary.Normals)geometry.Normals.Add(n);
                foreach(var index in temporary.Indices)geometry.Indices.Add(index+offset);
            }
            var parts=new List<(string,MeshGeometry3D,PBRMaterialCore)>();
            foreach(var pair in buckets)parts.Add((pair.Key,pair.Value,Material(pair.Key)));
            if(!buckets.ContainsKey("Body") || !buckets.ContainsKey("Glass")) throw new InvalidDataException("Traffic body/glass missing.");
            // Soft contact shadow beyond the studio shadow-map range. Alpha
            // reaches zero at the texture border, avoiding a rectangular patch.
            var shadow=EmptyMesh();
            shadow.Positions.Add(new Vector3(-length*.54f,-.009f,-1.05f));
            shadow.Positions.Add(new Vector3(-length*.54f,-.009f,1.05f));
            shadow.Positions.Add(new Vector3(length*.54f,-.009f,1.05f));
            shadow.Positions.Add(new Vector3(length*.54f,-.009f,-1.05f));
            for(int i=0;i<4;i++)shadow.Normals.Add(Vector3.UnitY);
            foreach(var i in new[]{0,1,2,0,2,3})shadow.Indices.Add(i);
            shadow.TextureCoordinates=new(){new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
            parts.Add(("GroundShadow",shadow,Material("GroundShadow")));
            templates.Add(kind,parts);
        }
        finally { imported.Root.Dispose(); }
    }

    private static MeshGeometry3D EmptyMesh()=>new(){Positions=new(),Normals=new(),Indices=new()};
    private static MeshGeometry3D Transform(MeshGeometry3D source,Matrix4x4 transform)
    {
        var result=EmptyMesh();
        Matrix4x4.Invert(transform,out var inverse);var normalMatrix=Matrix4x4.Transpose(inverse);
        foreach(var p in source.Positions)result.Positions.Add(Vector3.Transform(p,transform));
        foreach(var n in source.Normals)result.Normals.Add(SafeNormal(Vector3.TransformNormal(n,normalMatrix)));
        foreach(var i in source.Indices)result.Indices.Add(i);
        return result;
    }

    // Average coincident normals within a modest crease angle, preserving
    // sharp glass and bumper edges while smoothing bevels and wheel surfaces.
    private static void SmoothNormals(MeshGeometry3D mesh)
    {
        var vertices=new Dictionary<(int,int,int),List<int>>();
        for(int i=0;i<mesh.Positions.Count;i++)
        {
            var p=mesh.Positions[i];var key=((int)MathF.Round(p.X*100000),(int)MathF.Round(p.Y*100000),(int)MathF.Round(p.Z*100000));
            if(!vertices.TryGetValue(key,out var group))vertices[key]=group=new();
            group.Add(i);
        }
        var original=mesh.Normals.ToArray();
        foreach(var group in vertices.Values)
        foreach(var i in group)
        {
            var sum=Vector3.Zero;
            foreach(var j in group)if(Vector3.Dot(original[i],original[j])>.57f)sum+=original[j];
            mesh.Normals[i]=SafeNormal(sum);
        }
    }
    private static Vector3 SafeNormal(Vector3 n)=>n.LengthSquared()>1e-12f?Vector3.Normalize(n):Vector3.UnitY;

    private PBRMaterialCore Material(string kind)
    {
        if(materials.TryGetValue(kind,out var material))return material;
        var color=kind switch
        {
            "Body"=>new Color4(.73f,.75f,.78f,1),"Glass"=>new Color4(.16f,.18f,.21f,1),
            "DarkTrim"=>new Color4(.10f,.115f,.13f,1),"Metal"=>new Color4(.38f,.41f,.45f,1),
            "Headlamp"=>new Color4(.50f,.52f,.55f,1),"TailLamp"=>new Color4(.30f,.28f,.29f,1),
            _=>new Color4(0,0,0,.28f)
        };
        material=new PBRMaterialCore
        {
            Name="Traffic_"+kind,AlbedoColor=color,MetallicFactor=0,RoughnessFactor=.78f,
            ReflectanceFactor=.12f,ClearCoatStrength=0,RenderEnvironmentMap=false,
            RenderShadowMap=kind!="GroundShadow"
        };
        if(kind=="GroundShadow")
        {
            const int w=64,h=32;var pixels=new Color4[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                float u=2f*x/(w-1)-1,v=2f*y/(h-1)-1;float a=Math.Max(0,1-u*u-v*v);
                pixels[y*w+x]=new Color4(0,0,0,a*a);
            }
            material.AlbedoMap=new TextureModel(pixels,w,h);
        }
        materials.Add(kind,material);return material;
    }
}
