using System.Numerics;

namespace FbxImporter.Util;

public static class MatrixUtils
{
    /// <summary>
    /// Calculates the cofactor matrix of the 3x3 left-upper submatrix of a 4x4 matrix
    /// (the scale-rotation part of a transformation matrix).
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static Matrix4x4 Cofactor3x3(Matrix4x4 m)
    {
        return new Matrix4x4
        {
            M11 = m.M22 * m.M33 - m.M32 * m.M23, M12 = -(m.M21 * m.M33 - m.M31 * m.M23), M13 = m.M21 * m.M32 - m.M31 * m.M22, M14 = 0,
            M21 = -(m.M12 * m.M33 - m.M32 * m.M13), M22 = m.M11 * m.M33 - m.M31 * m.M13, M23 = -(m.M11 * m.M32 - m.M31 * m.M12), M24 = 0,
            M31 = m.M12 * m.M23 - m.M22 * m.M13, M32 = -(m.M11 * m.M23 - m.M21 * m.M13), M33 = m.M11 * m.M22 - m.M21 * m.M12, M34 = 0,
            M41 = 0, M42 = 0, M43 = 0, M44 = 1
        };
    }
}