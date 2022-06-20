using System.Xml.Linq;

namespace FbxImporter.ViewModels;

public record ClothReorderOptions(ClothData ClothData, bool MirrorX);