using UnityEngine;

namespace Chebyss.Battle
{
    [CreateAssetMenu(menuName = "Chebyss/Rook Movement Pattern", fileName = "RookMovementPattern")]
    public class RookMovementPatternSO : PieceMovementPatternSO
    {
        public int knockbackDistance = 1;

        [Tooltip("攻撃が当たったとき、吹き飛ばしが発生する確率（0〜1）")]
        [Range(0f, 1f)]
        public float knockbackChance = 0.5f;

        [Tooltip("Phase1はfalse固定。Phase2で有効化する")]
        public bool enableChainCollision;

        public int chainCollisionDamage;
    }
}
