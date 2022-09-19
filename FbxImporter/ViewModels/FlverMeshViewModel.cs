using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
using FbxImporter.Util;
using Newtonsoft.Json;
using SoulsFormats;

namespace FbxImporter.ViewModels;

public class FlverMeshViewModel : ViewModelBase
{
    public FlverMeshViewModel(FLVER2 flver, FLVER2.Mesh mesh)
    {
        Mesh = mesh;
        Material = flver.Materials[mesh.MaterialIndex];
        GxList = flver.GXLists[Material.GXIndex];
        Name = Material.Name;
    }

    public FlverMeshViewModel(FLVER2.Mesh mesh, FLVER2.Material material, FLVER2.GXList gxList)
    {
        Mesh = mesh;
        Material = material;
        GxList = gxList;
        Name = Material.Name;
    }

    public FLVER2.Mesh Mesh { get; }

    public FLVER2.Material Material { get; }

    public FLVER2.GXList GxList { get; }

    public string Name { get; set; }

    public void ReorderVerticesFromClothPose(ClothReorderOptions options)
    {
        (XElement skinOperator, bool mirrorX) = options;

        XElement objectSpaceDeformer = skinOperator.Elements()
            .First(x => x.Attribute("name")!.Value == "objectSpaceDeformer").Element("hkobject")!;

        Queue<XElement>[] blendEntries = new Queue<XElement>[4];

        blendEntries[0] = new Queue<XElement>(objectSpaceDeformer.Elements()
            .First(x => x.Attribute("name")!.Value == "fourBlendEntries").Elements());
        blendEntries[1] = new Queue<XElement>(objectSpaceDeformer.Elements()
            .First(x => x.Attribute("name")!.Value == "threeBlendEntries").Elements());
        blendEntries[2] = new Queue<XElement>(objectSpaceDeformer.Elements()
            .First(x => x.Attribute("name")!.Value == "twoBlendEntries").Elements());
        blendEntries[3] = new Queue<XElement>(objectSpaceDeformer.Elements()
            .First(x => x.Attribute("name")!.Value == "oneBlendEntries").Elements());

        int numEntries = blendEntries.Aggregate(0, (numEntries, queue) => numEntries + queue.Count);

        int[] controlBytes = objectSpaceDeformer.Elements()
            .First(x => x.Attribute("name")!.Value == "controlBytes").Value
            .Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();

        if (numEntries != controlBytes.Length)
        {
            throw new InvalidDataException("The selected skin operator is missing blend entries. Make sure no vertices are skinned to more than 4 bones.");
        }

        List<XElement> localPositionBlocks = skinOperator.Elements().First(x => x.Attribute("name")!.Value.StartsWith("localP")).Elements().ToList();

        int endVertexIndex = int.Parse(objectSpaceDeformer.Elements()
            .First(x => x.Attribute("name")!.Value == "endVertexIndex").Value);
        ushort[][] positionsPerVertex = new ushort[endVertexIndex + 1][];

        for (int i = 0; i < controlBytes.Length; i++)
        {
            int controlByte = controlBytes[i];
            XElement blendEntry = blendEntries[controlByte].Dequeue();
            XElement positionBlock = localPositionBlocks[i];
            ushort[][] positions = positionBlock.Elements().Select((element, index) => (element, index))
                .GroupBy(item => item.index / 4)
                .Select(g => g.Select(x => x.element.Value.Contains('-') ? unchecked((ushort)short.Parse(x.element.Value)) : ushort.Parse(x.element.Value)).ToArray())
                .ToArray();

            int[] vertexIndices = blendEntry.Elements().Take(16).Select(x => int.Parse(x.Value)).ToArray();
            for (int j = 0; j < vertexIndices.Length; j++)
            {
                int vertexIndex = vertexIndices[j];
                positionsPerVertex[vertexIndex] = positions[j];
            }
        }

        int xSign = mirrorX ? -1 : 1;
        FLVER.Vertex[] currentVertexOrder = Mesh.Vertices.ToArray();
        ushort[][] currentVertexOrderPositionsPacked =
            Mesh.Vertices.Select(x => x.Position with { X = x.Position.X * xSign }).Select(x => HavokUtils.PackHkPackedVector3(x.ToFloatArray())).ToArray();
        FLVER.Vertex?[] newVertexOrder = new FLVER.Vertex?[currentVertexOrder.Length];
        int?[] indexMappingArray = new int?[currentVertexOrder.Length];
        for (int i = 0; i < positionsPerVertex.Length; i++)
        {
            ushort[] position = positionsPerVertex[i];
            if (position.Length == 0)
            {
                continue;
            }
            
            int matchingVertexIndex = Array.FindIndex(currentVertexOrderPositionsPacked, x => x.SequenceEqual(position));

            if (matchingVertexIndex == -1)
            {
                Vector3 positionVector = HavokUtils.UnpackHkPackedVector3(position).ToVector3();
                IOrderedEnumerable<(Vector3 position, int index, float distance)> distances = currentVertexOrder.Select((vertex, index) => (position: vertex.Position, index, distance: Vector3.Distance(vertex.Position, positionVector)))
                    .OrderBy(x => x.distance);
                matchingVertexIndex = distances.First().index;
            }

            if (matchingVertexIndex == -1)
            {
                JsonSerializer serializer = JsonSerializer.Create();
                using StreamWriter writer = new("vertices.json");
                serializer.Serialize(writer, currentVertexOrderPositionsPacked);
                throw new InvalidDataException($"Error: No matching vertex found for position {HavokUtils.UnpackHkPackedVector3(position).ToVector3()}"
                + "\nPosition in cloth data:"
                + $"\n{position[0]} {position[1]} {position[2]} {position[3]}"
                + "Flver positions have been written to vertices.json");
            }

            if (indexMappingArray[matchingVertexIndex] is not null)
            {
                
            }

            newVertexOrder[i] = currentVertexOrder[matchingVertexIndex];
            indexMappingArray[matchingVertexIndex] = i;
        }

        Queue<int> unmappedIndices = new(newVertexOrder.Select((vertex, index) => (vertex, index))
            .Where(x => x.vertex is null).Select(x => x.index)) ;
        for (int i = 0; i < indexMappingArray.Length; i++)
        {
            if (indexMappingArray[i] is not null) continue;

            int newVertexIndex = unmappedIndices.Dequeue();
            indexMappingArray[i] = newVertexIndex;
            newVertexOrder[newVertexIndex] = currentVertexOrder[i];
        }

        if (newVertexOrder.Distinct().Count() != newVertexOrder.Length)
        {
            throw new NotImplementedException();
        }
        
        Mesh.Vertices = newVertexOrder.ToList();

        foreach (FLVER2.FaceSet faceSet in Mesh.FaceSets)
        {
            faceSet.Indices = faceSet.Indices.Select(index => (int)indexMappingArray[index]!).ToList();
            faceSet.Flip();
        }
    }
}