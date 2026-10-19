using System.Collections.Generic;
using System.Numerics;
using SoulsAssetPipeline.FLVERImporting;

namespace FbxImporter.ViewModels;

public record MeshImportOptions
{
    public TransformPreset TransformPreset { get; init; } = TransformPreset.None;
    public Matrix4x4 Transform { get; init; } = Matrix4x4.Identity;
    
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

public class TransformPreset
{
    private TransformPreset(string name, string description, Matrix4x4 transform)
    {
        Name = name;
        Description = description;
        Transform = transform;
    }

    public static readonly TransformPreset None = new("None", "No transformation", Matrix4x4.Identity);
    public static readonly TransformPreset MirrorX = new("Mirror X", "Mirrors the X axis", Matrix4x4.Identity with {M11 = -1});
    public static readonly TransformPreset MirrorY = new("Mirror Y", "Mirrors the Y axis", Matrix4x4.Identity with {M22 = -1});
    public static readonly TransformPreset MirrorZ = new("Mirror Z", "Mirrors the Z axis", Matrix4x4.Identity with {M33 = -1});
    public static readonly TransformPreset Blender = new("Blender/3ds Max", "Maps from the Blender/3ds Max coordinate system to the game's coordinate system (swap Z and Y)", Matrix4x4.Identity with
    {
        M11 = 1, M12 = 0, M13 = 0,
        M21 = 0, M22 = 0, M23 = 1,
        M31 = 0, M32 = 1, M33 = 0
    });
    public static readonly TransformPreset Houdini = new("Houdini", "Maps from the Houdini coordinate system to the game's coordinate system (mirror Z)", Matrix4x4.Identity with {M33 = -1});
    public static readonly TransformPreset Maya = new("Maya", "Maps from the Maya coordinate system to the game's coordinate system (mirror Z, cm to meters)", Matrix4x4.Identity with {M33 = -1} * 0.01f);
    public static readonly TransformPreset Custom = new("Custom", "A custom transformation matrix", Matrix4x4.Identity);

    public static readonly List<TransformPreset> Presets = 
    [
        None,
        MirrorX,
        MirrorY,
        MirrorZ,
        Blender,
        Houdini,
        Maya,
        Custom
    ];
        
    public string Name { get; init; }
    public string Description { get; init; }
    public Matrix4x4 Transform { get; init; }
}