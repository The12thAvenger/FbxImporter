using System.Linq;
using SoulsFormats;

namespace FbxImporter.Util;

public static class FlverUtils
{
    public static FLVER2.BufferLayout Clone(this FLVER2.BufferLayout layout)
    {
        FLVER2.BufferLayout newLayout = new();
        newLayout.AddRange(layout.Select(x => new FLVER.LayoutMember(x.Type, x.Semantic, x.Index, x.Unk00)));
        return newLayout;
    }

    public static FLVER2.GXList Clone(this FLVER2.GXList gxList)
    {
        FLVER2.GXList newGXList = new();
        newGXList.AddRange(gxList.Select(x => new FLVER2.GXItem
        {
            Data = (byte[]) x.Data.Clone(),
            ID = x.ID,
            Unk04 = x.Unk04
        }));
        return newGXList;
    }

    public static FLVER2.Texture Clone(this FLVER2.Texture texture)
    {
        return new FLVER2.Texture
        {
            Type = texture.Type,
            Path = texture.Path,
            Scale = texture.Scale,
            Unk10 = texture.Unk10,
            Unk11 = texture.Unk11,
            Unk14 = texture.Unk14,
            Unk18 = texture.Unk18,
            Unk1C = texture.Unk1C
        };
    }

    public static FLVER2.Material Clone(this FLVER2.Material material)
    {
        return new FLVER2.Material
        {
            Flags = material.Flags,
            GXIndex = material.GXIndex,
            MTD = material.MTD,
            Name = material.Name,
            Textures = material.Textures.Select(Clone).ToList(),
            Unk18 = material.Unk18
        };
    }

    public static void FlipFaceSets(this FLVER2.Mesh mesh)
    {
        foreach (FLVER2.FaceSet faceSet in mesh.FaceSets)
        {
            faceSet.Flip();
        }
    }
    
    public static void Flip(this FLVER2.FaceSet faceSet)
    {
        for (int i = 0; i < faceSet.Indices.Count; i += 3)
        {
            (faceSet.Indices[i + 1], faceSet.Indices[i + 2]) = (faceSet.Indices[i + 2], faceSet.Indices[i + 1]);
        }
    }
}