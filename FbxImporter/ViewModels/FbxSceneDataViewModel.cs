using System.Collections.ObjectModel;
using Newtonsoft.Json;
using ReactiveUI.Fody.Helpers;

namespace FbxImporter.ViewModels;

[JsonObject(MemberSerialization.OptIn)]
public class FbxSceneDataViewModel : ViewModelBase
{
    [JsonProperty]
    [Reactive] public ObservableCollection<FbxMeshDataViewModel> MeshData { get; set; } = new();

    [Reactive] public FbxMeshDataViewModel? SelectedMesh { get; set; }
}