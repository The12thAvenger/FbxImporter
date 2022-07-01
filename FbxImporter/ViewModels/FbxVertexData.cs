using System.Collections.Generic;

namespace FbxImporter.ViewModels;

// ReSharper disable once ClassNeverInstantiated.Global
// Is instantiated through Json deserialization.
public record FbxVertexData(float[] Position, float[] Normal, List<float[]> Tangents, List<float[]> UVs, string[] BoneNames, float[] BoneWeights);