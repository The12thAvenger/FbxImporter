using System.Collections.Generic;
using FbxImporter.Util;
using SoulsAssetPipeline.FLVERImporting;

namespace FbxImporter.ViewModels;

public record MeshImportOptions
{
    // Applied to the FBX vertex data as v' = Transform * v. Covers axis swaps (Y-up/Z-up),
    // mirrors, rotations and scale, or any combination of them as a single matrix.
    public Matrix3x3 Transform { get; init; } = Matrix3x3.Identity;

    public bool FlipFaces { get; init; } = false;

    public string MTD { get; init; } = null!;

    public FLVER2MaterialInfoBank MaterialInfoBank { get; init; } = null!;

    public WeightingMode Weighting { get; init; } = WeightingMode.Skin;
}

public class WeightingMode
{
    private WeightingMode(string name, string description)
    {
        Name = name;
        Description = description;
    }

    public static readonly WeightingMode Static = new("Static", "Used for non-animated meshes.");
    public static readonly WeightingMode Single = new("Single Weight", "Used for meshes which are only weighted to a single bone. Assumes that the mesh is initially located at the origin and applies the bone transform.");
    public static readonly WeightingMode Skin = new("Skin", "Used for meshes with vertices weighted to multiple bones (up to a maximum of 4). Assumes that the mesh is in bind pose.");
    public static readonly List<WeightingMode> Values = new()
    {
        Skin,
        Single,
        Static
    };

    public string Name { get; init; }
    public string Description { get; init; }
}
