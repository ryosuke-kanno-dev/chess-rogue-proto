using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Chebyss.Battle;

// 駒の頭上に、かかっている状態異常（スタン・スロウ）と残りターン数を表示する。
// 表示データはBoardState側（PieceSnapshot.statusEffects）が唯一の真実で、
// BattleVisualSync.SyncAllから毎回渡される。このクラス自身は状態を判定・保持しない。
//
// 実装方式：既存のPieceHealthBar（HP_Text_Canvas）と同じ「World SpaceのCanvas + UGUI
// Text」方式に統一する。以前は3D TextMeshPro + 駒のScaleを打ち消す手動計算で実装して
// いたが、計算のたびに複数の不具合（親スケールとの合成ミス、ラベル自身のRendererを
// 高さ計算に巻き込む等）が発生した。PieceHealthBarは同じ状況下で既に安定して動作して
// いるため、同じ実装方式に合わせることで、これらの問題を作り込みごと回避する。
public class PieceStatusIndicator : MonoBehaviour
{
    private StatusIndicatorSettings settings;
    private GameObject canvasObj;
    private Text label;
    private Camera mainCamera;

    public void Initialize(StatusIndicatorSettings s)
    {
        settings = s;

        // このコンポーネント自身はPieceBoardLinkと同じルートGameObjectに付くが、
        // 駒の見た目（モデル）はルートではなく子オブジェクトにあり、PieceDataと
        // PieceHealthBarはそちらに付いている。ラベルをルート直下の子にすると、
        // ルートとモデルのローカル原点がズレている場合（インポート時のオフセット調整等）に、
        // モデルから大きく外れた位置（見えない場所）に表示されてしまう不具合があった。
        // PieceHealthBarと同じ基準点（PieceDataと同じGameObject）に揃えることで、
        // 同じ見え方になるようにする。
        var anchor = GetComponentInChildren<PieceData>(true);
        var parentTransform = anchor != null ? anchor.transform : transform;

        canvasObj = new GameObject($"StatusLabel_{gameObject.name}");
        canvasObj.transform.SetParent(parentTransform, worldPositionStays: false);
        canvasObj.transform.localPosition = new Vector3(0, settings.heightAboveModelTop, 0);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var canvasRect = canvasObj.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(240f, 80f);
        canvasObj.transform.localScale = Vector3.one * settings.canvasScale;

        var textObj = new GameObject("StatusLabelText");
        textObj.transform.SetParent(canvasObj.transform, false);

        label = textObj.AddComponent<Text>();
        label.alignment = TextAnchor.MiddleCenter;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = Mathf.RoundToInt(settings.fontSize);
        label.fontStyle = FontStyle.Bold;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.supportRichText = true;

        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        var outline = textObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        canvasObj.SetActive(false);
    }

    public void SetEffects(IReadOnlyList<ActiveStatusEffect> effects)
    {
        if (label == null) return;

        var sb = new StringBuilder();
        foreach (var effect in effects)
        {
            if (effect.remainingTurns <= 0) continue;

            string text;
            Color color;
            switch (effect.type)
            {
                case StatusEffectType.Stun: text = settings.stunLabel; color = settings.stunColor; break;
                case StatusEffectType.Slow: text = settings.slowLabel; color = settings.slowColor; break;
                default: continue;
            }

            if (sb.Length > 0) sb.Append('\n');
            sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>')
              .Append(text).Append(' ').Append(effect.remainingTurns).Append("</color>");
        }

        if (sb.Length == 0)
        {
            canvasObj.SetActive(false);
            return;
        }

        label.text = sb.ToString();
        canvasObj.SetActive(true);
    }

    void LateUpdate()
    {
        if (canvasObj == null || settings == null) return;
        if (mainCamera == null) mainCamera = Camera.main;

        // 再生中にInspectorで数値を変えても反映されるようにする
        canvasObj.transform.localPosition = new Vector3(0, settings.heightAboveModelTop, 0);
        canvasObj.transform.localScale = Vector3.one * settings.canvasScale;
        label.fontSize = Mathf.RoundToInt(settings.fontSize);

        // 調整用：実際にスタン・スロウをかけなくても、Inspectorのチェック1つで
        // 全駒のラベルを強制表示できるようにする。位置・サイズの調整のたびに
        // 戦闘を組み立て直す手間をなくすため。
        if (settings.forceShowForTesting)
        {
            label.text = $"<color=#{ColorUtility.ToHtmlStringRGB(settings.stunColor)}>{settings.stunLabel} 9</color>\n" +
                         $"<color=#{ColorUtility.ToHtmlStringRGB(settings.slowColor)}>{settings.slowLabel} 9</color>";
            canvasObj.SetActive(true);
        }

        if (mainCamera != null) canvasObj.transform.rotation = mainCamera.transform.rotation;
    }
}
