using UnityEngine;

namespace Chebyss.Battle
{
    public class StraightAdvanceMoveRule : IEnemyMoveRule
    {
        public Vector2Int? DetermineMoveDestination(PieceSnapshot enemy, BoardState board, EnemyMovementPatternSO pattern)
        {
            var p = (StraightAdvanceMovementPatternSO)pattern;
            var dest = enemy.position + p.advanceDirection;
            if (!board.IsInBounds(dest)) return null;
            if (board.IsOccupied(dest)) return null; // 塞がれていたら待機
            return dest;
        }
    }
}
