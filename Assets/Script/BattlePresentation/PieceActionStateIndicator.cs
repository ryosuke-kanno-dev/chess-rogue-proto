using UnityEngine;

// プレイヤー駒の行動済み／行動可能を、モデルの色の明るさで示す。
// 敵駒にはactionsLeftという概念が無い（常に0）ため、プレイヤー駒にのみ使用する
// （BattleVisualSync側でisPlayerSideの駒にだけAddComponentする）。
public class PieceActionStateIndicator : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float actedDarkenAmount = 0.5f;

    private Renderer[] renderers;
    private Color[] originalColors;
    private bool initialized;
    private bool lastActedState;
    private bool hasAppliedOnce;

    private void EnsureInitialized()
    {
        if (initialized) return;
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].material.color;
        initialized = true;
    }

    public void SetActed(bool hasActed)
    {
        EnsureInitialized();

        // 変化がなければ再代入しない（毎回ren.materialにアクセスするとマテリアルの
        // インスタンス化コストがかかり続けるため。BoardTile.SetHighlightと異なり、
        // こちらはSyncAllのたびに毎回呼ばれる想定のため明示的にガードする）
        if (hasAppliedOnce && hasActed == lastActedState) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material.color = hasActed
                ? Color.Lerp(originalColors[i], Color.black, actedDarkenAmount)
                : originalColors[i];
        }

        lastActedState = hasActed;
        hasAppliedOnce = true;
    }
}
