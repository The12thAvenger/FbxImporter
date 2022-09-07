using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsAssetPipeline.FLVERImporting;

namespace FbxImporter.ViewModels;

public class MeshImportOptionsViewModel : ViewModelBase
{
    private readonly FLVER2MaterialInfoBank _materialInfoBank;

    public MeshImportOptionsViewModel(string meshName, FLVER2MaterialInfoBank materialInfoBank, MeshImportOptions? optionsCache)
    {
        CreateDefaultBone = optionsCache?.CreateDefaultBone ?? true;
        MirrorX = optionsCache?.MirrorX ?? false;

        string? lastUsedMaterial =
            optionsCache?.MTD is not null && materialInfoBank.MaterialDefs.ContainsKey(optionsCache.MTD)
                ? optionsCache?.MTD
                : null;

        _materialInfoBank = materialInfoBank;
        bool isErBank = materialInfoBank.MaterialDefs.Keys.Any(x => x.ToLower().StartsWith("aeg"));
        Materials = new ObservableCollection<string>(materialInfoBank.MaterialDefs.Keys.Where(x => IsDisplayedMaterial(x, isErBank)).OrderBy(x => x));

        string[] meshNameParts = meshName.Split('|', StringSplitOptions.TrimEntries);
        SelectedMaterial = meshNameParts.Length > 1
            ? Materials.FirstOrDefault(x => string.Equals(x.Replace(".mtd", ""),
                  meshNameParts[1].Replace(".mtd", ""),
                  StringComparison.CurrentCultureIgnoreCase)) ??
              lastUsedMaterial ?? Materials[0]
            : lastUsedMaterial ?? Materials[0];

        CancelCommand = ReactiveCommand.Create(Cancel);

        ConfirmCommand = ReactiveCommand.Create(Confirm);
    }

    private static bool IsDisplayedMaterial(string materialName, bool isErBank)
    {
        if (string.IsNullOrEmpty(materialName))
        {
            return false;
        }

        if (isErBank && !materialName.ToLower().StartsWith("p[") && !materialName.ToLower().StartsWith("c["))
        {
            return false;
        }

        return true;
    }

    [Reactive] public bool CreateDefaultBone { get; set; }

    [Reactive] public bool MirrorX { get; set; }

    public ObservableCollection<string> Materials { get; }

    [Reactive] public string SelectedMaterial { get; set; }

    public ReactiveCommand<Unit, MeshImportOptions> ConfirmCommand { get; }

    public ReactiveCommand<Unit, MeshImportOptions?> CancelCommand { get; }

    public MeshImportOptions? Cancel()
    {
        return null;
    }

    public MeshImportOptions Confirm()
    {
        return new MeshImportOptions
        {
            CreateDefaultBone = CreateDefaultBone,
            MirrorX = MirrorX,
            MTD = SelectedMaterial,
            MaterialInfoBank = _materialInfoBank
        };
    }
}