using System.Collections.Generic;

namespace FbxImporter.Util;

public sealed record Matrix3x3Preset(string Name, Matrix3x3 Matrix)
{
    // Shown directly by ComboBox.
    public sealed override string ToString() => Name;
}

public static class Matrix3x3Presets
{
    // Presets for the import transform T, applied as v' = T * v.
    // Axis swap names show what a source vector (x, y, z) becomes.
    public static IReadOnlyList<Matrix3x3Preset> All { get; } = new List<Matrix3x3Preset>
    {
        new("Default", Matrix3x3.Identity),

        new("Mirror X", Matrix3x3.Diagonal(-1, 1, 1)),
        new("Mirror Y", Matrix3x3.Diagonal(1, -1, 1)),
        new("Mirror Z", Matrix3x3.Diagonal(1, 1, -1)),

        new("Z-up to Y-up  (x, z, -y)", new Matrix3x3(1, 0, 0, 0, 0, 1, 0, -1, 0)),
        new("Y-up to Z-up  (x, -z, y)", new Matrix3x3(1, 0, 0, 0, 0, -1, 0, 1, 0)),
        new("Swap X/Y  (y, x, z)", new Matrix3x3(0, 1, 0, 1, 0, 0, 0, 0, 1)),
        new("Swap X/Z  (z, y, x)", new Matrix3x3(0, 0, 1, 0, 1, 0, 1, 0, 0)),
        new("Swap Y/Z  (x, z, y)", new Matrix3x3(1, 0, 0, 0, 0, 1, 0, 1, 0)),

        new("Rotate +90° X", Matrix3x3.RotationX(1)),
        new("Rotate -90° X", Matrix3x3.RotationX(-1)),
        new("Rotate +90° Y", Matrix3x3.RotationY(1)),
        new("Rotate -90° Y", Matrix3x3.RotationY(-1)),
        new("Rotate +90° Z", Matrix3x3.RotationZ(1)),
        new("Rotate -90° Z", Matrix3x3.RotationZ(-1)),
        new("Rotate 180° Y", Matrix3x3.RotationY(2)),

        new("Scale ×0.01 (cm to m)", Matrix3x3.Scale(0.01f)),
        new("Scale ×100 (m to cm)", Matrix3x3.Scale(100f)),
    };
}
