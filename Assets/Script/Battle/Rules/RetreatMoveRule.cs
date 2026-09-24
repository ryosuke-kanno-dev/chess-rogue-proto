using UnityEngine;

namespace Chebyss.Battle
{
    public class RetreatMoveRule : IEnemyMoveRule
    {
        public Vector2Int? DetermineMoveDestination(PieceSnapshot enemy, BoardState board, EnemyMovementPatternSO pattern)
        {
            var nearest = NearestPieceMoveHelper.FindNearestPlayerPiece(enemy.position, board);
            if (nearest is null) return null;
            return NearestPieceMoveHelper.FindFarthestAdjacentTile(enemy.position, nearest.Value, board);
        }
    }
}
