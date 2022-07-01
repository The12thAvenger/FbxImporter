using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Xml.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FbxImporter.ViewModels;

public record ClothData(string Name, XElement PosePositions, List<int> TriangleIndices);

public class ClothDataSelectorViewModel : ViewModelBase
{
    public ClothDataSelectorViewModel(XElement clothContainer)
    {
        string[] clothDatas = clothContainer.Elements().First(x => x.Attribute("name")?.Value == "clothDatas").Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string clothDataRef in clothDatas)
        {
            XElement? clothData = clothContainer.Parent!.Elements()
                .FirstOrDefault(x => x.Attribute("name")?.Value == clothDataRef);
            if (clothData is null) continue;
            
            string name = clothData.Elements().First(x => x.Attribute("name")?.Value == "name").Value;
            
            string simClothRef = clothData.Elements().First(x => x.Attribute("name")?.Value == "simClothDatas").Value
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
            XElement? simClothData = clothContainer.Parent!.Elements()
                .FirstOrDefault(x => x.Attribute("name")?.Value == simClothRef);
            if (simClothData is null) continue;
            
            List<int> triangleIndices = simClothData.Elements().First(x => x.Attribute("name")?.Value == "triangleIndices").Value
                .Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();

            string simPoseRef = simClothData.Elements().First(x => x.Attribute("name")?.Value == "simClothPoses").Value
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)[0];
            XElement? simClothPose = clothContainer.Parent!.Elements()
                .FirstOrDefault(x => x.Attribute("name")?.Value == simPoseRef);
            if (simClothPose is null) continue;

            XElement posePositions = simClothPose.Elements().First(x => x.Attribute("name")?.Value == "positions");
            
            ClothData.Add(new ClothData(name, posePositions, triangleIndices));
        }

        SelectClothDataCommand = ReactiveCommand.Create(SelectClothData);
        
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public ObservableCollection<ClothData> ClothData { get; } = new();

    [Reactive] public bool MirrorX { get; set; } = true;

    [Reactive] public ClothData? SelectedClothData { get; set; }

    public ReactiveCommand<Unit, ClothReorderOptions?> SelectClothDataCommand { get; }

    public ReactiveCommand<Unit, ClothReorderOptions?> CancelCommand { get; }

    private ClothReorderOptions? Cancel()
    {
        return null;
    }

    // ReSharper disable once ReturnTypeCanBeNotNullable
    private ClothReorderOptions? SelectClothData()
    {
        return new ClothReorderOptions(SelectedClothData!, MirrorX);
    }
}