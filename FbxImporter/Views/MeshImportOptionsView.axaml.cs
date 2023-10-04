using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using FbxImporter.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Specialized;
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
            ViewModel.FilteredMaterials.CollectionChanged -= UpdateMaterialSelection;
            ViewModel.FilteredMaterials.CollectionChanged += UpdateMaterialSelection;
        });
#if DEBUG
        this.AttachDevTools();
#endif
    }
    
    private void UpdateMaterialSelection(object? sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            int selectedIndex = ViewModel!.FilteredMaterials.IndexOf(ViewModel!.SelectedMaterial!);
            selectedIndex = Math.Clamp(selectedIndex, 0, int.MaxValue);
            MaterialListBox.ScrollIntoView(selectedIndex);
            MaterialListBox.SelectedItem = ViewModel.SelectedMaterial;
        });
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}