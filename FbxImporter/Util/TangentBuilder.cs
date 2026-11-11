using System;
using System.Collections.Generic;
using System.Numerics;

namespace FbxImporter.Util;

public static class TangentBuilder
{
    /// <summary>
    /// Builds a tangent per vertex in the convention of Elden Ring's own models: the tangent points along +V of the
    /// stored UVs, and W is +1 or -1 so that W * (normal x tangent) points along +U. Each vertex adds up the
    /// +V and +U directions of the triangles around it, weighted by triangle area, and the +V sum is made
    /// perpendicular to the normal. The result does not depend on the winding order of the triangles.
    /// </summary>
    /// <param name="positions">Vertex positions, after the import transform.</param>
    /// <param name="normals">Vertex normals, after the import transform.</param>
    /// <param name="uvs">The UV set the tangents are for, as stored in the FLVER (V already flipped).</param>
    /// <param name="triangleIndices">Vertex indices, three per triangle.</param>
    /// <param name="fallback">Tangents to keep, with W made +1 or -1, for vertices without usable UV area.</param>
    /// <returns>One tangent per vertex.</returns>
    public static List<Vector4> Build(IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> normals,
        IReadOnlyList<Vector2> uvs, IReadOnlyList<int> triangleIndices, IReadOnlyList<Vector4> fallback)
    {
        int count = positions.Count;
        var vSum = new Vector3[count];
        var uSum = new Vector3[count];
        for (int k = 0; k + 2 < triangleIndices.Count; k += 3)
        {
            int a = triangleIndices[k], b = triangleIndices[k + 1], c = triangleIndices[k + 2];
            if (a == b || b == c || a == c) continue;
            Vector3 e1 = positions[b] - positions[a], e2 = positions[c] - positions[a];
            Vector2 d1 = uvs[b] - uvs[a], d2 = uvs[c] - uvs[a];
            float det = d1.X * d2.Y - d2.X * d1.Y;
            float area = Vector3.Cross(e1, e2).Length();
            if (MathF.Abs(det) < 1e-12f || area < 1e-20f) continue;
            Vector3 uDir = (e1 * d2.Y - e2 * d1.Y) / det;
            Vector3 vDir = (e2 * d1.X - e1 * d2.X) / det;
            if (uDir.LengthSquared() < 1e-24f || vDir.LengthSquared() < 1e-24f) continue;
            uDir = Vector3.Normalize(uDir) * area;
            vDir = Vector3.Normalize(vDir) * area;
            foreach (int i in new[] { a, b, c })
            {
                uSum[i] += uDir;
                vSum[i] += vDir;
            }
        }

        var result = new List<Vector4>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 n = normals[i].LengthSquared() > 0 ? Vector3.Normalize(normals[i]) : Vector3.Zero;
            Vector3 t = vSum[i] - n * Vector3.Dot(n, vSum[i]);
            if (n == Vector3.Zero || vSum[i] == Vector3.Zero || t.LengthSquared() <= 1e-8f * vSum[i].LengthSquared())
            {
                Vector4 f = fallback[i];
                result.Add(f with { W = f.W < 0 ? -1 : 1 });
                continue;
            }

            t = Vector3.Normalize(t);
            float w = Vector3.Dot(Vector3.Cross(n, t), uSum[i]) >= 0 ? 1 : -1;
            result.Add(new Vector4(t, w));
        }

        return result;
    }
}
