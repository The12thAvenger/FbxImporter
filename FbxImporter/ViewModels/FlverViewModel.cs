using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
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
    }

    public FLVER2 Flver { get; }

    public FLVER2MaterialInfoBank MaterialInfoBank { get; set; }

    public ObservableCollection<FlverMeshViewModel> Meshes { get; set; }

    [Reactive] public FlverMeshViewModel? SelectedMesh { get; set; }

    public ReactiveCommand<Unit, Unit> DeleteMeshCommand { get; }

    public Interaction<Unit, ClothReorderOptions?> GetClothPose { get; } = new();

    public Interaction<(string, string), Unit> ShowMessage { get; } = new();

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