using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FbxImporter.ViewModels;
using JetBrains.Annotations;

namespace FbxImporter.Views;

[UsedImplicitly]
// ReSharper disable once InconsistentNaming
public partial class Matrix3x3View : ReactiveUserControl<Matrix3x3View>
{
    public Matrix3x3View()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}