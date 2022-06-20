using ReactiveUI.Fody.Helpers;

namespace FbxImporter.ViewModels;

public class TextureOptionsViewModel : ViewModelBase
{
    public TextureOptionsViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [Reactive] public bool IsUsed { get; set; } = true;
}