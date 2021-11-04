using UnityEngine;

namespace CombinedRampGenerator
{
    /// <summary>
    /// CombinedRampGeneratorの作業内容データ
    /// </summary>
    public class CombinedRampGeneratorData : ScriptableObject
    {
        public PaletteData[] paletteDatas;
        public int width;
        public int height;
        public int divideCount;
        public ExportMode exportMode;
    }
}