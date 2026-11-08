using SharpGLTF.Schema2;
using Showcase.Domain;

namespace Showcase.Worker;

/// <summary>Reads triangle/vertex/material/texture counts and which PBR maps a .glb carries.</summary>
public static class GltfStats
{
    private static readonly (string Channel, string Name)[] Maps =
    [
        ("BaseColor", "baseColor"), ("MetallicRoughness", "metallicRoughness"), ("Normal", "normal"),
        ("Occlusion", "occlusion"), ("Emissive", "emissive"),
    ];

    /// <summary>Throws <see cref="InvalidDataException"/> with a readable reason when the file is not usable glTF.</summary>
    public static ModelStats Read(string path, Guid modelId)
    {
        ModelRoot root;
        try
        {
            root = ModelRoot.Load(path, new ReadSettings { Validation = SharpGLTF.Validation.ValidationMode.Skip });
        }
        catch (Exception e)
        {
            throw new InvalidDataException($"The file is not a readable glTF/GLB ({e.Message}).");
        }

        long triangles = 0, vertices = 0;
        // Count per node so instanced meshes count every time they appear in the scene.
        foreach (var node in root.LogicalNodes.Where(n => n.Mesh is not null))
        foreach (var prim in node.Mesh.Primitives)
        {
            vertices += prim.GetVertexAccessor("POSITION")?.Count ?? 0;
            triangles += prim.DrawPrimitiveType switch
            {
                PrimitiveType.TRIANGLES => (prim.IndexAccessor?.Count ?? prim.GetVertexAccessor("POSITION")?.Count ?? 0) / 3,
                PrimitiveType.TRIANGLE_STRIP or PrimitiveType.TRIANGLE_FAN => Math.Max(0, (prim.IndexAccessor?.Count ?? prim.GetVertexAccessor("POSITION")?.Count ?? 0) - 2),
                _ => 0,
            };
        }
        if (vertices == 0) throw new InvalidDataException("The file has no mesh geometry.");

        var maxTexture = 0;
        foreach (var image in root.LogicalImages)
        {
            try
            {
                using var stream = image.Content.Open();
                var info = SixLabors.ImageSharp.Image.Identify(stream);
                maxTexture = Math.Max(maxTexture, Math.Max(info.Width, info.Height));
            }
            catch
            {
                // KTX2 or an unknown format: size unknown, not an error
            }
        }

        var maps = Maps
            .Where(m => root.LogicalMaterials.Any(mat => mat.FindChannel(m.Channel)?.Texture is not null))
            .Select(m => m.Name)
            .ToList();

        return new ModelStats
        {
            ModelId = modelId,
            Triangles = (int)Math.Min(triangles, int.MaxValue),
            Vertices = (int)Math.Min(vertices, int.MaxValue),
            Meshes = root.LogicalMeshes.Count,
            Materials = root.LogicalMaterials.Count,
            Textures = root.LogicalTextures.Count,
            MaxTextureSize = maxTexture,
            HasAnimation = root.LogicalAnimations.Count > 0,
            AvailableMaps = System.Text.Json.JsonSerializer.Serialize(maps),
        };
    }
}
