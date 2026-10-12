using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Chebyss.Battle;

// 敵の次の行動の予告表示。EnemyTurnProcessor.PreviewNextTurnでBoardStateの複製上で
// 実際に敵ターンを実行し、その結果をタイルのハイライトとして示す。本物の盤面は変更しない。
// 攻撃する敵は EnemyIntentAttack、攻撃される対象のマスは EnemyIntentTarget で示し、
// 同じ対象への合計ダメージをマスの上に表示する（HP以上なら KO）。
// プレイヤーが駒を選択している間は、BoardTileのハイライト枠が競合するため
// ClearHighlights()で一旦隠す（BattleInputController側で呼び分ける）。
public class EnemyIntentDisplay : MonoBehaviour
{
    [SerializeField] private BoardGenerator boardGenerator;

    [Header("ダメージ予告の文字（3D TextMeshPro。数字と英字のみ）")]
    [SerializeField] private float labelFontSize = 6f;
    [SerializeField] private float labelHeight = 1.3f;

    private static readonly Color NormalLabelColor = new Color(1f, 0.25f, 0.25f);
    private static readonly Color KoLabelColor = new Color(0.65f, 0.05f, 0.05f);
    private const float KoFontScale = 1.5f;

    private readonly List<Vector2Int> highlightedTiles = new();
    private readonly List<GameObject> labelObjects = new();
    private Camera mainCamera;

    public void UpdateIntents(BoardState board, EnemyTurnProcessor enemyTurnProcessor)
    {
        ClearHighlights();

        var preview = enemyTurnProcessor.PreviewNextTurn(board);

        // 対象マスごとの合計ダメージ（出現順を保つため、順序リストも持つ）
        var damageByTarget = new Dictionary<Vector2Int, int>();
        var targetOrder = new List<Vector2Int>();

        // 1パス目：敵自身のタイル（攻撃=EnemyIntentAttack / 移動=EnemyIntentMove）
        foreach (var action in preview.actions)
        {
            if (action.wasStunned) continue;

            bool attacks = action.targetPosition.HasValue;
            if (!attacks && action.moveDestination == null) continue;

            var displayPos = action.moveDestination ?? action.startPosition;
            var highlight = attacks ? HighlightType.EnemyIntentAttack : HighlightType.EnemyIntentMove;

            boardGenerator.GetTile(displayPos)?.SetHighlight(highlight);
            highlightedTiles.Add(displayPos);

            if (attacks)
            {
                var target = action.targetPosition.Value;
                if (!damageByTarget.ContainsKey(target))
                {
                    damageByTarget[target] = 0;
                    targetOrder.Add(target);
                }
                damageByTarget[target] += action.damage;
            }
        }

        // 2パス目：対象のタイルは後から描く（敵のタイルと重なっても対象の色を優先する）
        foreach (var target in targetOrder)
        {
            boardGenerator.GetTile(target)?.SetHighlight(HighlightType.EnemyIntentTarget);
            highlightedTiles.Add(target);

            int total = damageByTarget[target];
            int currentHp = board.TryGetPlayerPiece(target, out var piece) ? piece.currentHp : int.MaxValue;
            CreateDamageLabel(target, total, total >= currentHp);
        }
    }

    public void ClearHighlights()
    {
        foreach (var pos in highlightedTiles)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);
        highlightedTiles.Clear();

        foreach (var label in labelObjects)
            if (label != null) Destroy(label);
        labelObjects.Clear();
    }

    void LateUpdate()
    {
        if (labelObjects.Count == 0) return;
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        foreach (var label in labelObjects)
        {
            if (label != null) label.transform.rotation = mainCamera.transform.rotation;
        }
    }

    private void CreateDamageLabel(Vector2Int pos, int totalDamage, bool isKo)
    {
        var tile = boardGenerator.GetTile(pos);
        if (tile == null) return;

        var labelObject = new GameObject($"IntentDamageLabel_{pos.x}_{pos.y}");
        labelObject.transform.position = tile.transform.position + Vector3.up * labelHeight;

        var text = labelObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.text = isKo ? $"-{totalDamage} KO" : $"-{totalDamage}";
        text.color = isKo ? KoLabelColor : NormalLabelColor;
        text.fontSize = isKo ? labelFontSize * KoFontScale : labelFontSize;

        // 暗い背景でも読めるよう黒い縁取りを付ける
        text.outlineColor = new Color32(0, 0, 0, 255);
        text.outlineWidth = 0.25f;

        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera != null) labelObject.transform.rotation = mainCamera.transform.rotation;

        labelObjects.Add(labelObject);
    }
}
