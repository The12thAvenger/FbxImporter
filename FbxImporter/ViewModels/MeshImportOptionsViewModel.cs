using System;
using System.Collections.Generic;
using System.IO;
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
    private string? _selectedMaterial;

    private class FilteredStringComparer : IComparer<string>
    {
        private readonly string _filter;
        public FilteredStringComparer(string filter)
        {
            _filter = filter;
        }

        public int Compare(string? x, string? y)
        {
            if (x is null) return 1;
            if (y is null) return -1;
            if (x.StartsWith(_filter) && !y.StartsWith(_filter)) return -1;
            if (!x.StartsWith(_filter) && y.StartsWith(_filter)) return 1;
            return StringComparer.InvariantCultureIgnoreCase.Compare(x, y);
        }
    }

    public MeshImportOptionsViewModel(string? mtd, FLVER2MaterialInfoBank materialInfoBank,
        MeshImportOptions? optionsCache)
    {
        CreateDefaultBone = optionsCache?.CreateDefaultBone ?? true;
        MirrorZ = optionsCache?.MirrorZ ?? false;
        FlipFaces = optionsCache?.FlipFaces ?? false;
        Weighting = optionsCache?.Weighting ?? WeightingMode.Skin;

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
            .Select(x => (Func<string, bool>)(y => y.Contains(x)));
        
        IObservable<IComparer<string>> sortComparer = this.WhenAnyValue(x => x.Filter)
            .Throttle(TimeSpan.FromMilliseconds(100))
            .Select(x => new FilteredStringComparer(x));

        FilteredMaterials = new ObservableCollectionExtended<string>();
        Materials.Connect()
            .Filter(materialFilter)
            .Sort(sortComparer)
            .Bind(FilteredMaterials)
            .Subscribe();

        SelectedMaterial = materialNameList.FirstOrDefault(x => string.Equals(Path.GetFileNameWithoutExtension(x),
            Path.GetFileNameWithoutExtension(mtd),
            StringComparison.CurrentCultureIgnoreCase)) ?? lastUsedMaterial;

        IObservable<bool> isMaterialSelected = this.WhenAnyValue(x => x.SelectedMaterial).Select(x => x is not null);

        CancelCommand = ReactiveCommand.Create(Cancel);
        ConfirmCommand = ReactiveCommand.Create(Confirm, isMaterialSelected);
    }

    [Reactive] public string Filter { get; set; } = string.Empty;

    [Reactive] public bool CreateDefaultBone { get; set; }

    [Reactive] public bool MirrorZ { get; set; }
    
    [Reactive] public bool FlipFaces { get; set; }

    public SourceCache<string, string> Materials { get; }

    public ObservableCollectionExtended<string> FilteredMaterials { get; }

    public string? SelectedMaterial
    {
        get => _selectedMaterial;
        set 
        {
            if (value is null) return;
            this.RaiseAndSetIfChanged(ref _selectedMaterial, value);
        }
    }
    
    [Reactive] public WeightingMode Weighting { get; set; }
    public List<WeightingMode> WeightingModes => WeightingMode.Values;

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
            FlipFaces = FlipFaces,
            MTD = SelectedMaterial!,
            MaterialInfoBank = _materialInfoBank,
            Weighting = Weighting
        };
    }
}