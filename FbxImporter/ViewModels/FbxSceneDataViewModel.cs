using System.Collections.ObjectModel;
using ReactiveUI.Fody.Helpers;

namespace FbxImporter.ViewModels;

public class FbxSceneDataViewModel : ViewModelBase
{
    [Reactive] public ObservableCollection<FbxMeshDataViewModel> MeshData { get; set; } = new();

    [Reactive] public FbxMeshDataViewModel? SelectedMesh { get; set; }
}