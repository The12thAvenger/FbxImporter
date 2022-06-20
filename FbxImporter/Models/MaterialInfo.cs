using System.Collections.Generic;
using System.IO;
using SoulsFormats;

namespace FbxImporter.Models;

public class MaterialInfo
{
    public MaterialInfo()
    {
        Name = "";
        MtdPath = "";
        Material = new FLVER2.Material();
        BufferLayouts = new List<FLVER2.BufferLayout>();
        GXList = new FLVER2.GXList();
    }

    public MaterialInfo(FLVER2.Material material, List<FLVER2.BufferLayout> bufferLayouts, FLVER2.GXList gxList)
    {
        Material = material;
        MtdPath = Material.MTD;
        Name = Path.GetFileNameWithoutExtension(Material.MTD);
        BufferLayouts = bufferLayouts;
        GXList = gxList;
    }

    public string Name { get; init; }

    public string MtdPath { get; init; }

    public FLVER2.Material Material { get; init; }

    public List<FLVER2.BufferLayout> BufferLayouts { get; init; }

    public FLVER2.GXList GXList { get; init; }
}