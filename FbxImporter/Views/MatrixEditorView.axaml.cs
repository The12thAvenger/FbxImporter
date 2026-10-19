using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using JetBrains.Annotations;

namespace FbxImporter.Views;

[UsedImplicitly]
public partial class MatrixEditorView : UserControl
{
    public MatrixEditorView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
