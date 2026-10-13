using System.Collections.Generic;
using UnityEngine;
using Chebyss.Battle;

public class TileHighlightController : MonoBehaviour
{
    public BoardGenerator boardGenerator;

    private List<Vector2Int> lastMovable = new();
    private List<Vector2Int> lastAttackable = new();
    private Vector2Int? lastSelfPos;

    public void ShowHighlightsFor(BattleTurnController turnController, Vector2Int piecePos)
    {
        ClearHighlights();

        lastMovable = turnController.GetMovableTiles(piecePos);
        lastAttackable = turnController.GetAttackableTiles(piecePos);

        // 選択中の駒の位置（どの駒を選択しているか）を示す。
        lastSelfPos = piecePos;
        boardGenerator.GetTile(piecePos)?.SetHighlight(HighlightType.SelectedSelf);

        // ドロップがあるマスは別色で示す（停止すると拾得できることを事前に伝えるため）
        foreach (var pos in lastMovable)
        {
            var type = turnController.HasDropAt(pos) ? HighlightType.MovableWithDrop : HighlightType.Movable;
            boardGenerator.GetTile(pos)?.SetHighlight(type);
        }

        foreach (var pos in lastAttackable)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.Attackable);
    }

    public void ClearHighlights()
    {
        foreach (var pos in lastMovable)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);
        foreach (var pos in lastAttackable)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);
        if (lastSelfPos.HasValue)
            boardGenerator.GetTile(lastSelfPos.Value)?.SetHighlight(HighlightType.None);

        lastMovable.Clear();
        lastAttackable.Clear();
        lastSelfPos = null;
    }
}
