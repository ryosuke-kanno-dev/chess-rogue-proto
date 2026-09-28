using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using Chebyss.Battle;

// 駒の頭上に、かかっている状態異常（スタン・スロウ）と残りターン数を表示する。
// 表示データはBoardState側（PieceSnapshot.statusEffects）が唯一の真実で、
// BattleVisualSync.SyncAllから毎回渡される。このクラス自身は状態を判定・保持しない。
// ラベルは駒の子にせず独立したオブジェクトとして追従させる（駒のScaleに表示サイズや
// 高さが左右されないようにするため）。駒が破棄されたらOnDestroyでラベルも破棄する。
public class PieceStatusIndicator : MonoBehaviour
{
    private StatusIndicatorSettings settings;
    private TextMeshPro label;
    private Transform labelTransform;
    private float modelTopOffset;
    private Camera mainCamera;

    public void Initialize(StatusIndicatorSettings s)
    {
        settings = s;

        // 駒モデル全体（Rendererが複数あっても）の上端が、原点からどれだけ上にあるか
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            modelTopOffset = bounds.max.y - transform.position.y;
        }

        var labelObject = new GameObject($"StatusLabel_{gameObject.name}");
        labelTransform = labelObject.transform;

        label = labelObject.AddComponent<TextMeshPro>();
        // 下端基準で上へ伸びるようにし、複数行になってもHPバー側へ食い込まないようにする
        label.alignment = TextAlignmentOptions.Bottom;
        label.fontSize = settings.fontSize;
        label.fontStyle = FontStyles.Bold;
        label.rectTransform.sizeDelta = new Vector2(10f, 1f);
        label.rectTransform.pivot = new Vector2(0.5f, 0f);

        labelObject.SetActive(false);
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
            label.gameObject.SetActive(false);
            return;
        }

        label.text = sb.ToString();
        label.gameObject.SetActive(true);
    }

    void LateUpdate()
    {
        if (labelTransform == null || settings == null) return;
        if (mainCamera == null) mainCamera = Camera.main;

        // 再生中にInspectorでFont Sizeを変えても既存のラベルへ反映されるようにする
        // （生成時にしか適用していなかったため、調整のたびに再生し直す必要があった）
        label.fontSize = settings.fontSize;

        labelTransform.position = transform.position + Vector3.up * (modelTopOffset + settings.heightAboveModelTop);
        if (mainCamera != null) labelTransform.rotation = mainCamera.transform.rotation;
    }

    void OnDestroy()
    {
        if (labelTransform != null) Destroy(labelTransform.gameObject);
    }
}
