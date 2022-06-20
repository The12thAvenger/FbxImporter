namespace FbxImporter.Models;

public record MeshImportOptions
{
    public bool CreateDefaultBone { get; init; }

    public bool MirrorX { get; init; } = true;

    public bool IsCloth { get; init; } = true;

    public MaterialInfo MaterialInfo { get; init; }
}