using System.Collections.Generic;

namespace FbxImporter.Models;

public class MaterialLibrary
{
    public MaterialLibrary(string version)
    {
        Version = version;
    }

    public string Version { get; }
    public List<MaterialInfo> Materials { get; } = new();
}