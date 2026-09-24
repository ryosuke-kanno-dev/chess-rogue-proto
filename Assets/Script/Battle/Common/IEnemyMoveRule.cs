using UnityEngine;

namespace Chebyss.Battle
{
    public interface IEnemyMoveRule
    {
        Vector2Int? DetermineMoveDestination(
            PieceSnapshot enemy,
            BoardState board,
            EnemyMovementPatternSO pattern);
    }
}
