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
using MessageBox.Avalonia.DTO;
using MessageBox.Avalonia.Enums;
using MessageBox.Avalonia.Models;
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

        ClothDataSelectorView clothDataSelectorView = new()
        {
            DataContext = clothDataSelectorViewModel
        };
        ClothReorderOptions? options = await clothDataSelectorView.ShowDialog<ClothReorderOptions?>(mainWindow);
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