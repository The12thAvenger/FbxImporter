using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace FbxImporter.Util;

// A 3x3 matrix stored in row-major order. Vectors are treated as columns (v' = M * v), so row i
// defines how output axis i is built from the input axes, and the diagonal holds per-axis scale.
//
// The import transform is built with a change of basis (conjugation):
//
//     tMat = permut * localTM * inverse(permut)
//
//   localTM  describes the edit you want (mirror, rotate, scale...) in the source (FBX) axes.
//   permut   maps the source axes into the target (FLVER) axes (axis swaps / sign flips / rotations).
//   tMat     is the same edit expressed in the target axes, and is what gets applied to the vertex data.
//
// Conjugation preserves determinant, trace and eigenvalues: a mirror stays a mirror, a rotation stays
// a rotation, a scale stays a scale. Only the axes they act on change.

public readonly record struct Matrix3x3(
    float M11, float M12, float M13,
    float M21, float M22, float M23,
    float M31, float M32, float M33)
{
    public const int ElementCount = 9;

    // Relative tolerance for treating a matrix as singular (see IsSingular).
    private const float SingularityTolerance = 1e-6f;

    public static Matrix3x3 Identity { get; } = new(1, 0, 0, 0, 1, 0, 0, 0, 1);

    #region Factories

    public static Matrix3x3 Diagonal(float x, float y, float z) => new(x, 0, 0, 0, y, 0, 0, 0, z);

    public static Matrix3x3 Scale(float s) => Diagonal(s, s, s);

    // Exact 90 degree step rotations (no floating point noise). Positive = counter clockwise looking
    // down the axis towards the origin (right handed rotation).

    public static Matrix3x3 RotationX(int quarterTurns)
    {
        (float c, float s) = CosSin(quarterTurns);
        return new Matrix3x3(1, 0, 0, 0, c, -s, 0, s, c);
    }

    public static Matrix3x3 RotationY(int quarterTurns)
    {
        (float c, float s) = CosSin(quarterTurns);
        return new Matrix3x3(c, 0, s, 0, 1, 0, -s, 0, c);
    }

    public static Matrix3x3 RotationZ(int quarterTurns)
    {
        (float c, float s) = CosSin(quarterTurns);
        return new Matrix3x3(c, -s, 0, s, c, 0, 0, 0, 1);
    }

    private static (float Cos, float Sin) CosSin(int quarterTurns)
    {
        int q = ((quarterTurns % 4) + 4) % 4;
        return q switch
        {
            0 => (1f, 0f),
            1 => (0f, 1f),
            2 => (-1f, 0f),
            _ => (0f, -1f)
        };
    }

    public static Matrix3x3 FromRowMajor(IReadOnlyList<float> e)
    {
        if (e.Count != ElementCount) throw new ArgumentException("A 3x3 matrix needs exactly 9 elements.", nameof(e));
        return new Matrix3x3(e[0], e[1], e[2], e[3], e[4], e[5], e[6], e[7], e[8]);
    }

    public float[] ToRowMajor() => new[] { M11, M12, M13, M21, M22, M23, M31, M32, M33 };

    #endregion

    #region Properties

    public float Determinant =>
        M11 * (M22 * M33 - M23 * M32) -
        M12 * (M21 * M33 - M23 * M31) +
        M13 * (M21 * M32 - M22 * M31);

    // True when the matrix flips handedness (mirror). Triangle winding has to be flipped to match.
    public bool IsReflection => Determinant < 0f;

    public bool IsFinite => Array.TrueForAll(ToRowMajor(), float.IsFinite);

    // Scale-invariant singularity test: |det| against the product of the row lengths (Hadamard bound).
    // A plain "det == 0" test would wrongly reject small uniform scales like 0.001.
    public bool IsSingular
    {
        get
        {
            if (!IsFinite) return true;
            float bound = MathF.Sqrt(M11 * M11 + M12 * M12 + M13 * M13) *
                          MathF.Sqrt(M21 * M21 + M22 * M22 + M23 * M23) *
                          MathF.Sqrt(M31 * M31 + M32 * M32 + M33 * M33);
            return bound == 0f || MathF.Abs(Determinant) <= SingularityTolerance * bound;
        }
    }

    #endregion

    #region Algebra

    public Matrix3x3 Transpose() => new(M11, M21, M31, M12, M22, M32, M13, M23, M33);

    public static Matrix3x3 operator *(Matrix3x3 a, Matrix3x3 b) => new(
        a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
        a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
        a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
        a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
        a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
        a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
        a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
        a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
        a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33);

    // Inverse via adjugate / determinant. Fails (returns false) for singular matrices.
    public bool TryInvert(out Matrix3x3 inverse)
    {
        if (IsSingular)
        {
            inverse = Identity;
            return false;
        }

        float inv = 1f / Determinant;
        inverse = new Matrix3x3(
            (M22 * M33 - M23 * M32) * inv, (M13 * M32 - M12 * M33) * inv, (M12 * M23 - M13 * M22) * inv,
            (M23 * M31 - M21 * M33) * inv, (M11 * M33 - M13 * M31) * inv, (M13 * M21 - M11 * M23) * inv,
            (M21 * M32 - M22 * M31) * inv, (M12 * M31 - M11 * M32) * inv, (M11 * M22 - M12 * M21) * inv);
        return true;
    }

    public Matrix3x3 Invert() =>
        TryInvert(out Matrix3x3 inverse)
            ? inverse
            : throw new InvalidOperationException("The matrix is singular and cannot be inverted.");

    // tMat = permut * localTM * inverse(permut)
    // Returns false when permut is not invertible (there is no valid change of basis then).
    public static bool TryConjugate(Matrix3x3 permut, Matrix3x3 localTM, out Matrix3x3 tMat)
    {
        if (!permut.TryInvert(out Matrix3x3 permutInverse))
        {
            tMat = Identity;
            return false;
        }

        tMat = permut * localTM * permutInverse;
        return true;
    }

    public static Matrix3x3 Conjugate(Matrix3x3 permut, Matrix3x3 localTM) =>
        TryConjugate(permut, localTM, out Matrix3x3 tMat)
            ? tMat
            : throw new InvalidOperationException("The permutation matrix is singular and cannot be inverted.");

    #endregion

    #region Applying to vertex data

    // Positions.
    public Vector3 Transform(Vector3 v) => new(
        M11 * v.X + M12 * v.Y + M13 * v.Z,
        M21 * v.X + M22 * v.Y + M23 * v.Z,
        M31 * v.X + M32 * v.Y + M33 * v.Z);

    // Directions that lie along the surface (tangents): transform like positions, then renormalize
    // so non uniform scale does not change their length.
    public Vector3 TransformDirection(Vector3 d)
    {
        Vector3 result = Transform(d);
        return result.LengthSquared() > 0 ? Vector3.Normalize(result) : result;
    }

    // Matrix to use for normals: the inverse transpose, so normals stay perpendicular under non uniform scale.
    // Falls back to the matrix itself when it is singular. Use TransformNormal on the result.
    public Matrix3x3 ToNormalMatrix() => TryInvert(out Matrix3x3 inverse) ? inverse.Transpose() : this;

    // Call on the matrix returned by ToNormalMatrix(). Normalizes the result.
    public Vector3 TransformNormal(Vector3 n) => TransformDirection(n);

    #endregion

    // Fixed width, multi line text for monospace display.
    public string ToDisplayString()
    {
        static string F(float v) =>
            (MathF.Abs(v) < 5e-7f ? 0f : v).ToString("0.####", CultureInfo.InvariantCulture).PadLeft(9);

        return $"{F(M11)}{F(M12)}{F(M13)}\n{F(M21)}{F(M22)}{F(M23)}\n{F(M31)}{F(M32)}{F(M33)}";
    }
}
