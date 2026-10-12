using UnityEngine;

public enum HighlightType
{
    None,
    Movable,
    Attackable,
    EnemyIntentMove,
    EnemyIntentAttack,
    SelectedSelf,
    MovableWithDrop,
    EnemyIntentTarget,
    PreviewMovable,
    PreviewAttackable
}

[RequireComponent(typeof(Renderer))]
public class BoardTile : MonoBehaviour
{
    [Header("ハイライト色")]
    public Color movableColor = new Color(0.3f, 0.5f, 1f);
    public Color attackableColor = new Color(1f, 0.3f, 0.3f);
    public Color enemyIntentMoveColor = new Color(1f, 0.6f, 0.1f);
    public Color enemyIntentAttackColor = new Color(0.7f, 0.1f, 0.7f);
    public Color selectedSelfColor = new Color(1f, 1f, 1f);
    public Color movableWithDropColor = new Color(1f, 0.85f, 0.2f);
    public Color enemyIntentTargetColor = new Color(1f, 0.5f, 0.1f);
    public Color previewMovableColor = new Color(0.45f, 0.75f, 1f);
    public Color previewAttackableColor = new Color(1f, 0.5f, 0.5f);

    public Vector2Int GridPosition { get; private set; }

    // 現在かかっているハイライト（配置プレビューが、上書き前の状態へ戻すために参照する）
    public HighlightType CurrentHighlight { get; private set; }

    [Header("ハイライトの混ぜ具合（0=元の色のまま、1=ハイライト色そのまま）")]
    [Range(0f, 1f)]
    public float highlightBlendFactor = 0.6f;

    private Renderer ren;
    private Material defaultMaterial;
    private Color originalColor;

    public void Initialize(Material baseMaterial, Vector2Int gridPosition)
    {
        ren = GetComponent<Renderer>();
        defaultMaterial = baseMaterial;
        ren.material = baseMaterial;
        originalColor = baseMaterial.color;
        GridPosition = gridPosition;
    }

    public void SetHighlight(HighlightType type)
    {
        CurrentHighlight = type;
        switch (type)
        {
            case HighlightType.None:
                ren.material = defaultMaterial;
                break;
            case HighlightType.Movable:
                // 元のチェッカー色を完全に上書きせず、混ぜることでマスの境界（白黒模様）を保つ
                ren.material.color = Color.Lerp(originalColor, movableColor, highlightBlendFactor);
                break;
            case HighlightType.Attackable:
                ren.material.color = Color.Lerp(originalColor, attackableColor, highlightBlendFactor);
                break;
            case HighlightType.EnemyIntentMove:
                ren.material.color = Color.Lerp(originalColor, enemyIntentMoveColor, highlightBlendFactor);
                break;
            case HighlightType.EnemyIntentAttack:
                ren.material.color = Color.Lerp(originalColor, enemyIntentAttackColor, highlightBlendFactor);
                break;
            case HighlightType.SelectedSelf:
                ren.material.color = Color.Lerp(originalColor, selectedSelfColor, highlightBlendFactor);
                break;
            case HighlightType.MovableWithDrop:
                ren.material.color = Color.Lerp(originalColor, movableWithDropColor, highlightBlendFactor);
                break;
            case HighlightType.EnemyIntentTarget:
                ren.material.color = Color.Lerp(originalColor, enemyIntentTargetColor, highlightBlendFactor);
                break;
            case HighlightType.PreviewMovable:
                ren.material.color = Color.Lerp(originalColor, previewMovableColor, highlightBlendFactor);
                break;
            case HighlightType.PreviewAttackable:
                ren.material.color = Color.Lerp(originalColor, previewAttackableColor, highlightBlendFactor);
                break;
        }
    }
}
