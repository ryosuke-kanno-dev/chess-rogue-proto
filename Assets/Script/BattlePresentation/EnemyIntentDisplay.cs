using System.Collections.Generic;
using UnityEngine;
using Chebyss.Battle;

// 敵の次の行動の予告表示。EnemyTurnProcessor.PreviewNextTurnでBoardStateの複製上で
// 実際に敵ターンを実行し、その結果をタイルのハイライトとして示す。本物の盤面は変更しない。
// プレイヤーが駒を選択している間は、BoardTileのハイライト枠が競合するため
// ClearHighlights()で一旦隠す（BattleInputController側で呼び分ける）。
public class EnemyIntentDisplay : MonoBehaviour
{
    [SerializeField] private BoardGenerator boardGenerator;

    private readonly List<Vector2Int> highlightedTiles = new();

    public void UpdateIntents(BoardState board, EnemyTurnProcessor enemyTurnProcessor)
    {
        ClearHighlights();

        var preview = enemyTurnProcessor.PreviewNextTurn(board);

        foreach (var action in preview.actions)
        {
            if (action.wasStunned) continue;
            if (!action.attackedKing && action.moveDestination == null) continue;

            var displayPos = action.moveDestination ?? action.startPosition;
            var highlight = action.attackedKing ? HighlightType.EnemyIntentAttack : HighlightType.EnemyIntentMove;

            boardGenerator.GetTile(displayPos)?.SetHighlight(highlight);
            highlightedTiles.Add(displayPos);
        }
    }

    public void ClearHighlights()
    {
        foreach (var pos in highlightedTiles)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);
        highlightedTiles.Clear();
    }
}
