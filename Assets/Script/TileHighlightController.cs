using System.Collections.Generic;
using UnityEngine;
using Chebyss.Battle;

public class TileHighlightController : MonoBehaviour
{
    public BoardGenerator boardGenerator;

    private List<Vector2Int> lastMovable = new();
    private List<Vector2Int> lastAttackable = new();

    public void ShowHighlightsFor(BattleTurnController turnController, Vector2Int piecePos)
    {
        ClearHighlights();

        lastMovable = turnController.GetMovableTiles(piecePos);
        lastAttackable = turnController.GetAttackableTiles(piecePos);

        foreach (var pos in lastMovable)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.Movable);

        foreach (var pos in lastAttackable)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.Attackable);
    }

    public void ClearHighlights()
    {
        foreach (var pos in lastMovable)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);
        foreach (var pos in lastAttackable)
            boardGenerator.GetTile(pos)?.SetHighlight(HighlightType.None);

        lastMovable.Clear();
        lastAttackable.Clear();
    }
}
