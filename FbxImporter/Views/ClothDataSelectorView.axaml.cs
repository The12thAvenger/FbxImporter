using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FbxImporter.ViewModels;
using ReactiveUI;
using System;

namespace FbxImporter.Views;

public partial class ClothDataSelectorView : ReactiveWindow<ClothDataSelectorViewModel>
{
    public ClothDataSelectorView()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            d(ViewModel!.SelectClothDataCommand.Subscribe(Close));
            d(ViewModel!.CancelCommand.Subscribe(Close));
        });

#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}