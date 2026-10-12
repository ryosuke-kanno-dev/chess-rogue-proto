using UnityEngine;

namespace Chebyss.Battle
{
    [CreateAssetMenu(menuName = "Chebyss/Pawn Movement Pattern", fileName = "PawnMovementPattern")]
    public class PawnMovementPatternSO : PieceMovementPatternSO
    {
        // 基礎攻撃力はPieceSnapshot.attack（PieceData由来）を使う。

        [Tooltip("★2のポーンが、前・左・右へ移動できる最大マス数")]
        [Min(1)]
        public int star2MoveRange = 2;

        [Tooltip("★2のポーンが、前・斜め前へ攻撃できる最大マス数")]
        [Min(1)]
        public int star2AttackRange = 2;
    }
}
