using SoulsAssetPipeline.FLVERImporting;

namespace FbxImporter.ViewModels;

public record MeshImportOptions
{
    public bool CreateDefaultBone { get; init; }

    public bool MirrorX { get; init; } = true;

    public bool IsCloth { get; init; } = true;

    public string MTD { get; init; } = null!;

    public FLVER2MaterialInfoBank MaterialInfoBank { get; init; } = null!;
}