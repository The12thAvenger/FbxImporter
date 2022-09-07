using System.Xml.Linq;

namespace FbxImporter.ViewModels;

public record ClothReorderOptions(XElement SkinOperator, bool MirrorX);