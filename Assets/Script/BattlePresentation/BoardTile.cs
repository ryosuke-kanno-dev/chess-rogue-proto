using UnityEngine;

public enum HighlightType
{
    None,
    Movable,
    Attackable,
    EnemyIntentMove,
    EnemyIntentAttack,
    SelectedSelf
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

    public Vector2Int GridPosition { get; private set; }

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
        }
    }
}
