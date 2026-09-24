using UnityEngine;

namespace Chebyss.Battle
{
    [CreateAssetMenu(menuName = "Chebyss/Rook Movement Pattern", fileName = "RookMovementPattern")]
    public class RookMovementPatternSO : PieceMovementPatternSO
    {
        public int knockbackDistance = 1;

        [Tooltip("Phase1はfalse固定。Phase2で有効化する")]
        public bool enableChainCollision;

        public int chainCollisionDamage;
    }
}
