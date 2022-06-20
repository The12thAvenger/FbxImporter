using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using FbxImporter.Models;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SoulsFormats;

namespace FbxImporter.ViewModels;

public class MeshImportOptionsViewModel : ViewModelBase
{
    public MeshImportOptionsViewModel(MaterialLibrary materialLibrary)
    {
        Materials = new ObservableCollection<MaterialInfo>(materialLibrary.Materials);
        SelectedMaterial = Materials[0];
        
        this.WhenAnyValue(x => x.SelectedMaterial).Select(x => new ObservableCollection<FLVER2.Texture>(x.Material.Textures)).ToPropertyEx(this, x => x.Textures);

        CancelCommand = ReactiveCommand.Create(Cancel);

        ConfirmCommand = ReactiveCommand.Create(Confirm);
    }
    
    [Reactive] public bool IsCloth { get; set; } = true;

    [Reactive] public bool CreateDefaultBone { get; set; } = true;

    [Reactive] public bool MirrorX { get; set; }  = true;

    public ObservableCollection<MaterialInfo> Materials { get; }

    [Reactive] public MaterialInfo SelectedMaterial { get; set; }

    [ObservableAsProperty] public extern ObservableCollection<FLVER2.Texture> Textures { get; }

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
            IsCloth = IsCloth,
            MaterialInfo = SelectedMaterial
        };
    }
}