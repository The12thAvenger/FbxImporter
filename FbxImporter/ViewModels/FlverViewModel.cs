using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FbxImporter.Util;
using ReactiveHistory;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsAssetPipeline.FLVERImporting;
using SoulsFormats;
using ReactiveCommand = ReactiveUI.ReactiveCommand;

namespace FbxImporter.ViewModels;

public class FlverViewModel : ViewModelBase
{
    private readonly IHistory _history;

    public enum FlverVersion
    {
        DS3,
        SDT,
        ER
    }

    public FlverViewModel(FLVER2 flver, FlverVersion version,  IHistory history)
    {
        Flver = flver;
        _history = history;

        string basePath = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "SapResources", "FLVER2MaterialInfoBank");
        string xmlPath = Path.Join(basePath, $"Bank{version}.xml");
        MaterialInfoBank = FLVER2MaterialInfoBank.ReadFromXML(xmlPath);
        
        Meshes = new ObservableCollection<FlverMeshViewModel>(flver.Meshes.Select(x => new FlverMeshViewModel(flver, x)));
        
        IObservable<bool> isMeshSelected = this.WhenAnyValue(x => x.SelectedMesh).Select(x => x is not null);
        DeleteMeshCommand = ReactiveCommand.Create(DeleteMeshWithHistory, isMeshSelected);
        ReorderVerticesCommand = ReactiveCommand.CreateFromTask(ReorderVerticesWithHistoryAsync, isMeshSelected);
        ReorderVerticesCommand.ThrownExceptions.Subscribe(e =>
        {
            if (e is InvalidDataException)
            {
                Logger.Log(e.Message);
            }
            else
            {
                Logger.Log(e);
            }
        });
    }

    public FLVER2 Flver { get; }

    public FLVER2MaterialInfoBank MaterialInfoBank { get; set; }

    public ObservableCollection<FlverMeshViewModel> Meshes { get; set; }

    [Reactive] public FlverMeshViewModel? SelectedMesh { get; set; }

    public ReactiveCommand<Unit, Unit> DeleteMeshCommand { get; }

    public ReactiveCommand<Unit, Unit> ReorderVerticesCommand { get; }

    public Interaction<Unit, ClothReorderOptions?> GetClothPose { get; } = new();

    public Interaction<(string, string), Unit> ShowMessage { get; } = new();

    private async Task ReorderVerticesWithHistoryAsync()
    {
        ClothReorderOptions? options = await GetClothPose.Handle(Unit.Default);
        if (options is null) return;

        FLVER2.Mesh mesh = SelectedMesh!.Mesh;
        List<List<int>> facesetIndices = mesh.FaceSets.Select(x => x.Indices.ToList()).ToList();
        List<FLVER.Vertex> vertices = mesh.Vertices.Select(x => new FLVER.Vertex(x)).ToList();

        try
        {
            Redo();
            _history.Snapshot(Undo, Redo);
            Logger.Log("Successfully reordered vertices.");
        }
        catch (InvalidDataException e)
        {
            await ShowMessage.Handle(("Error Reordering Vertices", e.Message));
            Undo();
        }

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
            SelectedMesh.ReorderVerticesFromClothPose(options);
        }
    }

    private void DeleteMeshWithHistory()
    {
        int index = Meshes.IndexOf(SelectedMesh!);
        Meshes.RemoveWithHistory(SelectedMesh!, _history);
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

            if (Flver.Header.Version == 131098)
            {
                Meshes[i].Material.Unk18 = i;
            }
            Flver.GXLists.Add(Meshes[i].GxList);
            Meshes[i].Material.GXIndex = i;
        }

        // if (Flver.Header.Version == 131098)
        // {
            Flver.FixAllBoundingBoxes();
        // }


        // Soulsformats will corrupt the file if there is an exception on write so back up the file first and write it back to disk if the write fails.
        FLVER2? backupFlver;
        try
        {
            backupFlver = FLVER2.Read(path);
        }
        catch
        {
            backupFlver = null;
        }

        try
        {
            Flver.Write(path);
        }
        catch (Exception)
        {
            backupFlver?.Write(path);
            throw;
        }
    }
}