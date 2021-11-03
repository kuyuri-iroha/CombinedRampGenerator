using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

public enum ExportMode
{
    Merged,
    Split
}

public class PaletteData
{
    public Gradient gradient;
    public bool selected;
    public int monopoly;

    public PaletteData()
    {
        gradient = new Gradient();
        selected = false;
        monopoly = 1;
    }

    public void CopyGradient(Gradient newValue)
    {
        gradient.mode = newValue.mode;
        gradient.colorKeys = newValue.colorKeys.Clone() as GradientColorKey[];
        gradient.alphaKeys = newValue.alphaKeys.Clone() as GradientAlphaKey[];
    }
}

public class CombinedRampGenerator : EditorWindow
{
    private static string DataPath = "Assets/Editor/CombinedRampGeneratorData.asset";
    private List<PaletteData> paletteData = new List<PaletteData>();
    private int width = 256;
    private int height = 256;
    private int divideCount = 6;
    private ExportMode exportMode = ExportMode.Merged;
    private Texture2D previewTexture;

    [MenuItem("Tools/Combined Ramp Generator")]
    public static void ShowWindow()
    {
        GetWindow<CombinedRampGenerator>("Combined Ramp Generator");
    }

    private static TResult DeepFind<TResult>(string elementName, VisualElement visualElement)
        where TResult : VisualElement
    {
        if (visualElement.name == elementName)
        {
            return visualElement as TResult;
        }

        if (visualElement.childCount == 0)
        {
            return null;
        }

        VisualElement result = null;
        foreach (var child in visualElement.Children())
        {
            result = DeepFind<TResult>(elementName, child);

            if (result != null)
            {
                break;
            }
        }

        return (TResult)result;
    }

    private static VisualElement DeepFind(string elementName, VisualElement visualElement)
    {
        return DeepFind<VisualElement>(elementName, visualElement);
    }

    private static void CalcColorPaletteInfo(int divideCount, IReadOnlyList<PaletteData> paletteDatas,
        out int[] indexMap, out int[] accumulatedMonopolies)
    {
        indexMap = new int[divideCount];
        accumulatedMonopolies = new int[paletteDatas.Count];
        var j = 0;
        var monopolySum = 0;
        for (var i = 0; i < indexMap.Length; i++)
        {
            indexMap[i] = j;

            if (monopolySum + paletteDatas[j].monopoly <= i + 1)
            {
                accumulatedMonopolies[j] = monopolySum;
                monopolySum += paletteDatas[j].monopoly;
                j++;
            }
        }
    }

    private static Color[] GenerateMergedColorPalette(int width, int height, int divideCount,
        IReadOnlyList<PaletteData> paletteDatas, IReadOnlyList<int> indexMap, IReadOnlyList<int> accumulatedMonopolies)
    {
        var result = new Color[width * height];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var value = (float)x / width * divideCount;
                var index = Mathf.FloorToInt(value);
                var data = paletteDatas[indexMap[index]];
                result[x + y * width] =
                    data.gradient.Evaluate((value - accumulatedMonopolies[indexMap[index]]) / data.monopoly);
            }
        }

        return result;
    }

    private static Color[][] GenerateSplitColorPalette(int width, int height, int divideCount,
        IReadOnlyList<PaletteData> paletteDatas, IReadOnlyList<int> indexMap, IReadOnlyList<int> accumulatedMonopolies)
    {
        var result = new Color[divideCount][];

        for (var i = 0; i < divideCount; i++)
        {
            result[i] = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = ((float)x / width / divideCount + (float)i / divideCount) * divideCount;
                    var index = Mathf.FloorToInt(value);
                    var data = paletteDatas[indexMap[index]];
                    result[i][x + y * width] =
                        data.gradient.Evaluate((value - accumulatedMonopolies[indexMap[index]]) / data.monopoly);
                }
            }
        }

        return result;
    }

    private void OnEnable()
    {
        var visualTree = Resources.Load<VisualTreeAsset>("CombinedRampGeneratorUI");
        var combinedRampGeneratorUI = visualTree.CloneTree();
        rootVisualElement.Add(combinedRampGeneratorUI);

        var numberOfDivision = DeepFind<IntegerField>("NumberOfDivision", combinedRampGeneratorUI);
        var paletteContainer = DeepFind("PaletteContainer", combinedRampGeneratorUI);
        var buttonsContainer = DeepFind("ButtonsContainer", combinedRampGeneratorUI);
        var mergeButton = DeepFind("MergeButton", buttonsContainer);
        var divideButton = DeepFind("DivideButton", buttonsContainer);
        var settingContainer = DeepFind("SettingContainer", combinedRampGeneratorUI);
        var exportModeSelector = DeepFind<EnumField>("ExportModeSelector", combinedRampGeneratorUI);
        var exportButton = DeepFind<Button>("ExportButton", combinedRampGeneratorUI);
        var previewButton = DeepFind<Button>("PreviewButton", combinedRampGeneratorUI);
        var widthInput = DeepFind<IntegerField>("Width", combinedRampGeneratorUI);
        var heightInput = DeepFind<IntegerField>("Height", combinedRampGeneratorUI);
        var preview = DeepFind("Preview", combinedRampGeneratorUI);

        var data = AssetDatabase.LoadAssetAtPath<CombinedRampGeneratorData>(DataPath);
        if (data && data.paletteDatas != null)
        {
            paletteData.AddRange(data.paletteDatas);
            width = data.width;
            height = data.height;
            divideCount = data.divideCount;
            exportMode = data.exportMode;
            widthInput.value = width;
            heightInput.value = height;
            numberOfDivision.value = divideCount;
        }

        void InitializePalettes()
        {
            paletteData.Clear();
            for (var i = 0; i < numberOfDivision.value; i++)
            {
                paletteData.Add(new PaletteData());
            }
        }

        void ApplyPalettes()
        {
            paletteContainer.Clear();

            for (var i = 0; i < paletteData.Count; i++)
            {
                var gradientField = new GradientField();
                gradientField.style.height = 20;
                gradientField.value = paletteData[i].gradient;

                var visualElement = new VisualElement();
                visualElement.style.position = Position.Absolute;
                visualElement.style.top = 0;
                visualElement.style.bottom = 0;
                visualElement.style.height = 20;
                visualElement.style.opacity = 0.0f;
                var index = i;
                visualElement.RegisterCallback<GeometryChangedEvent>(e =>
                {
                    var width = paletteContainer.contentRect.width / numberOfDivision.value *
                        paletteData[index].monopoly - 5;
                    visualElement.style.width = width;
                    gradientField.style.width = width;
                });
                visualElement.RegisterCallback<MouseDownEvent>(e => { e.PreventDefault(); });
                visualElement.RegisterCallback<ClickEvent>(e =>
                {
                    e.PreventDefault();
                    var targetElement = paletteData.ElementAt(index);
                    var changeable = true;
                    if (paletteData.Any(val => val.selected))
                    {
                        var left = paletteData.ElementAtOrDefault(index - 1);
                        var right = paletteData.ElementAtOrDefault(index + 1);

                        changeable = targetElement.selected
                            ? !((left?.selected ?? false) && (right?.selected ?? false))
                            : (left?.selected ?? false) || (right?.selected ?? false);
                    }

                    if (!changeable) return;

                    Color borderColor;
                    int borderWidth;
                    if (!targetElement.selected)
                    {
                        borderColor = Color.white;
                        borderWidth = 2;
                    }
                    else
                    {
                        borderColor = Color.clear;
                        borderWidth = 0;
                    }

                    gradientField.style.borderTopColor = borderColor;
                    gradientField.style.borderBottomColor = borderColor;
                    gradientField.style.borderLeftColor = borderColor;
                    gradientField.style.borderRightColor = borderColor;
                    gradientField.style.borderTopWidth = borderWidth;
                    gradientField.style.borderBottomWidth = borderWidth;
                    gradientField.style.borderLeftWidth = borderWidth;
                    gradientField.style.borderRightWidth = borderWidth;
                    targetElement.selected = !targetElement.selected;

                    var selectedPalette = paletteData.Where(val => val.selected).ToArray();
                    mergeButton.SetEnabled(1 < selectedPalette.Length);
                    divideButton.SetEnabled(selectedPalette.Length == 1 && selectedPalette[0].monopoly != 1);

                    // 1つだけ選択で編集可能
                    if (selectedPalette.Length == 1)
                    {
                        settingContainer.style.display = DisplayStyle.Flex;
                        var gradient = DeepFind<GradientField>("Gradient", settingContainer);
                        var selectedIndex = paletteData.FindIndex(val => val.selected);
                        gradient.label = $"Color {selectedIndex}";
                        gradient.value = paletteData[selectedIndex].gradient;
                    }
                    else
                    {
                        settingContainer.style.display = DisplayStyle.None;
                    }
                });
                gradientField.Add(visualElement);
                paletteContainer.Add(gradientField);
            }
        }

        numberOfDivision.RegisterValueChangedCallback(e =>
        {
            InitializePalettes();
            ApplyPalettes();
        });

        mergeButton.RegisterCallback<ClickEvent>(e =>
        {
            var selectStartIndex = -1;
            var selectEndIndex = 0;
            for (var i = 0; i < paletteData.Count; i++)
            {
                if (paletteData[i].selected && selectStartIndex == -1)
                {
                    selectStartIndex = i;
                }

                if (!paletteData[i].selected && selectStartIndex != -1)
                {
                    break;
                }

                selectEndIndex = i;
            }

            if (selectStartIndex == -1 || selectStartIndex == selectEndIndex) return;

            paletteData[selectStartIndex].monopoly =
                paletteData.Aggregate(0, (prev, val) => prev + (val.selected ? val.monopoly : 0));
            paletteData.RemoveRange(selectStartIndex + 1, selectEndIndex - selectStartIndex);

            paletteData.ForEach(data => data.selected = false);

            ApplyPalettes();
        });

        divideButton.RegisterCallback<ClickEvent>(e =>
        {
            var targetIndex = paletteData.FindIndex(val => val.selected);
            if (targetIndex == -1) return;

            var addingCount = paletteData[targetIndex].monopoly - 1;
            for (var i = 0; i < addingCount; i++)
            {
                paletteData.Insert(i + targetIndex + 1, new PaletteData());
            }

            paletteData[targetIndex].monopoly = 1;
            paletteData.ForEach(data => data.selected = false);

            ApplyPalettes();
        });

        mergeButton.SetEnabled(false);
        divideButton.SetEnabled(false);

        exportModeSelector.Init(exportMode);
        exportModeSelector.RegisterValueChangedCallback(e =>
        {
            exportMode = Enum.Parse<ExportMode>(e.newValue.ToString());
        });

        exportButton.RegisterCallback<ClickEvent>(e =>
        {
            var defaultFolderPath = $"{Application.dataPath}/..";

            // テクスチャ出力
            CalcColorPaletteInfo(divideCount, paletteData, out var indexMap, out var accumulatedMonopolies);

            if (exportMode == ExportMode.Merged)
            {
                var texture = new Texture2D(width, height, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
                texture.SetPixels(
                    GenerateMergedColorPalette(width, height, divideCount, paletteData, indexMap, accumulatedMonopolies)
                );
                texture.Apply();

                var filePath = EditorUtility.SaveFilePanel("Merged Export", defaultFolderPath,
                    $"ColorPalette_{DateTime.Now:yyyyMMddHHmm}.png", "png");

                if (!string.IsNullOrEmpty(filePath))
                {
                    File.WriteAllBytes(filePath, texture.EncodeToPNG());
                    EditorUtility.DisplayDialog("Export Successful", $"Exported to \"{filePath}\"", "Done");
                }

                DestroyImmediate(texture);
            }
            else if (exportMode == ExportMode.Split)
            {
                var colors = GenerateSplitColorPalette(width, height, divideCount, paletteData, indexMap,
                    accumulatedMonopolies);

                var textures = new Texture2D[divideCount];

                for (var i = 0; i < divideCount; i++)
                {
                    var texture = new Texture2D(width, height, GraphicsFormat.R8G8B8A8_SRGB,
                        TextureCreationFlags.None);
                    texture.SetPixels(colors[i]);
                    texture.Apply();

                    textures[i] = texture;
                }

                var folderPath = EditorUtility.SaveFolderPanel("Split Export", defaultFolderPath,
                    $"ColorPalettes_{DateTime.Now:yyyyMMddHHmm}");
                if (!string.IsNullOrEmpty(folderPath))
                {
                    for (var i = 0; i < divideCount; i++)
                    {
                        File.WriteAllBytes($"{folderPath}/Color_{i}.png", textures[i].EncodeToPNG());
                    }

                    EditorUtility.DisplayDialog("Export Successful", $"Exported to under \"{folderPath}/\"", "Done");
                }

                foreach (var texture in textures)
                {
                    DestroyImmediate(texture);
                }
            }

            AssetDatabase.Refresh();
        });

        previewButton.RegisterCallback<ClickEvent>(e =>
        {
            if (previewTexture) DestroyImmediate(previewTexture);

            const int previewSizeConstant = 100;
            var previewSize =
                Mathf.FloorToInt(previewSizeConstant * ((float)Mathf.Min(width, height) / Mathf.Max(width, height)));
            var previewWidth = width < height ? previewSize : previewSizeConstant;
            var previewHeight = height < width ? previewSize : previewSizeConstant;
            previewTexture = new Texture2D(previewWidth, previewHeight, GraphicsFormat.R8G8B8A8_SRGB,
                TextureCreationFlags.None);

            CalcColorPaletteInfo(divideCount, paletteData, out var indexMap, out var accumulatedMonopolies);
            previewTexture.SetPixels(
                GenerateMergedColorPalette(previewWidth, previewHeight, divideCount, paletteData, indexMap,
                    accumulatedMonopolies)
            );
            previewTexture.Apply();

            preview.style.width = previewWidth;
            preview.style.height = previewHeight;
            preview.style.backgroundImage = previewTexture;
        });

        widthInput.RegisterValueChangedCallback(e => { width = e.newValue; });
        heightInput.RegisterValueChangedCallback(e => { height = e.newValue; });
        numberOfDivision.RegisterValueChangedCallback(e => { divideCount = e.newValue; });

        var gradientSetting = DeepFind<GradientField>("Gradient", settingContainer);
        gradientSetting.RegisterValueChangedCallback(val =>
        {
            var selectedDataIndex = paletteData.FindIndex(data => data.selected);
            if (selectedDataIndex == -1) return;
            paletteData[selectedDataIndex].CopyGradient(val.newValue);
            (paletteContainer.Children().ToArray()[selectedDataIndex] as GradientField).value =
                paletteData[selectedDataIndex].gradient;
        });
        settingContainer.Add(gradientSetting);

        if (paletteData.Count == 0) InitializePalettes();
        ApplyPalettes();
    }

    private void OnDisable()
    {
        var saveData = CreateInstance<CombinedRampGeneratorData>();
        paletteData.ForEach(val => val.selected = false);
        saveData.paletteDatas = paletteData.ToArray();
        saveData.width = width;
        saveData.height = height;
        saveData.divideCount = divideCount;
        saveData.exportMode = exportMode;
        AssetDatabase.CreateAsset(saveData, DataPath);
        AssetDatabase.Refresh();

        if (previewTexture) DestroyImmediate(previewTexture);
    }
}