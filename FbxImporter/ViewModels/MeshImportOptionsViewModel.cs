using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
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
        MirrorZ = optionsCache?.MirrorZ ?? false;
        IsStatic = optionsCache?.IsStatic ?? false;

        string? lastUsedMaterial =
            optionsCache?.MTD is not null && materialInfoBank.MaterialDefs.ContainsKey(optionsCache.MTD)
                ? optionsCache.MTD
                : null;

        _materialInfoBank = materialInfoBank;
        List<string> materialNameList = materialInfoBank.MaterialDefs.Keys.Where(x => !string.IsNullOrEmpty(x))
            .OrderBy(x => x).ToList();
        Materials = new SourceCache<string, string>(x => x);
        Materials.AddOrUpdate(materialNameList);
        
        IObservable<Func<string, bool>> materialFilter = this.WhenAnyValue(x => x.Filter)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(x => (Func<string, bool>)(y => y.StartsWith(x)));

        FilteredMaterials = new ObservableCollectionExtended<string>();
        Materials.Connect()
            .Filter(materialFilter)
            .Sort(StringComparer.OrdinalIgnoreCase)
            .Bind(FilteredMaterials)
            .Subscribe();

        string[] meshNameParts = meshName.Split('|', StringSplitOptions.TrimEntries);
        if (meshNameParts.Length > 1)
        {
            SelectedMaterial = FilteredMaterials.FirstOrDefault(x => string.Equals(x.Replace(".mtd", ""),
                meshNameParts[1].Replace(".mtd", ""),
                StringComparison.CurrentCultureIgnoreCase))!;
        }
        SelectedMaterial ??= lastUsedMaterial ?? materialNameList[0];

        IObservable<bool> isMaterialSelected = this.WhenAnyValue(x => x.SelectedMaterial).Select(x => x is not null);

        CancelCommand = ReactiveCommand.Create(Cancel);
        ConfirmCommand = ReactiveCommand.Create(Confirm, isMaterialSelected);
    }

    [Reactive] public string Filter { get; set; } = string.Empty;
    
    [Reactive] public bool CreateDefaultBone { get; set; }

    [Reactive] public bool MirrorZ { get; set; }

    public SourceCache<string, string> Materials { get; }
    
    public ObservableCollectionExtended<string> FilteredMaterials { get; }

    [Reactive] public string? SelectedMaterial { get; set; }

    [Reactive] public bool IsStatic { get; set; }

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
            MirrorZ = MirrorZ,
            MTD = SelectedMaterial!,
            MaterialInfoBank = _materialInfoBank,
            IsStatic = IsStatic
        };
    }
}