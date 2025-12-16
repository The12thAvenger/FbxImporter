using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using FbxImporter.ViewModels;
using JetBrains.Annotations;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;
using ReactiveUI;

namespace FbxImporter.Views;

[UsedImplicitly]
public partial class FlverView : ReactiveUserControl<FlverViewModel>
{
    public FlverView()
    {
        InitializeComponent();
        this.WhenActivated(d =>
        {
            d(ViewModel!.ShowMessage.RegisterHandler(HandleShowMessageInteraction));
        });
    }

    private async Task HandleShowMessageInteraction(IInteractionContext<(string, string), Unit> interaction)
    {
        (string title, string text) = interaction.Input;
        await ShowMessage(title, text);
        interaction.SetOutput(Unit.Default);
    }

    private async Task ShowMessage(string title, string text)
    {
        Window mainWindow = (Window) this.GetVisualRoot()!;
        IMsBox<ButtonResult>? messageBoxError = MessageBoxManager.GetMessageBoxStandard(title, text);
        await messageBoxError.ShowWindowAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}