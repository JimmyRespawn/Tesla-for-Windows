using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Assimp;
using HelixToolkit.SharpDX.Cameras;
using HelixToolkit.SharpDX.Model;
using HelixToolkit.SharpDX.Model.Scene;
using SharpDX.Direct3D11;

namespace TeslaMurphy.Services
{
    // This core-only scene factory is also used by the off-screen Direct3D
    // validation. Both renderers load the same licensed GLB and embedded textures.
    internal static class Model3HelixSceneAssets
    {
        // Leave clearance between the folded housing and the side glass.
        internal const float MirrorFoldAngle = .90f;
        internal static Color4 ApplyDrivingAtmosphere(GroupNode studio, bool lightTheme, bool driving, bool wet, bool tunnel, IEnumerable<(MeshNode Source, MeshNode Reflection)> reflections)
        {
            ApplyTheme(studio, lightTheme);
            wet &= driving; tunnel &= driving;
            foreach (var pair in reflections)
                if (pair.Source.Material is PBRMaterialCore source && pair.Reflection.Material is PBRMaterialCore reflection)
                {
                    var color = reflection.AlbedoColor;
                    color.Alpha = source.AlbedoColor.Alpha * (wet ? .04f : .20f);
                    reflection.AlbedoColor = color;
                }
            foreach (var light in studio.Items.OfType<DirectionalLightNode>())
            {
                var c = light.Color;
                float strength = tunnel ? .30f : wet ? .72f : 1;
                light.Color = new Color4(c.Red * strength, c.Green * strength, c.Blue * strength, 1);
            }
            foreach (var node in studio.Items.OfType<MeshNode>())
                if (node.Name is "Studio_Floor" or "Tire_Contact_Shadow") node.Visible = !driving && (node.Name == "Studio_Floor" || !lightTheme);
            return tunnel ? new Color4(16/255f,20/255f,28/255f,1) : wet
                ? lightTheme ? new Color4(183/255f,196/255f,207/255f,1) : new Color4(16/255f,26/255f,37/255f,1)
                : lightTheme ? new Color4(241/255f,243/255f,247/255f,1) : new Color4(16/255f,21/255f,30/255f,1);
        }
        internal static void ApplyHeadlightEmission(System.Collections.Generic.IEnumerable<PBRMaterialCore> materials, bool locked, bool flashing)
        {
            var emission = flashing ? new Color4(3.4f, 3.6f, 4f, 1) : locked
                ? new Color4(.04f, .06f, .09f, 1) : new Color4(.40f, .48f, .60f, 1);
            foreach (var material in materials) material.EmissiveColor = emission;
        }
        internal static HelixToolkitScene ImportModel(string path)
        {
            using var importer = new Importer();
            importer.Configuration.ImportMaterialType = MaterialType.PBR;
            importer.Configuration.EnableParallelProcessing = true;
            importer.Configuration.ImportAnimations = false;
            importer.Configuration.BuildOctree = false;
            var scene = importer.Load(path) ?? throw new InvalidOperationException(Model3Text.Get("Model 3 模型导入失败：") + importer.ErrorCode);
            try
            {
                ApplyGltfMaterialValues(scene.Root, path);
                ExpandGrayscaleAlbedoTextures(scene.Root);
                scene.Root.UpdateAllTransformMatrix();
                foreach (var mesh in Traverse(scene.Root).OfType<MeshNode>())
                {
                    // Replace the trim's single gray texel with an equivalent
                    // native constant. Keep the artist's detailed texture maps.
                    if (mesh.Material is PBRMaterialCore original && original.Name == "movsteer_1.0.1")
                    {
                        original.AlbedoMap = null;
                        original.AlbedoColor = new Color4(.20f, .23f, .27f, 1);
                        original.MetallicFactor = .78f;
                        original.RoughnessFactor = .24f;
                    }
                    if (mesh.Name.StartsWith("tembus_depan_ok", StringComparison.Ordinal))
                    {
                        mesh.Material = new PBRMaterialCore
                        {
                            Name = "Headlamp_Neutral_Lens",
                            AlbedoColor = new Color4(.13f, .16f, .20f, .28f),
                            RoughnessFactor = .15f,
                            MetallicFactor = .08f,
                            ClearCoatStrength = 1,
                            ClearCoatRoughness = .10f,
                            RenderEnvironmentMap = true,
                            RenderShadowMap = false
                        };
                    }
                    if (mesh.Material is PBRMaterialCore material)
                        mesh.IsTransparent = material.AlbedoColor.Alpha < .999f;
                    mesh.IsThrowingShadow = !mesh.IsTransparent;
                }
                foreach (var name in new[] { "Charge_Port_Hinge", "Mirror_Left_Hinge", "Mirror_Right_Hinge" })
                {
                    if (Traverse(scene.Root).Count(node => node.Name == name) != 1)
                        throw new InvalidOperationException(Model3Text.Get("模型缺少交互部件：") + name);
                }
                return scene;
            }
            catch
            {
                scene.Root.Dispose();
                throw;
            }
        }

        internal static GroupNode CreateStudio(SceneNode car,
            out List<(MeshNode Source, MeshNode Reflection)> reflections,
            out MemoryStream environmentStream)
        {
            var studio = new GroupNode { Name = "Helix_Studio" };
            environmentStream = CreateEnvironmentCube();
            studio.AddChildNode(new EnvironmentMapNode
            {
                Name = "Studio_Softboxes",
                Texture = new TextureModel(environmentStream, false),
                SkipRendering = true
            });
            // Helix PBR adds ambient directly to the shaded color. A small
            // neutral fill preserves dark trim, tire and floor contrast.
            studio.AddChildNode(new AmbientLightNode { Color = new Color4(.025f, .028f, .035f, 1) });
            studio.AddChildNode(new DirectionalLightNode
            {
                Direction = new Vector3(.55f, -1, -.35f),
                Color = new Color4(.86f, .85f, .81f, 1)
            });
            studio.AddChildNode(new DirectionalLightNode
            {
                Direction = new Vector3(-.75f, -.6f, .8f),
                Color = new Color4(.23f, .25f, .28f, 1)
            });
            studio.AddChildNode(new DirectionalLightNode
            {
                Direction = new Vector3(-.2f, -.8f, -1),
                Color = new Color4(.31f, .34f, .39f, 1)
            });
            studio.AddChildNode(new ShadowMapNode
            {
                Name = "Studio_Shadow",
                Resolution = new SharpDX.Size2(2048, 2048),
                Bias = .0015f,
                Intensity = .35f,
                LightCamera = new OrthographicCameraCore
                {
                    Position = new Vector3(-4.5f, 7, 3.4f),
                    LookDirection = new Vector3(4.5f, -7, -3.4f),
                    UpDirection = Vector3.UnitY,
                    Width = 10,
                    NearPlaneDistance = .1f,
                    FarPlaneDistance = 25
                }
            });
            studio.AddChildNode(car);

            // Geometry buffers and original textures are shared with the car.
            // The faint mirrored layer moves with the actual animated hinges.
            var mirrorGroup = new GroupNode { Name = "Floor_Reflection", IsHitTestVisible = false };
            reflections = new List<(MeshNode, MeshNode)>();
            var materials = new Dictionary<PBRMaterialCore, PBRMaterialCore>();
            foreach (var source in Traverse(car).OfType<MeshNode>())
            {
                if (source.Material is not PBRMaterialCore original) continue;
                if (!ReflectsOnFloor(original.Name)) continue;
                if (!materials.TryGetValue(original, out var material))
                {
                    var color = original.AlbedoColor;
                    color.Alpha *= .20f;
                    material = new PBRMaterialCore
                    {
                        Name = original.Name + "_Reflection",
                        AlbedoColor = color,
                        AlbedoMap = original.AlbedoMap,
                        NormalMap = original.NormalMap,
                        RoughnessMetallicMap = original.RoughnessMetallicMap,
                        AmbientOcculsionMap = original.AmbientOcculsionMap,
                        SurfaceMapSampler = original.SurfaceMapSampler,
                        UVTransform = original.UVTransform,
                        RoughnessFactor = Math.Max(original.RoughnessFactor, .35f),
                        MetallicFactor = original.MetallicFactor,
                        ReflectanceFactor = original.ReflectanceFactor,
                        AmbientOcclusionFactor = original.AmbientOcclusionFactor,
                        RenderEnvironmentMap = true,
                        RenderShadowMap = false,
                        EmissiveColor = new Color4(0, 0, 0, 1)
                    };
                    materials.Add(original, material);
                }
                var reflection = new MeshNode
                {
                    Name = source.Name + "_Reflection",
                    Geometry = source.Geometry,
                    Material = material,
                    ModelMatrix = source.TotalModelMatrix * FloorMirror,
                    IsTransparent = true,
                    IsThrowingShadow = false,
                    IsHitTestVisible = false,
                    CullMode = CullMode.None,
                    InvertNormal = true
                };
                mirrorGroup.AddChildNode(reflection);
                reflections.Add((source, reflection));
            }
            studio.AddChildNode(mirrorGroup);
            studio.AddChildNode(new MeshNode
            {
                Name = "Studio_Floor",
                Geometry = CreateFloorGeometry(),
                Material = new PBRMaterialCore
                {
                    Name = "Studio_Floor_Material",
                    AlbedoColor = new Color4(.0018f, .0022f, .0032f, .86f),
                    VertexColorBlendingFactor = 1,
                    MetallicFactor = .03f,
                    RoughnessFactor = .72f,
                    ReflectanceFactor = .03f,
                    RenderEnvironmentMap = false,
                    RenderShadowMap = true
                },
                IsTransparent = true,
                IsThrowingShadow = false,
                IsHitTestVisible = false,
                CullMode = CullMode.None
            });
            studio.AddChildNode(new MeshNode
            {
                Name = "Tire_Contact_Shadow",
                Geometry = new MeshGeometry3D
                {
                    Positions = new()
                    {
                        new Vector3(-2.8f, -.005f, 1.3f), new Vector3(2.8f, -.005f, 1.3f),
                        new Vector3(2.8f, -.005f, -1.3f), new Vector3(-2.8f, -.005f, -1.3f)
                    },
                    Indices = new() { 0, 1, 2, 0, 2, 3 },
                    Normals = new() { Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY },
                    TextureCoordinates = new() { Vector2.Zero, Vector2.UnitX, Vector2.One, Vector2.UnitY }
                },
                Material = new DiffuseMaterialCore
                {
                    Name = "Contact_Shadow_Material",
                    DiffuseColor = Color4.White,
                    DiffuseMap = ContactShadowTexture()
                },
                IsTransparent = true,
                IsThrowingShadow = false,
                IsHitTestVisible = false,
                CullMode = CullMode.None
            });
            return studio;
        }

        private static bool ReflectsOnFloor(string name) =>
            name.StartsWith("primary", StringComparison.Ordinal) ||
            name.StartsWith("wheels", StringComparison.Ordinal) ||
            name.StartsWith("hub_", StringComparison.Ordinal) ||
            name.StartsWith("glass", StringComparison.Ordinal) ||
            name.StartsWith("front_black", StringComparison.Ordinal) ||
            name.Contains("light", StringComparison.OrdinalIgnoreCase) ||
            name == "Headlamp_Neutral_Lens" ||
            name == "Indicators";

        internal static void ApplyTheme(GroupNode studio, bool light)
        {
            var nodes = Traverse(studio).ToArray();
            foreach (var ambient in nodes.OfType<AmbientLightNode>())
                ambient.Color = light ? new Color4(.028f,.030f,.033f,1) : new Color4(.025f,.028f,.035f,1);
            var directions = nodes.OfType<DirectionalLightNode>().ToArray();
            var colors = light
                ? new[] { new Color4(.85f,.86f,.88f,1), new Color4(.32f,.33f,.34f,1), new Color4(.35f,.36f,.38f,1) }
                : new[] { new Color4(.86f,.85f,.81f,1), new Color4(.23f,.25f,.28f,1), new Color4(.31f,.34f,.39f,1) };
            for (int i=0; i<Math.Min(directions.Length,colors.Length); i++) directions[i].Color = colors[i];
            var floor = nodes.OfType<MeshNode>().Single(node => node.Name == "Studio_Floor");
            floor.Geometry = CreateFloorGeometry(light);
            // The dark studio's supplementary contact quad is visible as a
            // rectangular patch on white. Daylight uses shadow mapping + SSAO.
            nodes.OfType<MeshNode>().Single(node => node.Name == "Tire_Contact_Shadow").Visible = !light;
        }

        private static MeshGeometry3D CreateFloorGeometry(bool light = false)
        {
            const int segments = 64;
            var floorColor = light ? new Color4(.88f,.90f,.93f,.86f) : new Color4(.0018f,.0022f,.0032f,.86f);
            var geometry = new MeshGeometry3D
            {
                Positions = new() { new Vector3(0, -.012f, 0) },
                Normals = new() { Vector3.UnitY },
                Colors = new() { floorColor },
                TextureCoordinates = new() { Vector2.Zero },
                Indices = new()
            };
            var radii = new[] { 12f, 70f, 500f };
            var alpha = new[] { .86f, .18f, 0f };
            for (var ring = 0; ring < radii.Length; ++ring)
                for (var segment = 0; segment < segments; ++segment)
                {
                    var angle = segment * MathF.Tau / segments;
                    geometry.Positions.Add(new Vector3(MathF.Cos(angle) * radii[ring], -.012f, MathF.Sin(angle) * radii[ring]));
                    geometry.Normals.Add(Vector3.UnitY);
                    var color = floorColor;
                    color.Alpha = alpha[ring];
                    geometry.Colors.Add(color);
                    geometry.TextureCoordinates.Add(Vector2.Zero);
                }
            for (var segment = 0; segment < segments; ++segment)
            {
                var next = (segment + 1) % segments;
                geometry.Indices.Add(0); geometry.Indices.Add(1 + next); geometry.Indices.Add(1 + segment);
                for (var ring = 0; ring < radii.Length - 1; ++ring)
                {
                    var inner = 1 + ring * segments;
                    var outer = inner + segments;
                    geometry.Indices.Add(inner + segment); geometry.Indices.Add(inner + next); geometry.Indices.Add(outer + segment);
                    geometry.Indices.Add(inner + next); geometry.Indices.Add(outer + next); geometry.Indices.Add(outer + segment);
                }
            }
            return geometry;
        }

        private static TextureModel ContactShadowTexture()
        {
            const int size = 128;
            var pixels = new Color4[size * size];
            for (var y = 0; y < size; ++y)
                for (var x = 0; x < size; ++x)
                {
                    var u = (2f * (x + .5f) / size - 1) / .81f;
                    var v = (2f * (y + .5f) / size - 1) / .78f;
                    var alpha = .36f * MathF.Exp(-MathF.Pow(u, 4) - MathF.Pow(v, 4));
                    pixels[y * size + x] = new Color4(0, 0, 0, alpha);
                }
            return new TextureModel(pixels, size, size);
        }

        internal static readonly Matrix4x4 FloorMirror =
            Matrix4x4.CreateScale(1, -1, 1) * Matrix4x4.CreateTranslation(0, -.025f, 0);

        internal static IEnumerable<SceneNode> Traverse(SceneNode root)
        {
            yield return root;
            foreach (var child in root.Items)
                foreach (var node in Traverse(child)) yield return node;
        }

        private static void ApplyGltfMaterialValues(SceneNode root, string path)
        {
            using var file = File.OpenRead(path);
            using var reader = new BinaryReader(file);
            if (reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2)
                throw new InvalidDataException(Model3Text.Get("需要 GLB 2.0 模型"));
            _ = reader.ReadUInt32();
            var length = reader.ReadInt32();
            if (reader.ReadUInt32() != 0x4E4F534A || length <= 0 || length > file.Length - 20)
                throw new InvalidDataException(Model3Text.Get("GLB 元数据无效"));
            using var json = JsonDocument.Parse(reader.ReadBytes(length));
            var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var material in json.RootElement.GetProperty("materials").EnumerateArray())
                if (material.TryGetProperty("name", out var name)) values[name.GetString()] = material;
            foreach (var material in Traverse(root).OfType<MeshNode>().Select(mesh => mesh.Material).OfType<PBRMaterialCore>().Distinct())
            {
                if (!values.TryGetValue(material.Name, out var source)) continue;
                // Helix 3.1.2 reads metallicFactor into roughnessFactor. Restore
                // the GLTF values explicitly, including omitted GLTF defaults.
                if (source.TryGetProperty("pbrMetallicRoughness", out var pbr))
                {
                    material.MetallicFactor = Number(pbr, "metallicFactor", 1);
                    material.RoughnessFactor = Number(pbr, "roughnessFactor", 1);
                    if (pbr.TryGetProperty("baseColorFactor", out var color))
                        material.AlbedoColor = new Color4(color[0].GetSingle(), color[1].GetSingle(), color[2].GetSingle(), color[3].GetSingle());
                }
                if (source.TryGetProperty("extensions", out var extensions) && extensions.TryGetProperty("KHR_materials_clearcoat", out var coat))
                {
                    material.ClearCoatStrength = Number(coat, "clearcoatFactor", 0);
                    material.ClearCoatRoughness = Number(coat, "clearcoatRoughnessFactor", 0);
                }
                material.EmissiveColor = source.TryGetProperty("emissiveFactor", out var emissive)
                    ? new Color4(emissive[0].GetSingle(), emissive[1].GetSingle(), emissive[2].GetSingle(), 1)
                    : new Color4(0, 0, 0, 1);
                if (material.Name.StartsWith("primary", StringComparison.Ordinal))
                {
                    material.AlbedoColor = new Color4(.96f, .96f, .955f, 1);
                    material.MetallicFactor = .02f;
                    material.RoughnessFactor = .38f;
                    material.ReflectanceFactor = .30f;
                    material.ClearCoatStrength = .55f;
                    material.ClearCoatRoughness = .16f;
                }
                material.RenderEnvironmentMap = true;
                material.RenderShadowMap = true;
                material.EnableAutoTangent = true;
                material.VertexColorBlendingFactor = 0;
            }
        }

        private static float Number(JsonElement element, string name, float fallback) =>
            element.TryGetProperty(name, out var value) ? value.GetSingle() : fallback;

        private static void ExpandGrayscaleAlbedoTextures(SceneNode root)
        {
            var converted = new Dictionary<TextureModel, TextureModel>();
            foreach (var material in Traverse(root).OfType<MeshNode>().Select(mesh => mesh.Material).OfType<PBRMaterialCore>().Distinct())
            {
                var original = material.AlbedoMap;
                if (original == null) continue;
                if (!converted.TryGetValue(original, out var texture))
                {
                    texture = original;
                    var info = original.TextureInfoLoader.Load(original.Guid);
                    if (info.Texture is MemoryStream source)
                    {
                        var png = source.ToArray();
                        if (png.Length > 26 && png[0] == 137 && png[1] == 80 && png[2] == 78 && png[3] == 71 &&
                            (png[25] == 0 || png[25] == 4))
                        {
                            // WIC can expose grayscale PNGs as R8 textures.
                            // The PBR albedo shader expects RGB, so expand gray
                            // into all three channels before creating its SRV.
                            using var factory = new SharpDX.WIC.ImagingFactory();
                            using var data = new MemoryStream(png, false);
                            using var decoder = new SharpDX.WIC.BitmapDecoder(factory, data, SharpDX.WIC.DecodeOptions.CacheOnLoad);
                            using var frame = decoder.GetFrame(0);
                            using var converter = new SharpDX.WIC.FormatConverter(factory);
                            converter.Initialize(frame, SharpDX.WIC.PixelFormat.Format32bppRGBA);
                            var size = converter.Size;
                            var pixels = new byte[checked(size.Width * size.Height * 4)];
                            converter.CopyPixels(pixels, size.Width * 4);
                            texture = new TextureModel(pixels, SharpDX.DXGI.Format.R8G8B8A8_UNorm, size.Width, size.Height);
                        }
                    }
                    converted.Add(original, texture);
                }
                material.AlbedoMap = texture;
            }
        }

        // A locally generated floating-point DDS cube supplies broad softbox
        // reflections. Mips progressively soften the boxes for rough surfaces.
        internal static MemoryStream CreateEnvironmentCube()
        {
            const int size = 128, levels = 8;
            var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);
            writer.Write(0x20534444u); writer.Write(124u); writer.Write(0x0002100Fu);
            writer.Write(size); writer.Write(size); writer.Write(size * 8); writer.Write(0); writer.Write(levels);
            for (var i = 0; i < 11; ++i) writer.Write(0);
            writer.Write(32); writer.Write(4); writer.Write(0x30315844u);
            for (var i = 0; i < 5; ++i) writer.Write(0);
            writer.Write(0x00401008u); writer.Write(0x0000FE00u);
            writer.Write(0); writer.Write(0); writer.Write(0);
            writer.Write(10); writer.Write(3); writer.Write(4); writer.Write(1); writer.Write(0);
            for (var face = 0; face < 6; ++face)
                for (var mip = 0; mip < levels; ++mip)
                {
                    var width = Math.Max(1, size >> mip);
                    for (var y = 0; y < width; ++y)
                        for (var x = 0; x < width; ++x)
                        {
                            var u = 2f * (x + .5f) / width - 1;
                            var v = 2f * (y + .5f) / width - 1;
                            var direction = Vector3.Normalize(face switch
                            {
                                0 => new Vector3(1, -v, -u), 1 => new Vector3(-1, -v, u),
                                2 => new Vector3(u, 1, v), 3 => new Vector3(u, -1, -v),
                                4 => new Vector3(u, -v, 1), _ => new Vector3(-u, -v, -1)
                            });
                            var color = EnvironmentColor(direction, mip / 7f);
                            writer.Write(BitConverter.HalfToUInt16Bits((System.Half)color.X));
                            writer.Write(BitConverter.HalfToUInt16Bits((System.Half)color.Y));
                            writer.Write(BitConverter.HalfToUInt16Bits((System.Half)color.Z));
                            writer.Write(BitConverter.HalfToUInt16Bits((System.Half)1));
                        }
                }
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private static Vector3 EnvironmentColor(Vector3 direction, float blur)
        {
            var baseLight = .035f + .10f * Math.Max(direction.Y, 0);
            var color = new Vector3(baseLight * .82f, baseLight * .91f, baseLight);
            color += Box(direction, Vector3.Normalize(new Vector3(-.55f, .8f, .42f)), .75f, .20f, blur) * new Vector3(3.1f, 3.0f, 2.8f);
            color += Box(direction, Vector3.Normalize(new Vector3(.75f, .6f, -.65f)), .18f, .65f, blur) * new Vector3(1.5f, 1.9f, 2.6f);
            color += Box(direction, Vector3.Normalize(new Vector3(.12f, .95f, .05f)), 1.25f, .18f, blur) * new Vector3(1.9f, 2.0f, 2.1f);
            return color;
        }

        private static float Box(Vector3 direction, Vector3 normal, float width, float height, float blur)
        {
            var forward = Vector3.Dot(direction, normal);
            if (forward <= 0) return 0;
            var right = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitZ));
            var up = Vector3.Cross(right, normal);
            var x = Math.Abs(Vector3.Dot(direction, right) / forward) / (width + blur * .8f);
            var y = Math.Abs(Vector3.Dot(direction, up) / forward) / (height + blur * .8f);
            return MathF.Exp(-MathF.Pow(x, 8 - 6 * blur) - MathF.Pow(y, 8 - 6 * blur)) / (1 + blur * 2);
        }
    }
}
