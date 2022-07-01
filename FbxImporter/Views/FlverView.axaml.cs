using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using FbxImporter.ViewModels;
using JetBrains.Annotations;
using MessageBox.Avalonia.BaseWindows.Base;
using MessageBox.Avalonia.Enums;
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
            d(ViewModel!.GetClothPose.RegisterHandler(GetClothPoseAsync));
            d(ViewModel!.ShowMessage.RegisterHandler(HandleShowMessageInteraction));
        });
    }

    private async Task HandleShowMessageInteraction(InteractionContext<(string, string), Unit> interaction)
    {
        (string title, string text) = interaction.Input;
        await ShowMessage(title, text);
        interaction.SetOutput(Unit.Default);
    }

    private async Task GetClothPoseAsync(InteractionContext<Unit, ClothReorderOptions?> interaction)
    {
        Window mainWindow = (Window) this.GetVisualRoot();
        if (mainWindow is null) throw new Exception("Main Window is null");
        
        XElement clothContainer;
        while (true)
        {
            OpenFileDialog openFileDialog = new()
            {
                Title = "Open Cloth File",
                Filters = new List<FileDialogFilter>
                {
                    new()
                    {
                        Name = "Havok XML Packfile",
                        Extensions = new List<string> {"xml"}
                    },
                    new()
                    {
                        Name = "All Files",
                        Extensions = new List<string> {"*"}
                    }
                },
                AllowMultiple = false
            };

            string? clothPath = (await openFileDialog.ShowAsync(mainWindow))?[0];
            if (clothPath == null)
            {
                interaction.SetOutput(null);
                return;
            }

            try
            {
                clothContainer = XElement.Load(clothPath).Descendants()
                    .First(x => x.Attribute("class")?.Value == "hclClothContainer");
                break;
            }
            catch (Exception)
            {
                await ShowMessage("Error: Invalid File",
                                  "The selected file is not a valid havok 2014 xml packfile or does not contain any cloth data.");
            }
        }

        ClothDataSelectorViewModel clothDataSelectorViewModel = new(clothContainer);

        ClothReorderOptions? options;
        while (true)
        {
            ClothDataSelectorView clothDataSelectorView = new()
            {
                DataContext = clothDataSelectorViewModel
            };
            options = await clothDataSelectorView.ShowDialog<ClothReorderOptions?>(mainWindow);
            if (options is null) break;

            int numPoseVertices = options.ClothData.PosePositions.Value.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries).Length;
            int numSelectedVertices = ViewModel!.SelectedMesh!.Mesh.Vertices.Count;
            if (numPoseVertices == numSelectedVertices) break;

            if (numPoseVertices > numSelectedVertices)
            {
                IMsBoxWindow<ButtonResult>? messageBoxWarning = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow("Warning: Vertex Count Mismatch", 
                    $"The vertex count of the selected cloth data ({numPoseVertices}) is higher\nthan the vertex count of the selected mesh ({numSelectedVertices})." + 
                    "\nPlease note that this operation will add additional vertices to the mesh." +
                    "\nAre you sure you wish to continue?",
                    ButtonEnum.OkCancel);
                ButtonResult result = await messageBoxWarning.Show(mainWindow);
                if (result == ButtonResult.Ok) break;
                
                clothDataSelectorViewModel.SelectedClothData = null;
                continue;
            }

            await ShowMessage("Error: Vertex Count Mismatch",
                        $"The vertex count of the selected cloth data ({numPoseVertices}) does not match the vertex count of the selected mesh ({numSelectedVertices}).");
            
            clothDataSelectorViewModel.SelectedClothData = null;
        }
        interaction.SetOutput(options);
    }

    private async Task ShowMessage(string title, string text)
    {
        Window mainWindow = (Window) this.GetVisualRoot();
        IMsBoxWindow<ButtonResult>? messageBoxError = MessageBox.Avalonia.MessageBoxManager.GetMessageBoxStandardWindow(title, text);
        await messageBoxError.Show(mainWindow);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}