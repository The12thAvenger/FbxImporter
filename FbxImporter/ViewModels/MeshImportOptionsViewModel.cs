using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Binding;
using FbxImporter.Util;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsAssetPipeline.FLVERImporting;

namespace FbxImporter.ViewModels;

public class MeshImportOptionsViewModel : ViewModelBase
{
    private readonly FLVER2MaterialInfoBank _materialInfoBank;

    private class FilteredStringComparer : IComparer<string>
    {
        private readonly string _filter;
        public FilteredStringComparer(string filter)
        {
            _filter = filter;
        }

        public int Compare(string? x, string? y)
        {
            x = x?.ToLower();
            y = y?.ToLower();
            if (x is null) return 1;
            if (y is null) return -1;
            if (x.StartsWith(_filter) && !y.StartsWith(_filter)) return -1;
            if (!x.StartsWith(_filter) && y.StartsWith(_filter)) return 1;
            int xIndex = x.IndexOf(_filter, StringComparison.InvariantCultureIgnoreCase);
            int yIndex = y.IndexOf(_filter, StringComparison.InvariantCultureIgnoreCase);
            if (xIndex < yIndex) return -1;
            if (yIndex < xIndex) return 1;
            return StringComparer.InvariantCultureIgnoreCase.Compare(x, y);
        }
    }

    public MeshImportOptionsViewModel(string? mtd, FLVER2MaterialInfoBank materialInfoBank,
        MeshImportOptions? optionsCache)
    {
        FlipFaces = optionsCache?.FlipFaces ?? false;
        Weighting = optionsCache?.Weighting ?? WeightingMode.Skin;

        // Invertible is required: a singular matrix would collapse the mesh onto a plane or a line.
        Transform = new MatrixEditorViewModel("Transform",
            optionsCache?.Transform ?? Matrix3x3.Identity, Matrix3x3Presets.All, requireInvertible: true);

        // Value is null while any cell is invalid or the matrix is singular.
        IObservable<Matrix3x3?> transform = Transform.WhenAnyValue(x => x.Value);
        transform.Select(GetTransformHint).ToPropertyEx(this, x => x.TransformHint);
        IObservable<bool> isTransformValid = transform.Select(x => x.HasValue);

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
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(x => (Func<string, bool>)(y => y.ToLower().Contains(x.ToLower())));
        
        IObservable<IComparer<string>> sortComparer = this.WhenAnyValue(x => x.Filter)
            .Throttle(TimeSpan.FromMilliseconds(50))
            .Select(x => new FilteredStringComparer(x));

        Materials.Connect()
            .Filter(materialFilter)
            .Sort(sortComparer)
            .Bind(FilteredMaterials)
            .Subscribe();

        string? selectedMaterial = materialNameList.FirstOrDefault(x => string.Equals(Path.GetFileNameWithoutExtension(x),
            Path.GetFileNameWithoutExtension(mtd),
            StringComparison.CurrentCultureIgnoreCase)) ?? lastUsedMaterial;
        if (selectedMaterial is not null)
        {
            FilteredMaterialSelection.Add(selectedMaterial);
        }

        FilteredMaterialSelection.ToObservableChangeSet()
            .ToCollection()
            .Select(x => x.FirstOrDefault())
            // we reject deselections to avoid them when the current selection is filtered out
            .Where(x => x is not null)
            .ToPropertyEx(this, x => x.SelectedMaterial, selectedMaterial);
        
        IObservable<bool> isMaterialSelected = this.WhenAnyValue(x => x.SelectedMaterial).Select(x => x is not null);

        CancelCommand = ReactiveCommand.Create(Cancel);
        ConfirmCommand = ReactiveCommand.Create(Confirm,
            isMaterialSelected.CombineLatest(isTransformValid, (material, transformOk) => material && transformOk));
    }

    private static string? GetTransformHint(Matrix3x3? transform) =>
        transform is { IsReflection: true }
            ? "This transform mirrors the mesh (determinant < 0). If faces look inside out, enable Flip Faces."
            : null;

    [Reactive] public string Filter { get; set; } = string.Empty;

    [Reactive] public bool FlipFaces { get; set; }

    public MatrixEditorViewModel Transform { get; }

    [ObservableAsProperty] public string? TransformHint { get; set; }

    private SourceCache<string, string> Materials { get; }

    public ObservableCollectionExtended<string> FilteredMaterials { get; } = new();

    public ObservableCollectionExtended<string> FilteredMaterialSelection { get; set; } = new();
    
    [ObservableAsProperty] public string? SelectedMaterial { get; set; }
    
    [Reactive] public WeightingMode Weighting { get; set; }
    public List<WeightingMode> WeightingModes => WeightingMode.Values;

    public ReactiveCommand<Unit, MeshImportOptions> ConfirmCommand { get; }

    public ReactiveCommand<Unit, MeshImportOptions?> CancelCommand { get; }

    private MeshImportOptions? Cancel()
    {
        return null;
    }

    private MeshImportOptions Confirm()
    {
        return new MeshImportOptions
        {
            Transform = Transform.Value ?? Matrix3x3.Identity,
            FlipFaces = FlipFaces,
            MTD = SelectedMaterial!,
            MaterialInfoBank = _materialInfoBank,
            Weighting = Weighting
        };
    }
}
