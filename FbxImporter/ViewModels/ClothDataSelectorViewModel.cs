using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Xml.Linq;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FbxImporter.ViewModels;

public record ClothData(string Name, IEnumerable<SkinOperator> SkinOperators);

public record SkinOperator(string Name, XElement Data);

public class ClothDataSelectorViewModel : ViewModelBase
{
    public ClothDataSelectorViewModel(XElement clothContainer)
    {
        LoadClothData(clothContainer);

        SelectClothDataCommand = ReactiveCommand.Create(SelectClothData);
        
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public List<ClothData> ClothData { get; } = new();

    [Reactive] public bool MirrorX { get; set; } = true;

    [Reactive] public SkinOperator? SelectedSkinOperatorData { get; set; }

    public ReactiveCommand<Unit, ClothReorderOptions?> SelectClothDataCommand { get; }

    public ReactiveCommand<Unit, ClothReorderOptions?> CancelCommand { get; }

    private static ClothReorderOptions? Cancel()
    {
        return null;
    }

    // ReSharper disable once ReturnTypeCanBeNotNullable
    private ClothReorderOptions? SelectClothData()
    {
        return new ClothReorderOptions(SelectedSkinOperatorData!.Data, MirrorX);
    }

    private void LoadClothData(XElement clothContainer)
    {

        Dictionary<string, XElement> clothDataDict = new();
        Dictionary<string, XElement> skinOperatorDict = new();
        foreach (XElement hkobject in clothContainer.Parent!.Elements())
        {
            if (hkobject.Attribute("class")?.Value == "hclClothData")
            {
                clothDataDict.Add(hkobject.Attribute("name")!.Value, hkobject);
            }

            if (hkobject.Attribute("class")?.Value.Contains("hclObjectSpaceSkin") ?? false)
            {
                skinOperatorDict.Add(hkobject.Attribute("name")!.Value, hkobject);
            }
        }

        string[] clothDatas = clothContainer.Elements().First(x => x.Attribute("name")?.Value == "clothDatas").Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string clothDataRef in clothDatas.Where(clothDataDict.ContainsKey))
        {
            XElement clothData = clothDataDict[clothDataRef];
            string name = clothData.Elements().First(x => x.Attribute("name")?.Value == "name").Value;

            IEnumerable<string> operatorRefs = clothData.Elements().First(x => x.Attribute("name")?.Value == "operators").Value
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            IEnumerable<SkinOperator> skinOperators = operatorRefs.Where(skinOperatorDict.ContainsKey).Select(x =>
            {
                XElement skinOperator = skinOperatorDict[x];
                string skinName = skinOperator.Elements().First(x => x.Attribute("name")?.Value == "name").Value;
                return new SkinOperator(skinName, skinOperator);
            });

            ClothData.Add(new ClothData(name, skinOperators));
        }
    }

}