using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FbxImporter.Util;
using ReactiveHistory;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsFormats;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace FbxImporter.ViewModels;

public class FlverViewModel : ViewModelBase
{
    public FlverViewModel(FLVER2 flver, IHistory history)
    {
        Flver = flver;
        History = history;
        Meshes = new ObservableCollection<FlverMeshViewModel>(flver.Meshes.Select(x => new FlverMeshViewModel(flver, x)));
        
        IObservable<bool> isMeshSelected = this.WhenAnyValue(x => x.SelectedMesh).Select(x => x is not null);
        DeleteMeshCommand = ReactiveCommand.Create(DeleteMeshWithHistory, isMeshSelected);
        ReorderVerticesCommand = ReactiveCommand.CreateFromTask(ReorderVerticesWithHistoryAsync, isMeshSelected);
        ReorderVerticesCommand.ThrownExceptions.Subscribe(Logger.Log);
    }

    public FLVER2 Flver { get; set; }

    private IHistory History { get; }

    public ObservableCollection<FlverMeshViewModel> Meshes { get; set; }

    [Reactive] public FlverMeshViewModel? SelectedMesh { get; set; }

    public ReactiveCommand<Unit, Unit> DeleteMeshCommand { get; }

    public ReactiveCommand<Unit, Unit> ReorderVerticesCommand { get; }

    public Interaction<Unit, ClothReorderOptions?> GetClothPose { get; } = new();

    private async Task ReorderVerticesWithHistoryAsync()
    {
        ClothReorderOptions? options = await GetClothPose.Handle(Unit.Default);
        if (options is null) return;

        FLVER2.Mesh mesh = SelectedMesh!.Mesh;
        List<List<int>> facesetIndices = mesh.FaceSets.Select(x => x.Indices.ToList()).ToList();
        List<FLVER.Vertex> vertices = mesh.Vertices.Select(x => new FLVER.Vertex(x)).ToList();

        History.Snapshot(Undo, Redo);
        Redo();
        Logger.Log("Successfully reordered vertices.");

        void Undo()
        {
            mesh.Vertices = vertices;
            for (int i = 0; i < mesh.FaceSets.Count; i++)
            {
                mesh.FaceSets[i].Indices = facesetIndices[i];
            }
        }

        void Redo()
        {
            ReorderVerticesFromClothPose(options);
        }
    }

    private void DeleteMeshWithHistory()
    {
        int index = Meshes.IndexOf(SelectedMesh!);
        Meshes.RemoveWithHistory(SelectedMesh!, History);
        if (Meshes.Any())
        {
            SelectedMesh = Meshes.Count > index ? Meshes[index] : Meshes[index - 1];
        }
    }

    public void Write(string path)
    {
        Flver.Meshes = new List<FLVER2.Mesh>(Meshes.Select(x => x.Mesh));
        Flver.Materials = new List<FLVER2.Material>();
        Flver.GXLists = new List<FLVER2.GXList>();
        for (int i = 0; i < Meshes.Count; i++)
        {
            Flver.Materials.Add(Meshes[i].Material);
            Meshes[i].Mesh.MaterialIndex = i;
            
            Flver.GXLists.Add(Meshes[i].GxList);
            Meshes[i].Material.GXIndex = i;
        }
        Flver.Write(path);
    }

    private void ReorderVerticesFromClothPose(ClothReorderOptions options)
    {
        ((_, XElement posePositions, List<int> triangleIndices), bool mirrorX) = options;
        
        List<Vector3> positions = posePositions.Value.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries)
            .Select(position => position.Substring(1, position.Length - 2)
                .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries)
                .Select(float.Parse)
                .ToArray())
            .Select(x => new Vector3(x[0], x[1], x[2]))
            .ToList();

        List<FLVER.Vertex> newVertexOrder = positions.Select(position => GetVertex(position, SelectedMesh!.Mesh, mirrorX))
            .Select(matchingVertex => new FLVER.Vertex(matchingVertex))
            .ToList();

        SelectedMesh!.Mesh.Vertices = newVertexOrder;

        foreach (FLVER2.FaceSet faceSet in SelectedMesh.Mesh.FaceSets)
        {
            faceSet.Indices = triangleIndices;
            faceSet.Flip();
        }
    }

    private static FLVER.Vertex GetVertex(Vector3 position, FLVER2.Mesh mesh, bool mirrorX)
    {
            List<FLVER.Vertex> matchingVertices = GetMatchingVertices(position, mesh, mirrorX);
            
            if (matchingVertices.Any())
            {
                return matchingVertices[0];
            }
            throw new InvalidDataException($"No matching vertex was found for vertex {position}");
    }

    private static List<FLVER.Vertex> GetMatchingVertices(Vector3 position, FLVER2.Mesh mesh, bool mirrorX, double accuracy = 0.00001)
    {
        int xSign = mirrorX ? -1 : 1;
        return mesh.Vertices.Where(x =>
                Math.Abs(x.Position.X - xSign * position.X) < accuracy &&
                Math.Abs(x.Position.Y - position.Y) < accuracy &&
                Math.Abs(x.Position.Z - position.Z) < accuracy)
            .ToList();
    }
}