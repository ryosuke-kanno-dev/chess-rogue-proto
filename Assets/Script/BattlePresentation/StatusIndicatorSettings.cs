using UnityEngine;

// 状態異常ラベルの見た目の調整値。BattleBootstrapperのInspectorに出して、
// 実行時に追加されるコンポーネント（BattleVisualSync等）を介さず編集できるようにする。
[System.Serializable]
public class StatusIndicatorSettings
{
    [Tooltip("駒モデルの上端からの高さ（ワールド単位）。HPバーの数値と重なる場合は大きくする")]
    public float heightAboveModelTop = 0.9f;

    [Tooltip("ラベルの大きさ。大きすぎる・小さすぎる場合に調整する")]
    public float fontSize = 3f;

    public Color stunColor = new Color(1f, 0.85f, 0.3f);
    public Color slowColor = new Color(0.4f, 0.75f, 1f);

    [Tooltip("表示文言。日本語は日本語対応のTMPフォントアセットが必要なため、現状は英語")]
    public string stunLabel = "STUN";
    public string slowLabel = "SLOW";
}
