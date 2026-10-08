using UnityEngine;

namespace Chebyss.Battle
{
    [CreateAssetMenu(menuName = "Chebyss/Pawn Movement Pattern", fileName = "PawnMovementPattern")]
    public class PawnMovementPatternSO : PieceMovementPatternSO
    {
        // 固有フィールドは現状なし。基礎攻撃力はPieceSnapshot.attack（PieceData由来）を使う。
    }
}
