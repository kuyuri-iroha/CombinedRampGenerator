using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;

namespace CombinedRampGenerator
{
    public enum ExportMode
    {
        Merged,
        Split
    }

    /// <summary>
    /// パレット（色編集可能な単位）のデータ
    /// </summary>
    public class PaletteData
    {
        public Gradient gradient;
        public bool selected;
        public int monopoly; // セルの専有量

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

    /// <summary>
    /// GradientMode混在可能なRampTextureのジェネレータ
    /// </summary>
    public class CombinedRampGenerator : EditorWindow
    {
        private const string DataPath = "Editor/CombinedRampGenerator/CombinedRampGeneratorData.asset";
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

        /// <summary>
        /// 渡したVisualElementの下層すべての要素から名前が一致する要素を再帰的に検索する
        /// </summary>
        /// <param name="elementName">検索する要素名</param>
        /// <param name="visualElement">検索対象要素</param>
        /// <typeparam name="TResult">検索結果の型</typeparam>
        /// <returns>検索結果</returns>
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

        /// <summary>
        /// カラーパレットテクスチャの生成に必要な情報の算出
        /// </summary>
        /// <param name="divideCount">セルの分割数</param>
        /// <param name="paletteDatas">パレットデータ</param>
        /// <param name="indexMap">セルがどのパレットデータへ対応するかを示す配列</param>
        /// <param name="accumulatedMonopolies">前のパレットまでの専有済みセルの合計を示す配列</param>
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

        /// <summary>
        /// １つのTextureとして出力するときのColor配列生成
        /// </summary>
        /// <param name="width">Textureの幅</param>
        /// <param name="height">Textureの高さ</param>
        /// <param name="divideCount">セルの分割数</param>
        /// <param name="paletteDatas">パレットデータ</param>
        /// <param name="indexMap">セルとパレットデータの対応配列</param>
        /// <param name="accumulatedMonopolies">専有済みセルの合計配列</param>
        /// <returns>全てのセルを１つにまとめたColor配列</returns>
        private static Color[] GenerateMergedColorPalette(int width, int height, int divideCount,
            IReadOnlyList<PaletteData> paletteDatas, IReadOnlyList<int> indexMap,
            IReadOnlyList<int> accumulatedMonopolies)
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

        /// <summary>
        /// セル毎にTextureとして出力するときのColorジャグ配列生成
        /// </summary>
        /// <param name="width">Textureの幅</param>
        /// <param name="height">Textureの高さ</param>
        /// <param name="divideCount">セルの分割数</param>
        /// <param name="paletteDatas">パレットデータ</param>
        /// <param name="indexMap">セルとパレットデータの対応配列</param>
        /// <param name="accumulatedMonopolies">専有済みセルの合計配列</param>
        /// <returns>セル毎に分割したColorジャグ配列</returns>
        private static Color[][] GenerateSplitColorPalette(int width, int height, int divideCount,
            IReadOnlyList<PaletteData> paletteDatas, IReadOnlyList<int> indexMap,
            IReadOnlyList<int> accumulatedMonopolies)
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

            // 必要な要素の取得
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
            var gradientSetting = DeepFind<GradientField>("Gradient", settingContainer);

            // データのロード
            var data = AssetDatabase.LoadAssetAtPath<CombinedRampGeneratorData>($"Assets/{DataPath}");
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

            // パレットデータの初期化
            void InitializePalettes()
            {
                paletteData.Clear();
                for (var i = 0; i < numberOfDivision.value; i++)
                {
                    paletteData.Add(new PaletteData());
                }
            }

            // パレットデータをもとにセルを作成
            void ApplyPalettes()
            {
                paletteContainer.Clear();

                for (var i = 0; i < paletteData.Count; i++)
                {
                    // 指定した色の反映用
                    var gradientField = new GradientField();
                    gradientField.style.height = 20;
                    gradientField.value = paletteData[i].gradient;

                    // 選択処理用
                    var visualElement = new VisualElement();
                    visualElement.style.position = Position.Absolute;
                    visualElement.style.top = 0;
                    visualElement.style.bottom = 0;
                    visualElement.style.height = 20;
                    visualElement.style.opacity = 0.0f;
                    var index = i;

                    // 親要素のサイズに合わせて整列させる
                    visualElement.RegisterCallback<GeometryChangedEvent>(e =>
                    {
                        var width = paletteContainer.contentRect.width / numberOfDivision.value *
                            paletteData[index].monopoly - 5;
                        visualElement.style.width = width;
                        gradientField.style.width = width;
                    });

                    // VisualElementの下にあるGradientFieldのイベント遮断
                    visualElement.RegisterCallback<MouseDownEvent>(e => { e.PreventDefault(); });

                    // 選択処理
                    visualElement.RegisterCallback<ClickEvent>(e =>
                    {
                        e.PreventDefault();

                        // 変更可能かの判断
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

                        // 選択または選択解除の反映
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

                        // 選択状況の変化による変更
                        var selectedPalette = paletteData.Where(val => val.selected).ToArray();
                        mergeButton.SetEnabled(1 < selectedPalette.Length);
                        divideButton.SetEnabled(selectedPalette.Length == 1 && selectedPalette[0].monopoly != 1);

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

            // セルの分割数の変更
            numberOfDivision.RegisterValueChangedCallback(e =>
            {
                divideCount = e.newValue;
                InitializePalettes();
                ApplyPalettes();
            });

            // マージ
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

                // monopolyをマージ
                paletteData[selectStartIndex].monopoly =
                    paletteData.Aggregate(0, (prev, val) => prev + (val.selected ? val.monopoly : 0));
                paletteData.RemoveRange(selectStartIndex + 1, selectEndIndex - selectStartIndex);

                paletteData.ForEach(data => data.selected = false);

                ApplyPalettes();
            });

            // 再分割
            divideButton.RegisterCallback<ClickEvent>(e =>
            {
                var targetIndex = paletteData.FindIndex(val => val.selected);
                if (targetIndex == -1) return;

                // monopolyを再分割
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

            // Export Modeの変更
            exportModeSelector.Init(exportMode);
            exportModeSelector.RegisterValueChangedCallback(e =>
            {
                exportMode = Enum.Parse<ExportMode>(e.newValue.ToString());
            });

            // Export
            exportButton.RegisterCallback<ClickEvent>(e =>
            {
                var defaultFolderPath = $"{Application.dataPath}/..";

                CalcColorPaletteInfo(divideCount, paletteData, out var indexMap, out var accumulatedMonopolies);

                // マージモードでのExport
                if (exportMode == ExportMode.Merged)
                {
                    var texture = new Texture2D(width, height, GraphicsFormat.R8G8B8A8_SRGB, TextureCreationFlags.None);
                    texture.SetPixels(
                        GenerateMergedColorPalette(width, height, divideCount, paletteData, indexMap,
                            accumulatedMonopolies)
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
                // スプリットモードでのExport
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

                        EditorUtility.DisplayDialog("Export Successful", $"Exported to under \"{folderPath}/\"",
                            "Done");
                    }

                    foreach (var texture in textures)
                    {
                        DestroyImmediate(texture);
                    }
                }

                AssetDatabase.Refresh();
            });

            // プレビュー
            previewButton.RegisterCallback<ClickEvent>(e =>
            {
                if (previewTexture) DestroyImmediate(previewTexture);

                // 表示サイズ決定
                const int previewSizeConstant = 100;
                var previewSize =
                    Mathf.FloorToInt(previewSizeConstant *
                                     ((float)Mathf.Min(width, height) / Mathf.Max(width, height)));
                var previewWidth = width < height ? previewSize : previewSizeConstant;
                var previewHeight = height < width ? previewSize : previewSizeConstant;
                previewTexture = new Texture2D(previewWidth, previewHeight, GraphicsFormat.R8G8B8A8_SRGB,
                    TextureCreationFlags.None);

                // マージモードでプレビュー表示
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

            // 出力サイズ変更
            widthInput.RegisterValueChangedCallback(e => { width = e.newValue; });
            heightInput.RegisterValueChangedCallback(e => { height = e.newValue; });

            // 色指定
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
            //データのセーブ
            var saveData = CreateInstance<CombinedRampGeneratorData>();
            paletteData.ForEach(val => val.selected = false);
            saveData.paletteDatas = paletteData.ToArray();
            saveData.width = width;
            saveData.height = height;
            saveData.divideCount = divideCount;
            saveData.exportMode = exportMode;

            var directoryPath = Path.GetDirectoryName($"{Application.dataPath}/{DataPath}");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            AssetDatabase.CreateAsset(saveData, $"Assets/{DataPath}");
            AssetDatabase.Refresh();

            if (previewTexture) DestroyImmediate(previewTexture);
        }
    }
}