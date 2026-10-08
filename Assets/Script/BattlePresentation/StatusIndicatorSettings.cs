using UnityEngine;

// 状態異常ラベルの見た目の調整値。BattleBootstrapperのInspectorに出して、
// 実行時に追加されるコンポーネント（BattleVisualSync等）を介さず編集できるようにする。
[System.Serializable]
public class StatusIndicatorSettings
{
    [Tooltip("駒モデルの上端からの高さ（ワールド単位）。HPバーの数値と重なる場合は大きくする")]
    public float heightAboveModelTop = 0.9f;

    [Tooltip("ラベル（Canvas）のワールド単位での縮尺。PieceHealthBarのHP_Text_Canvasと同じ考え方")]
    public float canvasScale = 0.006f;

    [Tooltip("ラベルの文字サイズ（UGUI Textのfont size相当）")]
    public float fontSize = 40f;

    public Color stunColor = new Color(1f, 0.85f, 0.3f);
    public Color slowColor = new Color(0.4f, 0.75f, 1f);

    [Tooltip("表示文言。日本語は日本語対応のTMPフォントアセットが必要なため、現状は英語")]
    public string stunLabel = "STUN";
    public string slowLabel = "SLOW";

    [Header("調整用（Playモードで位置・サイズを確認するため、状態異常なしでも強制表示する）")]
    public bool forceShowForTesting = false;
}
