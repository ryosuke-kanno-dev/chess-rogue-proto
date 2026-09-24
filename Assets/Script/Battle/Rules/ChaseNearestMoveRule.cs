using UnityEngine;

namespace Chebyss.Battle
{
    public class ChaseNearestMoveRule : IEnemyMoveRule
    {
        public Vector2Int? DetermineMoveDestination(PieceSnapshot enemy, BoardState board, EnemyMovementPatternSO pattern)
        {
            var nearest = NearestPieceMoveHelper.FindNearestPlayerPiece(enemy.position, board);
            if (nearest is null) return null;
            return NearestPieceMoveHelper.BfsFindNextStep(enemy.position, nearest.Value, board);
        }
    }
}
