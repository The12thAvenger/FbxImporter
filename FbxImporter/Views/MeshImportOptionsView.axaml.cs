using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FbxImporter.ViewModels;
using ReactiveUI;
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace FbxImporter.Views;

public partial class MeshImportOptionsView : ReactiveWindow<MeshImportOptionsViewModel>
{
    public MeshImportOptionsView()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            d(ViewModel!.ConfirmCommand.Subscribe(Close));
            d(ViewModel!.CancelCommand.Subscribe(Close));
            ViewModel.FilteredMaterials.CollectionChanged += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    int selectedIndex = ViewModel.FilteredMaterials.IndexOf(ViewModel!.SelectedMaterial!);
                    selectedIndex = Math.Clamp(selectedIndex, 0, int.MaxValue);
                    MaterialListBox.ScrollIntoView(selectedIndex);
                    MaterialListBox.SelectedItem = ViewModel.SelectedMaterial;
                });
            };
        });
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void MaterialListBox_OnInitialized(object o, EventArgs _)
    {
        MaterialListBox.ScrollIntoView(ViewModel.SelectedMaterial!);
        MaterialListBox.SelectedItem = ViewModel.SelectedMaterial;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}