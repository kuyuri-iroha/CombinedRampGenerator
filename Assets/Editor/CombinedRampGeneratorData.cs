using UnityEngine;

public class CombinedRampGeneratorData : ScriptableObject
{
    public PaletteData[] paletteDatas;
    public int width;
    public int height;
    public int divideCount;
    public ExportMode exportMode;
}