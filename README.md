# CombinedRampGenerator

## 概要

![CombinedRampGenerator](./Documentation/Images/generator_window.png)

Fixed・Blendの両モードのグラデーションを結合したRampテクスチャのジェネレータです。

結合するグラデーションの数、生成するテクスチャサイズを変更できます。また、出力時に結合して出力するか分割して出力するかも選択可能です。

## Installation

このアセットはUnity Package manager (UPM)を使用してインストールが可能です。

インストール必要なパッケージはありません。

### From git URL

`Window > Package Manager` を開いて左上の+マークをクリックすると表示されるAdd package from git URL... をクリックすると入力欄が表示されます。

そこに `git+ssh://git@github.com/Kuyuri-Iroha/CombinedRampGenerator.git` と入力することで最新バージョンをインストールすることができます。

### From local disk

GitHubのReleaseから`CombinedRampGenerator.zip`をダウンロードして解凍した後、Window > Package Manager を開いて左上の+マークをクリックすると表示されるAdd package
from disk...から解凍したフォルダを選択することでインストールできます。

## 使い方

`Tools > CombinedRampGenerator` からCombinedRampGeneratorを起動します。

### 分割数

![NumberOfDivision](./Documentation/Images/number_of_division.png)

生成するテクスチャの分割数を指定できます。

カラーパレットの最小単位のとなり、Export ModeがSplitの場合はここで指定された数のテクスチャに分割されて出力されます。

また、変更するとカラーパレットがリセットされます。

### カラーパレット

![ColorPalette](./Documentation/Images/color_palette.png)

個別にグラデーションを指定できる各セルの割合を編集できます。

クリックしてセルを選択できます。（選択できるのは選択済みの隣接セルのみ）

2つ以上のセルを選択し、Mergeボタンをクリックしてセルを結合することで各セルの割合を編集できます。

また、結合したセル1つを選択した状態でDivideボタンをクリックすると、結合済みのセルが占めていた範囲のセルを初期状態に再分割できます。

### グラデーション編集

![ColorEdit](./Documentation/Images/color_edit.png)

セルを1つ選択すると表示され、グラデーションを編集できます。

### サイズ指定とプレビュー

![SizePreview](./Documentation/Images/size_preview.png)

テクスチャの出力サイズを指定できます。

Previewボタンを押すことで、枠で囲まれた領域にテクスチャの出力サイズの縦横比でプレビュー画像が表示されます。

このときのプレビュー画像はExport ModeがMergedモードのときの出力結果と同じものとなります。（黒枠内の空白領域は最終出力には含まれません）

### 出力

![Export](./Documentation/Images/export.png)

Exportボタンをクリックすると、生成したTextureをpng画像として出力します。

#### Merged Mode

Export ModeをMergedにして出力する場合、以下のような画像が出力されます。

![MergedResult](Documentation/Images/ColorPalette_202111041708.png)
> Width: 1024 \
> Height: 256 \
> Export Mode: Merged

#### Split Mode

Export ModeをSplitにして出力する場合、分割数の数の画像が出力され、以下のような画像が出力されます。

なお、結合済みのセルは途中で切り離されて出力されます。

![Color0](Documentation/Images/split/Color_0.png)
![Color1](Documentation/Images/split/Color_1.png)
![Color2](Documentation/Images/split/Color_2.png)
![Color3](Documentation/Images/split/Color_3.png)
![Color4](Documentation/Images/split/Color_4.png)
![Color5](Documentation/Images/split/Color_5.png)
> Width: 1024 \
> Height: 256 \
> Export Mode: Split

## Unity Version

開発バージョン：2021.2.0